using System.Reflection;
using NightPatrol.Interaction;
using NightPatrol.Player;
using NUnit.Framework;
using UnityEngine;

namespace NightPatrol.Tests
{
    // Edit Mode: Awake does not run, so Teleport has to find its components itself.
    public class PlayerControlTests
    {
        private GameObject player;

        [SetUp]
        public void SetUp() => player = new GameObject("Player");

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(player);

        // ── Blocking (same rule on interactor, movement and look) ──
        [Test]
        public void Interactor_StaysBlocked_UntilEveryOwnerReleases()
        {
            var interactor = player.AddComponent<PlayerInteractor>();
            object journal = new object(), cutscene = new object();

            interactor.SetBlocked(journal, true);
            interactor.SetBlocked(cutscene, true);
            interactor.SetBlocked(journal, false);
            Assert.IsTrue(interactor.IsBlocked);

            interactor.SetBlocked(cutscene, false);
            Assert.IsFalse(interactor.IsBlocked);
        }

        [Test]
        public void SameOwnerTwice_CountsOnce_AndNullOwnerIsIgnored()
        {
            var interactor = player.AddComponent<PlayerInteractor>();
            var owner = new object();
            interactor.SetBlocked(owner, true);
            interactor.SetBlocked(owner, true);
            interactor.SetBlocked(owner, false);
            Assert.IsFalse(interactor.IsBlocked);

            interactor.SetBlocked(null, true);
            Assert.IsFalse(interactor.IsBlocked);
        }

        [Test]
        public void MovementAndLook_FollowTheSameRule()
        {
            var movement = player.AddComponent<PlayerMovement>();
            var look = player.AddComponent<PlayerLook>();
            var owner = new object();

            movement.SetBlocked(owner, true);
            look.SetBlocked(owner, true);
            Assert.IsTrue(movement.IsBlocked);
            Assert.IsTrue(look.IsBlocked);

            movement.SetBlocked(owner, false);
            look.SetBlocked(owner, false);
            Assert.IsFalse(movement.IsBlocked);
            Assert.IsFalse(look.IsBlocked);
        }

        // ── Teleport ──
        [Test]
        public void Teleport_Moves_KeepsOnlyYaw_AndResetsPitch()
        {
            var movement = player.AddComponent<PlayerMovement>();
            var look = player.AddComponent<PlayerLook>();
            var cam = new GameObject("Camera").transform;
            cam.SetParent(player.transform);
            cam.localRotation = Quaternion.Euler(30f, 0f, 0f);
            typeof(PlayerLook).GetField("cameraTransform", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(look, cam);

            movement.Teleport(new Vector3(3f, 1f, -2f), Quaternion.Euler(20f, 90f, 10f));

            Assert.That(Vector3.Distance(player.transform.position, new Vector3(3f, 1f, -2f)), Is.LessThan(0.001f));
            Assert.That(Quaternion.Angle(player.transform.rotation, Quaternion.Euler(0f, 90f, 0f)), Is.LessThan(0.01f));
            Assert.That(Quaternion.Angle(cam.localRotation, Quaternion.identity), Is.LessThan(0.01f));
            Assert.IsTrue(player.GetComponent<CharacterController>().enabled);
        }

        [Test]
        public void Teleport_WithoutLook_DoesNotThrow()
        {
            var movement = player.AddComponent<PlayerMovement>();
            Assert.DoesNotThrow(() => movement.Teleport(Vector3.one, Quaternion.identity));
            Assert.That(Vector3.Distance(player.transform.position, Vector3.one), Is.LessThan(0.001f));
        }

        [Test]
        public void ResetPitch_WithoutCamera_DoesNotThrow()
        {
            var look = player.AddComponent<PlayerLook>();
            Assert.DoesNotThrow(look.ResetPitch);
        }
    }
}
