using System;
using System.Collections.Generic;
using NightPatrol.Audio;
using NightPatrol.Interaction;
using NightPatrol.Journal;
using NightPatrol.Menu;
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
    }
}
