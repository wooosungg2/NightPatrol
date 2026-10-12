using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NightPatrol.Run;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace NightPatrol.Tests
{
    // 10/12 야간 5번: PatrolRun.Current — PlayerControl.Current와 같은 방식.
    // 먼저 켜진 PatrolRun, 꺼지거나 사라지면 null, Play를 시작할 때마다 비움 (도메인 리로드를 꺼도)
    public class PatrolRunCurrentTests
    {
        private readonly List<GameObject> created = new List<GameObject>();
        private readonly List<Scene> scenes = new List<Scene>();

        [SetUp]
        public void SetUp()
        {
            Assert.IsNull(Object.FindAnyObjectByType<PatrolRun>(), "앞 테스트가 PatrolRun을 남김");
            Assert.IsNull(PatrolRun.Current, "앞 테스트의 PatrolRun.Current가 남음");
        }

        [UnityTearDown]
        public IEnumerator UnloadLeftoverScenes()
        {
            foreach (var scene in scenes)
                if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            scenes.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in created)
                if (go != null) Object.DestroyImmediate(go);
            created.Clear();
        }

        private PatrolRun NewRun(string name, bool expectSecondWarning = false)
        {
            if (expectSecondWarning) LogAssert.Expect(LogType.Warning, new Regex("PatrolRun이 둘 이상"));   // Awake의 기존 경고
            var go = new GameObject(name);
            created.Add(go);
            return go.AddComponent<PatrolRun>();
        }

        private PatrolRun NewRunInScene(string name, Scene scene)
        {
            var go = new GameObject(name);
            created.Add(go);
            SceneManager.MoveGameObjectToScene(go, scene);
            return go.AddComponent<PatrolRun>();
        }

        [Test]
        public void Current_IsTheFirstEnabledRun_SecondDoesNotReplaceIt()
        {
            var first = NewRun("Run A");
            Assert.AreSame(first, PatrolRun.Current);

            NewRun("Run B", expectSecondWarning: true);
            Assert.AreSame(first, PatrolRun.Current);
        }

        [Test]
        public void Current_WhenTheFirstIsDisabled_IsNull_EvenIfAnotherIsEnabled()
        {
            var first = NewRun("Run A");
            var second = NewRun("Run B", expectSecondWarning: true);

            // 알려진 한계 (가이드 7장): 다른 PatrolRun으로 넘어가지 않음. 씬에는 하나만 둔다
            first.enabled = false;
            Assert.IsNull(PatrolRun.Current);
            Assert.IsTrue(second.isActiveAndEnabled);

            // 빈자리는 다음에 켜지는 쪽이 차지
            second.enabled = false;
            second.enabled = true;
            Assert.AreSame(second, PatrolRun.Current);

            first.enabled = true;
            Assert.AreSame(second, PatrolRun.Current);
        }

        [Test]
        public void Current_IgnoresAnInactiveRun_UntilItIsActivated()
        {
            var go = new GameObject("Run (꺼진 오브젝트)");
            created.Add(go);
            go.SetActive(false);
            var run = go.AddComponent<PatrolRun>();
            Assert.IsNull(PatrolRun.Current);

            go.SetActive(true);
            Assert.AreSame(run, PatrolRun.Current);

            go.SetActive(false);
            Assert.IsNull(PatrolRun.Current);

            go.SetActive(true);
            Assert.AreSame(run, PatrolRun.Current);

            run.enabled = false;
            Assert.IsNull(PatrolRun.Current);
        }

        [UnityTest]
        public IEnumerator Current_IsClearedWhenDestroyed_AndTheNextRunTakesIt()
        {
            var first = NewRun("Run A");
            Object.Destroy(first.gameObject);
            yield return null;   // Destroy는 프레임 끝에
            Assert.IsNull(PatrolRun.Current);

            var second = NewRun("Run B");   // 앞의 것은 사라져서 둘 이상 경고 없음
            Assert.AreSame(second, PatrolRun.Current);

            Object.DestroyImmediate(second);   // 컴포넌트만 지워도
            Assert.IsNull(PatrolRun.Current);
        }

        [UnityTest]
        public IEnumerator Current_FollowsSceneUnloadAndLoad()
        {
            var sceneA = SceneManager.CreateScene("PatrolRunCurrent_A");
            scenes.Add(sceneA);
            var runA = NewRunInScene("Run A", sceneA);
            Assert.AreSame(runA, PatrolRun.Current);

            yield return SceneManager.UnloadSceneAsync(sceneA);
            Assert.IsTrue(runA == null, "씬을 내리면 PatrolRun도 사라져야 함");
            Assert.IsNull(PatrolRun.Current, "씬과 함께 사라진 PatrolRun을 붙잡고 있음");

            // 같은 층 진행 씬을 다시 연 것과 같음: 새 PatrolRun이 Current
            var sceneB = SceneManager.CreateScene("PatrolRunCurrent_B");
            scenes.Add(sceneB);
            var runB = NewRunInScene("Run B", sceneB);
            Assert.AreSame(runB, PatrolRun.Current);

            yield return SceneManager.UnloadSceneAsync(sceneB);
            Assert.IsNull(PatrolRun.Current);
        }

        [Test]
        public void ResetStatics_RunsWhenPlayStarts_AndClearsCurrent()
        {
            var reset = typeof(PatrolRun).GetMethod("ResetStatics", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(reset, "PatrolRun.ResetStatics가 없음");
            var attribute = reset.GetCustomAttribute<RuntimeInitializeOnLoadMethodAttribute>();
            Assert.IsNotNull(attribute, "ResetStatics에 RuntimeInitializeOnLoadMethod가 없음 — 도메인 리로드를 끄면 지난 Play 값이 남음");
            Assert.AreEqual(RuntimeInitializeLoadType.SubsystemRegistration, attribute.loadType);

            // 도메인 리로드를 끈 에디터에서 지난 Play의 값이 남은 상황: 지워진 PatrolRun을 가리킴
            var setter = typeof(PatrolRun).GetProperty(nameof(PatrolRun.Current)).GetSetMethod(true);
            var stale = NewRun("Run (지난 Play)");
            Object.DestroyImmediate(stale.gameObject);
            setter.Invoke(null, new object[] { stale });
            Assert.IsFalse(ReferenceEquals(PatrolRun.Current, null));

            reset.Invoke(null, null);
            Assert.IsTrue(ReferenceEquals(PatrolRun.Current, null));

            // 비우지 않았더라도 지워진 값은 새 PatrolRun이 바로 대신함
            setter.Invoke(null, new object[] { stale });
            var fresh = NewRun("Run (이번 Play)");
            Assert.AreSame(fresh, PatrolRun.Current);
        }
    }
}
