using System.Collections.Generic;
using NightPatrol.Interaction;
using NUnit.Framework;
using UnityEngine;

namespace NightPatrol.Tests
{
    // 조준 보정: the thin ray first, then a 5 cm sphere. The aim runs from the origin along +Z.
    public class AimAssistTests
    {
        private class Switch : MonoBehaviour, IInteractable
        {
            public string Prompt => "E - 스위치";
            public void Interact(PlayerInteractor who) { }
        }

        private static readonly Ray Aim = new Ray(Vector3.zero, Vector3.forward);
        private readonly List<GameObject> created = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in created)
                if (go != null) Object.DestroyImmediate(go);
            created.Clear();
        }

        private GameObject Box(Vector3 position, Vector3 size, bool interactable)
        {
            var go = new GameObject(interactable ? "Switch" : "Wall");
            go.transform.position = position;
            go.transform.localScale = size;
            go.AddComponent<BoxCollider>();
            if (interactable) go.AddComponent<Switch>();
            created.Add(go);
            Physics.SyncTransforms();
            return go;
        }

        private static IInteractable Find(float assist) =>
            PlayerInteractor.FindTarget(Aim, 2f, assist, Physics.DefaultRaycastLayers);

        [Test]
        public void DirectHit_FindsTheTarget()
        {
            var target = Box(new Vector3(0f, 0f, 1f), Vector3.one * 0.04f, true).GetComponent<Switch>();
            Assert.AreSame(target, Find(0f));
        }

        [Test]
        public void NearMiss_IsCaughtOnlyByTheAssist()
        {
            // 4 cm switch whose near edge is 1.5 cm beside the aim line
            var target = Box(new Vector3(0.035f, 0f, 1f), Vector3.one * 0.04f, true).GetComponent<Switch>();
            Assert.IsNull(Find(0f));
            Assert.AreSame(target, Find(0.05f));
        }

        [Test]
        public void WallInFront_BlocksTheAssist()
        {
            Box(new Vector3(0f, 0f, 0.5f), new Vector3(2f, 2f, 0.1f), false);
            Box(new Vector3(0.035f, 0f, 1f), Vector3.one * 0.04f, true);
            Assert.IsNull(Find(0.05f));
        }

        [Test]
        public void BeyondInteractionDistance_IsIgnored()
        {
            Box(new Vector3(0f, 0f, 3f), Vector3.one * 0.04f, true);
            Assert.IsNull(Find(0.05f));
        }

        [Test]
        public void DisabledTarget_IsNotOffered()
        {
            Box(new Vector3(0f, 0f, 1f), Vector3.one * 0.04f, true).GetComponent<Switch>().enabled = false;
            Assert.IsNull(Find(0.05f));
        }
    }
}
