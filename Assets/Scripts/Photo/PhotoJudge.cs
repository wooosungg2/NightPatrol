using System;
using System.Collections.Generic;
using NightPatrol.Anomalies;
using UnityEngine;

namespace NightPatrol.Photo
{
    [Serializable]
    public sealed class PhotoRules
    {
        [Tooltip("조준 박스: 가운데에서 레이를 쏘는 가로·세로 비율 (0.4 = 40%)")]
        [Range(0.1f, 1f)] public float aimBox = 0.4f;
        [Tooltip("인정 거리 = 부품 크기(m, 바운드 대각선) × 이 값")]
        [Min(0.1f)] public float distancePerSize = 6f;
        [Tooltip("아주 작은 부품도 찍을 수 있는 최소 인정 거리(m)")]
        [Min(0.1f)] public float minDistance = 2f;
        [Tooltip("촬영과 가림 레이의 최대 거리(m)")]
        [Min(0.1f)] public float maxDistance = 15f;
        [Tooltip("부품 앞의 충돌체를 가림으로 보는 여유 거리(m)")]
        [Min(0f)] public float surfaceLift = 0.05f;
        [Tooltip("안쪽부터 각 링의 레이 수. 가운데 레이 1발은 항상 추가됨")]
        public int[] raysPerRing = { 8, 12, 16 };
        [Tooltip("가운데와 이 번호까지의 안쪽 링은 1발만 보여도 찍힘 (0 = 가운데만)")]
        [Min(0)] public int innerRings = 1;
        [Tooltip("바깥 링에서만 보일 때 찍힘에 필요한 레이 수 (여러 링 합산)")]
        [Min(1)] public int outerMinHits = 3;

        public float AllowedDistance(float size) => Mathf.Clamp(size * distancePerSize, minDistance, Mathf.Max(minDistance, maxDistance));

        public bool InAimBox(Vector2 viewport)
        {
            float half = aimBox * 0.5f;
            return Mathf.Abs(viewport.x - 0.5f) <= half && Mathf.Abs(viewport.y - 0.5f) <= half;
        }
    }

    /// <summary>셔터 레이 하나. Ring 0은 가운데, 빗나간 Distance는 +Infinity.
    /// TooFar는 본 부품이 인정 거리 밖이라 집계에서 빠졌다는 뜻이다.
    /// UsedForCapture는 거리 안의 레이가 본 이상현상이 이번 사진에 찍혔다는 뜻이다.</summary>
    public readonly struct PhotoRayResult
    {
        public readonly Vector2 Viewport;
        public readonly int Ring;
        public readonly Anomaly Seen;
        public readonly float Distance;
        public readonly bool TooFar;
        public readonly bool UsedForCapture;

        public PhotoRayResult(Vector2 viewport, int ring, Anomaly seen, float distance, bool usedForCapture, bool tooFar = false)
        {
            Viewport = viewport;
            Ring = ring;
            Seen = seen;
            Distance = distance;
            TooFar = tooFar;
            UsedForCapture = usedForCapture && !tooFar;
        }
    }

    /// <summary>한 장의 판정 스냅샷. 레이는 가운데부터 바깥 링 순서다.</summary>
    public sealed class PhotoCaptureResult
    {
        public IReadOnlyList<Anomaly> Captured { get; }
        public IReadOnlyList<PhotoRayResult> Rays { get; }

        internal PhotoCaptureResult(List<Anomaly> captured, List<PhotoRayResult> rays)
        {
            Captured = captured.AsReadOnly();
            Rays = rays.AsReadOnly();
        }
    }

    public static class PhotoJudge
    {
        /// <summary>각 레이에서 처음 보이는 부품을 찾고 안쪽 1발 또는 바깥 N발로 판정한다.</summary>
        public static PhotoCaptureResult Capture(Camera camera, PhotoRules rules, IReadOnlyList<PhotoSubject> subjects)
        {
            var captured = new List<Anomaly>();
            var rays = new List<PhotoRayResult>();
            if (camera == null || rules == null || subjects == null) return new PhotoCaptureResult(captured, rays);
            var inner = new HashSet<Anomaly>();
            var outer = new Dictionary<Anomaly, int>();
            int mask = Occluders();
            Physics.SyncTransforms();   // 이번 프레임에 움직인 문·물체의 가림도 셔터 순간 위치로 확인
            foreach (var sample in Samples(rules))
            {
                var ray = CameraRay(camera, sample.viewport);
                var blockers = Physics.RaycastAll(ray, Mathf.Max(0f, rules.maxDistance), mask, QueryTriggerInteraction.Ignore);
                Anomaly seen = null;
                float nearest = float.PositiveInfinity;
                float allowedDistance = 0f;
                foreach (var subject in subjects)
                {
                    if (subject.Source == null || !subject.Source.IsActive) continue;
                    foreach (var shape in subject.Shapes)
                    {
                        if (!Visible(ray, shape, rules, blockers, out float distance) || distance >= nearest) continue;
                        nearest = distance;
                        allowedDistance = rules.AllowedDistance(shape.Size);
                        seen = subject.Source;
                    }
                }
                // 가까운 작은 부품이 거리 밖이라고 그 뒤의 큰 이상현상으로 넘어가지는 않는다.
                bool tooFar = seen != null && nearest > allowedDistance;
                rays.Add(new PhotoRayResult(sample.viewport, sample.ring, seen, nearest, false, tooFar));
                if (seen == null || tooFar) continue;
                if (sample.ring <= Mathf.Max(0, rules.innerRings)) inner.Add(seen);
                else outer[seen] = outer.TryGetValue(seen, out int count) ? count + 1 : 1;
            }
            var accepted = new HashSet<Anomaly>();
            foreach (var subject in subjects)
            {
                var source = subject.Source;
                if (source == null) continue;
                if ((inner.Contains(source) || outer.TryGetValue(source, out int count) && count >= Mathf.Max(1, rules.outerMinHits))
                    && accepted.Add(source)) captured.Add(source);
            }
            for (int i = 0; i < rays.Count; i++)
            {
                var ray = rays[i];
                rays[i] = new PhotoRayResult(ray.Viewport, ray.Ring, ray.Seen, ray.Distance,
                    !ray.TooFar && ray.Seen != null && accepted.Contains(ray.Seen), ray.TooFar);
            }
            return new PhotoCaptureResult(captured, rays);
        }

        /// <summary>부품 하나에 같은 링 규칙을 적용한다 (유리·가림 확인에도 사용).</summary>
        public static bool Sees(Camera camera, PhotoShape shape, PhotoRules rules)
        {
            if (camera == null || rules == null) return false;
            int outer = 0, mask = Occluders();
            Physics.SyncTransforms();
            foreach (var sample in Samples(rules))
            {
                var ray = CameraRay(camera, sample.viewport);
                var blockers = Physics.RaycastAll(ray, Mathf.Max(0f, rules.maxDistance), mask, QueryTriggerInteraction.Ignore);
                if (!Visible(ray, shape, rules, blockers, out float distance) || distance > rules.AllowedDistance(shape.Size)) continue;
                if (sample.ring <= Mathf.Max(0, rules.innerRings)) return true;
                if (++outer >= Mathf.Max(1, rules.outerMinHits)) return true;
            }
            return false;
        }

        private static IEnumerable<(Vector2 viewport, int ring)> Samples(PhotoRules rules)
        {
            yield return (new Vector2(0.5f, 0.5f), 0);
            int rings = rules.raysPerRing?.Length ?? 0;
            for (int ring = 1; ring <= rings; ring++)
            {
                int count = Mathf.Max(0, rules.raysPerRing[ring - 1]);
                float radius = Mathf.Clamp01(rules.aimBox) * 0.5f * ring / rings;
                for (int i = 0; i < count; i++)
                {
                    float angle = (i + 0.5f * ring) * 2f * Mathf.PI / count;
                    yield return (new Vector2(0.5f + Mathf.Cos(angle) * radius, 0.5f + Mathf.Sin(angle) * radius), ring);
                }
            }
        }

        private static Ray CameraRay(Camera camera, Vector2 viewport)
        {
            var ray = camera.ViewportPointToRay(new Vector3(viewport.x, viewport.y, 0f));
            if (!camera.orthographic) ray.origin = camera.transform.position;
            return ray;
        }

        private static bool Visible(Ray ray, PhotoShape shape, PhotoRules rules, RaycastHit[] blockers, out float distance)
        {
            distance = 0f;
            if (shape.IsSurface && Vector3.Dot(ray.origin - shape.Center, shape.Normal) <= 0f) return false;
            // 눈이 부품 상자 안 (방 전체를 꾸민 이상현상 안에 서 있음): 어느 쪽을 찍어도 보인다.
            // 다른 부품과는 상자를 나가는 거리로 앞뒤를 가린다 (방 안의 다른 이상현상이 가까우면 그쪽이 먼저)
            if (!shape.IsSurface && shape.Bounds.Contains(ray.origin))
            {
                distance = Mathf.Min(ExitDistance(shape.Bounds, ray), Mathf.Max(0f, rules.maxDistance));
                return true;
            }
            if (!shape.Bounds.IntersectRay(ray, out distance) || distance < 0f ||
                distance > rules.maxDistance) return false;
            foreach (var hit in blockers)
            {
                if (hit.distance >= distance - Mathf.Max(0f, rules.surfaceLift)) continue;
                if (!shape.Hides(hit.collider)) continue;   // 대상 자신의 충돌체 (달라지지 않은 자기 몸 부품은 가림)
                if (PhotoOcclusion.AllowsPhotos(hit.collider)) continue;
                return false;
            }
            return true;
        }

        // 상자 안에서 출발한 레이가 상자를 나가는 거리: 상자 너머 먼 점에서 거꾸로 쏴 들어오는 점을 찾는다
        private static float ExitDistance(Bounds bounds, Ray ray)
        {
            float far = bounds.size.magnitude + 1f;
            var back = new Ray(ray.origin + ray.direction * far, -ray.direction);
            return bounds.IntersectRay(back, out float entry) ? Mathf.Max(0f, far - entry) : 0f;
        }

        private static int Occluders()
        {
            int mask = Physics.DefaultRaycastLayers;
            int firstPerson = LayerMask.NameToLayer("FirstPerson");
            return firstPerson < 0 ? mask : mask & ~(1 << firstPerson);
        }
    }
}
