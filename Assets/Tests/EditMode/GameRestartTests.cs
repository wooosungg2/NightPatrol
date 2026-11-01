using NightPatrol.Run;
using NUnit.Framework;
using UnityEngine.SceneManagement;

namespace NightPatrol.Tests
{
    // 00_16: 시작 화면 씬에서 들어왔으면 처음으로·퇴근이 시작 화면으로, 아니면 지금 씬을 다시
    public class GameRestartTests
    {
        private string loaded;

        [SetUp]
        public void SetUp()
        {
            loaded = null;
            GameRestart.Override = path => loaded = path;
            GameRestart.TitleScenePath = null;
        }

        [TearDown]
        public void TearDown()
        {
            GameRestart.Override = null;
            GameRestart.TitleScenePath = null;
        }

        [Test]
        public void Restart_WithoutATitle_ReloadsTheCurrentScene()
        {
            GameRestart.Restart();
            Assert.AreEqual(SceneManager.GetActiveScene().path, loaded);
        }

        [Test]
        public void StartGame_LoadsTheGameScene_AndRemembersTheTitle()
        {
            GameRestart.StartGame("Assets/Scenes/Floor5.unity");
            Assert.AreEqual("Assets/Scenes/Floor5.unity", loaded);
            Assert.AreEqual(SceneManager.GetActiveScene().path, GameRestart.TitleScenePath);
        }

        [Test]
        public void Restart_AfterTheTitle_GoesBackToTheTitle()
        {
            GameRestart.TitleScenePath = "Assets/Scenes/Title.unity";
            GameRestart.Restart();
            Assert.AreEqual("Assets/Scenes/Title.unity", loaded);
        }

        [Test]
        public void CanLoad_OnlyExistingScenes()
        {
            Assert.IsTrue(GameRestart.CanLoad("Assets/Scenes/Floor5.unity"));
            Assert.IsFalse(GameRestart.CanLoad("Assets/Scenes/NoSuchScene.unity"));
            Assert.IsFalse(GameRestart.CanLoad(null));
            Assert.IsFalse(GameRestart.CanLoad(""));
        }
    }
}
