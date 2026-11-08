using System;
using System.Collections.Generic;
using UnityEngine;

namespace NightPatrol.Phone
{
    /// <summary>
    /// 전화 화면 그림을 코드로 만든다: 모서리 둥근 판, 원, 세로 그라데이션, 앱 아이콘(옛날 휴대폰처럼 둥근 네모 + 그라데이션 + 윗부분 광택 + 흰 기호).
    /// 같은 그림은 한 번만 만들고, 만든 텍스처·스프라이트·메시·머티리얼은 Dispose에서 지운다 (PhoneScreen이 없어질 때)
    /// </summary>
    public sealed class PhoneArt : IDisposable
    {
        /// <summary>아이콘 텍스처 크기 (픽셀)</summary>
        public const int IconPixels = 96;
        // 아이콘에서 기호가 차지하는 비율 (기호 좌표 -1~1이 아이콘 가운데 이만큼)
        private const float SymbolScale = 0.66f;
        // 가장자리를 매끄럽게: 한 픽셀을 3×3으로 나눠 칠한 비율
        private const int Samples = 3;

        private readonly List<UnityEngine.Object> created = new List<UnityEngine.Object>();
        private readonly Dictionary<int, Sprite> rounded = new Dictionary<int, Sprite>();
        private readonly Dictionary<int, Sprite> circles = new Dictionary<int, Sprite>();
        private readonly Dictionary<long, Sprite> gradients = new Dictionary<long, Sprite>();
        private readonly Dictionary<string, Sprite> symbols = new Dictionary<string, Sprite>();
        private readonly Dictionary<PhoneApp, Sprite> icons = new Dictionary<PhoneApp, Sprite>();

        /// <summary>모서리 반지름 radius(픽셀)인 흰 판. 9칸 늘이기라 Image.Type.Sliced로 어느 크기에나 쓴다</summary>
        public Sprite Rounded(int radius)
        {
            radius = Mathf.Clamp(radius, 1, 64);
            if (rounded.TryGetValue(radius, out var sprite) && sprite != null) return sprite;
            int size = radius * 2 + 4;
            Func<float, float, bool> inside = (x, y) => InRoundedRect(x, y, size, size, radius);
            var texture = Paint(size, size, (x, y) => new Color(1f, 1f, 1f, Coverage(x, y, inside)));
            float border = radius + 1;
            sprite = Keep(Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(border, border, border, border)));
            rounded[radius] = sprite;
            return sprite;
        }

        /// <summary>흰 원 (지름 diameter 픽셀). 늘여 쓰면 타원</summary>
        public Sprite Circle(int diameter = 48)
        {
            diameter = Mathf.Clamp(diameter, 4, 256);
            if (circles.TryGetValue(diameter, out var sprite) && sprite != null) return sprite;
            float r = diameter / 2f;
            Func<float, float, bool> inside = (x, y) => (x - r) * (x - r) + (y - r) * (y - r) <= r * r;
            var texture = Paint(diameter, diameter, (x, y) => new Color(1f, 1f, 1f, Coverage(x, y, inside)));
            sprite = Keep(Sprite.Create(texture, new Rect(0, 0, diameter, diameter), new Vector2(0.5f, 0.5f), 100f));
            circles[diameter] = sprite;
            return sprite;
        }

        /// <summary>위에서 아래로 색이 바뀌는 판 (Image.Type.Simple로 늘여 쓴다)</summary>
        public Sprite Gradient(Color top, Color bottom)
        {
            long key = ((long)Pack(top) << 32) | Pack(bottom);
            if (gradients.TryGetValue(key, out var sprite) && sprite != null) return sprite;
            const int height = 64;
            var texture = Paint(2, height, (x, y) => Color.Lerp(bottom, top, y / (height - 1f)));
            sprite = Keep(Sprite.Create(texture, new Rect(0, 0, 2, height), new Vector2(0.5f, 0.5f), 100f));
            gradients[key] = sprite;
            return sprite;
        }

        /// <summary>앱 아이콘: 인스펙터에서 넣은 그림이 있으면 그것, 없으면 앱 색과 기호로 그린 것 (기호가 없으면 바탕만 — 화면이 이름 첫 글자를 얹는다)</summary>
        public Sprite Icon(PhoneApp app)
        {
            if (app == null) return null;
            if (app.IconSprite != null) return app.IconSprite;
            if (icons.TryGetValue(app, out var sprite) && sprite != null) return sprite;
            Func<Vector2, bool> symbol = null;
            if (app.HasSymbol) symbol = app.SymbolAt;
            sprite = Symbol(null, app.IconTop, app.IconBottom, symbol);
            icons[app] = sprite;
            return sprite;
        }

        /// <summary>
        /// 아이콘 모양 그림: 둥근 네모 바탕(top → bottom 색) + 윗부분 광택 + 흰 기호(symbol: -1~1 좌표, 위가 +, 칠할 곳이면 true).
        /// key가 같으면 처음 만든 것을 돌려준다
        /// </summary>
        public Sprite Symbol(string key, Color top, Color bottom, Func<Vector2, bool> symbol)
        {
            if (key != null && symbols.TryGetValue(key, out var sprite) && sprite != null) return sprite;
            const int n = IconPixels;
            float radius = n * 0.22f;
            Func<float, float, bool> shape = (x, y) => InRoundedRect(x, y, n, n, radius);
            Func<float, float, bool> gloss = (x, y) => InGloss(x / n, y / n);
            // 기호는 픽셀마다 한 번만 칠해 두고, 그림자는 그것을 2픽셀 아래로 옮겨 쓴다 (기호 계산이 가장 비쌈)
            float[] mark = symbol != null ? Mask(n, (x, y) => symbol(ToSymbol(x, y, n))) : null;
            int shadowShift = Mathf.RoundToInt(n * 0.025f);
            var texture = Paint(n, n, (x, y) =>
            {
                float cover = Coverage(x, y, shape);
                if (cover <= 0f) return Color.clear;
                float t = (y + 0.5f) / n;
                var color = Color.Lerp(bottom, top, t);
                float shine = Coverage(x, y, gloss);
                if (shine > 0f) color = Color.Lerp(color, Color.white, shine * Mathf.Lerp(0.1f, 0.42f, Mathf.InverseLerp(0.5f, 1f, t)));
                if (mark != null)
                {
                    float shadow = y + shadowShift < n ? mark[(y + shadowShift) * n + x] : 0f;
                    color = Color.Lerp(color, Color.black, 0.22f * shadow);
                    color = Color.Lerp(color, Color.white, mark[y * n + x]);
                }
                color.a = cover;
                return color;
            });
            sprite = Keep(Sprite.Create(texture, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f));
            if (key != null) symbols[key] = sprite;
            return sprite;
        }

        /// <summary>이 그림 묶음이 지울 것으로 맡긴다 (메시·머티리얼 등)</summary>
        public T Keep<T>(T obj) where T : UnityEngine.Object
        {
            if (obj != null) created.Add(obj);
            return obj;
        }

        public void Dispose()
        {
            foreach (var obj in created) Discard(obj);
            created.Clear();
            rounded.Clear();
            circles.Clear();
            gradients.Clear();
            symbols.Clear();
            icons.Clear();
        }

        /// <summary>실행 중이면 Destroy, 에디터 테스트면 DestroyImmediate</summary>
        public static void Discard(UnityEngine.Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(obj);
            else UnityEngine.Object.DestroyImmediate(obj);
        }

        // ── 기호 (흰색으로 칠할 곳, -1~1 좌표, 위가 +) ──

        private static readonly Vector2 HandsetEar = new Vector2(-0.55f, 0.55f);
        private static readonly Vector2 HandsetMouth = new Vector2(0.55f, -0.55f);
        private static readonly Vector2[] HandsetGrip = Bezier(HandsetEar, new Vector2(-0.6f, -0.6f), HandsetMouth, 12);

        /// <summary>수화기 (전화): 왼쪽 위 귀 쪽과 오른쪽 아래 입 쪽 받침을 왼쪽 아래로 휜 손잡이가 잇는다</summary>
        public static bool Handset(Vector2 p)
        {
            if (DistanceToPolyline(p, HandsetGrip) <= 0.16f) return true;
            return DistanceToSegment(p, HandsetEar, new Vector2(-0.28f, 0.5f)) <= 0.22f
                || DistanceToSegment(p, HandsetMouth, new Vector2(0.5f, -0.28f)) <= 0.22f;
        }

        /// <summary>말풍선 (메시지): 타원 + 왼쪽 아래 꼬리</summary>
        public static bool Bubble(Vector2 p)
        {
            float ex = p.x / 0.82f, ey = (p.y - 0.12f) / 0.62f;
            if (ex * ex + ey * ey <= 1f) return true;
            return InTriangle(p, new Vector2(-0.46f, -0.28f), new Vector2(-0.7f, -0.78f), new Vector2(-0.08f, -0.42f));
        }

        /// <summary>지구 (인터넷): 테두리 + 적도·위선 + 가운데·휜 경선</summary>
        public static bool Globe(Vector2 p)
        {
            const float radius = 0.8f, half = 0.07f;   // 반지름, 선 두께의 반
            float r = p.magnitude;
            if (r > radius + half) return false;
            if (Mathf.Abs(r - radius) <= half) return true;
            if (Mathf.Abs(p.y) <= half || Mathf.Abs(p.x) <= half) return true;
            if (Mathf.Abs(Mathf.Abs(p.y) - 0.44f) <= half) return true;
            float ex = p.x / 0.42f, ey = p.y / radius;
            return Mathf.Abs(Mathf.Sqrt(ex * ex + ey * ey) - 1f) * 0.42f <= half;
        }

        // ── 그리기 도우미 ──

        /// <summary>(x, y)가 (0,0)~(width,height)의 모서리 둥근 판 안인지 (픽셀 좌표)</summary>
        internal static bool InRoundedRect(float x, float y, float width, float height, float radius)
        {
            if (x < 0f || y < 0f || x > width || y > height) return false;
            float dx = Mathf.Max(radius - x, 0f, x - (width - radius));
            float dy = Mathf.Max(radius - y, 0f, y - (height - radius));
            return dx * dx + dy * dy <= radius * radius;
        }

        // 아이콘 윗부분 광택: 위쪽 가운데에 걸친 큰 타원 안 (u, v는 0~1, v는 위가 1)
        private static bool InGloss(float u, float v)
        {
            float ex = (u - 0.5f) / 0.95f, ey = (v - 1.02f) / 0.5f;
            return ex * ex + ey * ey <= 1f;
        }

        private static Vector2 ToSymbol(float x, float y, int n) =>
            new Vector2((x / n * 2f - 1f) / SymbolScale, (y / n * 2f - 1f) / SymbolScale);

        // 픽셀마다 칠한 비율 (아래 줄부터)
        private static float[] Mask(int n, Func<float, float, bool> inside)
        {
            var mask = new float[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                    mask[y * n + x] = Coverage(x, y, inside);
            return mask;
        }

        // 픽셀 (x, y)의 칠한 비율: 네 귀퉁이가 모두 같으면(가장자리가 지나지 않음) 0 또는 1, 아니면 3×3 점 중 inside가 참인 비율.
        // 픽셀보다 가는 모양은 놓칠 수 있다 — 기호 선은 2픽셀 이상으로 그린다
        private static float Coverage(int x, int y, Func<float, float, bool> inside)
        {
            bool corner = inside(x, y);
            if (corner == inside(x + 1f, y) && corner == inside(x, y + 1f) && corner == inside(x + 1f, y + 1f)) return corner ? 1f : 0f;
            int hit = 0;
            for (int sy = 0; sy < Samples; sy++)
                for (int sx = 0; sx < Samples; sx++)
                    if (inside(x + (sx + 0.5f) / Samples, y + (sy + 0.5f) / Samples)) hit++;
            return hit / (float)(Samples * Samples);
        }

        private Texture2D Paint(int width, int height, Func<int, int, Color> color)
        {
            var texture = Keep(new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "PhoneArt",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            });
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    pixels[y * width + x] = color(x, y);
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static uint Pack(Color color)
        {
            Color32 c = color;
            return ((uint)c.r << 24) | ((uint)c.g << 16) | ((uint)c.b << 8) | c.a;
        }

        private static Vector2[] Bezier(Vector2 a, Vector2 control, Vector2 b, int segments)
        {
            var points = new Vector2[segments + 1];
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments, u = 1f - t;
                points[i] = u * u * a + 2f * u * t * control + t * t * b;
            }
            return points;
        }

        internal static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
            return (p - (a + ab * t)).magnitude;
        }

        private static float DistanceToPolyline(Vector2 p, Vector2[] points)
        {
            float best = float.MaxValue;
            for (int i = 0; i + 1 < points.Length; i++) best = Mathf.Min(best, DistanceToSegment(p, points[i], points[i + 1]));
            return best;
        }

        private static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Cross(p, a, b), d2 = Cross(p, b, c), d3 = Cross(p, c, a);
            bool negative = d1 < 0f || d2 < 0f || d3 < 0f, positive = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(negative && positive);
        }

        private static float Cross(Vector2 p, Vector2 a, Vector2 b) => (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);
    }
}
