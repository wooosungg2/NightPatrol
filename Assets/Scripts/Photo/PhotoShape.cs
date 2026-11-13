using UnityEngine;

namespace NightPatrol.Photo
{
    /// <summary>
    /// 사진에 찍혔는지 볼 모양: 가운데 점, 크기(바운드 대각선, m), 벽·바닥에 붙은 것이면 보이는 쪽.
    /// 가림을 볼 때 Owner 아래 충돌체(대상 자신)는 막는 것으로 치지 않는다
    /// </summary>
    public readonly struct PhotoShape
    {
        /// <summary>크기를 이보다 작게 보지 않는다 (렌더러가 없거나 아주 작은 대상)</summary>
        public const float MinSize = 0.3f;

        public readonly Vector3 Center;
        public readonly float Size;
        /// <summary>면 대상이면 보이는 쪽(단위 벡터), 아니면 zero</summary>
        public readonly Vector3 Normal;
        public readonly Transform Owner;

        public PhotoShape(Vector3 center, float size, Transform owner, Vector3 normal = default)
        {
            Center = center;
            Size = Mathf.Max(MinSize, size);
            Owner = owner;
            Normal = normal.sqrMagnitude > 0f ? normal.normalized : Vector3.zero;
        }

        /// <summary>벽·바닥에 붙어 한쪽에서만 보이는 대상인지</summary>
        public bool IsSurface => Normal != Vector3.zero;

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
            if (found) shape = new PhotoShape(bounds.center, bounds.size.magnitude, root.transform, normal);
            return found;
        }
    }
}
