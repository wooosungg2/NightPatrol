using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace NightPatrol.Phone
{
    /// <summary>
    /// 모델이 정해지기 전 전화 모델을 코드로 만든다 — 옛날 휴대폰 크기(61×115×11.6 mm)의 둥근 은색 몸통,
    /// 앞면(검은 유리 + 가장자리 은색 테, 수화 구멍, 둥근 홈 버튼), 화면 칸(240×360 UI). 앞면은 -Z(카메라 쪽)
    /// </summary>
    public static class PhoneModelBuilder
    {
        public const float Width = 0.061f, Height = 0.115f, Depth = 0.0116f, CornerRadius = 0.009f;
        /// <summary>앞면 UI 1픽셀의 크기(m). 화면 240×360픽셀 = 49.7×74.5 mm</summary>
        public const float PixelSize = 0.000207f;
        public const int DisplayWidth = 240, DisplayHeight = 360;

        /// <summary>만든 모델: 뿌리, 앞면 캔버스, 화면 칸</summary>
        public sealed class Parts
        {
            public Transform Root;
            public RectTransform Face;
            public RectTransform Display;
        }

        /// <summary>parent 아래에 모델을 만든다 (parent와 같은 레이어). 메시·머티리얼·그림은 art가 지운다</summary>
        public static Parts Build(Transform parent, PhoneArt art)
        {
            int layer = parent.gameObject.layer;
            var root = new GameObject("PhoneModel").transform;
            root.gameObject.layer = layer;
            root.SetParent(parent, false);

            var body = new GameObject("Body");
            body.layer = layer;
            body.transform.SetParent(root, false);
            body.AddComponent<MeshFilter>().sharedMesh = art.Keep(RoundedSlab(Width, Height, Depth, CornerRadius, 8));
            var renderer = body.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sharedMaterial = art.Keep(BodyMaterial());

            // 앞면: 몸통 앞 0.2 mm에 붙은 World Space Canvas
            var faceObject = new GameObject("Face", typeof(RectTransform));
            faceObject.layer = layer;
            var face = (RectTransform)faceObject.transform;
            face.SetParent(root, false);
            var canvas = faceObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            float faceWidth = Mathf.Round(Width / PixelSize), faceHeight = Mathf.Round(Height / PixelSize);   // 295×556
            face.sizeDelta = new Vector2(faceWidth, faceHeight);
            face.localScale = Vector3.one * PixelSize;
            face.localPosition = new Vector3(0f, 0f, -Depth / 2f - 0.0002f);

            // 검은 유리 — 가장자리 1.2 mm는 은색 몸통이 테처럼 보이게 남긴다
            var glass = PhoneUI.Fill(face, "Glass");
            glass.offsetMin = new Vector2(6f, 6f);
            glass.offsetMax = new Vector2(-6f, -6f);
            PhoneUI.Image(glass, art.Rounded(36), new Color(0.02f, 0.02f, 0.025f));

            float top = (faceHeight - DisplayHeight) / 2f;
            PhoneUI.Image(PhoneUI.Box(face, "Earpiece", faceWidth / 2f - 26f, top / 2f - 4f, 52f, 8f), art.Rounded(4), new Color(0.16f, 0.17f, 0.19f));

            var display = PhoneUI.Box(face, "Display", (faceWidth - DisplayWidth) / 2f, top, DisplayWidth, DisplayHeight);
            display.gameObject.AddComponent<RectMask2D>();

            // 홈 버튼: 바깥 테 + 오목한 안쪽 + 둥근 네모 표시
            float homeY = top + DisplayHeight + (faceHeight - top - DisplayHeight) / 2f;
            var home = PhoneUI.Box(face, "HomeButton", faceWidth / 2f - 27f, homeY - 27f, 54f, 54f);
            PhoneUI.Image(home, art.Circle(), new Color(0.3f, 0.31f, 0.33f));
            PhoneUI.Image(PhoneUI.Box(home, "Inside", 3f, 3f, 48f, 48f), art.Circle(), new Color(0.06f, 0.06f, 0.07f));
            PhoneUI.Image(PhoneUI.Box(home, "Mark", 18f, 18f, 18f, 18f), art.Rounded(5), new Color(0.55f, 0.56f, 0.6f));
            PhoneUI.Image(PhoneUI.Box(home, "MarkInside", 20f, 20f, 14f, 14f), art.Rounded(4), new Color(0.06f, 0.06f, 0.07f));

            return new Parts { Root = root, Face = face, Display = display };
        }

        // 은색 몸통 (URP 기본 머티리얼을 복사해 색만 — 어두운 건물에서도 테가 보이게 너무 금속성은 아니게)
        private static Material BodyMaterial()
        {
            var probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var material = new Material(probe.GetComponent<Renderer>().sharedMaterial) { name = "PhoneBody" };
            Object.DestroyImmediate(probe);
            material.color = new Color(0.78f, 0.79f, 0.82f);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0.3f);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.6f);
            return material;
        }

        /// <summary>모서리 둥근 판 메시 (앞 -Z, 뒤 +Z, 옆은 둥근 모서리를 따라 부드럽게)</summary>
        internal static Mesh RoundedSlab(float width, float height, float depth, float radius, int cornerSegments)
        {
            radius = Mathf.Clamp(radius, 0.0001f, Mathf.Min(width, height) / 2f);
            cornerSegments = Mathf.Max(1, cornerSegments);
            float halfWidth = width / 2f - radius, halfHeight = height / 2f - radius;
            Vector2[] centers =
            {
                new Vector2(halfWidth, halfHeight), new Vector2(-halfWidth, halfHeight),
                new Vector2(-halfWidth, -halfHeight), new Vector2(halfWidth, -halfHeight),
            };
            // 둘레 점 (앞에서 볼 때 반시계)과 바깥 방향
            var outline = new List<Vector2>();
            var outward = new List<Vector2>();
            for (int c = 0; c < 4; c++)
                for (int i = 0; i <= cornerSegments; i++)
                {
                    float angle = (c * 90f + i * 90f / cornerSegments) * Mathf.Deg2Rad;
                    var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    outline.Add(centers[c] + direction * radius);
                    outward.Add(direction);
                }

            int n = outline.Count;
            float front = -depth / 2f, back = depth / 2f;
            var vertices = new List<Vector3>(n * 4 + 2);
            var normals = new List<Vector3>(n * 4 + 2);
            var triangles = new List<int>(n * 12);

            // 앞면·뒷면: 가운데 점에서 부채꼴
            int frontCenter = vertices.Count;
            vertices.Add(new Vector3(0f, 0f, front));
            normals.Add(Vector3.back);
            for (int i = 0; i < n; i++)
            {
                vertices.Add(new Vector3(outline[i].x, outline[i].y, front));
                normals.Add(Vector3.back);
            }
            int backCenter = vertices.Count;
            vertices.Add(new Vector3(0f, 0f, back));
            normals.Add(Vector3.forward);
            for (int i = 0; i < n; i++)
            {
                vertices.Add(new Vector3(outline[i].x, outline[i].y, back));
                normals.Add(Vector3.forward);
            }
            for (int i = 0; i < n; i++)
            {
                int next = (i + 1) % n;
                triangles.Add(frontCenter);
                triangles.Add(frontCenter + 1 + next);
                triangles.Add(frontCenter + 1 + i);
                triangles.Add(backCenter);
                triangles.Add(backCenter + 1 + i);
                triangles.Add(backCenter + 1 + next);
            }

            // 옆면
            int sideFront = vertices.Count;
            for (int i = 0; i < n; i++)
            {
                vertices.Add(new Vector3(outline[i].x, outline[i].y, front));
                normals.Add(new Vector3(outward[i].x, outward[i].y, 0f));
            }
            int sideBack = vertices.Count;
            for (int i = 0; i < n; i++)
            {
                vertices.Add(new Vector3(outline[i].x, outline[i].y, back));
                normals.Add(new Vector3(outward[i].x, outward[i].y, 0f));
            }
            for (int i = 0; i < n; i++)
            {
                int next = (i + 1) % n;
                triangles.Add(sideFront + i);
                triangles.Add(sideFront + next);
                triangles.Add(sideBack + i);
                triangles.Add(sideFront + next);
                triangles.Add(sideBack + next);
                triangles.Add(sideBack + i);
            }

            var mesh = new Mesh { name = "PhoneBody" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
