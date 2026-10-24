using System.Linq;
using NightPatrol.Debugging;
using NightPatrol.Menu;
using NightPatrol.Title;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace NightPatrol.Tests
{
    // 00_16: 시작 화면 씬 연결 — 메뉴 칸이 비거나, 근무 시작이 빌드에 없는 씬을 부르면 실패
    public class TitleSceneWiringTests
    {
        private const string TitleScene = "Assets/Scenes/Title.unity";
        private Scene scene;
        private bool opened;

        [OneTimeSetUp]
        public void Open()
        {
            scene = SceneManager.GetSceneByPath(TitleScene);
            if (scene.isLoaded) return;
            scene = EditorSceneManager.OpenScene(TitleScene, OpenSceneMode.Additive);   // 열린 씬은 그대로 두고 옆에 열었다가 닫는다
            opened = true;
        }

        [OneTimeTearDown]
        public void Close()
        {
            if (opened) EditorSceneManager.CloseScene(scene, true);
        }

        private T Only<T>() where T : UnityEngine.Component
        {
            var found = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<T>(true)).ToArray();
            Assert.AreEqual(1, found.Length, $"시작 화면 씬에 {typeof(T).Name}이 하나여야 함");
            return found[0];
        }

        [Test]
        public void TitleScene_IsTheFirstBuildScene()
        {
            Assert.AreEqual(TitleScene, DevBuildPlan.DefaultScenes[0]);
        }

        [Test]
        public void Start_LoadsAGameSceneThatIsBuilt()
        {
            var screen = new SerializedObject(Only<TitleScreen>());
            string game = screen.FindProperty("gameScene").stringValue;
            CollectionAssert.Contains(DevBuildPlan.DefaultScenes, game, "근무 시작이 부르는 씬(TitleScreen의 Game Scene)이 빌드 씬 목록(DevBuildPlan)에 없음");
            Assert.AreNotEqual(TitleScene, game);
            Assert.IsNotNull(screen.FindProperty("fader").objectReferenceValue, "TitleScreen의 Fader가 비어 있음");
        }

        [Test]
        public void Menu_IsConnected()
        {
            var menu = new SerializedObject(Only<PauseMenu>());
            foreach (var field in new[] { "menuPage", "titleText", "bodyText", "keyBindings" })
                Assert.IsNotNull(menu.FindProperty(field).objectReferenceValue, $"시작 화면 씬 PauseMenu의 {field} 칸이 비어 있음");
            Assert.IsFalse(menu.FindProperty("openOnFocusLoss").boolValue, "시작 화면에서 창을 벗어나면 일시정지 장이 열림 — Open On Focus Loss 끄기");
        }

        // 가장 긴 장(조작 키 10줄 + 기본값으로 + 뒤로 + 바닥글)도 줄 글 상자 안에 들어가야 화면 아래로 안 잘린다
        [Test]
        public void Body_FitsTheLongestPage()
        {
            var body = (TMPro.TMP_Text)new SerializedObject(Only<PauseMenu>()).FindProperty("bodyText").objectReferenceValue;
            var labels = Enumerable.Repeat("마우스 오른쪽", PauseMenuText.MaxRows - 2).ToArray();
            string longest = PauseMenu.BodyText(PauseMenu.Page.Keys, 0, new PauseMenu.Values { KeyLabels = labels, KeyNames = labels });
            var box = body.rectTransform.rect;
            float height = body.GetPreferredValues(longest, box.width, 0f).y;
            Assert.LessOrEqual(height, box.height, $"조작 키 장이 줄 글 상자(Canvas/Menu/Body, 높이 {box.height:0})를 넘침 — 글자 크기·줄 간격을 줄이거나 상자를 키울 것");
        }
    }
}
