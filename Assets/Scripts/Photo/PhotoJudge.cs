using System;
using System.Collections.Generic;
using NightPatrol.Anomalies;
using UnityEngine;

namespace NightPatrol.Photo
{
    /// <summary>촬영 판정 설정. 카메라 앱 Inspector에서 바꾸고 플레이테스트로 맞춘다</summary>
    [Serializable]
    public sealed class PhotoRules
    {
        [Tooltip("조준 박스: 뷰파인더 가운데 이 비율 안에 대상의 가운데가 있어야 함 (0.4 = 가로·세로 40%)")]
        [Range(0.1f, 1f)] public float aimBox = 0.4f;
        [Tooltip("인정 거리 = 크기(m) × 이 값")]
        [Min(0.1f)] public float distancePerSize = 6f;
        [Tooltip("인정 거리 최소(m) — 아주 작은 것도 이만큼은 떨어져서 찍을 수 있음")]
        [Min(0.1f)] public float minDistance = 2f;
        [Tooltip("인정 거리 최대(m) — 아무리 큰 것도 이보다 멀면 안 찍힘")]
        [Min(0.1f)] public float maxDistance = 15f;
        [Tooltip("가림: 가운데와 주변 4점, 5점 중 이만큼이 안 막혀야 찍힘")]
        [Range(1, 5)] public int pointsNeeded = 3;
        [Tooltip("벽·바닥에 붙은 대상은 면에서 이만큼(m) 띄운 점을 본다 (자기 벽에 막히지 않게)")]
        [Min(0f)] public float surfaceLift = 0.05f;

        /// <summary>그 크기의 대상을 찍을 수 있는 최대 거리</summary>
        public float AllowedDistance(float size) => Mathf.Clamp(size * distancePerSize, minDistance, Mathf.Max(minDistance, maxDistance));

        /// <summary>뷰포트 좌표(0~1)가 가운데 조준 박스 안인지</summary>
        public bool InAimBox(Vector2 viewport)
        {
            float half = aimBox * 0.5f;
            return Mathf.Abs(viewport.x - 0.5f) <= half && Mathf.Abs(viewport.y - 0.5f) <= half;
        }
    }

    /// <summary>
    /// 한 장에 어떤 이상현상이 찍혔는지. 카메라 앱이 셔터 때 부른다 (화면 없이도 테스트할 수 있게 따로 둠).
    /// 찍힘 = 가운데가 조준 박스 안 + 크기에 비례한 거리 안 + 5점 중 N점이 안 가림 (+ 면 대상은 앞쪽에서)
    /// </summary>
    public static class PhotoJudge
    {
        /// <summary>그 카메라로 지금 찍으면 찍히는 이상현상 (subjects 순서대로)</summary>
        public static List<Anomaly> Capture(Camera camera, PhotoRules rules, IReadOnlyList<PhotoSubject> subjects)
        {
            var result = new List<Anomaly>();
            if (camera == null || rules == null || subjects == null) return result;
            int occluders = Occluders();
            foreach (var subject in subjects)
            {
                foreach (var shape in subject.Shapes)
                {
                    if (!Sees(camera, shape, rules, occluders)) continue;
                    result.Add(subject.Source);
                    break;
                }
            }
            return result;
        }

        /// <summary>그 카메라로 지금 찍으면 이 모양이 찍히는지</summary>
        public static bool Sees(Camera camera, PhotoShape shape, PhotoRules rules) => Sees(camera, shape, rules, Occluders());

        private static bool Sees(Camera camera, PhotoShape shape, PhotoRules rules, int occluders)
        {
            if (camera == null || rules == null) return false;
            var eye = camera.transform.position;
            var center = shape.Center;
            if (shape.IsSurface)
            {
                if (Vector3.Dot(eye - center, shape.Normal) <= 0f) return false;   // 면 뒤쪽 (벽 너머)
                center += shape.Normal * rules.surfaceLift;
            }

            var viewport = camera.WorldToViewportPoint(center);
            if (viewport.z <= camera.nearClipPlane || !rules.InAimBox(viewport)) return false;
            if (Vector3.Distance(eye, center) > rules.AllowedDistance(shape.Size)) return false;

            // 가운데와 카메라 기준 좌우·위아래로 크기의 1/3 떨어진 4점
            var right = camera.transform.right * (shape.Size / 3f);
            var up = camera.transform.up * (shape.Size / 3f);
            var lift = shape.IsSurface ? shape.Normal * rules.surfaceLift : Vector3.zero;
            int needed = Mathf.Clamp(rules.pointsNeeded, 1, 5), seen = 0, left = 5;
            foreach (var point in new[] { center, center + right, center - right, center + up, center - up })
            {
                // 면 대상은 옆 점도 면에서 띄운다 (카메라가 비스듬하면 옆 점이 벽 안으로 들어갈 수 있어서)
                var target = shape.IsSurface ? ProjectOnSurface(point, shape) + lift : point;
                if (!Blocked(eye, target, shape.Owner, occluders)) seen++;
                left--;
                if (seen >= needed) return true;
                if (seen + left < needed) return false;
            }
            return false;
        }

        // 면 위로 내린 점 (면 대상의 옆 점용)
        private static Vector3 ProjectOnSurface(Vector3 point, PhotoShape shape) =>
            point - shape.Normal * Vector3.Dot(point - shape.Center, shape.Normal);

        // 카메라 → 점 사이를 다른 충돌체가 막는지. 대상 자신(Owner 아래)의 충돌체에 닿은 것은 막힌 게 아님. 트리거는 무시
        private static bool Blocked(Vector3 from, Vector3 to, Transform owner, int occluders)
        {
            if (!Physics.Linecast(from, to, out var hit, occluders, QueryTriggerInteraction.Ignore)) return false;
            return owner == null || !hit.transform.IsChildOf(owner);
        }

        // 가림에 쓰는 레이어: 기본 레이어에서 손에 든 물체(FirstPerson)를 뺌
        private static int Occluders()
        {
            int mask = Physics.DefaultRaycastLayers;
            int firstPerson = LayerMask.NameToLayer("FirstPerson");
            if (firstPerson >= 0) mask &= ~(1 << firstPerson);
            return mask;
        }
    }
}
