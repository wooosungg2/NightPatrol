using System.Collections;
using UnityEngine;

namespace NightPatrol.Run
{
    /// <summary>
    /// 화면 전체를 덮는 검은 화면의 투명도를 조절한다 (0 = 안 보임, 1 = 완전히 검음).
    /// 검은 Image는 세팅에서 같은 오브젝트에 붙인다 — 코드는 CanvasGroup만 다룬다 (Image 색을 직접 바꾸지 않아 자식 글자까지 같이 흐려짐)
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ScreenFader : MonoBehaviour
    {
        [Tooltip("비어 있으면 같은 오브젝트에서 찾는다")]
        [SerializeField] private CanvasGroup group;

        /// <summary>지금 어두운 정도 (0~1)</summary>
        public float Alpha => group != null ? group.alpha : 0f;

        private void Awake()
        {
            if (group == null) group = GetComponent<CanvasGroup>();
            if (group == null) return;
            group.blocksRaycasts = false;
            group.interactable = false;
        }

        /// <summary>바로 그 어두움으로</summary>
        public void SetAlpha(float alpha)
        {
            if (group != null) group.alpha = Mathf.Clamp01(alpha);
        }

        /// <summary>seconds 동안 target 어두움까지 (0 이하면 즉시)</summary>
        public IEnumerator FadeTo(float target, float seconds)
        {
            target = Mathf.Clamp01(target);
            // 도중에 CanvasGroup이 없어지면 바로 끝낸다 (기다리던 연출이 멈춰 조작이 잠긴 채로 남지 않게)
            while (group != null)
            {
                SetAlpha(Step(Alpha, target, Time.deltaTime, seconds));
                if (Mathf.Approximately(Alpha, target)) break;
                yield return null;
            }
            SetAlpha(target);
        }

        /// <summary>어두움을 target 쪽으로 한 프레임만큼 옮김 (seconds 0 이하면 바로 target)</summary>
        public static float Step(float alpha, float target, float deltaTime, float seconds)
        {
            if (seconds <= 0f) return Mathf.Clamp01(target);
            return Mathf.MoveTowards(Mathf.Clamp01(alpha), Mathf.Clamp01(target), Mathf.Max(0f, deltaTime) / seconds);
        }
    }
}
