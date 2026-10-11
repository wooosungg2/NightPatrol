using System;
using System.Collections.Generic;
using System.Text;
using NightPatrol.Controls;
using NightPatrol.Interaction;
using NightPatrol.Journal;
using NightPatrol.Player;
using NightPatrol.Run;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace NightPatrol.Menu
{
    /// <summary>
    /// ESC 일시정지 메뉴. 클립보드를 들어 올려 일지 장 대신 메뉴 장을 보여준다.
    /// 첫 장: 계속하기 · 설정 · 처음으로 · 게임 종료.
    /// 설정 장: 마우스 감도 · 소리 크기 · 화면 모드 · 해상도 · 프레임 제한 · 조작 키 · 뒤로.
    /// 조작 키 장: KeyBindingList의 줄들 · 기본값으로 · 뒤로 (줄을 고르면 "키를 누르세요", ESC 취소).
    /// 여는 동안 시간 정지(Time.timeScale 0)·소리 일시정지·이동·시점·E 잠금, 마우스 커서가 보인다.
    /// 층 이동 연출(어두운 화면) 중에는 열리지 않는다.
    /// 마우스: 줄에 올리면 선택, 클릭하면 실행, ◀ ▶ 클릭으로 값 조절, 휠로 선택 이동.
    /// 키보드: W/S·↑↓ 선택, A/D·←→ 조절, E·Enter·Space 확인, ESC 닫기(안쪽 장에서는 뒤로).
    /// "처음으로"·"게임 종료"는 진행을 잃으므로 한 번 더 눌러야 실행. Player 프리팹의 Clipboard(JournalHolder와 같은 오브젝트)에 붙인다
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PauseMenu : MonoBehaviour
    {
        public enum Page { Main, Settings, Keys }

        public enum Row
        {
            Continue, Settings, Restart, Quit,
            Sensitivity, Volume, ScreenMode, Resolution, FrameLimit, Keys, Back,
            KeyBinding, ResetKeys,
        }

        /// <summary>메뉴 장에 보이는 값</summary>
        public struct Values
        {
            public float Sensitivity;
            public float Volume;
            public bool FullScreen;
            public Vector2Int Resolution;
            public int FrameLimit;
            /// <summary>"처음으로"를 한 번 눌러 확인을 기다리는 중</summary>
            public bool RestartArmed;
            /// <summary>"게임 종료"를 한 번 눌러 확인을 기다리는 중</summary>
            public bool QuitArmed;
            /// <summary>조작 키 장의 줄 이름 (예: 앞으로)</summary>
            public IReadOnlyList<string> KeyLabels;
            /// <summary>조작 키 장의 지금 키 (예: W)</summary>
            public IReadOnlyList<string> KeyNames;
            /// <summary>키 입력을 기다리는 줄 (없으면 null)</summary>
            public int? WaitingKey;
        }

        private static readonly Row[] MainRows = { Row.Continue, Row.Settings, Row.Restart, Row.Quit };
        private static readonly Row[] SettingsRows =
            { Row.Sensitivity, Row.Volume, Row.ScreenMode, Row.Resolution, Row.FrameLimit, Row.Keys, Row.Back };
        private static readonly Key[] UpKeys = { Key.W, Key.UpArrow };
        private static readonly Key[] DownKeys = { Key.S, Key.DownArrow };
        private static readonly Key[] LeftKeys = { Key.A, Key.LeftArrow };
        private static readonly Key[] RightKeys = { Key.D, Key.RightArrow };
        private static readonly Key[] ConfirmKeys = { Key.E, Key.Enter, Key.NumpadEnter, Key.Space };
        // ◀ ▶는 글자가 작아서 클릭 판정을 이만큼(메뉴 장 글자 단위) 넓힌다
        private const float ArrowPadding = 6f;
        // 클립보드 한 장에 들어가는 줄 수 (조작 키 장은 목록 + 기본값으로 + 뒤로)
        private const int MaxRows = 12;

        [Tooltip("비어 있으면 같은 오브젝트에서 찾는다")]
        [SerializeField] private JournalHolder holder;
        [Tooltip("메뉴를 여는 동안 숨길 일지 장 (JournalCanvas/Panel)")]
        [SerializeField] private GameObject journalPage;
        [Tooltip("메뉴 장 (JournalCanvas/MenuPanel). 평소엔 꺼 둔다")]
        [SerializeField] private GameObject menuPage;
        [SerializeField] private TMP_Text titleText;
        [Tooltip("메뉴 줄 글자. 마우스 판정에 쓰므로 줄바꿈 없이(No Wrap)")]
        [SerializeField] private TMP_Text bodyText;
        [Tooltip("클립보드를 그리는 카메라 (ViewModelCamera). 비어 있으면 부모 카메라")]
        [SerializeField] private Camera pointerCamera;
        [Tooltip("조작 키 장에 나오는 목록. 비어 있으면 설정 장에 조작 키 줄은 있어도 목록이 비어 보임")]
        [SerializeField] private KeyBindingList keyBindings;

        private readonly float[] lineTops = new float[MaxRows];
        private readonly float[] lineBottoms = new float[MaxRows];
        private PlayerMovement movement;
        private PlayerControl control;
        private PlayerLook look;
        private PlayerInteractor interactor;
        private FloorTransition transition;
        private List<Vector2Int> resolutions = new List<Vector2Int>();
        private Row[] rows = MainRows;
        private Vector2Int screenSize;
        private bool fullScreen = true;
        private float pausedTimeScale = 1f;
        private int selected;
        private bool restartArmed, quitArmed;
        private int waitingKey = -1;
        private int inputIgnoredFrame = -1;
        private Vector2 lastPointer;

        /// <summary>메뉴가 열려 있는지 (이 동안 Time.timeScale = 0)</summary>
        public bool IsOpen { get; private set; }
        /// <summary>지금 보이는 장</summary>
        public Page CurrentPage { get; private set; }
        /// <summary>지금 고른 줄</summary>
        public Row Selected => rows[selected];
        /// <summary>지금 고른 줄 번호 (조작 키 장에서는 목록 번호와 같음)</summary>
        public int SelectedIndex => selected;
        /// <summary>"처음으로"를 한 번 눌러 확인을 기다리는 중인지</summary>
        public bool RestartArmed => restartArmed;
        /// <summary>"게임 종료"를 한 번 눌러 확인을 기다리는 중인지</summary>
        public bool QuitArmed => quitArmed;
        /// <summary>키 입력을 기다리는 조작 키 줄 (없으면 −1)</summary>
        public int WaitingKey => waitingKey;
        /// <summary>열거나 닫을 때 (true = 열림). 메뉴 소리 등</summary>
        public event Action<bool> OpenChanged;

        private int KeyCount => keyBindings != null ? Mathf.Min(keyBindings.Entries.Count, MaxRows - 2) : 0;

        private void Awake()
        {
            if (holder == null) holder = GetComponent<JournalHolder>();
            if (pointerCamera == null) pointerCamera = GetComponentInParent<Camera>();
            control = GetComponentInParent<PlayerControl>();
            movement = GetComponentInParent<PlayerMovement>();
            if (movement != null)
            {
                look = movement.GetComponent<PlayerLook>();
                interactor = movement.GetComponent<PlayerInteractor>();
            }
            transition = UnityEngine.Object.FindAnyObjectByType<FloorTransition>();
            GameSettings.Load();
            ApplySettings();
            if (keyBindings != null) KeyBindings.Load(keyBindings.Asset);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            WarnKeyBindings();
#endif
            if (menuPage != null) menuPage.SetActive(false);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void WarnKeyBindings()
        {
            if (keyBindings == null) return;
            if (keyBindings.Asset != InputSystem.actions)
                Debug.LogWarning("[Keys] 조작 키 목록의 입력 에셋이 프로젝트 기본 입력과 다름 — 바꾼 키가 일부만 적용될 수 있음", this);
            var entries = keyBindings.Entries;
            if (entries.Count > MaxRows - 2)
                Debug.LogWarning("[Keys] 조작 키 목록이 10줄을 넘음 — 11번째부터 메뉴에 안 나옴", this);
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                var action = entry?.action != null ? entry.action.action : null;
                if (action == null || entry.BindingIndex < 0)
                    Debug.LogWarning($"[Keys] 조작 키 목록 {i + 1}번째 줄 '{entry?.label}': 동작·합성 파트·그룹을 찾을 수 없음 — 메뉴에서 눌러도 안 바뀜", this);
            }
        }
#endif

        // ESC는 메뉴가 맡는다 (PlayerLook이 커서를 풀지 않게)
        private void OnEnable()
        {
            if (look != null) look.EscapeReleasesCursor = false;
        }

        private void OnDisable()
        {
            Close();
            if (look != null) look.EscapeReleasesCursor = true;
        }

        private void Update()
        {
            // 기다리던 입력이 알림 없이 끝났으면(다른 곳에서 취소 등) 메뉴가 멈추지 않게 푼다
            if (waitingKey >= 0 && !KeyBindings.IsRebinding)
            {
                waitingKey = -1;
                Render();
            }
            // 키를 기다리는 동안과, 그 키를 누른 프레임은 메뉴 입력으로 쓰지 않는다 (ESC = 취소만)
            if (waitingKey >= 0 || Time.frameCount == inputIgnoredFrame) return;
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                if (IsOpen) Back();
                else Open();
                return;
            }
            if (!IsOpen || HandlePointer(Mouse.current)) return;

            if (Pressed(keyboard, UpKeys)) Move(-1);
            else if (Pressed(keyboard, DownKeys)) Move(1);
            else if (Pressed(keyboard, LeftKeys)) Adjust(-1);
            else if (Pressed(keyboard, RightKeys)) Adjust(1);
            else if (Pressed(keyboard, ConfirmKeys)) Confirm();
        }

        /// <summary>메뉴 열기 (첫 장). 이미 열렸거나 층 이동 연출 중이면 무시</summary>
        public void Open()
        {
            if (IsOpen || !isActiveAndEnabled) return;
            if (transition != null && transition.IsBusy) return;   // 어두운 화면 아래라 메뉴가 안 보임
            IsOpen = true;
            pausedTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            AudioListener.pause = true;
            Lock(true);
            if (holder != null) holder.SetMenuOpen(true);
            SetPages(true);
            screenSize = new Vector2Int(Screen.width, Screen.height);
            fullScreen = ScreenOptions.IsFullScreen(Screen.fullScreenMode);
            resolutions = ScreenOptions.ResolutionOptions(Screen.resolutions, screenSize);
            Cursor.lockState = CursorLockMode.None;   // 마우스로 고를 수 있게
            Cursor.visible = true;
            lastPointer = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
            ShowPage(Page.Main, 0);
            SafeEvent.Raise(OpenChanged, true, this);
        }

        /// <summary>메뉴 닫기: 시간·소리·조작·커서를 되돌리고 설정을 저장한다</summary>
        public void Close()
        {
            if (!IsOpen) return;
            CancelKeyWait();
            IsOpen = false;
            CurrentPage = Page.Main;
            rows = MainRows;
            selected = 0;
            Disarm();
            Time.timeScale = pausedTimeScale;
            AudioListener.pause = false;
            Lock(false);
            if (holder != null) holder.SetMenuOpen(false);
            SetPages(false);
            GameSettings.Save();
            if (look != null && look.isActiveAndEnabled) look.SetCursorLocked(true);
            SafeEvent.Raise(OpenChanged, false, this);
        }

        /// <summary>ESC·"뒤로": 조작 키 장 → 설정 장(조작 키 줄), 설정 장 → 첫 장(설정 줄), 첫 장이면 닫기</summary>
        public void Back()
        {
            if (!IsOpen) return;
            CancelKeyWait();
            if (CurrentPage == Page.Keys) ShowPage(Page.Settings, Array.IndexOf(SettingsRows, Row.Keys));
            else if (CurrentPage == Page.Settings) ShowPage(Page.Main, Array.IndexOf(MainRows, Row.Settings));
            else Close();
        }

        /// <summary>고른 줄을 위(−)·아래(+)로. 끝에서는 반대쪽 끝으로</summary>
        public void Move(int direction)
        {
            if (IsOpen && direction != 0) Select(selected + Math.Sign(direction));
        }

        /// <summary>지금 장의 줄을 바로 고른다 (마우스를 올렸을 때). 다른 줄로 옮기면 실행 확인은 취소</summary>
        public void Select(int row)
        {
            if (!IsOpen || waitingKey >= 0) return;
            row = Wrap(row, rows.Length);
            if (row == selected) return;
            selected = row;
            Disarm();
            Render();
        }

        /// <summary>고른 줄의 값을 한 칸 올림(+)·내림(−). 화면 모드는 어느 쪽이든 바꿈. 값이 없는 줄이면 무시</summary>
        public void Adjust(int direction)
        {
            if (!IsOpen || direction == 0 || waitingKey >= 0) return;
            switch (Selected)
            {
                case Row.Sensitivity:
                    GameSettings.Sensitivity = GameSettings.StepSensitivity(GameSettings.Sensitivity, direction);
                    break;
                case Row.Volume:
                    GameSettings.Volume = GameSettings.StepVolume(GameSettings.Volume, direction);
                    break;
                case Row.FrameLimit:
                    GameSettings.FrameLimit = GameSettings.StepFrameLimit(GameSettings.FrameLimit, direction);
                    break;
                case Row.ScreenMode:
                    fullScreen = !fullScreen;
                    if (!fullScreen)
                        screenSize = ScreenOptions.WindowedSize(resolutions, screenSize,
                            new Vector2Int(Screen.currentResolution.width, Screen.currentResolution.height));
                    ScreenOptions.Apply(screenSize, fullScreen);
                    break;
                case Row.Resolution:
                    var next = ScreenOptions.StepResolution(resolutions, screenSize, direction);
                    if (next == screenSize) return;
                    screenSize = next;
                    ScreenOptions.Apply(screenSize, fullScreen);
                    break;
                default:
                    return;
            }
            ApplySettings();
            Render();
        }

        /// <summary>
        /// 고른 줄 실행: 계속하기 = 닫기, 설정·조작 키 = 그 장으로, 뒤로 = 앞 장, 값 줄 = 한 칸 올림,
        /// 키 줄 = 새 키 기다리기, 기본값으로 = 모든 키 되돌림, 처음으로·게임 종료 = 한 번 더 누르면 실행 (실수로 진행을 잃지 않게)
        /// </summary>
        public void Confirm()
        {
            if (!IsOpen || waitingKey >= 0) return;
            switch (Selected)
            {
                case Row.Continue:
                    Close();
                    break;
                case Row.Settings:
                    ShowPage(Page.Settings, 0);
                    break;
                case Row.Keys:
                    ShowPage(Page.Keys, 0);
                    break;
                case Row.Back:
                    Back();
                    break;
                case Row.KeyBinding:
                    StartKeyWait(selected);
                    break;
                case Row.ResetKeys:
                    if (keyBindings != null) KeyBindings.ResetAll(keyBindings.Asset);
                    Render();
                    break;
                case Row.Restart:
                    if (restartArmed) RestartGame();
                    else Arm(ref restartArmed);
                    break;
                case Row.Quit:
                    if (quitArmed) QuitGame();
                    else Arm(ref quitArmed);
                    break;
                default:
                    Adjust(1);
                    break;
            }
        }

        /// <summary>
        /// 화면 좌표가 메뉴 장의 몇 번째 줄 위인지. arrow는 ◀ 위면 −1, ▶ 위면 +1, 아니면 0.
        /// 클립보드가 기울어져 있어도 맞도록 글자 상자의 로컬 좌표로 판단한다
        /// </summary>
        public bool TryHit(Vector2 screenPosition, out int row, out int arrow)
        {
            row = -1;
            arrow = 0;
            if (bodyText == null || pointerCamera == null || !bodyText.isActiveAndEnabled) return false;
            var rect = bodyText.rectTransform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screenPosition, pointerCamera, out Vector2 local))
                return false;
            if (local.x < rect.rect.xMin || local.x > rect.rect.xMax) return false;

            var info = bodyText.textInfo;
            for (int i = 0; i < info.linkCount; i++)
            {
                var link = info.linkInfo[i];
                string id = link.GetLinkID();
                int direction = id.StartsWith("dec") ? -1 : id.StartsWith("inc") ? 1 : 0;
                if (direction == 0 || !LinkBounds(info, link, out Rect bounds)) continue;
                bounds = Rect.MinMaxRect(bounds.xMin - ArrowPadding, bounds.yMin, bounds.xMax + ArrowPadding, bounds.yMax);
                if (!bounds.Contains(local) || !int.TryParse(id.Substring(3), out int arrowRow)) continue;
                row = arrowRow;
                arrow = direction;
                return true;
            }

            int lines = Mathf.Min(info.lineCount, Mathf.Min(rows.Length, MaxRows));
            for (int i = 0; i < lines; i++)
            {
                lineTops[i] = info.lineInfo[i].ascender;
                lineBottoms[i] = info.lineInfo[i].descender;
            }
            row = RowAt(local.y, lineTops, lineBottoms, lines);
            return row >= 0;
        }

        /// <summary>그 장의 줄 순서 (조작 키 장은 키 줄 keyCount개 + 기본값으로 + 뒤로)</summary>
        public static Row[] RowsOf(Page page, int keyCount = 0)
        {
            if (page == Page.Main) return MainRows;
            if (page == Page.Settings) return SettingsRows;
            keyCount = Mathf.Clamp(keyCount, 0, MaxRows - 2);
            var keys = new Row[keyCount + 2];
            for (int i = 0; i < keyCount; i++) keys[i] = Row.KeyBinding;
            keys[keyCount] = Row.ResetKeys;
            keys[keyCount + 1] = Row.Back;
            return keys;
        }

        /// <summary>
        /// y가 몇 번째 줄인지. 줄 사이 틈은 가운데에서 나눠 빈틈 없이. 어느 줄도 아니면 −1.
        /// tops·bottoms는 위에서부터 각 줄의 위·아래 y (아래로 갈수록 작아짐)
        /// </summary>
        public static int RowAt(float y, IReadOnlyList<float> tops, IReadOnlyList<float> bottoms, int count)
        {
            count = Mathf.Min(count, Mathf.Min(tops?.Count ?? 0, bottoms?.Count ?? 0));
            for (int i = 0; i < count; i++)
            {
                float top = i == 0 ? tops[0] : (bottoms[i - 1] + tops[i]) * 0.5f;
                float bottom = i == count - 1 ? bottoms[i] : (bottoms[i] + tops[i + 1]) * 0.5f;
                if (y <= top && y >= bottom) return i;
            }
            return -1;
        }

        /// <summary>장 제목</summary>
        public static string TitleText(Page page) =>
            page == Page.Settings ? "설정" : page == Page.Keys ? "조작 키" : "일시정지";

        /// <summary>메뉴 장 본문. 고른 줄 앞에 ▶, 값은 오른쪽에 ◀ 값 ▶ (마우스 판정용 link 태그 포함)</summary>
        public static string BodyText(Page page, int selected, Values values)
        {
            int keyCount = values.KeyLabels?.Count ?? 0;
            var pageRows = RowsOf(page, keyCount);
            var text = new StringBuilder();
            for (int i = 0; i < pageRows.Length; i++)
            {
                var row = pageRows[i];
                if (i == selected) text.Append("<pos=0%>▶");
                text.Append("<pos=8%><link=\"row").Append(i).Append("\">").Append(Label(row, i, values)).Append("</link>");
                if (row == Row.KeyBinding)
                {
                    // 키는 ◀ ▶로 넘기는 값이 아니라 눌러서 새로 받는다
                    text.Append("<pos=50%>").Append(i == values.WaitingKey
                        ? "<color=#8A0000>키를 누르세요</color>"
                        : At(values.KeyNames, i, "-"));
                }
                else
                {
                    string value = Value(row, values);
                    if (value.Length > 0)
                        text.Append("<pos=50%><link=\"dec").Append(i).Append("\"> ◀ </link>").Append(value)
                            .Append("<link=\"inc").Append(i).Append("\"> ▶ </link>");
                }
                text.Append('\n');
            }
            text.Append("\n<size=70%>").Append(Footer(page, values.WaitingKey.HasValue)).Append("</size>");
            return text.ToString();
        }

        /// <summary>index를 0~count−1 안으로 (끝을 넘으면 반대쪽 끝)</summary>
        public static int Wrap(int index, int count) => count <= 0 ? 0 : ((index % count) + count) % count;

        private static string Footer(Page page, bool waiting)
        {
            if (waiting) return "새 키를 누르세요 · ESC 취소";
            switch (page)
            {
                case Page.Settings: return "클릭 · W/S 선택 · A/D 조절 · ESC 뒤로";
                case Page.Keys: return "클릭·E 키 바꾸기 · 겹치면 서로 바뀜 · ESC 뒤로";
                default: return "클릭 · W/S 선택 · E 확인 · ESC 닫기";
            }
        }

        private static string Label(Row row, int index, Values values)
        {
            switch (row)
            {
                case Row.Continue: return "계속하기";
                case Row.Settings: return "설정";
                case Row.Restart: return values.RestartArmed ? "정말 처음부터? 한 번 더" : "처음으로";
                case Row.Quit: return values.QuitArmed ? "정말 종료? 한 번 더" : "게임 종료";
                case Row.Sensitivity: return "마우스 감도";
                case Row.Volume: return "소리 크기";
                case Row.ScreenMode: return "화면 모드";
                case Row.Resolution: return "해상도";
                case Row.FrameLimit: return "프레임 제한";
                case Row.Keys: return "조작 키";
                case Row.KeyBinding: return At(values.KeyLabels, index, "?");
                case Row.ResetKeys: return "기본값으로";
                default: return "뒤로";
            }
        }

        private static string Value(Row row, Values values)
        {
            switch (row)
            {
                case Row.Sensitivity: return GameSettings.SensitivityLabel(values.Sensitivity);
                case Row.Volume: return GameSettings.VolumeLabel(values.Volume);
                case Row.ScreenMode: return ScreenOptions.ScreenModeLabel(values.FullScreen);
                case Row.Resolution: return ScreenOptions.ResolutionLabel(values.Resolution);
                case Row.FrameLimit: return GameSettings.FrameLimitLabel(values.FrameLimit);
                default: return string.Empty;
            }
        }

        private static string At(IReadOnlyList<string> list, int index, string fallback) =>
            list != null && index >= 0 && index < list.Count && !string.IsNullOrEmpty(list[index]) ? list[index] : fallback;

        // 마우스: 올리면 그 줄 선택, 클릭하면 실행(◀ ▶는 값 조절), 휠은 선택 이동. 이번 프레임 입력을 다 썼으면 true
        private bool HandlePointer(Mouse mouse)
        {
            if (mouse == null) return false;
            Vector2 pointer = mouse.position.ReadValue();
            bool moved = (pointer - lastPointer).sqrMagnitude > 0.25f;
            lastPointer = pointer;
            bool clicked = mouse.leftButton.wasPressedThisFrame;
            if ((moved || clicked) && TryHit(pointer, out int row, out int arrow))
            {
                Select(row);
                if (!clicked) return false;
                if (arrow != 0) Adjust(arrow);
                else Confirm();
                return true;
            }
            float wheel = mouse.scroll.ReadValue().y;
            if (wheel == 0f) return false;
            Move(wheel > 0f ? -1 : 1);
            return true;
        }

        // 조작 키 줄 index의 새 키를 기다린다. 받으면 겹친 줄과 맞바꾸고 저장
        private void StartKeyWait(int index)
        {
            if (keyBindings == null || index < 0 || index >= KeyCount) return;
            var entry = keyBindings.Entries[index];
            var action = entry?.action != null ? entry.action.action : null;
            int binding = entry != null ? entry.BindingIndex : -1;
            if (action == null || binding < 0) return;
            string previous = action.bindings[binding].effectivePath;
            waitingKey = index;
            Render();
            try
            {
                KeyBindings.StartRebind(action, binding, changed =>
                {
                    waitingKey = -1;
                    inputIgnoredFrame = Time.frameCount;   // 키를 누른 이번 프레임은 메뉴 입력으로 쓰지 않는다
                    if (changed)
                    {
                        KeyBindings.SwapConflict(BindingRows(), index, previous);
                        KeyBindings.Save(keyBindings.Asset);
                    }
                    if (IsOpen) Render();
                });
            }
            catch (Exception e)
            {
                waitingKey = -1;   // 시작을 못 하면 "키를 누르세요"에 멈추지 않게
                Debug.LogException(e, this);
                Render();
            }
        }

        private void CancelKeyWait()
        {
            if (waitingKey >= 0) KeyBindings.Cancel();
        }

        private List<(InputAction action, int index)> BindingRows()
        {
            var list = new List<(InputAction action, int index)>();
            if (keyBindings == null) return list;
            for (int i = 0; i < keyBindings.Entries.Count; i++)
            {
                var entry = keyBindings.Entries[i];
                var action = entry?.action != null ? entry.action.action : null;
                list.Add((action, entry != null ? entry.BindingIndex : -1));
            }
            return list;
        }

        private static bool Pressed(Keyboard keyboard, Key[] keys)
        {
            if (keyboard == null) return false;
            foreach (var key in keys)
                if (keyboard[key].wasPressedThisFrame) return true;
            return false;
        }

        // 링크 글자들이 차지하는 로컬 상자 (글자 시작~다음 글자 자리, 그 줄의 위~아래)
        private static bool LinkBounds(TMP_TextInfo info, TMP_LinkInfo link, out Rect bounds)
        {
            bounds = default;
            int first = link.linkTextfirstCharacterIndex;
            int length = link.linkTextLength;
            if (length <= 0 || first < 0 || first + length > info.characterCount) return false;
            float xMin = float.MaxValue, xMax = float.MinValue;
            for (int c = first; c < first + length; c++)
            {
                xMin = Mathf.Min(xMin, info.characterInfo[c].origin);
                xMax = Mathf.Max(xMax, info.characterInfo[c].xAdvance);
            }
            var line = info.lineInfo[info.characterInfo[first].lineNumber];
            bounds = Rect.MinMaxRect(xMin, line.descender, xMax, line.ascender);
            return true;
        }

        private void ShowPage(Page page, int row)
        {
            CurrentPage = page;
            rows = RowsOf(page, KeyCount);
            selected = Wrap(row, rows.Length);
            Disarm();
            Render();
        }

        private void Arm(ref bool flag)
        {
            flag = true;
            Render();
        }

        private void Disarm()
        {
            restartArmed = false;
            quitArmed = false;
        }

        private Values CurrentValues()
        {
            var labels = new string[KeyCount];
            var names = new string[KeyCount];
            for (int i = 0; i < KeyCount; i++)
            {
                var entry = keyBindings.Entries[i];
                labels[i] = entry?.label;
                var action = entry?.action != null ? entry.action.action : null;
                names[i] = KeyBindings.KeyName(action, entry != null ? entry.BindingIndex : -1);
            }
            return new Values
            {
                Sensitivity = GameSettings.Sensitivity,
                Volume = GameSettings.Volume,
                FullScreen = fullScreen,
                Resolution = screenSize,
                FrameLimit = GameSettings.FrameLimit,
                RestartArmed = restartArmed,
                QuitArmed = quitArmed,
                KeyLabels = labels,
                KeyNames = names,
                WaitingKey = waitingKey >= 0 ? waitingKey : (int?)null,
            };
        }

        private void ApplySettings()
        {
            if (look != null) look.SensitivityScale = GameSettings.Sensitivity;
            AudioListener.volume = GameSettings.Volume;
            GameSettings.ApplyFrameLimit(GameSettings.FrameLimit);
        }

        private void Render()
        {
            if (titleText != null) titleText.text = TitleText(CurrentPage);
            if (bodyText == null) return;
            bodyText.text = BodyText(CurrentPage, selected, CurrentValues());
            if (bodyText.isActiveAndEnabled) bodyText.ForceMeshUpdate();   // 같은 프레임의 마우스 판정이 새 줄 위치를 쓰게
        }

        private void SetPages(bool menu)
        {
            if (journalPage != null) journalPage.SetActive(!menu);
            if (menuPage != null) menuPage.SetActive(menu);
        }

        private void Lock(bool locked)
        {
            if (control != null)
            {
                control.Lock(this, locked, includeJournal: false);
                return;
            }
            if (movement != null) movement.SetBlocked(this, locked);
            if (look != null) look.SetBlocked(this, locked);
            if (interactor != null) interactor.SetBlocked(this, locked);
        }

        // 처음으로: 지금 씬을 다시 불러 5층 1회차부터 (사물·이상현상·일지·기록이 전부 처음 상태로)
        private void RestartGame()
        {
            Close();   // 시간·소리·커서를 먼저 되돌린다
            var scene = SceneManager.GetActiveScene();
#if UNITY_EDITOR
            // 에디터에서는 Build Settings에 없는 테스트 씬도 다시 불러야 하므로 경로로
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                scene.path, new LoadSceneParameters(LoadSceneMode.Single));
#else
            SceneManager.LoadScene(scene.buildIndex);
#endif
        }

        private static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
