using System.Collections.Generic;
using NightPatrol.Anomalies;
using NightPatrol.Photo;
using NUnit.Framework;
using UnityEngine;

namespace NightPatrol.Tests
{
    // 06_04 촬영 판정: 조준 박스·크기에 비례한 거리·5점 가림·면 대상(벽 뒤 막기)·켜진 이상현상만·
    // 찍을 곳(PhotoTarget → IPhotoVisual이 알려 준 바깥 물체 → 자기 아래 렌더러)
    // 눈은 (0, 1.6, 0)에서 +Z를 본다 (세로 시야 60°, 4:3 — 카메라 앱 뷰파인더와 같은 비율)
    public class PhotoJudgeTests
    {
        private class FakeAnomaly : Anomaly
        {
            public override void Activate() => IsActive = true;
            public override void Deactivate() => IsActive = false;
        }

        // 맵의 물체를 바꾸는 이상현상 (모니터·등·벨처럼): 보이는 물체가 자기 아래에 없어서 알려 준다
        private class OutsideAnomaly : Anomaly, IPhotoVisual
        {
            public GameObject visual;
            public override void Activate() => IsActive = true;
            public override void Deactivate() => IsActive = false;
            public GameObject PhotoVisual => IsActive ? visual : null;
        }

        private static readonly Vector3 EyePosition = new Vector3(0f, 1.6f, 0f);
        private readonly List<GameObject> created = new List<GameObject>();
        private Camera eye;
        private PhotoRules rules;

        [SetUp]
        public void SetUp()
        {
            eye = New("Eye").AddComponent<Camera>();
            eye.transform.position = EyePosition;
            eye.fieldOfView = 60f;
            eye.aspect = 4f / 3f;
            eye.enabled = false;
            rules = new PhotoRules();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in created)
                if (go != null) Object.DestroyImmediate(go);
            created.Clear();
        }

        private GameObject New(string name)
        {
            var go = new GameObject(name);
            created.Add(go);
            return go;
        }

        // 켜진 이상현상 + 보이는 물체(자식 큐브, 충돌체 포함)
        private FakeAnomaly AnomalyWithCube(Vector3 position, float scale = 1f, bool active = true)
        {
            var anomaly = New("Anomaly").AddComponent<FakeAnomaly>();
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Visual";
            cube.transform.SetParent(anomaly.transform, false);
            cube.transform.position = position;
            cube.transform.localScale = Vector3.one * scale;
            if (active) anomaly.Activate();
            return anomaly;
        }

        private GameObject Block(Vector3 position, Vector3 size)
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            created.Add(block);
            block.name = "Block";
            block.transform.position = position;
            block.transform.localScale = size;
            return block;
        }

        private List<Anomaly> Capture(List<Anomaly> missing = null)
        {
            Physics.SyncTransforms();
            return PhotoJudge.Capture(eye, rules, PhotoSubjects.Active(missing));
        }

        [Test]
        public void Centered_InFront_IsCaptured()
        {
            var anomaly = AnomalyWithCube(new Vector3(0f, 1.6f, 4f));
            CollectionAssert.AreEqual(new Anomaly[] { anomaly }, Capture());
        }

        [Test]
        public void OutsideTheAimBox_IsNot()
        {
            AnomalyWithCube(new Vector3(2.5f, 1.6f, 4f));   // 뷰포트 x ≈ 0.9
            Assert.IsEmpty(Capture());
            var near = AnomalyWithCube(new Vector3(0.5f, 1.6f, 4f));   // 뷰포트 x ≈ 0.58
            CollectionAssert.AreEqual(new Anomaly[] { near }, Capture());
        }

        [Test]
        public void BehindTheCamera_IsNot()
        {
            AnomalyWithCube(new Vector3(0f, 1.6f, -4f));
            Assert.IsEmpty(Capture());
        }

        [Test]
        public void SmallThings_NeedToBeCloser()
        {
            var small = AnomalyWithCube(new Vector3(0f, 1.6f, 4f), 0.2f);   // 대각선 0.35m → 인정 거리 2m(최소)
            Assert.IsEmpty(Capture(), "작은 것은 4m에서 안 찍힘");
            small.transform.GetChild(0).position = new Vector3(0f, 1.6f, 1.8f);
            CollectionAssert.AreEqual(new Anomaly[] { small }, Capture(), "2m 안이면 찍힘");
        }

        [Test]
        public void AWall_Blocks()
        {
            AnomalyWithCube(new Vector3(0f, 1.6f, 4f));
            Block(new Vector3(0f, 1.6f, 2f), new Vector3(6f, 4f, 0.2f));
            Assert.IsEmpty(Capture());
        }

        [Test]
        public void ASmallObstacle_OnOnePoint_DoesNotBlock()
        {
            var anomaly = AnomalyWithCube(new Vector3(0f, 1.6f, 4f));
            // 오른쪽 점(가운데에서 크기의 1/3 = 0.58m)으로 가는 선 위, 가운데 선과는 0.29m 떨어진 작은 상자
            Block(new Vector3(0.289f, 1.6f, 2f), Vector3.one * 0.15f);
            CollectionAssert.AreEqual(new Anomaly[] { anomaly }, Capture());
        }

        [Test]
        public void InactiveAnomaly_IsNotRequired()
        {
            var anomaly = AnomalyWithCube(new Vector3(0f, 1.6f, 4f), active: false);
            Assert.IsEmpty(PhotoSubjects.Active());
            anomaly.Activate();
            Assert.AreEqual(1, PhotoSubjects.Active().Count);
        }

        [Test]
        public void AnomalyWithNothingToPhotograph_IsLeftOut()
        {
            var bare = New("Sound only").AddComponent<FakeAnomaly>();
            bare.Activate();
            var missing = new List<Anomaly>();
            Assert.IsEmpty(PhotoSubjects.Active(missing), "찍을 곳이 없으면 요구하지 않음 (통과할 수 없게 만들지 않게)");
            CollectionAssert.AreEqual(new Anomaly[] { bare }, missing);
        }

        // 맵의 모니터처럼 이상현상 오브젝트 밖에 있는 물체: 이상현상이 IPhotoVisual로 알려 주면 그걸 찍는다
        private OutsideAnomaly AnomalyShowing(GameObject visual)
        {
            var anomaly = New("Red monitor").AddComponent<OutsideAnomaly>();
            anomaly.transform.position = new Vector3(30f, 0f, 30f);   // 이상현상 오브젝트 자체는 멀리 (렌더러 없음)
            anomaly.visual = visual;
            anomaly.Activate();
            return anomaly;
        }

        private GameObject MapCube(Vector3 position)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            created.Add(cube);
            cube.name = "Map monitor";
            cube.transform.position = position;
            return cube;
        }

        [Test]
        public void OutsideVisual_IsWhatGetsPhotographed()
        {
            var anomaly = AnomalyShowing(MapCube(new Vector3(0f, 1.6f, 4f)));
            CollectionAssert.AreEqual(new Anomaly[] { anomaly }, Capture());
        }

        [Test]
        public void OutsideVisual_ComesBeforeOwnRenderers()
        {
            var anomaly = AnomalyShowing(MapCube(new Vector3(0f, 1.6f, 4f)));
            var own = GameObject.CreatePrimitive(PrimitiveType.Cube);   // 자기 아래 렌더러는 카메라 뒤
            own.transform.SetParent(anomaly.transform, false);
            own.transform.position = new Vector3(0f, 1.6f, -4f);
            CollectionAssert.AreEqual(new Anomaly[] { anomaly }, Capture(), "알려 준 바깥 물체를 씀");
        }

        [Test]
        public void OutsideVisual_NullOrDestroyed_IsLeftOut()
        {
            var cube = MapCube(new Vector3(0f, 1.6f, 4f));
            var anomaly = AnomalyShowing(cube);
            Object.DestroyImmediate(cube);
            var missing = new List<Anomaly>();
            Assert.IsEmpty(PhotoSubjects.Active(missing), "파괴된 물체");
            CollectionAssert.AreEqual(new Anomaly[] { anomaly }, missing);

            anomaly.visual = null;
            missing.Clear();
            Assert.IsEmpty(PhotoSubjects.Active(missing), "모름(null)");
            CollectionAssert.AreEqual(new Anomaly[] { anomaly }, missing);
        }

        [Test]
        public void OutsideVisual_WithoutRenderers_IsLeftOut()
        {
            var lightOnly = New("Light only");   // 빛만 있고 렌더러가 없는 물체 — 기구(케이스·형광등)를 알려 줘야 함
            lightOnly.AddComponent<Light>();
            var anomaly = AnomalyShowing(lightOnly);
            var missing = new List<Anomaly>();
            Assert.IsEmpty(PhotoSubjects.Active(missing));
            CollectionAssert.AreEqual(new Anomaly[] { anomaly }, missing);
        }

        [Test]
        public void SeveralPhotoTargets_AnyOneCounts()
        {
            var anomaly = New("Bells").AddComponent<FakeAnomaly>();
            var hidden = New("Bell behind");
            hidden.transform.position = new Vector3(0f, 1.6f, -3f);   // 카메라 뒤
            hidden.AddComponent<PhotoTarget>().Setup(anomaly);
            var visible = New("Bell in front");
            visible.transform.position = new Vector3(0f, 1.6f, 3f);
            visible.AddComponent<PhotoTarget>().Setup(anomaly);
            anomaly.Activate();

            CollectionAssert.AreEqual(new Anomaly[] { anomaly }, Capture());
        }

        // 벽(두께 0.2m, z 3.9~4.1)의 앞면 얼룩: 방 쪽(앞)에서는 찍히고, 앞을 반대로 두면(벽 너머) 안 찍힘
        [Test]
        public void SurfaceTarget_OnlyFromItsFrontSide()
        {
            Block(new Vector3(0f, 1.6f, 4f), new Vector3(6f, 4f, 0.2f));
            var anomaly = New("Stain").AddComponent<FakeAnomaly>();
            var stain = New("Stain spot");
            stain.transform.SetPositionAndRotation(new Vector3(0f, 1.6f, 3.9f), Quaternion.LookRotation(Vector3.back));
            stain.AddComponent<PhotoTarget>().Setup(anomaly, onSurface: true);   // 크기 1m → 6m까지
            anomaly.Activate();
            CollectionAssert.AreEqual(new Anomaly[] { anomaly }, Capture(), "앞면을 앞에서");

            stain.transform.rotation = Quaternion.LookRotation(Vector3.forward);
            Assert.IsEmpty(Capture(), "보이는 쪽이 반대면 안 찍힘");
        }

        // 얇은 벽(2cm) 너머의 얼룩은 선 끝을 잘라 내는 방식이면 찍혀 버린다 — 앞쪽 확인으로 막는다
        [Test]
        public void SurfaceTarget_BehindAThinWall_IsNot()
        {
            Block(new Vector3(0f, 1.6f, 4f), new Vector3(6f, 4f, 0.02f));
            var anomaly = New("Stain").AddComponent<FakeAnomaly>();
            var stain = New("Stain spot");
            stain.transform.SetPositionAndRotation(new Vector3(0f, 1.6f, 4.01f), Quaternion.LookRotation(Vector3.forward));
            stain.AddComponent<PhotoTarget>().Setup(anomaly, onSurface: true);
            anomaly.Activate();
            Assert.IsEmpty(Capture());
        }
    }
}
