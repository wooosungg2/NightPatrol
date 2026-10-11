using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NightPatrol.Interaction;
using NightPatrol.Journal;
using NightPatrol.Menu;
using NightPatrol.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace NightPatrol.Tests
{
    // 00_08c: PlayerControl(한 번에 잠금·조작 가능 여부·둘이면 경고), 공용 입력 액션을 끄지 않기, 설정 시작 적용
    public class PlayerControlTests
    {
        private readonly List<Object> created = new List<Object>();
        private PlayerMovement movement;
        private PlayerLook look;
        private PlayerInteractor interactor;
        private JournalHolder journal;

        private T Track<T>(T obj) where T : Object
        {
            created.Add(obj);
            return obj;
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            for (int i = created.Count - 1; i >= 0; i--)
                if (created[i] != null) Object.DestroyImmediate(created[i]);
            created.Clear();
        }

        private static void SetField(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        // 설정이 빠진 플레이어 (이동·시점·E는 오류를 찍고 스스로 꺼지지만 잠금 집합은 그대로 쓸 수 있음)
        private PlayerControl MakePlayer(string name = "Player", bool expectSecondWarning = false)
        {
            LogAssert.Expect(LogType.Error, new Regex("Assign PlayerSettings and Player/Move"));
            LogAssert.Expect(LogType.Error, new Regex("Assign PlayerSettings, a child camera, and Player/Look"));
            LogAssert.Expect(LogType.Error, new Regex("Assign PlayerSettings, the player camera, and Player/Interact"));
            if (expectSecondWarning) LogAssert.Expect(LogType.Warning, new Regex("플레이어가 둘 이상"));   // PlayerControl.OnEnable (오류들 다음)
            var body = Track(new GameObject(name));
            movement = body.AddComponent<PlayerMovement>();
            look = body.AddComponent<PlayerLook>();
            interactor = body.AddComponent<PlayerInteractor>();
            var clipboard = new GameObject("Clipboard");
            clipboard.transform.SetParent(body.transform);
            journal = clipboard.AddComponent<JournalHolder>();
            return body.AddComponent<PlayerControl>();   // Awake가 위 넷을 찾음
        }

        [Test]
        public void Lock_LocksAllFourParts_AndUnlocks()
        {
            var control = MakePlayer();
            var owner = new object();

            control.Lock(owner, true);
            Assert.IsTrue(control.IsLocked);
            Assert.IsTrue(movement.IsBlocked && look.IsBlocked && interactor.IsBlocked && journal.IsBlocked);

            control.Lock(owner, false);
            Assert.IsFalse(control.IsLocked);
            Assert.IsFalse(movement.IsBlocked || look.IsBlocked || interactor.IsBlocked || journal.IsBlocked);
        }

        [Test]
        public void Lock_WithoutJournal_LeavesJournalFree()
        {
            var control = MakePlayer();
            var owner = new object();

            control.Lock(owner, true, includeJournal: false);   // ESC 메뉴는 클립보드를 빌려 쓰므로 일지를 내리지 않음

            Assert.IsTrue(movement.IsBlocked && look.IsBlocked && interactor.IsBlocked);
            Assert.IsFalse(journal.IsBlocked);
            control.Lock(owner, false, includeJournal: false);
        }

        [Test]
        public void Lock_KeepsOtherOwners()
        {
            var control = MakePlayer();
            var a = new object();
            var b = new object();
            control.Lock(a, true);
            control.Lock(b, true);
            control.Lock(a, false);
            Assert.IsTrue(control.IsLocked);
            Assert.IsTrue(movement.IsBlocked);
            control.Lock(b, false);
            Assert.IsFalse(control.IsLocked);
        }

        [Test]
        public void Current_IsTheFirstPlayer_SecondWarns_ClearedWhenDisabled()
        {
            var first = MakePlayer("Player A");
            Assert.AreSame(first, PlayerControl.Current);

            MakePlayer("Player B", expectSecondWarning: true);
            Assert.AreSame(first, PlayerControl.Current);

            first.enabled = false;
            Assert.IsNull(PlayerControl.Current);
        }

        [Test]
        public void CanAct_FalseWhilePausedOrLocked()
        {
            var control = MakePlayer();
            Time.timeScale = 0f;
            Assert.IsFalse(PlayerControl.CanAct, "메뉴(시간 정지) 중");
            Time.timeScale = 1f;

            control.Lock(this, true);
            Assert.IsFalse(PlayerControl.CanAct, "연출이 잠근 동안");
            control.Lock(this, false);
        }

        // ── 공용 입력 액션은 컴포넌트가 꺼져도 끄지 않는다 (같은 액션을 쓰는 다른 기능이 멈추지 않게) ──
        [Test]
        public void DisablingJournal_KeepsSharedActionEnabled()
        {
            var asset = Track(ScriptableObject.CreateInstance<InputActionAsset>());
            var action = asset.AddActionMap("Player").AddAction("Journal", InputActionType.Button, "<Keyboard>/tab");
            var reference = Track(InputActionReference.Create(action));
            var go = Track(new GameObject("Clipboard"));
            go.SetActive(false);
            var holder = go.AddComponent<JournalHolder>();
            SetField(holder, "journalAction", reference);
            go.SetActive(true);
            Assert.IsTrue(action.enabled);

            holder.enabled = false;
            Assert.IsTrue(action.enabled);
            action.Disable();
        }

        [Test]
        public void DisablingMovement_KeepsSharedActionEnabled()
        {
            var asset = Track(ScriptableObject.CreateInstance<InputActionAsset>());
            var action = asset.AddActionMap("Player").AddAction("Move", InputActionType.Value);
            var reference = Track(InputActionReference.Create(action));
            var settings = Track(ScriptableObject.CreateInstance<PlayerSettings>());
            var go = Track(new GameObject("Body"));
            go.SetActive(false);
            var move = go.AddComponent<PlayerMovement>();
            SetField(move, "settings", settings);
            SetField(move, "moveAction", reference);
            go.SetActive(true);
            Assert.IsTrue(action.enabled);

            move.enabled = false;
            Assert.IsTrue(action.enabled);
            action.Disable();
        }

        // ── 저장된 설정을 메뉴 없이도 적용 ──
        [Test]
        public void SettingsStartup_AppliesSensitivityAndVolume_WithoutMenu()
        {
            float savedSensitivity = GameSettings.Sensitivity, savedVolume = GameSettings.Volume, savedListener = AudioListener.volume;
            try
            {
                MakePlayer();
                look.SensitivityScale = 1f;
                GameSettings.Sensitivity = 1.7f;
                GameSettings.Volume = 0.4f;

                SettingsStartup.Apply(SceneManager.GetActiveScene(), LoadSceneMode.Single);

                Assert.AreEqual(1.7f, look.SensitivityScale, 1e-4f);
                Assert.AreEqual(0.4f, AudioListener.volume, 1e-4f);
            }
            finally
            {
                GameSettings.Sensitivity = savedSensitivity;
                GameSettings.Volume = savedVolume;
                AudioListener.volume = savedListener;
            }
        }
    }
}
