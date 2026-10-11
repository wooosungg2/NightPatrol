using UnityEngine;
using UnityEngine.UI;

namespace NightPatrol.Interaction
{
    /// <summary>
    /// 원형 진행 표시 (E 길게 누르기). 텍스처 없이 메시로 그린다.
    /// Fill 0~1 = 12시 방향부터 시계 방향으로 찬다. 크기는 RectTransform, 색은 Color
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ProgressRing : MaskableGraphic
    {
        [SerializeField, Range(0f, 1f)] private float fill;
        [Tooltip("링 두께 (UI 단위)")]
        [SerializeField, Min(0.5f)] private float thickness = 3f;
        [Tooltip("원 한 바퀴를 몇 조각으로 그릴지")]
        [SerializeField, Range(12, 128)] private int segments = 48;

        public float Fill
        {
            get => fill;
            set
            {
                value = Mathf.Clamp01(value);
                if (fill == value) return;
                fill = value;
                SetVerticesDirty();
            }
        }

        /// <summary>fill만큼 그릴 조각 수 (0이면 안 그림)</summary>
        public static int SegmentCount(float fill, int segments) =>
            Mathf.CeilToInt(Mathf.Clamp01(fill) * Mathf.Max(1, segments));

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            int count = SegmentCount(fill, segments);
            if (count == 0) return;

            Rect rect = GetPixelAdjustedRect();
            float outer = Mathf.Min(rect.width, rect.height) * 0.5f;
            float inner = Mathf.Max(0f, outer - thickness);
            Vector2 center = rect.center;
            float end = fill * 2f * Mathf.PI;
            for (int i = 0; i <= count; i++)
            {
                // 12시에서 시작해 시계 방향 (각도가 커질수록 오른쪽으로)
                float angle = Mathf.Min(end, i * 2f * Mathf.PI / segments);
                var dir = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));
                vh.AddVert(center + dir * outer, color, Vector4.zero);
                vh.AddVert(center + dir * inner, color, Vector4.zero);
                if (i == 0) continue;
                int v = i * 2;
                vh.AddTriangle(v - 2, v, v + 1);
                vh.AddTriangle(v - 2, v + 1, v - 1);
            }
        }
    }
}
