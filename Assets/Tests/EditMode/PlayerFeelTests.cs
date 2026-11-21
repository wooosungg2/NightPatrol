using System.Reflection;
using NightPatrol.Player;
using NUnit.Framework;
using UnityEngine;

namespace NightPatrol.Tests
{
    // 11/21 걷는 느낌 (PlayerFeel): 걸음마다 화면이 살짝 내려가고 두 걸음에 한 번 좌우, 걸음 수 = 걸은 거리 ÷ 한 걸음,
    // 멈추면 흔들림이 0으로, 달리면 시야가 넓어졌다가 돌아옴, 휴대폰을 든 동안은 시야 그대로, 흔들림 끄기
    public class PlayerFeelTests
    {
        private const float Dt = 0.01f;
        private GameObject player;
        private Camera eye;
        private PlayerFeel feel;
        private PlayerSettings settings;

        [SetUp]
        public void SetUp()
        {
            player = new GameObject("Player");
            var cameraObject = new GameObject("PlayerCamera");
            cameraObject.transform.SetParent(player.transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            eye = cameraObject.AddComponent<Camera>();
            eye.enabled = false;
            eye.fieldOfView = 70f;
            feel = player.AddComponent<PlayerFeel>();
            feel.Bind(cameraObject.transform);
            settings = ScriptableObject.CreateInstance<PlayerSettings>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(player);
            Object.DestroyImmediate(settings);
        }

        private void Run(float seconds, float speed, bool running, bool grounded = true, bool allowFov = true)
        {
            for (int i = 0; i < Mathf.RoundToInt(seconds / Dt); i++) feel.Tick(settings, speed, running, grounded, allowFov, Dt);
        }

        [Test]
        public void BobOffset_IsLowestAtEachFootfall_AndSwaysOnceEveryTwoSteps()
        {
            var footfall = PlayerFeel.BobOffset(Mathf.PI, 0.03f, 0.015f);
            Assert.AreEqual(-0.03f, footfall.y, 1e-5f, "디딜 때 가장 낮음");
            var midStep = PlayerFeel.BobOffset(Mathf.PI / 2f, 0.03f, 0.015f);
            Assert.AreEqual(0f, midStep.y, 1e-5f);
            Assert.AreEqual(0.015f, midStep.x, 1e-5f, "첫 걸음 가운데서 오른쪽");
            Assert.AreEqual(-0.015f, PlayerFeel.BobOffset(Mathf.PI * 1.5f, 0.03f, 0.015f).x, 1e-5f, "다음 걸음은 왼쪽");
        }

        // 디딜 때 툭 꺾이지 않게: 가장 낮은 곳 근처가 평평한 곡선 (예전 |sin| 모양은 V자로 꺾였다)
        [Test]
        public void BobOffset_IsSmoothAroundTheFootfall()
        {
            float lowest = PlayerFeel.BobOffset(Mathf.PI, 0.025f, 0.01f).y;
            float near = PlayerFeel.BobOffset(Mathf.PI + 0.1f, 0.025f, 0.01f).y;
            Assert.AreEqual(lowest, near, 0.025f * 0.02f, "디딘 직후에도 거의 같은 높이");
        }

        // 흔들림 세기: 걷는 속도까지 0→1, 달리기에서는 정한 배율(1.4)까지만
        [Test]
        public void BobStrength_GrowsToOneAtWalkSpeed_AndToTheRunScaleAtRunSpeed()
        {
            Assert.AreEqual(0f, PlayerFeel.BobStrength(0f, 2.6f, 4.5f, 1.4f));
            Assert.AreEqual(0.5f, PlayerFeel.BobStrength(1.3f, 2.6f, 4.5f, 1.4f), 1e-4f);
            Assert.AreEqual(1f, PlayerFeel.BobStrength(2.6f, 2.6f, 4.5f, 1.4f), 1e-4f);
            Assert.AreEqual(1.4f, PlayerFeel.BobStrength(4.5f, 2.6f, 4.5f, 1.4f), 1e-4f);
            Assert.AreEqual(1.4f, PlayerFeel.BobStrength(9f, 2.6f, 4.5f, 1.4f), 1e-4f, "더 빨라도 그대로");
            Assert.AreEqual(1.4f, settings.RunBobScale, 1e-4f, "기본 배율");
        }

        [Test]
        public void Walking_StepsByDistance_AndRunningTakesLongerSteps()
        {
            Run(1f, settings.WalkSpeed, false);
            Assert.AreEqual(settings.WalkSpeed / settings.WalkStepLength, feel.Steps, 1f, "1초에 걸은 거리 ÷ 한 걸음 (기본 2.6m ÷ 1.3m = 2걸음)");
            Assert.AreEqual(2f, settings.WalkSpeed / settings.WalkStepLength, 0.01f, "걷기는 초당 2걸음 (화면 흔들림 템포)");
            int afterWalk = feel.Steps;
            Run(1f, settings.RunSpeed, true);
            Assert.AreEqual(settings.RunSpeed / settings.RunStepLength, feel.Steps - afterWalk, 1f, "4.5m ÷ 1.8m = 2.5걸음");
        }

        [Test]
        public void Walking_MovesTheCamera_AndStoppingBringsItBack()
        {
            int steps = 0;
            feel.Stepped += _ => steps++;
            Run(0.6f, settings.WalkSpeed, false);
            Assert.Greater(feel.Offset.magnitude, 0f, "걷는 동안 흔들림");
            Assert.AreEqual(steps, feel.Steps, "걸음마다 Stepped");
            Run(0.3f, 0f, false);
            Assert.AreEqual(Vector3.zero, feel.Offset, "멈추면 제자리");
            Assert.AreEqual(new Vector3(0f, 1.6f, 0f), eye.transform.localPosition);
        }

        [Test]
        public void InTheAir_NoStepsAndNoBob()
        {
            Run(1f, settings.WalkSpeed, false, grounded: false);
            Assert.AreEqual(0, feel.Steps);
            Assert.AreEqual(Vector3.zero, feel.Offset);
        }

        [Test]
        public void HeadBobOff_KeepsTheCameraStill_ButStillCountsSteps()
        {
            typeof(PlayerSettings).GetField("headBob", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(settings, false);
            Run(1f, settings.WalkSpeed, false);
            Assert.AreEqual(Vector3.zero, feel.Offset);
            Assert.Greater(feel.Steps, 0, "발소리는 그대로");
        }

        // 설정 메뉴 "화면 흔들림"을 끄면 (PlayerFeel.HeadBobEnabled) 흔들림만 없음
        [Test]
        public void MenuHeadBobOff_KeepsTheCameraStill()
        {
            PlayerFeel.HeadBobEnabled = false;
            try
            {
                Run(1f, settings.WalkSpeed, false);
                Assert.AreEqual(Vector3.zero, feel.Offset);
                Assert.Greater(feel.Steps, 0);
            }
            finally { PlayerFeel.HeadBobEnabled = true; }
        }

        // 화면 흔들림 설정: 기본 켬, 켬·끔 표시, 저장했다가 다시 읽기
        [Test]
        public void HeadBobSetting_DefaultsOn_AndIsSaved()
        {
            bool before = NightPatrol.Menu.GameSettings.HeadBob;
            bool had = PlayerPrefs.HasKey(NightPatrol.Menu.GameSettings.HeadBobKey);
            int saved = PlayerPrefs.GetInt(NightPatrol.Menu.GameSettings.HeadBobKey, 1);
            try
            {
                PlayerPrefs.DeleteKey(NightPatrol.Menu.GameSettings.HeadBobKey);
                NightPatrol.Menu.GameSettings.Load();
                Assert.IsTrue(NightPatrol.Menu.GameSettings.HeadBob, "기본 켬");
                Assert.AreEqual("켬", NightPatrol.Menu.GameSettings.HeadBobLabel(true));
                Assert.AreEqual("끔", NightPatrol.Menu.GameSettings.HeadBobLabel(false));
                NightPatrol.Menu.GameSettings.HeadBob = false;
                NightPatrol.Menu.GameSettings.Save();
                NightPatrol.Menu.GameSettings.HeadBob = true;
                NightPatrol.Menu.GameSettings.Load();
                Assert.IsFalse(NightPatrol.Menu.GameSettings.HeadBob, "저장한 값을 다시 읽음");
            }
            finally
            {
                if (had) PlayerPrefs.SetInt(NightPatrol.Menu.GameSettings.HeadBobKey, saved);
                else PlayerPrefs.DeleteKey(NightPatrol.Menu.GameSettings.HeadBobKey);
                NightPatrol.Menu.GameSettings.HeadBob = before;
            }
        }

        [Test]
        public void Running_WidensTheView_ThenItComesBack()
        {
            Run(1f, settings.RunSpeed, true);
            Assert.AreEqual(settings.RunFovBoost, feel.FovKick, 0.05f);
            Assert.AreEqual(70f + feel.FovKick, eye.fieldOfView, 1e-3f);
            Run(1f, settings.WalkSpeed, false);
            Assert.AreEqual(0f, feel.FovKick, 0.01f);
            Assert.AreEqual(70f, eye.fieldOfView, 0.01f, "걸으면 원래 시야각");
        }

        // 휴대폰을 든 동안은 시야를 넓히지 않는다 (카메라 앱 뷰파인더가 눈의 시야각을 따라 만들어지므로)
        [Test]
        public void HoldingThePhone_KeepsTheView()
        {
            Run(1f, settings.RunSpeed, true, allowFov: false);
            Assert.AreEqual(0f, feel.FovKick, 1e-4f);
            Assert.AreEqual(70f, eye.fieldOfView, 1e-4f);
        }

        [Test]
        public void ResetFeel_PutsEverythingBackAtOnce()
        {
            Run(0.5f, settings.RunSpeed, true);
            feel.ResetFeel();
            Assert.AreEqual(Vector3.zero, feel.Offset);
            Assert.AreEqual(0f, feel.FovKick);
            Assert.AreEqual(70f, eye.fieldOfView, 1e-4f);
            Assert.AreEqual(new Vector3(0f, 1.6f, 0f), eye.transform.localPosition);
        }
    }
}
