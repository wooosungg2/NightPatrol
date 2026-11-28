using System.Collections.Generic;
using UnityEngine;

namespace NightPatrol.Photo
{
    /// <summary>
    /// 사진에 찍혔는지 볼 모양: 가운데 점, 크기(바운드 대각선, m), 벽·바닥에 붙은 것이면 보이는 쪽.
    /// 가림을 볼 때 Owner 아래 충돌체(대상 자신)는 막는 것으로 치지 않는다.
    /// 단 solid에 든 것(달라지지 않은 자기 몸 부품 — 강의실 벽·문처럼 원래 있던 것)과 그 아래 충돌체는 막는다
    /// </summary>
    public readonly struct PhotoShape
    {
        /// <summary>크기를 이보다 작게 보지 않는다 (렌더러가 없거나 아주 작은 대상)</summary>
        public const float MinSize = 0.3f;

        public readonly Vector3 Center;
        public readonly float Size;
        /// <summary>셔터 순간의 월드 바운드. 부품끼리 합치지 않고 각 부품에 레이를 쏜다.</summary>
        public readonly Bounds Bounds;
        /// <summary>면 대상이면 보이는 쪽(단위 벡터), 아니면 zero</summary>
        public readonly Vector3 Normal;
        public readonly Transform Owner;
        private readonly HashSet<Transform> solid;

        public PhotoShape(Vector3 center, float size, Transform owner, Vector3 normal = default)
            : this(new Bounds(center, Vector3.one * (Mathf.Max(MinSize, size) / Mathf.Sqrt(3f))), owner, normal)
        {
        }

        public PhotoShape(Bounds bounds, Transform owner, Vector3 normal = default)
        {
            Bounds = bounds;
            Center = bounds.center;
            Size = Mathf.Max(MinSize, bounds.size.magnitude);
            Owner = owner;
            Normal = normal.sqrMagnitude > 0f ? normal.normalized : Vector3.zero;
            solid = null;
        }

        /// <summary>solid: Owner 아래지만 앞을 가리는 것 (null이면 Owner 아래는 모두 안 가림)</summary>
        public PhotoShape(Bounds bounds, Transform owner, HashSet<Transform> solid) : this(bounds, owner)
        {
            this.solid = solid;
        }

        /// <summary>벽·바닥에 붙어 한쪽에서만 보이는 대상인지</summary>
        public bool IsSurface => Normal != Vector3.zero;

        /// <summary>이 충돌체가 이 부품을 가리는 것으로 치는지. Owner 밖은 늘 가림, Owner 아래는 solid에 든 것과 그 아래만</summary>
        public bool Hides(Collider collider)
        {
            if (collider == null) return false;
            var t = collider.transform;
            if (Owner == null || !t.IsChildOf(Owner)) return true;
            if (solid == null) return false;
            for (; t != null && t != Owner; t = t.parent)
                if (solid.Contains(t)) return true;
            return false;
        }

        /// <summary>
        /// 그 오브젝트와 자식 중 켜진 렌더러를 합친 모양. 켜진 렌더러가 없으면 false
        /// </summary>
        public static bool TryFromRenderers(GameObject root, out PhotoShape shape, Vector3 normal = default)
        {
            shape = default;
            if (root == null) return false;
            bool found = false;
            var bounds = new Bounds();
            foreach (var renderer in root.GetComponentsInChildren<Renderer>())
            {
                if (renderer == null || !renderer.enabled) continue;
                if (!found) bounds = renderer.bounds;
                else bounds.Encapsulate(renderer.bounds);
                found = true;
            }
            if (found) shape = new PhotoShape(bounds, root.transform, normal);
            return found;
        }
    }
}
