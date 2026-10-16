using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using System.Text;
using NightPatrol.Interaction;
using NightPatrol.Patrol;
using NightPatrol.Run;
using UnityEngine.InputSystem;
#endif

namespace NightPatrol.Debugging
{
    /// <summary>
    /// F1 개발자 정보 표시 (에디터·개발 빌드). F1을 누를 때마다 끔 → 간단(한 줄) → 자세히(패널) → 끔.
    /// 자세히: 진행 · 점검 항목(처리됨 초록·남음 빨강·예외 노랑) · 비정상 사물 · 조준 대상 · 다른 코드가 붙인 구역 · 개발자 키.
    /// 구역은 DevInfo에 등록된 것을 order 순으로 그린다 — 새 정보는 이 파일을 고치지 않고 DevInfo.Register로.
    /// 끔이 아닐 때만 개발자 키(DevMenu)가 동작한다. Player 루트(PlayerInteractor와 같은 오브젝트)에 붙인다
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InteractionDebugView : MonoBehaviour
    {
        [Tooltip("시작할 때 단계 (보통 끔)")]
        [SerializeField] private DevView view = DevView.Off;

        /// <summary>지금 단계</summary>
        public DevView View => view;
        /// <summary>끔이 아닌지. 개발자 키(F2~F5, PgUp/PgDn)는 이때만 동작한다 (테스터가 실수로 누르지 않게)</summary>
        public bool IsVisible => view != DevView.Off;

        /// <summary>단계를 바로 정한다 (F1 대신, 테스트·다른 도구용)</summary>
        public void SetView(DevView next) => view = next;

        /// <summary>프레임 시간을 부드럽게 (숫자가 매 프레임 튀지 않게). 처음(0)이면 그 값 그대로</summary>
        public static float SmoothFrameSeconds(float smoothed, float frameSeconds)
        {
            if (frameSeconds <= 0f) return smoothed;
            return smoothed <= 0f ? frameSeconds : Mathf.Lerp(smoothed, frameSeconds, 0.1f);
        }

        /// <summary>프레임 시간 → 초당 프레임 (0이면 0)</summary>
        public static float Fps(float frameSeconds) => frameSeconds > 0f ? 1f / frameSeconds : 0f;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // 글은 초당 4번, 씬 검색(비정상 사물)은 0.5초마다 — 매 프레임 하지 않는다
        private const float RebuildSeconds = 0.25f;
        private const float SearchSeconds = 0.5f;
        private const int AbnormalShown = 6;

        private readonly GUIContent compactContent = new GUIContent();
        private readonly GUIContent fullContent = new GUIContent();
        private readonly List<string> abnormalIds = new List<string>();
        private PlayerInteractor interactor;
        private PatrolRun run;
        private FloorTasks floorTasks;
        private PlaytestLog playtest;
        private float smoothedFrameSeconds;
        private double nextRebuild, nextSearch;
        private GUIStyle textStyle;
        private Texture2D background;

        private void Awake()
        {
            interactor = GetComponent<PlayerInteractor>();
            run = UnityEngine.Object.FindAnyObjectByType<PatrolRun>();
            floorTasks = UnityEngine.Object.FindAnyObjectByType<FloorTasks>();
            playtest = UnityEngine.Object.FindAnyObjectByType<PlaytestLog>();
        }

        private void OnEnable()
        {
            DevInfo.Register(this, "진행", ProgressText, 0);
            DevInfo.Register(this, "점검 항목", TasksText, 10);
            DevInfo.Register(this, "비정상 사물", () => DevInfo.AbnormalLine(abnormalIds, AbnormalShown), 20);
            DevInfo.Register(this, "조준 대상", AimText, 30);
        }

        private void OnDisable() => DevInfo.Unregister(this);

        private void OnDestroy()
        {
            if (background != null) Destroy(background);
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
            {
                view = DevInfo.Next(view);
                nextRebuild = 0;   // 바꾼 단계를 바로 그리게
            }
        }

        private void LateUpdate()
        {
            if (view == DevView.Off) return;
            smoothedFrameSeconds = SmoothFrameSeconds(smoothedFrameSeconds, Time.unscaledDeltaTime);
            double now = Time.unscaledTimeAsDouble;
            if (now >= nextSearch)
            {
                nextSearch = now + SearchSeconds;
                abnormalIds.Clear();
                foreach (var obj in PatrolInspection.FindAbnormal()) abnormalIds.Add(obj.Id);
            }
            if (now < nextRebuild) return;
            nextRebuild = now + RebuildSeconds;
            compactContent.text = ProgressText() + DevInfo.Paint("   F1: 자세히", DevInfo.DimColor);
            fullContent.text = view == DevView.Full ? FullText() : string.Empty;
        }

        /// <summary>조준 대상 (일반 글): "이름 — 거리 m" / 문구 / 상태. 대상이 없으면 "없음"</summary>
        public string GetTargetText()
        {
            if (!TryAim(out string where, out string prompt, out string state, out _)) return where;
            return prompt == null ? where : $"{where}\n{prompt}\n{state}";
        }

        // 게임과 같은 조준 레이로, 거리 밖 대상도 보이게 거리 제한 없이 쏜다
        private bool TryAim(out string where, out string prompt, out string state, out bool usable)
        {
            prompt = state = null;
            usable = false;
            if (interactor == null)
            {
                where = "PlayerInteractor 없음";
                return false;
            }
            var playerCamera = interactor.PlayerCamera;
            var settings = interactor.Settings;
            if (playerCamera == null || settings == null)
            {
                where = "카메라 / PlayerSettings 미지정";
                return false;
            }
            if (!playerCamera.isActiveAndEnabled ||
                !Physics.Raycast(interactor.AimRay(), out RaycastHit hit, Mathf.Infinity, interactor.RaycastMask,
                    QueryTriggerInteraction.Ignore))
            {
                where = "없음";
                return false;
            }
            where = $"{hit.collider.gameObject.name} — {hit.distance:F2} m";
            var target = hit.collider.GetComponentInParent<IInteractable>();
            if (target == null || (target is Object obj && obj == null)) return true;
            prompt = target.Prompt;
            bool inactive = target is Behaviour behaviour && !behaviour.isActiveAndEnabled;
            usable = !inactive && hit.distance <= settings.InteractionDistance;
            state = inactive ? "상호작용 비활성" : usable ? "상호작용 가능" : "거리 밖";
            return true;
        }

        private string ProgressText()
        {
            float fps = Fps(smoothedFrameSeconds);
            if (run == null) return $"PatrolRun 없음 · {Mathf.RoundToInt(fps)} FPS";
            string floorTime = playtest != null ? PlaytestLog.Clock(playtest.CurrentFloorSeconds) : null;
            return DevInfo.ProgressLine(run.CurrentFloor, run.Attempt, run.IsCleared, floorTime, fps);
        }

        private string TasksText()
        {
            if (floorTasks == null) return DevInfo.Paint("FloorTasks 없음", DevInfo.DimColor);
            var text = new StringBuilder();
            foreach (var task in floorTasks.CurrentTasks)
            {
                if (task == null) continue;
                if (text.Length > 0) text.Append('\n');
                bool clear = task.Target != null && task.Target.IsClear;
                text.Append(DevInfo.TaskLine(task.Label, task.Target != null ? task.Target.Id : null, clear, task.IsException));
            }
            return text.Length > 0 ? text.ToString() : DevInfo.Paint("없음", DevInfo.DimColor);
        }

        private string AimText()
        {
            if (!TryAim(out string where, out string prompt, out string state, out bool usable))
                return DevInfo.Paint(where, DevInfo.DimColor);
            if (prompt == null) return where;
            string shownPrompt = prompt.Length > 0 ? prompt : DevInfo.Paint("(문구 없음)", DevInfo.DimColor);
            return $"{where}\n{shownPrompt}\n{DevInfo.Paint(state, usable ? DevInfo.OkColor : DevInfo.NoteColor)}";
        }

        private string FullText()
        {
            var text = new StringBuilder();
            foreach (var section in DevInfo.Sections)
            {
                string body = DevInfo.SafeContent(section.Content);
                if (body.Length == 0) continue;
                if (text.Length > 0) text.Append("\n\n");
                text.Append(DevInfo.SectionText(section.Title, body));
            }
            return text.ToString();
        }

        private void OnGUI()
        {
            if (view == DevView.Off) return;
            EnsureStyle();
            var content = view == DevView.Full ? fullContent : compactContent;
            float pad = textStyle.fontSize * 0.6f;
            float width = view == DevView.Full
                ? Mathf.Clamp(Screen.width * 0.36f, Mathf.Min(360f, Screen.width - 24f), Screen.width - 24f)
                : Mathf.Min(textStyle.CalcSize(content).x + pad * 2f, Screen.width - 24f);
            float height = textStyle.CalcHeight(content, width - pad * 2f) + pad * 2f;
            var box = new Rect(12f, 12f, width, height);
            GUI.DrawTexture(box, background);
            GUI.Label(new Rect(box.x + pad, box.y + pad, width - pad * 2f, height - pad * 2f), content, textStyle);
            if (view == DevView.Full)   // 조준 레이 위치 (HUD 조준점과 겹치는지 확인용)
                GUI.DrawTexture(new Rect(Screen.width * 0.5f - 2f, Screen.height * 0.5f - 2f, 4f, 4f), Texture2D.whiteTexture);
        }

        // 기본 IMGUI 회색 상자 대신: 어두운 반투명 바탕 + 화면 높이에 맞춘 글자 + 리치 텍스트 색
        private void EnsureStyle()
        {
            if (background == null)
            {
                background = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
                background.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.72f));
                background.Apply();
            }
            int size = DevInfo.FontSize(Screen.height);
            if (textStyle != null && textStyle.fontSize == size) return;
            textStyle = new GUIStyle(GUI.skin.label) { fontSize = size, richText = true, wordWrap = true };
            textStyle.normal.textColor = new Color(0.93f, 0.93f, 0.93f);
        }
#endif
    }
}
