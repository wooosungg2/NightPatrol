using System;
using System.Collections.Generic;
using NightPatrol.Run;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using NightPatrol.Debugging;
#endif

namespace NightPatrol.Phone
{
    /// <summary>
    /// 전화 화면 (옛날 휴대폰 모양): 상태 표시줄(신호·시각·배터리), 홈(위 앱 격자 3×3 + 아래 독 4칸), 앱 화면, 통화 화면(울림·통화 중이면 어느 앱 위에도).
    /// 앱은 PhoneApp을 상속한 컴포넌트 — 이 오브젝트 아래에 붙어 있으면 시작할 때 설치하고(하나도 없으면 전화·메시지·인터넷을 만든다),
    /// 실행 중에는 Install로 더한다. 조작은 PhoneHolder가 넘긴다: 휠 Move, E Confirm, Q(홈 버튼) Back.
    /// PhoneHolder와 같은 오브젝트(Player 프리팹 PlayerCamera/Phone)에 붙인다. 모델 칸이 비면 실행할 때 모델을 만든다
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PhoneScreen : MonoBehaviour
    {
        public const int GridColumns = 3, GridRows = 3, DockSlots = 4;
        public const float StatusHeight = 18f;
        private const float Width = PhoneModelBuilder.DisplayWidth, Height = PhoneModelBuilder.DisplayHeight;
        private const float CellWidth = 80f, CellHeight = 80f, IconSize = 56f, GridTop = 22f, HintTop = 262f, DockTop = 280f;
        private static readonly Color CallGreen = new Color(0.18f, 0.68f, 0.24f);
        private static readonly Color CallRed = new Color(0.84f, 0.16f, 0.14f);
        private static readonly Color LabelColor = new Color(0.8f, 0.82f, 0.86f);

        [Tooltip("비어 있으면 같은 오브젝트에서 찾는다")]
        [SerializeField] private PhoneHolder phone;
        [Tooltip("전화 모델. 비어 있으면 실행할 때 만든다 (둥근 은색 몸통 + 검은 앞면 + 홈 버튼)")]
        [SerializeField] private Transform model;
        [Tooltip("모델의 화면 칸 (240×360 UI, World Space Canvas 안). 모델을 넣었다면 같이 넣는다")]
        [SerializeField] private RectTransform display;
        [Tooltip("앱이 하나도 없으면 기본 앱(전화·메시지·인터넷)을 만든다")]
        [SerializeField] private bool createDefaultApps = true;
        [Tooltip("화면 밝기 (1 = 그대로). 어두운 건물에서 눈부시면 낮춘다")]
        [SerializeField, Range(0.3f, 1f)] private float brightness = 0.9f;

        private sealed class HomeIcon
        {
            public PhoneApp App;
            public GameObject Root;
            public Image Highlight;
            public TextMeshProUGUI Label;
            public GameObject Badge;
            public TextMeshProUGUI BadgeText;
            public int ShownBadge = -1;
        }

        private readonly List<PhoneApp> installed = new List<PhoneApp>();
        private readonly List<PhoneApp> order = new List<PhoneApp>();
        private readonly List<HomeIcon> icons = new List<HomeIcon>();
        private readonly Dictionary<PhoneApp, RectTransform> appRoots = new Dictionary<PhoneApp, RectTransform>();
        private readonly Image[] bars = new Image[4];
        private PhoneArt art;
        private RectTransform home, iconLayer, appLayer, call, statusBar;
        private TextMeshProUGUI hint, clock, service, callName, callStatus, callAction, bigClock, floorLabel;
        private Image callButton, callPicture;
        private PhoneHolder subscribed;
        private int gridCount;
        private int selected = -1;
        private int shownClock = -1, shownSignal = -1, shownCall = -1, shownFloor = -1;
        private bool iconsDirty;
        private bool warnedHidden;

        /// <summary>이 화면의 전화</summary>
        public PhoneHolder Phone => phone;
        /// <summary>화면 그림 (앱이 둥근 판·그라데이션 등을 얻는다)</summary>
        public PhoneArt Art => art;
        /// <summary>홈 화면에 보이는 앱, 홈 순서 (격자 → 독)</summary>
        public IReadOnlyList<PhoneApp> Apps => order;
        /// <summary>설치된 앱 전부 (격자가 차서 안 보이는 것 포함)</summary>
        public IReadOnlyList<PhoneApp> Installed => installed;
        /// <summary>열린 앱 (홈이면 null)</summary>
        public PhoneApp OpenApp { get; private set; }
        /// <summary>홈에서 고른 앱</summary>
        public PhoneApp Selected => selected >= 0 && selected < order.Count ? order[selected] : null;
        /// <summary>신호 막대 수 (0~4, 0이면 "서비스 없음")</summary>
        public int Signal { get; private set; } = 4;
        /// <summary>지금 화면 이름 (F1): 홈 또는 앱 이름</summary>
        public string ScreenName => OpenApp != null ? OpenApp.Title : "홈";
        /// <summary>통화 화면이 떠 있는지</summary>
        public bool IsCallShown => call != null && call.gameObject.activeSelf;
        /// <summary>화면의 시각(분, 자정부터) — 근무 시각(PatrolRun.ClockMinutes), 층 진행이 없으면 씬 시간</summary>
        public int ClockMinutes
        {
            get
            {
                var run = PatrolRun.Current;
                return run != null ? run.ClockMinutes : Mathf.FloorToInt(Time.timeSinceLevelLoad / 60f);
            }
        }

        private void Awake()
        {
            if (phone == null) phone = GetComponent<PhoneHolder>();
            art = new PhoneArt();
            if (model == null)
            {
                var parts = PhoneModelBuilder.Build(transform, art);
                model = parts.Root;
                if (display == null) display = parts.Display;
            }
            if (display == null)
            {
                Debug.LogError("[Phone] 화면 칸(Display)이 없음 — 모델을 넣었다면 그 모델의 화면 칸도 넣을 것", this);
                return;
            }
            BuildChrome();
            // 층 진행보다 먼저 설치해야 첫 층 시작(PatrolRun.Start)을 앱이 듣는다
            var found = GetComponentsInChildren<PhoneApp>(true);
            if (found.Length == 0 && createDefaultApps) found = CreateDefaultApps();
            foreach (var app in found) AddApp(app);
            RefreshOrder();
            selected = DefaultSelection();
            RebuildIcons();
            SetCallPicture();
        }

        private void OnEnable()
        {
            subscribed = phone;
            if (subscribed != null) subscribed.StateChanged += OnPhoneState;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            DevInfo.Register(this, "전화 화면", DevText, 62);
#endif
        }

        private void OnDisable()
        {
            if (subscribed != null) subscribed.StateChanged -= OnPhoneState;
            subscribed = null;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            DevInfo.Unregister(this);
#endif
        }

        private void OnDestroy()
        {
            OpenApp = null;
            foreach (var app in new List<PhoneApp>(installed))
                if (app != null) app.Detach();
            installed.Clear();
            art?.Dispose();
        }

        private void Update()
        {
            if (display == null) return;
            if (iconsDirty) RebuildIcons();
            ShowClock();
            ShowBadges();
            ShowCall();
            if (statusBar != null) statusBar.gameObject.SetActive(!(OpenApp is CameraApp) || IsCallShown);
        }

        // ── 앱 설치 ──

        /// <summary>앱을 설치한다 (홈 화면에 나온다 — 아이콘은 다음 프레임에). 이미 있으면 무시</summary>
        public void Install(PhoneApp app)
        {
            if (!AddApp(app)) return;
            RefreshOrder();
            iconsDirty = true;
        }

        /// <summary>앱을 지운다 (열려 있으면 홈으로)</summary>
        public void Uninstall(PhoneApp app)
        {
            if (app == null || !installed.Contains(app)) return;
            if (OpenApp == app) GoHome();
            installed.Remove(app);
            if (appRoots.TryGetValue(app, out var root))
            {
                if (root != null) PhoneArt.Discard(root.gameObject);
                appRoots.Remove(app);
            }
            app.Detach();
            RefreshOrder();
            iconsDirty = true;
        }

        private bool AddApp(PhoneApp app)
        {
            if (app == null || installed.Contains(app)) return false;
            if (app.Screen != null && app.Screen != this) app.Screen.Uninstall(app);
            installed.Add(app);
            app.Attach(this);
            return true;
        }

        private PhoneApp[] CreateDefaultApps()
        {
            var holder = new GameObject("Apps");
            holder.layer = gameObject.layer;
            holder.transform.SetParent(transform, false);
            return new PhoneApp[] { holder.AddComponent<CallApp>(), holder.AddComponent<MessagesApp>(), holder.AddComponent<BrowserApp>() };
        }

        /// <summary>
        /// 홈 순서: 격자 앱(최대 9) 다음 독 앱(최대 4). 각각 Order, 같으면 이름순. 독이 넘친 앱은 격자로, 격자도 넘친 앱은 안 보인다
        /// </summary>
        internal static List<PhoneApp> HomeOrder(IEnumerable<PhoneApp> apps, out int gridCount)
        {
            var sorted = new List<PhoneApp>();
            foreach (var app in apps)
                if (app != null) sorted.Add(app);
            sorted.Sort((a, b) => a.Order != b.Order ? a.Order.CompareTo(b.Order) : string.CompareOrdinal(a.Title, b.Title));
            var grid = new List<PhoneApp>();
            var dock = new List<PhoneApp>();
            foreach (var app in sorted)
            {
                if (app.InDock && dock.Count < DockSlots) dock.Add(app);
                else if (grid.Count < GridColumns * GridRows) grid.Add(app);
            }
            gridCount = grid.Count;
            grid.AddRange(dock);
            return grid;
        }

        /// <summary>목록 고르기: 끝에서 넘어가면 처음으로 (count가 0이면 -1)</summary>
        public static int Wrap(int index, int step, int count) => count <= 0 ? -1 : ((index + step) % count + count) % count;

        private void RefreshOrder()
        {
            var previous = Selected;
            order.Clear();
            order.AddRange(HomeOrder(installed, out gridCount));
            if (order.Count < installed.Count && !warnedHidden)
            {
                warnedHidden = true;
                Debug.LogWarning($"[Phone] 홈 화면이 차서 앱 {installed.Count - order.Count}개가 안 보임 (격자 {GridColumns * GridRows}칸·독 {DockSlots}칸)", this);
            }
            selected = previous != null ? order.IndexOf(previous) : -1;
            if (selected < 0) selected = DefaultSelection();
        }

        // 기본 선택: 독 첫 앱(전화), 독이 없으면 첫 앱
        private int DefaultSelection() => gridCount < order.Count ? gridCount : order.Count > 0 ? 0 : -1;

        // ── 조작 ──

        /// <summary>휠 한 칸 (+1 다음, -1 앞). 앱이 열려 있으면 앱에 넘긴다</summary>
        public void Move(int step)
        {
            if (step == 0) return;
            if (OpenApp != null)
            {
                OpenApp.Moved(step);
                return;
            }
            if (order.Count == 0) return;
            selected = Wrap(selected, step, order.Count);
            ShowSelection();
        }

        /// <summary>E: 홈이면 고른 앱을 열고, 앱이면 앱에 넘긴다</summary>
        public void Confirm()
        {
            if (OpenApp != null)
            {
                OpenApp.Confirmed();
                return;
            }
            var app = Selected;
            if (app != null) Open(app);
        }

        /// <summary>Q(홈 버튼): 앱이면 한 단계 뒤로(더 없으면 홈) — true. 홈이면 false (전화를 넣는다)</summary>
        public bool Back()
        {
            if (OpenApp == null) return false;
            if (!OpenApp.WentBack()) GoHome();
            return true;
        }

        /// <summary>앱을 연다 (처음이면 화면을 만든다)</summary>
        public void Open(PhoneApp app)
        {
            if (app == null || !installed.Contains(app) || appLayer == null || OpenApp == app) return;
            CloseApp();
            if (!appRoots.TryGetValue(app, out var root) || root == null)
            {
                root = PhoneUI.Fill(appLayer, app.Title);
                appRoots[app] = root;
                app.BuildInto(root);
            }
            root.gameObject.SetActive(true);
            home.gameObject.SetActive(false);
            OpenApp = app;
            int index = order.IndexOf(app);
            if (index >= 0) selected = index;
            app.Opened();
        }

        /// <summary>홈 화면으로 (열린 앱을 닫는다)</summary>
        public void GoHome()
        {
            CloseApp();
            if (home != null) home.gameObject.SetActive(true);
            ShowSelection();
        }

        private void CloseApp()
        {
            var app = OpenApp;
            if (app == null) return;
            OpenApp = null;
            if (appRoots.TryGetValue(app, out var root) && root != null) root.gameObject.SetActive(false);
            app.Closed();
        }

        // 넣으면 홈으로 — 다음에 꺼낼 때는 늘 홈, 독 첫 앱(전화)을 고른 채
        private void OnPhoneState(PhoneState state)
        {
            if (state == PhoneState.Stowed)
            {
                GoHome();
                selected = DefaultSelection();
            }
            ShowSelection();
        }

        /// <summary>신호 막대 수를 정한다 (0~4). 층 응답 상태에 따라 PhoneCalls가 부른다</summary>
        public void SetSignal(int count)
        {
            Signal = Mathf.Clamp(count, 0, bars.Length);
            ShowSignal();
        }

        // ── 화면 만들기 ──

        // 바탕 → 홈 → 앱 → 통화 → 상태 표시줄 → 밝기 (뒤에 만든 것이 위)
        private void BuildChrome()
        {
            PhoneUI.Image(PhoneUI.Fill(display, "Wallpaper"),
                art.Gradient(new Color(0.07f, 0.09f, 0.14f), new Color(0.01f, 0.01f, 0.02f)), Color.white);

            home = PhoneUI.Fill(display, "Home");
            var dock = PhoneUI.Box(home, "Dock", 0f, DockTop, Width, Height - DockTop);
            PhoneUI.Image(dock, art.Gradient(new Color(0.74f, 0.77f, 0.82f, 0.5f), new Color(0.3f, 0.32f, 0.36f, 0.6f)), Color.white);
            PhoneUI.Image(PhoneUI.Box(dock, "Shine", 0f, 0f, Width, 1f), null, new Color(1f, 1f, 1f, 0.55f));
            hint = PhoneUI.Label(home, "Hint", 0f, HintTop, Width, 18f, "", 12f, new Color(0.62f, 0.66f, 0.72f), TextAlignmentOptions.Center);
            // 위 격자에 앱이 없으면 그 자리에 큰 시계와 층
            bigClock = PhoneUI.Label(home, "BigClock", 0f, 62f, Width, 64f, "", 46f, Color.white, TextAlignmentOptions.Center);
            floorLabel = PhoneUI.Label(home, "Floor", 0f, 128f, Width, 22f, "", 15f, new Color(0.7f, 0.74f, 0.8f), TextAlignmentOptions.Center);
            iconLayer = PhoneUI.Fill(home, "Icons");

            appLayer = PhoneUI.Box(display, "Apps", 0f, StatusHeight, PhoneUI.AppWidth, PhoneUI.AppHeight);
            BuildCall();
            BuildStatusBar();
            PhoneUI.Image(PhoneUI.Fill(display, "Dim"), null, new Color(0f, 0f, 0f, 1f - brightness));
        }

        private void BuildCall()
        {
            call = PhoneUI.Box(display, "Call", 0f, StatusHeight, PhoneUI.AppWidth, PhoneUI.AppHeight);
            PhoneUI.Image(call, art.Gradient(new Color(0.17f, 0.18f, 0.22f), new Color(0.02f, 0.02f, 0.03f)), Color.white);
            callName = PhoneUI.Label(call, "Name", 0f, 34f, Width, 38f, "", 28f, Color.white, TextAlignmentOptions.Center, bold: true);
            callStatus = PhoneUI.Label(call, "Status", 0f, 76f, Width, 22f, "", 15f, new Color(0.75f, 0.78f, 0.83f), TextAlignmentOptions.Center);
            callPicture = PhoneUI.Image(PhoneUI.Box(call, "Picture", 80f, 122f, 80f, 80f), null, Color.white);   // 그림은 앱을 설치한 뒤 (SetCallPicture)
            var button = PhoneUI.Box(call, "Button", 24f, 262f, 192f, 48f);
            callButton = PhoneUI.Image(button, art.Rounded(12), CallGreen);
            callAction = PhoneUI.Label(button, "Text", 0f, 0f, 192f, 48f, "", 19f, Color.white, TextAlignmentOptions.Center, bold: true);
            call.gameObject.SetActive(false);
        }

        private void BuildStatusBar()
        {
            var bar = PhoneUI.Box(display, "StatusBar", 0f, 0f, Width, StatusHeight);
            statusBar = bar;
            PhoneUI.Image(bar, art.Gradient(new Color(0.17f, 0.18f, 0.21f, 0.94f), new Color(0f, 0f, 0f, 0.94f)), Color.white);
            for (int i = 0; i < bars.Length; i++)
            {
                float height = 4f + 2.5f * i;
                bars[i] = PhoneUI.Image(PhoneUI.Box(bar, "Signal" + i, 6f + i * 5f, 14f - height, 3.5f, height), null, Color.white);
            }
            service = PhoneUI.Label(bar, "NoService", 5f, 0f, 90f, StatusHeight, "서비스 없음", 11f, Color.white, TextAlignmentOptions.MidlineLeft);
            clock = PhoneUI.Label(bar, "Clock", 70f, 0f, 100f, StatusHeight, "", 13f, Color.white, TextAlignmentOptions.Center, bold: true);
            var battery = PhoneUI.Box(bar, "Battery", Width - 31f, 4f, 22f, 10f);
            PhoneUI.Image(battery, art.Rounded(2), Color.white);
            var inside = PhoneUI.Box(battery, "Inside", 1.5f, 1.5f, 19f, 7f);
            PhoneUI.Image(inside, null, Color.black);
            PhoneUI.Image(PhoneUI.Box(inside, "Level", 1f, 1f, 13f, 5f), null, new Color(0.45f, 0.9f, 0.35f));
            PhoneUI.Image(PhoneUI.Box(bar, "Tip", Width - 9f, 7f, 2f, 4f), null, Color.white);
            ShowSignal();
            ShowClock();
        }

        private void RebuildIcons()
        {
            iconsDirty = false;
            if (iconLayer == null) return;
            foreach (var icon in icons)
                if (icon.Root != null) PhoneArt.Discard(icon.Root);
            icons.Clear();
            for (int i = 0; i < gridCount; i++)
                icons.Add(CreateIcon(order[i], (i % GridColumns) * CellWidth, GridTop + (i / GridColumns) * CellHeight, CellWidth));
            int dockCount = order.Count - gridCount;
            float dockCell = Width / Mathf.Max(3, dockCount);
            float dockLeft = (Width - dockCount * dockCell) / 2f;
            for (int i = 0; i < dockCount; i++)
                icons.Add(CreateIcon(order[gridCount + i], dockLeft + i * dockCell, DockTop + 2f, dockCell));
            bigClock.gameObject.SetActive(gridCount == 0);
            floorLabel.gameObject.SetActive(gridCount == 0);
            ShowSelection();
        }

        // 통화 화면 그림: 전화 앱 아이콘을 다시 쓴다 (그림을 한 번 더 그리지 않게). 전화 앱이 없으면 회색 수화기
        private void SetCallPicture()
        {
            if (callPicture == null) return;
            CallApp callApp = null;
            foreach (var app in installed)
                if (app is CallApp found)
                {
                    callApp = found;
                    break;
                }
            callPicture.sprite = callApp != null
                ? art.Icon(callApp)
                : art.Symbol("call", new Color(0.56f, 0.59f, 0.65f), new Color(0.22f, 0.24f, 0.29f), PhoneArt.Handset);
        }

        private HomeIcon CreateIcon(PhoneApp app, float x, float y, float width)
        {
            var root = PhoneUI.Box(iconLayer, app.Title, x, y, width, CellHeight);
            float left = (width - IconSize) / 2f;
            var icon = new HomeIcon
            {
                App = app,
                Root = root.gameObject,
                Highlight = PhoneUI.Image(PhoneUI.Box(root, "Highlight", left - 5f, -1f, IconSize + 10f, IconSize + 10f), art.Rounded(16), new Color(1f, 1f, 1f, 0.34f)),
            };
            var picture = PhoneUI.Box(root, "Icon", left, 4f, IconSize, IconSize);
            PhoneUI.Image(picture, art.Icon(app), Color.white);
            if (app.IconSprite == null && !app.HasSymbol)
                PhoneUI.Label(picture, "Letter", 0f, 0f, IconSize, IconSize, FirstLetter(app.Title), 28f, Color.white, TextAlignmentOptions.Center, bold: true);
            icon.Label = PhoneUI.Label(root, "Label", 0f, IconSize + 6f, width, 16f, app.Title, 12f, LabelColor, TextAlignmentOptions.Center);
            var badge = PhoneUI.Box(root, "Badge", left + IconSize - 16f, -3f, 24f, 24f);
            PhoneUI.Image(badge, art.Circle(), Color.white);
            PhoneUI.Image(PhoneUI.Box(badge, "Red", 2f, 2f, 20f, 20f), art.Circle(), PhoneUI.Alert);
            icon.BadgeText = PhoneUI.Label(badge, "Count", 0f, 0f, 24f, 24f, "", 12f, Color.white, TextAlignmentOptions.Center, bold: true);
            icon.Badge = badge.gameObject;
            badge.gameObject.SetActive(false);
            return icon;
        }

        private static string FirstLetter(string title) => string.IsNullOrEmpty(title) ? "?" : title.Substring(0, 1);

        // ── 화면 갱신 ──

        private void ShowSelection()
        {
            for (int i = 0; i < icons.Count; i++)
            {
                var icon = icons[i];
                if (icon.Highlight == null || icon.Label == null) continue;   // 플레이어를 지우는 중 (앱이 먼저 지워짐)
                bool picked = i == selected;
                icon.Highlight.enabled = picked;
                icon.Label.fontStyle = picked ? FontStyles.Bold : FontStyles.Normal;
                icon.Label.color = picked ? Color.white : LabelColor;
            }
            if (hint != null) hint.text = "휠 고르기 · 좌클릭 열기 · 우클릭 넣기";
        }

        private string PhoneKey => phone != null ? phone.PhoneKey : "Q";
        private string InteractKey => phone != null ? phone.InteractKey : "E";

        private void ShowClock()
        {
            if (clock == null) return;
            var run = PatrolRun.Current;
            int floor = run != null ? run.CurrentFloor : 0;
            if (floor != shownFloor && floorLabel != null)
            {
                shownFloor = floor;
                floorLabel.text = floor > 0 ? floor + "층" : string.Empty;
            }
            int minutes = ClockMinutes;
            if (minutes == shownClock) return;
            shownClock = minutes;
            clock.text = PatrolRun.ClockText(minutes);
            if (bigClock != null) bigClock.text = clock.text;
        }

        private void ShowSignal()
        {
            if (service == null || Signal == shownSignal) return;
            shownSignal = Signal;
            for (int i = 0; i < bars.Length; i++)
            {
                bars[i].gameObject.SetActive(Signal > 0);
                bars[i].color = i < Signal ? Color.white : new Color(1f, 1f, 1f, 0.25f);
            }
            service.gameObject.SetActive(Signal == 0);
        }

        private void ShowBadges()
        {
            foreach (var icon in icons)
            {
                int count = BadgeOf(icon.App);
                if (count == icon.ShownBadge) continue;
                icon.ShownBadge = count;
                icon.Badge.SetActive(count > 0);
                icon.BadgeText.text = count > 99 ? "99+" : count.ToString();
            }
        }

        private int BadgeOf(PhoneApp app)
        {
            if (app == null) return 0;
            try { return app.Badge; }
            catch (Exception e)
            {
                Debug.LogException(e, app);
                return 0;
            }
        }

        // 울림·통화 중이면 통화 화면 (이름, 상태, 받기·끊기 단추). 글은 초가 바뀔 때만 다시 쓴다
        private void ShowCall()
        {
            if (call == null) return;
            var state = phone != null ? phone.State : PhoneState.Stowed;
            bool shown = state == PhoneState.Ringing || state == PhoneState.Talking;
            if (call.gameObject.activeSelf != shown) call.gameObject.SetActive(shown);
            if (!shown)
            {
                shownCall = -1;
                return;
            }
            var playback = phone.Playback;
            int key = (int)state * 1000000 + (int)playback.Phase * 100000 + Mathf.FloorToInt(playback.TalkSeconds);
            if (key == shownCall) return;
            shownCall = key;
            callName.text = CallApp.CallerOf(phone.CurrentCall);
            callStatus.text = CallStatus(state, playback);
            bool ringing = state == PhoneState.Ringing;
            callButton.color = ringing ? CallGreen : CallRed;
            callAction.text = ringing ? $"{PhoneKey} 받기" : $"{PhoneKey} 끊기";
        }

        /// <summary>통화 화면 상태 글: "전화 옴" / "연결 중…" / "통화 중 0:12"</summary>
        internal static string CallStatus(PhoneState state, PhonePlayback playback)
        {
            if (state == PhoneState.Ringing) return "전화 옴";
            if (playback.Phase == PhonePlayback.Stage.Ringback) return "연결 중…";
            return $"통화 중 {PhonePlayback.Clock(playback.TalkSeconds)}";
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private string DevText() =>
            $"{ScreenName} · 고름 {(Selected != null ? Selected.Title : "없음")} · 앱 {installed.Count}개 · 신호 {Signal} · {PatrolRun.ClockText(ClockMinutes)}";
#endif
    }
}
