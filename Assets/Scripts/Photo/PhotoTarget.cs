using NightPatrol.Anomalies;
using UnityEngine;

namespace NightPatrol.Photo
{
    /// <summary>
    /// 보이는 물체가 아예 없는 이상현상의 찍을 자리 (예외용). 보통은 필요 없다 —
    /// 보이는 게 이상현상 오브젝트 아래에 있으면 그대로 되고, 밖에 있으면 이상현상이 IPhotoVisual로 알려 준다.
    /// 그 자리에 빈 오브젝트를 두고 붙인 뒤 Size(m). 벽·바닥에 붙은 자리면 Surface를 켜고 파란 화살표(앞)가 방 쪽을 향하게.
    /// 이상현상 하나에 여러 개를 붙이면 그중 하나만 찍어도 된다
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PhotoTarget : MonoBehaviour
    {
        [Tooltip("이 자리가 맡는 이상현상. 비우면 부모에서 찾는다")]
        [SerializeField] private Anomaly anomaly;
        [Tooltip("크기(m). 클수록 멀리서도 찍힌다 (렌더러가 있으면 렌더러 크기)")]
        [SerializeField, Min(PhotoShape.MinSize)] private float size = 1f;
        [Tooltip("벽·바닥에 붙은 자리면 켬. 파란 화살표(앞)가 보이는 쪽 — 뒤에서는 안 찍힘")]
        [SerializeField] private bool surface;

        /// <summary>맡은 이상현상 (칸이 비면 부모에서 찾은 것)</summary>
        public Anomaly Anomaly
        {
            get
            {
                if (anomaly == null) anomaly = GetComponentInParent<Anomaly>(true);
                return anomaly;
            }
        }

        public float Size => size;
        public bool Surface => surface;

        /// <summary>코드에서 칸을 한 번에 채운다 (테스트·만들어 붙일 때)</summary>
        public void Setup(Anomaly owner, float fallbackSize = 1f, bool onSurface = false)
        {
            anomaly = owner;
            size = Mathf.Max(PhotoShape.MinSize, fallbackSize);
            surface = onSurface;
        }

        /// <summary>찍을 모양: 이 오브젝트의 켜진 렌더러, 없으면 위치 + Size</summary>
        public PhotoShape Shape()
        {
            var normal = surface ? transform.forward : Vector3.zero;
            return PhotoShape.TryFromRenderers(gameObject, out var shape, normal)
                ? shape
                : new PhotoShape(transform.position, size, transform, normal);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.6f, 0.1f);
            Gizmos.DrawWireSphere(transform.position, size * 0.5f);
            if (surface) Gizmos.DrawRay(transform.position, transform.forward * 0.5f);
        }
    }
}
