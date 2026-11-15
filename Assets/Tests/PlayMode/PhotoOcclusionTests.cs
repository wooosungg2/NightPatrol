using System.Collections.Generic;
using NightPatrol.Photo;
using NUnit.Framework;
using UnityEngine;

namespace NightPatrol.Tests
{
    // 06_04n: generated glass must pass photo rays without changing movement collisions.
    public class PhotoOcclusionTests
    {
        private readonly List<GameObject> objects = new List<GameObject>();
        private readonly List<Material> materials = new List<Material>();
        private static readonly Vector3 Origin = new Vector3(3000f, 100f, 3000f);
        private Transform floor;
        private Camera eye;
        private PhotoShape subject;
        private Material glass;
        private PhotoRules rules;

        [SetUp]
        public void SetUp()
        {
            floor = New("GeneratedFloor").transform;
            floor.position = Origin;
            floor.gameObject.AddComponent<FloorGenerator>().generateOnStart = false;
            glass = MakeGlass();
            eye = New("Eye").AddComponent<Camera>();
            eye.enabled = false;
            eye.transform.position = Origin;
            eye.fieldOfView = 60f;
            eye.aspect = 4f / 3f;
            var target = New("Subject").transform;
            target.position = Origin + Vector3.forward * 4f;
            subject = new PhotoShape(target.position, 1f, target);
            rules = new PhotoRules();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in objects)
                if (go != null) Object.DestroyImmediate(go);
            objects.Clear();
            foreach (var material in materials)
                if (material != null) Object.DestroyImmediate(material);
            materials.Clear();
            Physics.SyncTransforms();
        }

        private GameObject New(string name, Transform parent = null)
        {
            var go = new GameObject(name);
            objects.Add(go);
            if (parent != null) go.transform.SetParent(parent, false);
            return go;
        }

        private Material MakeGlass()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            Assert.IsNotNull(shader);
            var material = new Material(shader);
            materials.Add(material);
            material.SetFloat("_Surface", 1f);
            material.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0.2f));
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.renderQueue = 3000;
            return material;
        }

        private BoxCollider Box(string name, Transform parent, float z = 2f)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            objects.Add(go);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = Origin + Vector3.forward * z;
            go.transform.localScale = new Vector3(4f, 4f, 0.05f);
            go.GetComponent<Renderer>().sharedMaterial = glass;
            return go.GetComponent<BoxCollider>();
        }

        private BoxCollider Pane(string kind, float z = 2f)
        {
            if (kind == "corridor")
                return Box("Glass", New("Window_Hall_N", floor).transform, z);
            if (kind == "stair")
            {
                var room = New("Stair-E", floor).transform;
                var stairwell = New("Stairwell", room).transform;
                return Box("Glass", New("Window", stairwell).transform, z);
            }
            if (kind.StartsWith("door") || kind == "frosted")
            {
                var door = New("Door", floor).AddComponent<Door>();
                door.style = kind == "frosted" ? DoorStyle.GlassDouble
                    : kind == "door-transom" ? DoorStyle.WoodSingle : DoorStyle.GlassClear;
                if (kind.EndsWith("transom")) return Box("Transom", door.transform, z);
                var leaf = New("Leaf_L", door.transform).transform;
                door.leaves = new[] { leaf };
                return Box("Glass", leaf, z);
            }

            var unit = New("Window_0", floor).AddComponent<WindowUnit>();
            if (kind == "upper-left") return Box("Glass_Upper_L", unit.transform, z);
            if (kind == "upper-right") return Box("Glass_Upper_R", unit.transform, z);
            var sash = New(kind == "fixed" ? "Sash_Fixed" : "Sash_Sliding", unit.transform).transform;
            if (kind != "fixed") unit.sash = sash;
            return Box("Glass", sash, z);
        }

        private bool Captured()
        {
            Physics.SyncTransforms();
            return PhotoJudge.Sees(eye, subject, rules);
        }

        [TestCase("upper-left")]
        [TestCase("upper-right")]
        [TestCase("fixed")]
        [TestCase("sliding")]
        [TestCase("corridor")]
        [TestCase("stair")]
        [TestCase("door-leaf")]
        [TestCase("door-transom")]
        [TestCase("door-clear-transom")]
        public void GeneratedTransparentPane_CanPhotographSubject_WithoutMarker(string kind)
        {
            var pane = Pane(kind);
            Assert.IsNull(pane.GetComponent<PhotoPassThrough>());
            Assert.IsTrue(Captured(), kind);
            Assert.IsTrue(pane.enabled);
            Assert.IsFalse(pane.isTrigger);
            Assert.AreEqual(0, pane.gameObject.layer);
        }

        [TestCase("door-leaf")]
        [TestCase("door-clear-transom")]
        [TestCase("corridor")]
        public void GeneratedInvisiblePane_CanPhotographSubject(string kind)
        {
            var pane = Pane(kind);
            pane.GetComponent<Renderer>().enabled = false;
            Assert.IsTrue(Captured());
        }

        [Test]
        public void TwoGlassPanes_StillAllowThePhoto()
        {
            Pane("corridor", 1f);
            Pane("door-leaf", 2f);
            Assert.IsTrue(Captured());
        }

        [TestCase(0.5f)]
        [TestCase(1.5f)]
        [TestCase(3f)]
        public void WallBeforeBetweenOrAfterGlass_Blocks(float z)
        {
            Pane("corridor", 1f);
            Pane("door-leaf", 2f);
            Box("Wall", floor, z);
            Assert.IsFalse(Captured());
        }

        [Test]
        public void WallBeyondTheSubject_DoesNotBlock()
        {
            Pane("corridor");
            Box("Wall", floor, 5f);
            Assert.IsTrue(Captured());
        }

        [Test]
        public void TriggerBetweenGlassAndSubject_DoesNotBlock()
        {
            Pane("corridor");
            Box("Trigger", floor, 3f).isTrigger = true;
            Assert.IsTrue(Captured());
        }

        [TestCase("Mirror_Back")]
        [TestCase("Frame_L")]
        [TestCase("Handle")]
        [TestCase("Cloth")]
        [TestCase("Transom")]
        public void TransparentMaterialAlone_DoesNotMakeAnObstaclePassable(string name)
        {
            var pane = Pane("corridor", 1f);
            Box(name, pane.transform.parent, 2f);
            Assert.IsFalse(Captured());
        }

        [Test]
        public void UnrecognizedGlassName_AndDisabledRenderer_StillBlock()
        {
            var pane = Box("Glass", floor);
            pane.GetComponent<Renderer>().enabled = false;
            Assert.IsFalse(Captured());
        }

        [Test]
        public void FrostedDoor_RemainsBlocked_EvenWithTransparentMaterial()
        {
            Pane("frosted");
            Assert.IsFalse(Captured());
        }

        [Test]
        public void OpaqueMaterial_WithLowAlpha_Blocks()
        {
            Pane("corridor");
            glass.SetFloat("_Surface", 0f);
            Assert.IsFalse(Captured());
        }

        [Test]
        public void MaterialChanges_AreObservedByTheNextPhoto()
        {
            var pane = Pane("corridor");
            Assert.IsTrue(Captured());
            var replacement = MakeGlass();
            replacement.SetColor("_BaseColor", Color.white);
            pane.GetComponent<Renderer>().sharedMaterial = replacement;
            Assert.IsFalse(Captured());
            pane.GetComponent<Renderer>().sharedMaterial = glass;
            Assert.IsTrue(Captured());
        }

        [Test]
        public void RendererAlphaOverride_Blocks_AndClearingItRestoresThePhoto()
        {
            var renderer = Pane("corridor").GetComponent<Renderer>();
            var properties = new MaterialPropertyBlock();
            properties.SetColor("_BaseColor", Color.white);
            renderer.SetPropertyBlock(properties);
            Assert.IsFalse(Captured());
            renderer.SetPropertyBlock(null);
            Assert.IsTrue(Captured());
        }

        [Test]
        public void MaterialSlotOverride_TakesPriorityOverRendererOverride()
        {
            var renderer = Pane("corridor").GetComponent<Renderer>();
            var properties = new MaterialPropertyBlock();
            properties.SetColor("_BaseColor", Color.white);
            renderer.SetPropertyBlock(properties);
            properties.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0.2f));
            renderer.SetPropertyBlock(properties, 0);
            Assert.IsTrue(Captured());
            renderer.SetPropertyBlock(null, 0);
            Assert.IsFalse(Captured());
        }

        [TestCase(true, false)]
        [TestCase(false, true)]
        public void ExplicitOff_OverridesAutomaticGlass(bool componentEnabled, bool allowPhotos)
        {
            var marker = Pane("corridor").gameObject.AddComponent<PhotoPassThrough>();
            marker.SetAllowed(allowPhotos);
            marker.enabled = componentEnabled;
            Assert.IsFalse(Captured());
        }

        [Test]
        public void RemovingExplicitOff_RestoresAutomaticGlass()
        {
            var marker = Pane("corridor").gameObject.AddComponent<PhotoPassThrough>();
            marker.SetAllowed(false);
            Assert.IsFalse(Captured());
            Object.DestroyImmediate(marker);
            Assert.IsTrue(Captured());
        }

        [Test]
        public void ExplicitOn_AllowsCustomGlass()
        {
            Box("CustomPane", floor).gameObject.AddComponent<PhotoPassThrough>();
            Assert.IsTrue(Captured());
        }

        [Test]
        public void ParentMarker_DoesNotPassItsChildWall()
        {
            var root = New("CustomAssembly", floor);
            root.AddComponent<PhotoPassThrough>();
            Box("Wall", root.transform);
            Assert.IsFalse(Captured());
        }

        [Test]
        public void RecreatedGlass_IsRecognizedWithoutRegistration()
        {
            var pane = Pane("corridor");
            Assert.IsTrue(Captured());
            Object.DestroyImmediate(pane.transform.parent.gameObject);
            Pane("corridor");
            Assert.IsTrue(Captured());
        }
    }
}
