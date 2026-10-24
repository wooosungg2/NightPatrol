using NightPatrol.Controls;
using TMPro;
using UnityEngine;

namespace NightPatrol.Interaction
{
    /// <summary>
    /// 화면 가운데 조준점과, 조작할 수 있는 대상을 볼 때 그 아래 문구(Prompt)를 띄운다.
    /// 일지를 든 동안·커서를 푼 동안은 숨기고, E를 누르면 조준점이 잠깐 커진다.
    /// 길게 눌러야 하는 대상(엘리베이터)은 누르는 동안 조준점 둘레 링이 찬다.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)] // PlayerInteractor의 LateUpdate가 대상을 고른 다음에 읽는다
    public sealed class InteractionPromptView : MonoBehaviour
    {
        [Tooltip("비어 있으면 부모에서, 없으면 씬에서 찾는다")]
        [SerializeField] private PlayerInteractor interactor;
        [Tooltip("조준점 글자 (•)")]
        [SerializeField] private TMP_Text crosshair;
        [Tooltip("조준한 대상의 안내 글자 (예: E - 전등 끄기). E 자리는 지금 설정된 키 이름으로 바뀐다")]
        [SerializeField] private TMP_Text promptText;
        [Tooltip("E 길게 누르기 진행 링 (엘리베이터 등). 없으면 안 보임")]
        [SerializeField] private ProgressRing holdRing;
        [Tooltip("조작할 대상이 없을 때 조준점 투명도")]
        [SerializeField] private float idleAlpha = 0.35f;
        [Tooltip("조작할 대상이 있을 때 조준점 투명도")]
        [SerializeField] private float targetAlpha = 1f;
        [Tooltip("E를 눌렀을 때 조준점이 커지는 배율")]
        [SerializeField] private float pulseScale = 1.6f;
        [Tooltip("커졌다가 돌아오는 시간(초)")]
        [SerializeField] private float pulseSeconds = 0.15f;
        [Header("첫 시작 조작 힌트 (기본 꺼짐)")]
        [Tooltip("게임을 시작하면 잠깐 조준점 아래에 조작 키를 보여 준다 (지금 설정된 키 이름으로). 대상을 조준하면 그 안내가 먼저")]
        [SerializeField] private bool showStartHint = false;
        [Tooltip("힌트를 보여 주는 시간 (초, 게임 시간이라 ESC 메뉴를 연 동안은 멈춤)")]
        [SerializeField, Min(0f)] private float startHintSeconds = 8f;
        [Tooltip("힌트에 쓸 조작 키 목록 (Assets/Settings/KeyBindingList). 비어 있으면 힌트 없음")]
        [SerializeField] private KeyBindingList hintKeys = null;
        private PlayerInteractor subscribed;
        private string shownPrompt;
        private string keyName = "E";
        private string cachedPrompt;
        private string cachedKey;
        private string cachedResult = string.Empty;
        private float pulseStartTime = float.NegativeInfinity;
        private string hintText = string.Empty;
        private float hintUntil = float.NegativeInfinity;

        private void Awake()
        {
            if (interactor == null) interactor = GetComponentInParent<PlayerInteractor>();
            if (interactor == null) interactor = Object.FindAnyObjectByType<PlayerInteractor>();
        }

        private void OnEnable()
        {
            subscribed = interactor;
            if (subscribed != null) subscribed.Interacted += OnInteracted;
            KeyBindings.Changed += RefreshKeyName;
        }

        private void OnDisable()
        {
            if (subscribed != null) subscribed.Interacted -= OnInteracted;
            subscribed = null;
            KeyBindings.Changed -= RefreshKeyName;
        }

        // PlayerInteractor.Awake 뒤에 키 이름을 읽는다 (OnEnable은 그보다 먼저일 수 있음)
        private void Start()
        {
            if (showStartHint && hintKeys != null) hintUntil = Time.time + startHintSeconds;
            RefreshKeyName();
        }

        private void RefreshKeyName()
        {
            var action = interactor != null ? interactor.InteractAction : null;
            keyName = action != null
                ? KeyBindings.KeyName(action, KeyBindings.FindBindingIndex(action, null, KeyBindings.KeyboardMouse))
                : "E";
            // 힌트를 보여 주는 동안 키를 바꾸면 힌트도 새 키로
            if (Time.time < hintUntil) hintText = ControlsHint.Text(ControlsHint.From(hintKeys));
        }

        private void OnInteracted(IInteractable _) => pulseStartTime = Time.time;

        private void LateUpdate()
        {
            bool visible = IsVisible(interactor != null && interactor.isActiveAndEnabled,
                interactor != null && interactor.IsBlocked, Cursor.lockState == CursorLockMode.Locked);
            string rawPrompt = PromptFor(visible, interactor != null ? interactor.CurrentTarget : null);
            if (rawPrompt != cachedPrompt || keyName != cachedKey)
            {
                cachedPrompt = rawPrompt;
                cachedKey = keyName;
                cachedResult = WithKey(rawPrompt, keyName);
            }
            string prompt = cachedResult;
            string shown = WithHint(prompt, visible, hintText, Time.time < hintUntil);

            if (crosshair != null)
            {
                crosshair.enabled = visible;
                // 문구가 없는 대상(예: 연출 중인 엘리베이터)은 조작할 게 없으므로 흐리게
                crosshair.alpha = prompt.Length > 0 ? targetAlpha : idleAlpha;
                // 크기가 그대로면 대입하지 않는다 (매 프레임 Canvas를 다시 그리지 않게)
                float scale = PulseScale(Time.time - pulseStartTime, pulseSeconds, pulseScale);
                if (crosshair.transform.localScale.x != scale) crosshair.transform.localScale = Vector3.one * scale;
            }
            if (promptText != null && shown != shownPrompt)
            {
                promptText.text = shown;   // 바뀔 때만 대입 (TMP가 매 프레임 글자를 다시 만들지 않게)
                shownPrompt = shown;
            }
            if (holdRing != null)
            {
                // 문구가 없는 대상(연출 중인 엘리베이터 등)은 눌러도 할 일이 없으므로 링도 안 보인다
                float progress = prompt.Length > 0 && interactor != null ? interactor.HoldProgress : 0f;
                holdRing.Fill = progress;
                holdRing.enabled = progress > 0f;
            }
        }

        /// <summary>조준점·문구를 보여도 되는지: 플레이어가 있고, 막혀 있지 않고, 커서가 잠겨 있을 때</summary>
        public static bool IsVisible(bool hasInteractor, bool blocked, bool cursorLocked) =>
            hasInteractor && !blocked && cursorLocked;

        /// <summary>보일 문구. 안 보이는 상태거나 대상이 없으면(파괴된 오브젝트 포함) 빈 문자열</summary>
        public static string PromptFor(bool visible, IInteractable target)
        {
            if (!visible || target == null) return string.Empty;
            if (target is Object obj && obj == null) return string.Empty;
            return target.Prompt ?? string.Empty;
        }

        /// <summary>
        /// 문구 맨 앞의 "E"(사물 문구는 "E - 문 닫기"처럼 씀)를 지금 상호작용 키 이름으로 바꾼다.
        /// 키가 E 그대로거나 문구가 "E " · "E-"로 시작하지 않으면 그대로
        /// </summary>
        public static string WithKey(string prompt, string key)
        {
            if (string.IsNullOrEmpty(prompt)) return string.Empty;
            if (string.IsNullOrEmpty(key) || key == "E" || key == "-") return prompt;
            if (prompt[0] != 'E' || (prompt.Length > 1 && prompt[1] != ' ' && prompt[1] != '-')) return prompt;
            return key + prompt.Substring(1);
        }

        /// <summary>
        /// 안내 자리에 보일 글: 대상 안내가 있으면 그것, 없고 힌트 시간이면 힌트. 안 보이는 상태(일지를 든 동안 등)면 힌트도 없음.
        /// 조준점 밝기·진행 링은 힌트와 상관없이 대상 안내로만 정한다
        /// </summary>
        public static string WithHint(string prompt, bool visible, string hint, bool hintActive) =>
            string.IsNullOrEmpty(prompt) && visible && hintActive && !string.IsNullOrEmpty(hint) ? hint : prompt ?? string.Empty;

        /// <summary>E를 누른 뒤 elapsed초가 지났을 때 조준점 배율: peak에서 1로 선형, 범위 밖이면 1</summary>
        public static float PulseScale(float elapsed, float seconds, float peak)
        {
            if (seconds <= 0f || elapsed < 0f || elapsed >= seconds) return 1f;
            return Mathf.Lerp(peak, 1f, elapsed / seconds);
        }
    }
}
