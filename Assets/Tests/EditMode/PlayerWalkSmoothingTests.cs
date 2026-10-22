using System.Reflection;
using NightPatrol.Player;
using NUnit.Framework;
using UnityEngine;

namespace NightPatrol.Tests
{
    // 01_09: 걷기 가속·감속 (PlayerMovement.StepWalkVelocity, PlayerSettings Acceleration Time·Deceleration Time)
    public class PlayerWalkSmoothingTests
    {
        private const float Speed = 2f;
        private const float Accel = 0.15f;
        private const float Decel = 0.1f;
        private const float Dt = 0.01f;

        private static Vector2 Run(Vector2 current, Vector2 target, int frames, float dt = Dt)
        {
            for (int i = 0; i < frames; i++)
                current = PlayerMovement.StepWalkVelocity(current, target, Speed, Accel, Decel, dt);
            return current;
        }

        [Test]
        public void FromRest_ReachesWalkSpeed_AfterAccelerationTime_Gradually()
        {
            var forward = new Vector2(0f, Speed);

            Vector2 half = Run(Vector2.zero, forward, 7);   // 0.07초
            Assert.That(half.y, Is.GreaterThan(0.5f).And.LessThan(1.5f), "중간에는 아직 다 빠르지 않음");

            Vector2 full = Run(Vector2.zero, forward, 15);  // 0.15초
            Assert.That(Vector2.Distance(full, forward), Is.LessThan(0.001f));
        }

        [Test]
        public void Stopping_TakesDecelerationTime()
        {
            var forward = new Vector2(0f, Speed);

            Vector2 half = Run(forward, Vector2.zero, 5);   // 0.05초
            Assert.That(half.y, Is.GreaterThan(0.5f).And.LessThan(1.5f));

            Vector2 stopped = Run(forward, Vector2.zero, 10);   // 0.1초
            Assert.That(stopped.magnitude, Is.LessThan(0.001f));
        }

        [Test]
        public void ZeroTimes_AreInstant_LikeBefore()
        {
            var forward = new Vector2(0f, Speed);
            Assert.AreEqual(forward, PlayerMovement.StepWalkVelocity(Vector2.zero, forward, Speed, 0f, 0f, Dt));
            Assert.AreEqual(Vector2.zero, PlayerMovement.StepWalkVelocity(forward, Vector2.zero, Speed, 0f, 0f, Dt));
        }

        [Test]
        public void LongFrame_DoesNotOvershoot()
        {
            var forward = new Vector2(0f, Speed);
            Assert.AreEqual(forward, PlayerMovement.StepWalkVelocity(Vector2.zero, forward, Speed, Accel, Decel, 1f));
            Assert.AreEqual(Vector2.zero, PlayerMovement.StepWalkVelocity(forward, Vector2.zero, Speed, Accel, Decel, 1f));
        }

        [Test]
        public void PausedFrame_KeepsVelocity()
        {
            var v = new Vector2(0.3f, 1.2f);
            Assert.AreEqual(v, PlayerMovement.StepWalkVelocity(v, Vector2.zero, Speed, Accel, Decel, 0f));
        }

        [Test]
        public void TurningAround_PassesThroughStop_WithAccelerationTime()
        {
            var forward = new Vector2(0f, Speed);
            var back = -forward;

            Vector2 middle = Run(forward, back, 15);   // 0.15초: 앞 2 → 0
            Assert.That(middle.magnitude, Is.LessThan(0.001f));

            Vector2 end = Run(forward, back, 30);      // 0.3초: 뒤로 2
            Assert.That(Vector2.Distance(end, back), Is.LessThan(0.001f));
        }

        [Test]
        public void Diagonal_NeverGoesFasterThanWalkSpeed()
        {
            Vector2 target = new Vector2(1f, 1f).normalized * Speed;
            Vector2 v = Vector2.zero;
            for (int i = 0; i < 40; i++)
            {
                v = PlayerMovement.StepWalkVelocity(v, target, Speed, Accel, Decel, Dt);
                Assert.That(v.magnitude, Is.LessThanOrEqualTo(Speed + 0.0001f));
            }
            Assert.That(Vector2.Distance(v, target), Is.LessThan(0.001f));
        }

        [Test]
        public void ZeroWalkSpeed_JustReturnsTarget()
        {
            Assert.AreEqual(Vector2.zero, PlayerMovement.StepWalkVelocity(new Vector2(1f, 0f), Vector2.zero, 0f, Accel, Decel, Dt));
        }

        [Test]
        public void Settings_Defaults_AndNegativeValuesClampToZero()
        {
            var settings = ScriptableObject.CreateInstance<PlayerSettings>();
            try
            {
                Assert.AreEqual(0.15f, settings.AccelerationTime, 0.0001f);
                Assert.AreEqual(0.1f, settings.DecelerationTime, 0.0001f);

                SetField(settings, "accelerationTime", -1f);
                SetField(settings, "decelerationTime", -1f);
                Assert.AreEqual(0f, settings.AccelerationTime);
                Assert.AreEqual(0f, settings.DecelerationTime);
            }
            finally
            {
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void Teleport_ClearsWalkVelocity()
        {
            var player = new GameObject("Player");
            try
            {
                var movement = player.AddComponent<PlayerMovement>();
                SetField(movement, "walkVelocity", new Vector2(0f, Speed));

                movement.Teleport(Vector3.zero, Quaternion.identity);

                Assert.AreEqual(Vector2.zero, (Vector2)GetField(movement, "walkVelocity"), "실패 후 시작 자리에서 미끄러지지 않게");
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        private static void SetField(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        private static object GetField(object target, string name) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    }
}
