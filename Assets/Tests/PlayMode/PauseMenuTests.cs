using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NightPatrol.Controls;
using NightPatrol.Interaction;
using NightPatrol.Journal;
using NightPatrol.Menu;
using NightPatrol.Player;
using NightPatrol.Run;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace NightPatrol.Tests
{
    // ESC 메뉴 on the clipboard. Settings are restored (and saved back) after every test.
    public class PauseMenuTests
    {
        private readonly List<GameObject> created = new List<GameObject>();
        private float savedSensitivity, savedVolume;
        private int savedFrameLimit;
        private PlayerMovement movement;
        private PlayerLook look;
        private PlayerInteractor interactor;
        private JournalHolder holder;
        private PauseMenu menu;
        private GameObject journalPage, menuPage;
        private readonly List<Object> createdAssets = new List<Object>();
        private InputAction keyInteract, keyJournal;
        private string savedKeyBindings;

        [SetUp]
        public void SetUp()
        {
            GameSettings.Load();
            savedSensitivity = GameSettings.Sensitivity;
            savedVolume = GameSettings.Volume;
            savedFrameLimit = GameSettings.FrameLimit;
            savedKeyBindings = PlayerPrefs.HasKey(KeyBindings.PrefsKey) ? PlayerPrefs.GetString(KeyBindings.PrefsKey) : null;

            // The player scripts log an error when their input references are empty; that is expected here.
            LogAssert.Expect(LogType.Error, new Regex("PlayerMovement에 PlayerSettings와 Player/Move"));
            LogAssert.Expect(LogType.Error, new Regex("PlayerLook에 PlayerSettings, 자식 카메라, Player/Look"));
            LogAssert.Expect(LogType.Error, new Regex("PlayerInteractor에 PlayerSettings, 플레이어 카메라, Player/Interact"));
            var body = New("Player");
            movement = body.AddComponent<PlayerMovement>();
            look = body.AddComponent<PlayerLook>();
            interactor = body.AddComponent<PlayerInteractor>();

            var clipboard = new GameObject("Clipboard");
            clipboard.SetActive(false);   // Awake waits until the pages are assigned
            clipboard.transform.SetParent(body.transform);
            journalPage = new GameObject("Panel");
            journalPage.transform.SetParent(clipboard.transform);
            menuPage = new GameObject("MenuPanel");
            menuPage.transform.SetParent(clipboard.transform);
            holder = clipboard.AddComponent<JournalHolder>();
            menu = clipboard.AddComponent<PauseMenu>();
            SetField(menu, "journalPage", journalPage);
            SetField(menu, "menuPage", menuPage);
            clipboard.SetActive(true);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in created)
                if (go != null) Object.DestroyImmediate(go);
            created.Clear();
            Time.timeScale = 1f;
            AudioListener.pause = false;
            GameSettings.Sensitivity = savedSensitivity;
            GameSettings.Volume = savedVolume;
            GameSettings.FrameLimit = savedFrameLimit;
            GameSettings.Save();
            AudioListener.volume = GameSettings.Volume;
            GameSettings.ApplyFrameLimit(GameSettings.FrameLimit);

            // 메뉴가 저장한 테스트용 키 설정을 지우고 원래 값으로 (실제 게임의 키 설정을 건드리지 않게)
            KeyBindings.Cancel();
            if (savedKeyBindings != null) PlayerPrefs.SetString(KeyBindings.PrefsKey, savedKeyBindings);
            else PlayerPrefs.DeleteKey(KeyBindings.PrefsKey);
            foreach (var asset in createdAssets)
                if (asset != null) Object.DestroyImmediate(asset);
            createdAssets.Clear();
        }

        // 조작 키 장용: 코드로 만든 Input Actions (상호작용 E, 근무일지 Tab) + 목록
        private void UseKeyList()
        {
            var actions = ScriptableObject.CreateInstance<InputActionAsset>();
            var map = actions.AddActionMap("Player");
            keyInteract = map.AddAction("Interact", InputActionType.Button);
            keyInteract.AddBinding("<Keyboard>/e", groups: KeyBindings.KeyboardMouse);
            keyJournal = map.AddAction("Journal", InputActionType.Button);
            keyJournal.AddBinding("<Keyboard>/tab", groups: KeyBindings.KeyboardMouse);
            var interactRef = InputActionReference.Create(keyInteract);
            var journalRef = InputActionReference.Create(keyJournal);
            var list = ScriptableObject.CreateInstance<KeyBindingList>();
            SetField(list, "entries", new[]
            {
                new KeyBindingList.Entry { label = "상호작용", action = interactRef },
                new KeyBindingList.Entry { label = "근무일지", action = journalRef },
            });
            createdAssets.AddRange(new Object[] { actions, interactRef, journalRef, list });
            SetField(menu, "keyBindings", list);
        }

        // 같은 동작을 count줄 넣은 목록 (가운데 빈 줄을 하나 넣을 수 있음)
        private KeyBindingList KeyListOf(int count, int emptyAt = -1)
        {
            var actions = ScriptableObject.CreateInstance<InputActionAsset>();
            var ok = actions.AddActionMap("Player").AddAction("Ok", InputActionType.Button);
            ok.AddBinding("<Keyboard>/e", groups: KeyBindings.KeyboardMouse);
            var okRef = InputActionReference.Create(ok);
            var entries = new KeyBindingList.Entry[count];
            for (int i = 0; i < count; i++)
                entries[i] = new KeyBindingList.Entry { label = i == emptyAt ? "빈 줄" : "줄" + i, action = i == emptyAt ? null : okRef };
            var list = ScriptableObject.CreateInstance<KeyBindingList>();
            SetField(list, "entries", entries);
            createdAssets.AddRange(new Object[] { actions, okRef, list });
            return list;
        }

        private GameObject New(string name)
        {
            var go = new GameObject(name);
            created.Add(go);
            return go;
        }

        private static void SetField(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        [Test]
        public void StartsClosed_WithMenuPageHidden_AndTakesOverEscape()
        {
            Assert.IsFalse(menu.IsOpen);
            Assert.IsFalse(menuPage.activeSelf);
            Assert.IsFalse(look.EscapeReleasesCursor);
        }

        [Test]
        public void Open_PausesTime_LocksPlayer_AndLiftsTheMenuPage()
        {
            menu.Open();
            Assert.IsTrue(menu.IsOpen);
            Assert.AreEqual(0f, Time.timeScale);
            Assert.IsTrue(AudioListener.pause);
            Assert.IsTrue(movement.IsBlocked);
            Assert.IsTrue(look.IsBlocked);
            Assert.IsTrue(interactor.IsBlocked);
            Assert.IsTrue(holder.IsMenuOpen);
            Assert.IsFalse(journalPage.activeSelf);
            Assert.IsTrue(menuPage.activeSelf);
        }

        [Test]
        public void Close_RestoresTime_Sound_Controls_AndJournalPage()
        {
            menu.Open();
            menu.Close();
            Assert.IsFalse(menu.IsOpen);
            Assert.AreEqual(1f, Time.timeScale);
            Assert.IsFalse(AudioListener.pause);
            Assert.IsFalse(movement.IsBlocked);
            Assert.IsFalse(interactor.IsBlocked);
            Assert.IsFalse(holder.IsMenuOpen);
            Assert.IsTrue(journalPage.activeSelf);
            Assert.IsFalse(menuPage.activeSelf);
        }

        [Test]
        public void OpenChanged_ReportsBothWays()
        {
            var seen = new List<bool>();
            menu.OpenChanged += seen.Add;
            menu.Open();
            menu.Open();   // already open: no second event
            menu.Close();
            CollectionAssert.AreEqual(new[] { true, false }, seen);
        }

        [Test]
        public void Disabling_WhileOpen_UnpausesTheGame_AndGivesEscapeBack()
        {
            menu.Open();
            menu.enabled = false;
            Assert.IsFalse(menu.IsOpen);
            Assert.AreEqual(1f, Time.timeScale);
            Assert.IsFalse(movement.IsBlocked);
            Assert.IsTrue(look.EscapeReleasesCursor);
        }

        // 첫 장 → "설정" → 설정 장
        private void OpenSettings()
        {
            menu.Open();
            menu.Select(1);
            Assert.AreEqual(PauseMenu.Row.Settings, menu.Selected);
            menu.Confirm();
            Assert.AreEqual(PauseMenu.Page.Settings, menu.CurrentPage);
        }

        [Test]
        public void Open_ShowsTheMainPage_ContinueSelected()
        {
            menu.Open();
            Assert.AreEqual(PauseMenu.Page.Main, menu.CurrentPage);
            Assert.AreEqual(PauseMenu.Row.Continue, menu.Selected);
        }

        [Test]
        public void Settings_BackReturnsToMainOnTheSettingsRow_ThenCloses()
        {
            OpenSettings();
            Assert.AreEqual(PauseMenu.Row.Sensitivity, menu.Selected);

            menu.Back();   // ESC on the settings page
            Assert.IsTrue(menu.IsOpen);
            Assert.AreEqual(PauseMenu.Page.Main, menu.CurrentPage);
            Assert.AreEqual(PauseMenu.Row.Settings, menu.Selected);

            menu.Back();   // ESC on the main page
            Assert.IsFalse(menu.IsOpen);
        }

        [Test]
        public void BackRow_OnSettings_ReturnsToMain()
        {
            OpenSettings();
            menu.Move(-1);
            Assert.AreEqual(PauseMenu.Row.Back, menu.Selected);
            menu.Confirm();
            Assert.AreEqual(PauseMenu.Page.Main, menu.CurrentPage);
        }

        [Test]
        public void ReopeningTheMenu_StartsOnTheMainPage()
        {
            OpenSettings();
            menu.Close();
            menu.Open();
            Assert.AreEqual(PauseMenu.Page.Main, menu.CurrentPage);
        }

        [Test]
        public void Selection_Wraps_AndSensitivityAppliesToLook()
        {
            GameSettings.Sensitivity = 1f;
            OpenSettings();
            menu.Adjust(1);
            Assert.AreEqual(1.1f, GameSettings.Sensitivity, 1e-5f);
            Assert.AreEqual(1.1f, look.SensitivityScale, 1e-5f);

            menu.Move(-1);
            Assert.AreEqual(PauseMenu.Row.Back, menu.Selected);
        }

        [Test]
        public void MainPage_HasNoValuesToAdjust()
        {
            float sensitivity = GameSettings.Sensitivity;
            menu.Open();
            menu.Adjust(1);
            Assert.AreEqual(sensitivity, GameSettings.Sensitivity);
            Assert.AreEqual(PauseMenu.Page.Main, menu.CurrentPage);
        }

        [Test]
        public void Volume_AppliesToTheAudioListener()
        {
            GameSettings.Volume = 1f;
            OpenSettings();
            menu.Move(1);
            Assert.AreEqual(PauseMenu.Row.Volume, menu.Selected);
            menu.Adjust(-1);
            Assert.AreEqual(0.9f, AudioListener.volume, 1e-5f);
            menu.Confirm();   // a value row: one step up
            Assert.AreEqual(1f, AudioListener.volume, 1e-5f);
        }

        [Test]
        public void Confirm_OnContinue_Closes()
        {
            menu.Open();
            menu.Confirm();
            Assert.IsFalse(menu.IsOpen);
        }

        [Test]
        public void ClosedMenu_IgnoresMenuInput()
        {
            menu.Move(1);
            menu.Adjust(1);
            menu.Confirm();
            Assert.IsFalse(menu.IsOpen);
            Assert.AreEqual(PauseMenu.Row.Continue, menu.Selected);
        }

        [Test]
        public void Open_ShowsTheMouseCursor()
        {
            menu.Open();
            Assert.AreEqual(CursorLockMode.None, Cursor.lockState);
            Assert.IsTrue(Cursor.visible);
        }

        [Test]
        public void Quit_NeedsASecondConfirm_AndMovingAwayCancels()
        {
            menu.Open();
            menu.Move(-1);
            Assert.AreEqual(PauseMenu.Row.Quit, menu.Selected);
            menu.Confirm();   // first press only asks again (a second one would stop Play mode)
            Assert.IsTrue(menu.QuitArmed);
            Assert.IsTrue(menu.IsOpen);

            menu.Move(1);
            Assert.IsFalse(menu.QuitArmed);
        }

        [Test]
        public void Restart_NeedsASecondConfirm_AndMovingAwayCancels()
        {
            menu.Open();
            menu.Select(2);
            Assert.AreEqual(PauseMenu.Row.Restart, menu.Selected);
            menu.Confirm();   // first press only asks again (a second one would reload the test scene)
            Assert.IsTrue(menu.RestartArmed);
            Assert.IsTrue(menu.IsOpen);

            menu.Move(-1);
            Assert.IsFalse(menu.RestartArmed);
        }

        [Test]
        public void FrameLimit_AppliesTargetFrameRate()
        {
            GameSettings.FrameLimit = 0;   // 수직 동기
            OpenSettings();
            menu.Select(4);
            Assert.AreEqual(PauseMenu.Row.FrameLimit, menu.Selected);
            menu.Adjust(1);
            Assert.AreEqual(30, GameSettings.FrameLimit);
            Assert.AreEqual(30, Application.targetFrameRate);
            menu.Adjust(1);
            Assert.AreEqual(60, Application.targetFrameRate);
        }

        // ── 마우스: 실제 TMP 글자와 카메라로, 클립보드처럼 기울어진 장에서 ──
        [Test]
        public void Pointer_HitsRowsAndArrows_OnATiltedPage()
        {
            var body = MakeTiltedMenuPage(out Camera cam);
            OpenSettings();   // writes the settings rows and updates the text layout

            const int screenModeRow = 2, sensitivityRow = 0, frameLimitRow = 4;
            var line = body.textInfo.lineInfo[screenModeRow];
            var onLabel = new Vector3(body.rectTransform.rect.xMin + 60f, (line.ascender + line.descender) * 0.5f, 0f);
            Assert.IsTrue(menu.TryHit(ToScreen(cam, body, onLabel), out int row, out int arrow));
            Assert.AreEqual(screenModeRow, row);
            Assert.AreEqual(0, arrow);

            Assert.IsTrue(menu.TryHit(ToScreen(cam, body, LinkCenter(body, "inc0")), out row, out arrow));
            Assert.AreEqual(sensitivityRow, row);
            Assert.AreEqual(1, arrow);

            Assert.IsTrue(menu.TryHit(ToScreen(cam, body, LinkCenter(body, "dec4")), out row, out arrow));
            Assert.AreEqual(frameLimitRow, row);
            Assert.AreEqual(-1, arrow);

            var beside = new Vector3(body.rectTransform.rect.xMax + 200f, (line.ascender + line.descender) * 0.5f, 0f);
            Assert.IsFalse(menu.TryHit(ToScreen(cam, body, beside), out _, out _));
        }

        private TMP_Text MakeTiltedMenuPage(out Camera cam)
        {
            cam = New("PointerCamera").AddComponent<Camera>();   // at the origin, looking along +Z
            var canvasObject = New("Canvas");
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var canvasRect = (RectTransform)canvasObject.transform;
            canvasRect.sizeDelta = new Vector2(420f, 540f);
            canvasRect.localScale = Vector3.one * 0.001f;
            canvasRect.SetPositionAndRotation(new Vector3(0f, 0f, 1f), Quaternion.Euler(20f, 10f, 0f));

            var page = new GameObject("MenuPanel", typeof(RectTransform));
            page.transform.SetParent(canvasObject.transform, false);
            var bodyObject = new GameObject("MenuBody", typeof(RectTransform));
            bodyObject.transform.SetParent(page.transform, false);
            var body = bodyObject.AddComponent<TextMeshProUGUI>();
            body.rectTransform.sizeDelta = new Vector2(380f, 440f);
            body.fontSize = 24f;
            body.textWrappingMode = TextWrappingModes.NoWrap;

            SetField(menu, "menuPage", page);
            SetField(menu, "bodyText", body);
            SetField(menu, "pointerCamera", cam);
            return body;
        }

        private static Vector2 ToScreen(Camera cam, TMP_Text text, Vector3 local) =>
            cam.WorldToScreenPoint(text.rectTransform.TransformPoint(local));

        private static Vector3 LinkCenter(TMP_Text text, string id)
        {
            var info = text.textInfo;
            for (int i = 0; i < info.linkCount; i++)
            {
                var link = info.linkInfo[i];
                if (link.GetLinkID() != id) continue;
                var first = info.characterInfo[link.linkTextfirstCharacterIndex];
                var last = info.characterInfo[link.linkTextfirstCharacterIndex + link.linkTextLength - 1];
                var line = info.lineInfo[first.lineNumber];
                return new Vector3((first.origin + last.xAdvance) * 0.5f, (line.ascender + line.descender) * 0.5f, 0f);
            }
            Assert.Fail($"link {id} not found");
            return Vector3.zero;
        }

        // ── 조작 키 장 ──
        private void OpenKeys()
        {
            OpenSettings();
            menu.Select(5);
            Assert.AreEqual(PauseMenu.Row.Keys, menu.Selected);
            menu.Confirm();
            Assert.AreEqual(PauseMenu.Page.Keys, menu.CurrentPage);
        }

        [Test]
        public void KeysRow_OpensTheKeysPage_BackReturnsToTheKeysRow()
        {
            UseKeyList();
            OpenKeys();
            Assert.AreEqual(PauseMenu.Row.KeyBinding, menu.Selected);

            menu.Back();
            Assert.AreEqual(PauseMenu.Page.Settings, menu.CurrentPage);
            Assert.AreEqual(PauseMenu.Row.Keys, menu.Selected);
        }

        [Test]
        public void KeysPage_HasOneRowPerEntry_ThenResetAndBack()
        {
            UseKeyList();
            OpenKeys();
            menu.Move(1);
            Assert.AreEqual(PauseMenu.Row.KeyBinding, menu.Selected);
            Assert.AreEqual(1, menu.SelectedIndex);
            menu.Move(1);
            Assert.AreEqual(PauseMenu.Row.ResetKeys, menu.Selected);
            menu.Move(1);
            Assert.AreEqual(PauseMenu.Row.Back, menu.Selected);
        }

        [Test]
        public void ResetKeys_PutsEveryKeyBack()
        {
            UseKeyList();
            keyInteract.ApplyBindingOverride(0, "<Keyboard>/q");
            OpenKeys();
            menu.Select(2);
            Assert.AreEqual(PauseMenu.Row.ResetKeys, menu.Selected);
            menu.Confirm();
            Assert.AreEqual("<Keyboard>/e", keyInteract.bindings[0].effectivePath);
        }

        [Test]
        public void KeyRow_WaitsForANewKey_MenuInputIsIgnored_ClosingCancels()
        {
            UseKeyList();
            OpenKeys();
            menu.Confirm();   // 상호작용: wait for a key (the test sends none)
            Assert.AreEqual(0, menu.WaitingKey);
            Assert.IsTrue(KeyBindings.IsRebinding);

            menu.Move(1);
            Assert.AreEqual(0, menu.SelectedIndex);   // menu input is ignored while waiting

            menu.Close();
            Assert.AreEqual(-1, menu.WaitingKey);
            Assert.IsFalse(KeyBindings.IsRebinding);
            Assert.AreEqual("<Keyboard>/e", keyInteract.bindings[0].effectivePath);
        }

        [Test]
        public void Open_IsIgnoredDuringFloorTransition()
        {
            New("PatrolRun").AddComponent<PatrolRun>();
            var transition = New("Transition").AddComponent<FloorTransition>();   // Awake finds the run and the player
            SetField(menu, "transition", transition);
            transition.Descend();
            Assert.IsTrue(transition.IsBusy);

            menu.Open();
            Assert.IsFalse(menu.IsOpen);
            Assert.AreEqual(1f, Time.timeScale);
        }

        // 00_08d: 조작 키 목록 설정 실수는 경고 (메뉴에서 눌러도 안 바뀌는 줄, 10줄 초과)
        [Test]
        public void KeyListMistakes_AreWarned()
        {
            SetField(menu, "keyBindings", KeyListOf(11, emptyAt: 10));
            LogAssert.Expect(LogType.Warning, new Regex("10줄을 넘음"));
            LogAssert.Expect(LogType.Warning, new Regex("11번째 줄 '빈 줄'"));

            typeof(PauseMenu).GetMethod("WarnKeyBindings", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(menu, null);
        }

        // 00_08d: 메뉴에는 10줄만 보여도 겹침 맞바꿈은 목록 전체를 본다 (숨은 줄의 키와도 안 겹치게)
        [Test]
        public void Swap_CoversRowsHiddenFromTheMenu()
        {
            SetField(menu, "keyBindings", KeyListOf(12));
            var rows = (System.Collections.IList)typeof(PauseMenu)
                .GetMethod("BindingRows", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(menu, null);

            Assert.AreEqual(12, rows.Count);
            Assert.AreEqual(12, PauseMenu.RowsOf(PauseMenu.Page.Keys, 12).Length);   // 키 10줄 + 기본값으로 + 뒤로
        }

        // 00_08c: PlayerControl이 있으면 메뉴는 그걸로 잠그되 일지는 내리지 않는다 (클립보드를 빌려 쓰므로)
        [Test]
        public void Open_WithPlayerControl_LocksWithoutJournal()
        {
            var control = movement.gameObject.AddComponent<PlayerControl>();
            SetField(menu, "control", control);

            menu.Open();
            Assert.IsTrue(control.IsLocked);
            Assert.IsTrue(movement.IsBlocked);
            Assert.IsFalse(holder.IsBlocked);

            menu.Close();
            Assert.IsFalse(control.IsLocked);
            Assert.IsFalse(movement.IsBlocked);
        }

        // ── 01_05d 화면 변경 로그: 적용은 프레임 끝이라 다음 프레임에 실제 값과 같이 남긴다 ──
        private readonly List<string> screenLogs = new List<string>();

        private void OnScreenLog(string message, string stackTrace, LogType type)
        {
            if (type == LogType.Log && message.StartsWith("[Menu] 화면:")) screenLogs.Add(message);
        }

        // 설정 장의 해상도 줄을 고르고, 해상도 목록·지금 크기를 정해 둔다 (에디터 모니터와 상관없이)
        private void SelectResolution(Vector2Int current, params Vector2Int[] options)
        {
            OpenSettings();
            SetField(menu, "resolutions", new List<Vector2Int>(options));
            SetField(menu, "screenSize", current);
            menu.Select(3);
            Assert.AreEqual(PauseMenu.Row.Resolution, menu.Selected);
        }

        private static readonly Vector2Int Small = new Vector2Int(800, 600), Medium = new Vector2Int(1280, 720);

        [UnityTest]
        public IEnumerator ScreenChange_IsLoggedNextFrame_EvenWhilePaused()
        {
            screenLogs.Clear();
            Application.logMessageReceived += OnScreenLog;
            try
            {
                SelectResolution(Small, Small, Medium);
                menu.Adjust(1);
                Assert.AreEqual(0f, Time.timeScale);
                Assert.IsEmpty(screenLogs, "바뀌기 전(같은 프레임)에는 남기지 않음");

                yield return null;
                Assert.AreEqual(1, screenLogs.Count);
                StringAssert.StartsWith("[Menu] 화면: 요청 1280×720 ", screenLogs[0]);
                StringAssert.Contains(" → 실제 ", screenLogs[0]);

                yield return null;
                Assert.AreEqual(1, screenLogs.Count, "한 번만 남김");
            }
            finally
            {
                Application.logMessageReceived -= OnScreenLog;
            }
        }

        [UnityTest]
        public IEnumerator SeveralChangesInOneFrame_LogOnce_WithTheLastRequest()
        {
            screenLogs.Clear();
            Application.logMessageReceived += OnScreenLog;
            try
            {
                SelectResolution(Small, Small, Medium, new Vector2Int(1920, 1080));
                menu.Adjust(1);
                menu.Adjust(1);
                yield return null;
                Assert.AreEqual(1, screenLogs.Count);
                StringAssert.StartsWith("[Menu] 화면: 요청 1920×1080 ", screenLogs[0]);
            }
            finally
            {
                Application.logMessageReceived -= OnScreenLog;
            }
        }

        [UnityTest]
        public IEnumerator ChangesOnLaterFrames_AreEachLogged()
        {
            screenLogs.Clear();
            Application.logMessageReceived += OnScreenLog;
            try
            {
                SelectResolution(Small, Small, Medium);
                menu.Adjust(1);
                yield return null;
                menu.Adjust(-1);
                yield return null;
                Assert.AreEqual(2, screenLogs.Count);
                StringAssert.StartsWith("[Menu] 화면: 요청 1280×720 ", screenLogs[0]);
                StringAssert.StartsWith("[Menu] 화면: 요청 800×600 ", screenLogs[1]);
            }
            finally
            {
                Application.logMessageReceived -= OnScreenLog;
            }
        }

        [UnityTest]
        public IEnumerator ClosingRightAfterAChange_StillLogs()
        {
            screenLogs.Clear();
            Application.logMessageReceived += OnScreenLog;
            try
            {
                SelectResolution(Small, Small, Medium);
                menu.Adjust(1);
                menu.Close();
                Assert.AreEqual(1f, Time.timeScale);
                yield return null;
                Assert.AreEqual(1, screenLogs.Count);
                StringAssert.StartsWith("[Menu] 화면: 요청 1280×720 ", screenLogs[0]);
            }
            finally
            {
                Application.logMessageReceived -= OnScreenLog;
            }
        }

        [UnityTest]
        public IEnumerator NoScreenChange_NoLog()
        {
            screenLogs.Clear();
            Application.logMessageReceived += OnScreenLog;
            try
            {
                SelectResolution(Medium, Small, Medium);
                menu.Adjust(1);          // 이미 맨 끝: 안 바뀜
                menu.Select(0);          // 마우스 감도
                menu.Adjust(1);
                yield return null;
                Assert.IsEmpty(screenLogs);
            }
            finally
            {
                Application.logMessageReceived -= OnScreenLog;
            }
        }

        [UnityTest]
        public IEnumerator ScreenModeToggle_LogsTheRequestedMode()
        {
            screenLogs.Clear();
            Application.logMessageReceived += OnScreenLog;
            try
            {
                OpenSettings();
                menu.Select(2);
                Assert.AreEqual(PauseMenu.Row.ScreenMode, menu.Selected);
                bool full = ScreenOptions.IsFullScreen(Screen.fullScreenMode);
                menu.Adjust(1);
                yield return null;
                Assert.AreEqual(1, screenLogs.Count);
                StringAssert.Contains(" " + ScreenOptions.ScreenModeLabel(!full) + " → 실제 ", screenLogs[0]);
                menu.Adjust(1);          // 원래 모드로
                yield return null;
                Assert.AreEqual(2, screenLogs.Count);
                StringAssert.Contains(" " + ScreenOptions.ScreenModeLabel(full) + " → 실제 ", screenLogs[1]);
            }
            finally
            {
                Application.logMessageReceived -= OnScreenLog;
            }
        }
    }
}
