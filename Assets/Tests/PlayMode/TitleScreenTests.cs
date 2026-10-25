using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NightPatrol.Menu;
using NightPatrol.Run;
using NightPatrol.Title;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace NightPatrol.Tests
{
    // 00_16: 시작 화면 씬 — 켜지면 시작 화면, 근무 시작 → 검게 덮인 뒤 게임 씬을 불러오고 돌아올 시작 화면을 기억
    public class TitleScreenTests
    {
        private readonly List<GameObject> created = new List<GameObject>();
        private PauseMenu menu;
        private TitleScreen title;
        private ScreenFader fader;
        private string loaded;

        [SetUp]
        public void SetUp()
        {
            loaded = null;
            GameRestart.Override = path => loaded = path;
            GameRestart.TitleScenePath = null;

            var root = New("Title");
            root.SetActive(false);   // 칸을 다 넣은 뒤에 Awake
            var page = new GameObject("MenuPanel");
            page.transform.SetParent(root.transform);
            menu = root.AddComponent<PauseMenu>();
            SetField(menu, "menuPage", page);
            SetField(menu, "openOnFocusLoss", false);

            var faderObject = New("Fader");
            faderObject.AddComponent<CanvasGroup>();
            fader = faderObject.AddComponent<ScreenFader>();

            title = root.AddComponent<TitleScreen>();
            SetField(title, "fader", fader);
            SetField(title, "fadeSeconds", 0.05f);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in created)
                if (go != null) Object.DestroyImmediate(go);
            created.Clear();
            GameRestart.Override = null;
            GameRestart.TitleScenePath = null;
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }

        private GameObject New(string name)
        {
            var go = new GameObject(name);
            created.Add(go);
            return go;
        }

        private static void SetField(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        private IEnumerator Begin()
        {
            menu.gameObject.SetActive(true);
            yield return null;   // Start
        }

        [UnityTest]
        public IEnumerator OnStart_OpensTheTitle()
        {
            yield return Begin();
            Assert.IsTrue(menu.IsTitle);
            Assert.AreEqual(PauseMenu.Row.Start, menu.Selected);
            Assert.AreEqual(0f, fader.Alpha);
        }

        [UnityTest]
        public IEnumerator Start_CoversTheScreen_ThenLoadsTheGameScene()
        {
            yield return Begin();
            menu.Confirm();   // 근무 시작
            Assert.IsTrue(title.IsStarting);
            Assert.IsNull(loaded, "검게 덮이기 전에 넘어감");

            for (int i = 0; i < 120 && loaded == null; i++) yield return null;
            Assert.AreEqual("Assets/Scenes/Floor5.unity", loaded);
            Assert.AreEqual(1f, fader.Alpha);
            Assert.IsFalse(menu.IsOpen);
            Assert.AreEqual(1f, Time.timeScale);
            Assert.AreEqual(SceneManager.GetActiveScene().path, GameRestart.TitleScenePath);
        }

        [UnityTest]
        public IEnumerator Start_WithAMissingScene_StaysOnTheTitle()
        {
            SetField(title, "gameScene", "Assets/Scenes/NoSuchScene.unity");
            yield return Begin();
            LogAssert.Expect(LogType.Error, new Regex("불러올 씬을 찾을 수 없음"));
            menu.Confirm();
            Assert.IsFalse(title.IsStarting);
            Assert.IsTrue(menu.IsTitle);
            Assert.IsNull(loaded);
        }
    }
}
