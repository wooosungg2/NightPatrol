using System.Collections;
using System.Reflection;
using NightPatrol.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace NightPatrol.Tests
{
    // 01_09: 걷던 중 조작이 잠기면 감속 시간에 걸쳐 멈춘다 (바로 서지도, 계속 가지도 않음)
    public class PlayerWalkSmoothingPlayTests
    {
        private GameObject body;
        private PlayerSettings settings;
        private InputActionAsset actions;

        [TearDown]
        public void TearDown()
        {
            Time.captureDeltaTime = 0f;
            if (body != null) Object.DestroyImmediate(body);
            if (settings != null) Object.DestroyImmediate(settings);
            if (actions != null) Object.DestroyImmediate(actions);
        }

        private static void SetField(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        private static Vector2 WalkVelocity(PlayerMovement movement) =>
            (Vector2)typeof(PlayerMovement).GetField("walkVelocity", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(movement);

        [UnityTest]
        public IEnumerator WalkingWhenLocked_SlowsDownOverDecelerationTime()
        {
            settings = ScriptableObject.CreateInstance<PlayerSettings>();   // 기본값: 걷기 2.6m/s, 감속 0.1초
            actions = ScriptableObject.CreateInstance<InputActionAsset>();
            var move = actions.AddActionMap("Player").AddAction("Move", InputActionType.Value, expectedControlLayout: "Vector2");

            body = new GameObject("Player");
            body.SetActive(false);
            var movement = body.AddComponent<PlayerMovement>();
            SetField(movement, "settings", settings);
            SetField(movement, "moveAction", InputActionReference.Create(move));
            body.SetActive(true);
            Assert.IsTrue(movement.enabled, "설정이 다 연결된 플레이어");

            Time.captureDeltaTime = 0.01f;   // 프레임마다 0.01초로 고정
            yield return null;
            Vector3 start = body.transform.position;
            SetField(movement, "walkVelocity", new Vector2(0f, settings.WalkSpeed));   // 앞으로 걷는 중
            movement.SetBlocked(this, true);

            for (int i = 0; i < 20; i++) yield return null;

            Vector3 moved = body.transform.position - start;
            float forward = Vector3.Dot(moved, body.transform.forward);
            Assert.AreEqual(Vector2.zero, WalkVelocity(movement), "감속 시간 뒤에는 멈춤");
            // 걷는 속도에서 감속 시간에 걸쳐 고르게 멈추면 속도 × 시간 ÷ 2만큼 더 감 (2.6m/s·0.1초 → 0.13m, 프레임 0.01초면 0.117m)
            float expected = settings.WalkSpeed * settings.DecelerationTime / 2f;
            Assert.That(forward, Is.GreaterThan(expected * 0.5f).And.LessThan(expected * 1.2f), $"미끄러진 거리 {forward:0.000}m");
        }
    }
}
