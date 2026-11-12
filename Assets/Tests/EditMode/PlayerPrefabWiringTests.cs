using System;
using System.Collections.Generic;
using NightPatrol.Audio;
using NightPatrol.Interaction;
using NightPatrol.Journal;
using NightPatrol.Menu;
using NightPatrol.Phone;
using NightPatrol.Photo;
using NightPatrol.Player;
using NightPatrol.Run;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace NightPatrol.Tests
{
    // 00_13 P4: 공용 Player 프리팹의 연결 검사. 단위 테스트는 코드로 만든 오브젝트만 써서 프리팹 칸이 빠져도 Play 때 오류로만 안다.
    // Codex·MCP 세팅이나 팀원이 프리팹을 고쳤을 때 여기서 바로 실패한다
    public class PlayerPrefabWiringTests
    {
        private const string PrefabPath = "Assets/Prefabs/Player.prefab";
        private GameObject prefab;

        [OneTimeSetUp]
        public void LoadPrefab()
        {
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(prefab, PrefabPath + "이 없음 (옮겼다면 이 테스트의 경로도 고칠 것)");
        }

        // (컴포넌트, 칸): 비면 Play 때 오류가 나고 그 기능이 꺼지거나, 조용히 안 보임
        private static readonly (Type type, string field)[] Required =
        {
            (typeof(PlayerMovement), "settings"), (typeof(PlayerMovement), "moveAction"),
            (typeof(PlayerLook), "settings"), (typeof(PlayerLook), "cameraTransform"), (typeof(PlayerLook), "lookAction"),
            (typeof(PlayerInteractor), "settings"), (typeof(PlayerInteractor), "playerCamera"), (typeof(PlayerInteractor), "interactAction"),
            (typeof(JournalHolder), "journalAction"),
            (typeof(JournalView), "titleText"), (typeof(JournalView), "bodyText"),
            (typeof(PauseMenu), "journalPage"), (typeof(PauseMenu), "menuPage"), (typeof(PauseMenu), "titleText"),
            (typeof(PauseMenu), "bodyText"), (typeof(PauseMenu), "keyBindings"),
            (typeof(FloorTransition), "fader"), (typeof(FloorTransition), "floorCaption"),
            (typeof(InteractionPromptView), "crosshair"), (typeof(InteractionPromptView), "promptText"), (typeof(InteractionPromptView), "holdRing"),
            (typeof(PhoneHolder), "phoneAction"), (typeof(PhoneHolder), "interactAction"),
        };

        [Test]
        public void RequiredFields_AreConnected()
        {
            var problems = new List<string>();
            foreach (var (type, field) in Required)
            {
                var components = prefab.GetComponentsInChildren(type, true);
                if (components.Length == 0)
                {
                    problems.Add($"{type.Name} 없음");
                    continue;
                }
                foreach (var component in components)
                {
                    var property = new SerializedObject(component).FindProperty(field);
                    if (property == null) problems.Add($"{type.Name}.{field} 칸을 못 찾음 (이름이 바뀌었으면 이 테스트도 고칠 것)");
                    else if (property.objectReferenceValue == null) problems.Add($"{component.name}의 {type.Name} → {field} 비어 있음");
                }
            }
            Assert.IsEmpty(problems, "Player 프리팹 연결 빠짐: " + string.Join(" / ", problems));
        }

        // 둘이면 진행·연출·메뉴가 엉뚱한 쪽을 잡는다
        [Test]
        public void CoreComponents_AreExactlyOne()
        {
            var types = new[]
            {
                typeof(PlayerMovement), typeof(PlayerLook), typeof(PlayerInteractor), typeof(PlayerControl), typeof(JournalHolder),
                typeof(JournalView), typeof(PauseMenu), typeof(FloorTransition), typeof(ScreenFader), typeof(InteractionPromptView),
                typeof(PlayerSounds), typeof(CharacterController),
                typeof(PhoneHolder), typeof(PhoneCalls), typeof(SubtitleView), typeof(PhoneScreen),
            };
            var problems = new List<string>();
            foreach (var type in types)
            {
                int count = prefab.GetComponentsInChildren(type, true).Length;
                if (count != 1) problems.Add($"{type.Name} {count}개");
            }
            Assert.IsEmpty(problems, "Player 프리팹에 하나씩 있어야 함: " + string.Join(", ", problems));
        }

        // 04_03b: 클립보드(일지·메뉴)는 FirstPerson 레이어로 ViewModelCamera만 그린다 — 메인 카메라가 그리면 벽에 박힘
        [Test]
        public void Clipboard_IsDrawnOnlyByTheViewModelCamera()
        {
            int layer = LayerMask.NameToLayer("FirstPerson");
            Assert.GreaterOrEqual(layer, 0, "FirstPerson 레이어가 없음 (Project Settings → Tags and Layers)");
            int bit = 1 << layer;

            var problems = new List<string>();
            var holder = prefab.GetComponentInChildren<JournalHolder>(true);
            foreach (var t in holder.GetComponentsInChildren<Transform>(true))
                if (t.gameObject.layer != layer) problems.Add($"{t.name} 레이어 {LayerMask.LayerToName(t.gameObject.layer)}");

            Camera main = null, viewModel = null;
            foreach (var camera in prefab.GetComponentsInChildren<Camera>(true))
            {
                if (camera.name == "PlayerCamera") main = camera;
                else if (camera.name == "ViewModelCamera") viewModel = camera;
            }
            if (main == null) problems.Add("PlayerCamera 없음");
            else if ((main.cullingMask & bit) != 0) problems.Add("PlayerCamera가 FirstPerson을 그림");
            if (viewModel == null) problems.Add("ViewModelCamera 없음");
            else if (viewModel.cullingMask != bit) problems.Add("ViewModelCamera가 FirstPerson만 그리지 않음");

            Assert.IsEmpty(problems, "클립보드·카메라 레이어: " + string.Join(" / ", problems));
        }

        // 06_01: 전화도 클립보드처럼 FirstPerson 레이어 (임시 모델은 실행할 때 이 레이어로 만든다).
        // 통화 표(PhoneCalls)·소리(이름 7개 — 06_04 셔터 포함, 파일은 유연주님)는 같은 오브젝트에
        [Test]
        public void Phone_IsOnTheFirstPersonLayer_WithItsCallsAndSoundNames()
        {
            int layer = LayerMask.NameToLayer("FirstPerson");
            var phone = prefab.GetComponentInChildren<PhoneHolder>(true);
            Assert.IsNotNull(phone, "PhoneHolder 없음 (세팅 .ai/setup/06_01-phone.md)");
            var problems = new List<string>();
            foreach (var t in phone.GetComponentsInChildren<Transform>(true))
                if (t.gameObject.layer != layer) problems.Add($"{t.name} 레이어 {LayerMask.LayerToName(t.gameObject.layer)}");
            if (phone.transform.parent == null || phone.transform.parent.name != "PlayerCamera") problems.Add("Phone이 PlayerCamera 아래가 아님");
            if (phone.GetComponent<PhoneCalls>() == null) problems.Add("PhoneCalls가 같은 오브젝트에 없음");
            var sounds = phone.GetComponent<SoundEmitter>();
            if (sounds == null) problems.Add("SoundEmitter가 같은 오브젝트에 없음");
            else
                foreach (var name in new[] { PhoneHolder.VibrateSound, PhoneHolder.BellSound, PhoneHolder.RingbackSound,
                             PhoneHolder.AnswerSound, PhoneHolder.HangUpSound, PhoneHolder.StaticSound, PhoneHolder.ShutterSound })
                    if (!sounds.Has(name)) problems.Add($"소리 묶음 '{name}' 없음");
            Assert.IsEmpty(problems, "전화 배치: " + string.Join(" / ", problems));
        }

        // 06_03: 홈 화면(PhoneScreen)이 전화 옆에, 독 앱(06_04 카메라·전화·메시지·인터넷)은 Apps 아래 — 인스펙터에서 앱을 더하고 뺄 수 있게
        [Test]
        public void Phone_HasItsHomeScreen_AndTheDockApps()
        {
            var phone = prefab.GetComponentInChildren<PhoneHolder>(true);
            Assert.IsNotNull(phone, "PhoneHolder 없음");
            var problems = new List<string>();
            if (phone.GetComponent<PhoneScreen>() == null) problems.Add("PhoneScreen이 같은 오브젝트에 없음 (세팅 .ai/setup/06_03-phone-home.md)");
            var apps = phone.transform.Find("Apps");
            if (apps == null) problems.Add("Phone/Apps 없음");
            else
            {
                if (apps.GetComponent<CallApp>() == null) problems.Add("CallApp 없음");
                if (apps.GetComponent<MessagesApp>() == null) problems.Add("MessagesApp 없음");
                if (apps.GetComponent<BrowserApp>() == null) problems.Add("BrowserApp 없음");
                if (apps.GetComponent<CameraApp>() == null) problems.Add("CameraApp 없음 (06_04)");
            }
            Assert.IsEmpty(problems, "전화 화면: " + string.Join(" / ", problems));
        }

        // 06_03: 프리팹에서 들기 자세를 바꿔도 전화 전체가 화면 안·가운데 왼쪽, 앞면이 눈을 향해야 한다
        [Test]
        public void Phone_HeldPoseInThePrefab_KeepsItOnScreen()
        {
            var phone = prefab.GetComponentInChildren<PhoneHolder>(true);
            Assert.IsNotNull(phone, "PhoneHolder 없음");
            PhoneHomeTests.AssertOnScreen((Vector3)PhoneHomeTests.Field(phone, "heldPosition"), (Vector3)PhoneHomeTests.Field(phone, "heldEuler"));
        }

        // 카메라로만 보이는 것(CameraOnly 레이어)은 눈(PlayerCamera)과 손에 든 것 카메라(ViewModelCamera)가 안 그린다 — 휴대폰 뷰파인더만
        [Test]
        public void CameraOnlyLayer_Exists_AndThePlayerCamerasDoNotDrawIt()
        {
            Assert.GreaterOrEqual(CameraOnlyLayer.Index, 0, "Project Settings → Tags and Layers에 CameraOnly 레이어가 없음");
            var cameras = prefab.GetComponentsInChildren<Camera>(true);
            Assert.IsNotEmpty(cameras);
            var problems = new List<string>();
            foreach (var camera in cameras)
                if (CameraOnlyLayer.In(camera.cullingMask)) problems.Add(camera.name + "이 CameraOnly를 그림 (Culling Mask에서 빼야 함)");
            Assert.IsEmpty(problems, string.Join(" / ", problems));
        }
    }
}
