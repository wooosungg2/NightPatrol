using System.Collections.Generic;
using NightPatrol.Anomalies;
using NightPatrol.Photo;
using NUnit.Framework;
using UnityEngine;

namespace NightPatrol.Tests
{
    // 06_04 촬영 판정: 조준 박스·크기에 비례한 거리·5점 가림·면 대상(벽 뒤 막기)·켜진 이상현상만·
    // 찍을 곳(PhotoTarget → 자기 아래 렌더러 → 이상현상이 바꾼 바깥 물체를 시스템이 찾음)
    // 눈은 (0, 1.6, 0)에서 +Z를 본다 (세로 시야 60°, 4:3 — 카메라 앱 뷰파인더와 같은 비율)
    public class PhotoJudgeTests
    {
        private class FakeAnomaly : Anomaly
        {
            public override void Activate() => IsActive = true;
            public override void Deactivate() => IsActive = false;
        }

        // 맵의 물체를 바꾸는 이상현상 (모니터·벨·의자·등·얼룩처럼): 바꾼 물체를 되돌리려고 칸에 들고 있다.
        // 따로 알려 주지 않아도 시스템이 그 칸의 물체 중 켜지기 전과 달라진 것을 찍을 곳으로 삼는다
        private class ChangingAnomaly : Anomaly
        {
            public GameObject[] holds = new GameObject[0];   // 들고 있는 물체 (바꾸는 것 + 가리키기만 하는 것)
            public System.Action<ChangingAnomaly> change;    // 켤 때 하는 일
            private GameObject made;                          // 켤 때 새로 만든 물체 (private 칸도 읽는다)
            public GameObject Made => made;

            public override void Activate()
            {
                IsActive = true;
                change?.Invoke(this);
            }

            public override void Deactivate() => IsActive = false;

            public void Make(Vector3 position)
            {
                made = GameObject.CreatePrimitive(PrimitiveType.Quad);
                made.name = "Stain";
                made.transform.position = position;
            }
        }

        private static readonly Vector3 EyePosition = new Vector3(0f, 1.6f, 0f);
        private readonly List<GameObject> created = new List<GameObject>();
        private readonly List<Material> paints = new List<Material>();
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
            foreach (var paint in paints)
                if (paint != null) Object.DestroyImmediate(paint);
            paints.Clear();
            PhotoSubjects.Forget();   // 적어 둔 모습이 다음 테스트로 넘어가지 않게
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

        // ── 이상현상이 바꾼 바깥 물체를 시스템이 찾는다 ──

        private static readonly Vector3 Front = new Vector3(0f, 1.6f, 4f), Back = new Vector3(0f, 1.6f, -4f);

        private GameObject MapCube(string name, Vector3 position, Transform parent = null)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            if (parent == null) created.Add(cube);
            else cube.transform.SetParent(parent, false);
            cube.name = name;
            cube.transform.position = position;
            return cube;
        }

        private ChangingAnomaly Changing(string name, params GameObject[] holds)
        {
            var anomaly = New(name).AddComponent<ChangingAnomaly>();
            anomaly.transform.position = new Vector3(30f, 0f, 30f);   // 이상현상 오브젝트 자체는 멀리 (렌더러 없음)
            anomaly.holds = holds;
            return anomaly;
        }

        // 재질을 바꿔 끼운다 (빨간 화면·파란 벨)
        private void Repaint(GameObject target)
        {
            var renderer = target.GetComponent<Renderer>();
            var paint = new Material(renderer.sharedMaterial);
            paints.Add(paint);
            renderer.sharedMaterial = paint;
        }

        // 프로퍼티 블록으로 색만 바꾼다 (조명 구역이 등 하나의 색을 바꾸는 방식)
        private static void Tint(GameObject target)
        {
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", Color.red);
            target.GetComponent<Renderer>().SetPropertyBlock(block);
        }

        // 층 시작 흉내: 켜지기 전 모습을 적고 → 이상현상을 켜고 → 달라진 물체를 찾는다 (PatrolRun이 하는 순서)
        private void StartFloor(params Anomaly[] anomalies)
        {
            PhotoSubjects.BeginFloor();
            foreach (var anomaly in anomalies) anomaly.Activate();
            PhotoSubjects.FindChanged();
        }

        private void LookBack() => eye.transform.rotation = Quaternion.LookRotation(Vector3.back);

        [Test]
        public void ChangedOutsideObject_IsWhatGetsPhotographed_NotTheOnesItOnlyHolds()
        {
            var door = MapCube("Door", Front);          // 가리키기만 하는 물체 (점검 대상 문 같은)
            var monitor = MapCube("Monitor", Back);     // 바꾸는 물체
            var anomaly = Changing("Red monitor", door, monitor);
            anomaly.change = _ => Repaint(monitor);
            StartFloor(anomaly);

            Assert.IsEmpty(Capture(), "들고만 있는 문은 찍어도 안 됨");
            LookBack();
            CollectionAssert.AreEqual(new Anomaly[] { anomaly }, Capture(), "바뀐 모니터를 찍어야 함");
            Assert.AreEqual("Monitor", PhotoSubjects.Active()[0].Where);
        }

        [Test]
        public void ObjectMadeWhenTurnedOn_IsFound()
        {
            var anomaly = Changing("Wall stain");
            anomaly.change = a => a.Make(new Vector3(0f, 1.6f, 3f));
            StartFloor(anomaly);
            created.Add(anomaly.Made);
            CollectionAssert.AreEqual(new Anomaly[] { anomaly }, Capture());
        }

        [Test]
        public void MovedObject_IsFoundWhereItIsNow()
        {
            var chair = MapCube("Chair", Back);
            var anomaly = Changing("Ceiling chair", chair);
            anomaly.change = _ => chair.transform.position = Front;
            StartFloor(anomaly);
            CollectionAssert.AreEqual(new Anomaly[] { anomaly }, Capture());
        }

        // 여러 부분으로 된 물체가 통째로 달라졌으면(옮긴 의자) 부분마다가 아니라 한 덩어리로 찍을 곳이 된다
        [Test]
        public void WholeObjectChanged_IsOneShape()
        {
            var chair = New("Chair");
            chair.transform.position = Back;
            MapCube("Seat", Back, chair.transform);
            MapCube("Back", Back + Vector3.up * 0.6f, chair.transform);
            var anomaly = Changing("Ceiling chair", chair);
            anomaly.change = _ => chair.transform.position = Front;
            StartFloor(anomaly);

            var subject = PhotoSubjects.Active()[0];
            Assert.AreEqual(1, subject.Shapes.Count);
            Assert.AreEqual("Chair", subject.Where);
            CollectionAssert.AreEqual(new Anomaly[] { anomaly }, Capture());
        }

        // 맵 물체를 카메라로만 보이게(CameraOnly 레이어로) 바꾼 이상현상: 레이어가 바뀐 것도 달라진 것으로 찾는다
        [Test]
        public void ObjectMadeCameraOnly_IsFound()
        {
            Assert.GreaterOrEqual(CameraOnlyLayer.Index, 0, "CameraOnly 레이어 없음");
            var statue = MapCube("Statue", Front);
            var anomaly = Changing("Ghost statue", statue);
            anomaly.change = _ => statue.layer = CameraOnlyLayer.Index;
            StartFloor(anomaly);
            CollectionAssert.AreEqual(new Anomaly[] { anomaly }, Capture());
        }

        // 조명 구역처럼 여러 부분으로 된 물체를 들고 있어도, 달라진 부분(등 하나)만 찍을 곳이 된다
        [Test]
        public void OnlyTheChangedPart_OfAnObjectWithManyParts()
        {
            var zone = New("Zone");
            MapCube("Light_00", Front, zone.transform);
            var odd = MapCube("Light_01", Back, zone.transform);
            var anomaly = Changing("Light color", zone);
            anomaly.change = _ => Tint(odd);
            StartFloor(anomaly);

            Assert.IsEmpty(Capture(), "안 바뀐 등은 찍어도 안 됨");
            LookBack();
            CollectionAssert.AreEqual(new Anomaly[] { anomaly }, Capture());
            Assert.AreEqual("Light_01", PhotoSubjects.Active()[0].Where);
        }

        [Test]
        public void LightOnlyChange_IsFoundAtTheLight()
        {
            var lamp = New("Lamp");   // 빛만 있고 렌더러가 없는 물체
            lamp.transform.position = new Vector3(0f, 1.6f, 1.5f);
            var light = lamp.AddComponent<Light>();
            var anomaly = Changing("Lamp color", lamp);
            anomaly.change = _ => light.color = Color.red;
            StartFloor(anomaly);
            CollectionAssert.AreEqual(new Anomaly[] { anomaly }, Capture());
        }

        // 깜빡이는 등처럼 켜질 때는 그대로였다가 조금 뒤에 달라지는 것: 층 준비 뒤 잠깐 지켜보는 동안 찾는다
        [Test]
        public void ChangeThatComesLater_IsFoundWhileWatching()
        {
            var lamp = MapCube("Lamp", Front);
            var anomaly = Changing("Flicker", lamp);
            StartFloor(anomaly);
            PhotoSubjects.Settle(1f);
            var missing = new List<Anomaly>();
            Assert.IsEmpty(PhotoSubjects.Active(missing));
            CollectionAssert.AreEqual(new Anomaly[] { anomaly }, missing);
            Assert.IsFalse(PhotoSubjects.Watch(), "아직 안 달라짐");

            lamp.GetComponent<Renderer>().enabled = false;   // 꺼진 순간
            Assert.IsTrue(PhotoSubjects.Watch());
            lamp.GetComponent<Renderer>().enabled = true;    // 다시 켜져도 찾아 둔 곳은 그대로
            CollectionAssert.AreEqual(new Anomaly[] { anomaly }, Capture());
        }

        // 지켜보는 동안에도 자리만 바뀐 것(저절로 열리는 문)은 치지 않고,
        // 지켜보는 시간이 지난 뒤 달라진 것(플레이어가 바꾼 것)은 찾지 않는다
        [Test]
        public void WhileWatching_MovesDoNotCount_AndAfterwardsNothingIsSearched()
        {
            var door = MapCube("Door", Front);
            var anomaly = Changing("Sound only", door);
            StartFloor(anomaly);
            PhotoSubjects.Settle(1f);
            door.transform.position += Vector3.right * 0.5f;
            Assert.IsFalse(PhotoSubjects.Watch(), "자리만 바뀐 것");

            PhotoSubjects.Settle(0f);
            Repaint(door);
            Assert.IsFalse(PhotoSubjects.Watch());
            Assert.IsEmpty(PhotoSubjects.Active(), "나중에 달라진 것은 이상현상 탓이 아님");
        }

        // 같은 물체(조명 구역)를 두 이상현상이 들고 있으면, 먼저 찾은 쪽이 가져간 부분은 다른 쪽 것이 되지 않는다
        [Test]
        public void TwoAnomalies_HoldingTheSameObject_EachGetsItsOwnPart()
        {
            var zone = New("Zone");
            var first = MapCube("Light_00", Front, zone.transform);
            var second = MapCube("Light_01", Back, zone.transform);
            var color = Changing("A color", zone);
            color.change = _ => Tint(first);
            var flicker = Changing("B flicker", zone);   // 켤 때는 그대로
            StartFloor(color, flicker);
            PhotoSubjects.Settle(1f);
            second.GetComponent<Renderer>().enabled = false;
            Assert.IsTrue(PhotoSubjects.Watch());

            CollectionAssert.AreEqual(new Anomaly[] { color }, Capture());
            LookBack();
            CollectionAssert.AreEqual(new Anomaly[] { flicker }, Capture());
        }

        // 둘 다 켜지는 순간에 바꿨어도(깜빡이는 등이 마침 꺼진 채로 시작) 한쪽이 다 가져가지 않고 하나씩 나눠 가진다
        [Test]
        public void TwoAnomalies_BothChangedAtOnce_TakeOnePartEach()
        {
            var zone = New("Zone");
            var first = MapCube("Light_00", Front, zone.transform);
            var second = MapCube("Light_01", Back, zone.transform);
            var color = Changing("A color", zone);
            color.change = _ => Tint(first);
            var flicker = Changing("B flicker", zone);
            flicker.change = _ => second.GetComponent<Renderer>().enabled = false;
            StartFloor(color, flicker);

            Assert.AreEqual(2, PhotoSubjects.Active().Count);
            CollectionAssert.AreEqual(new Anomaly[] { color }, Capture());
            LookBack();
            CollectionAssert.AreEqual(new Anomaly[] { flicker }, Capture());
        }

        // 층 시작을 거치지 않으면(적어 둔 모습이 없으면) 바깥 물체는 찾지 않는다 — 자기 아래 렌더러·PhotoTarget만
        [Test]
        public void WithoutABaseline_OutsideObjectsAreNotSearched()
        {
            var monitor = MapCube("Monitor", Front);
            var anomaly = Changing("Red monitor", monitor);
            anomaly.change = _ => Repaint(monitor);
            anomaly.Activate();
            var missing = new List<Anomaly>();
            Assert.IsEmpty(PhotoSubjects.Active(missing));
            CollectionAssert.AreEqual(new Anomaly[] { anomaly }, missing);
        }

        [Test]
        public void TurnedOff_IsDropped_AndSearchedAgainWhenTurnedOn()
        {
            var monitor = MapCube("Monitor", Front);
            var anomaly = Changing("Red monitor", monitor);
            anomaly.change = _ => Repaint(monitor);
            StartFloor(anomaly);
            Assert.AreEqual(1, PhotoSubjects.Active().Count);

            anomaly.Deactivate();
            Assert.IsEmpty(PhotoSubjects.Active());
            anomaly.Activate();
            CollectionAssert.AreEqual(new Anomaly[] { anomaly }, Capture());
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
