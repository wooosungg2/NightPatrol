using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NightPatrol.Anomalies;
using NightPatrol.Interaction;
using NightPatrol.Lighting;
using NightPatrol.Photo;
using NightPatrol.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace NightPatrol.Tests
{
    // 06_04q 셔터 순간 판정: 조준 박스 안 가운데 1발 + 링(8·12·16발), 가운데·안쪽 링은 1발·바깥 링만이면 3발,
    // 크기에 비례한 거리, 레이마다 가장 가까운 부품 하나, 가림(벽·유리·부품 주인의 충돌체).
    // 찍히는 물체: 자기 몸 · 칸(필드·List)에 든 물체 중 정상 모습과 달라진 것(층 내내 표시, 꺼질 때까지 유지) · PhotoTarget.
    // 자기 몸도 층을 시작했으면 달라진 부품만 (06_04s, 강의실처럼 방 전체에 붙은 이상현상의 벽·문은 빼고 앞을 가림). 못 찾으면 몸 전부.
    // 빼는 것: 플레이어 칸·FirstPerson 레이어, 플레이어가 E·스위치로 바꾼 것(2초 뒤 그 모습이 정상).
    // 눈은 (0, 1.6, 0)에서 +Z를 본다 (세로 시야 60°, 4:3)
    public class PhotoJudgeTests
    {
        private class FakeAnomaly : Anomaly
        {
            public override void Activate() => IsActive = true;
            public override void Deactivate() => IsActive = false;
        }

        // 맵의 물체를 바꾸는 이상현상 (모니터·벨·의자·등·얼룩처럼): 바꾼 물체를 되돌리려고 칸에 들고 있다.
        // 따로 알려 주지 않아도 시스템이 그 칸의 물체 중 정상 모습과 달라진 것을 찍히는 물체로 삼는다
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

        // 자판기처럼: 실행 중 만든 물체를 List 칸에 넣고, 오래된 것은 지운다
        private class SpawningAnomaly : Anomaly
        {
            private readonly List<GameObject> spawned = new List<GameObject>();

            public override void Activate() => IsActive = true;
            public override void Deactivate() => IsActive = false;

            public GameObject Spawn(Vector3 position)
            {
                var can = GameObject.CreatePrimitive(PrimitiveType.Cube);
                can.name = "SodaCan(Clone)";
                can.transform.position = position;
                can.transform.localScale = Vector3.one * 0.5f;
                spawned.Add(can);
                return can;
            }

            public void DropOldest()
            {
                Object.DestroyImmediate(spawned[0]);
                spawned.RemoveAt(0);
            }
        }

        // 권투 글러브처럼: 플레이어를 칸에 들고 있고, 다가가면 글러브가 튀어나온다
        private class GloveAnomaly : Anomaly
        {
            public Transform player;
            public GameObject glove;
            public override void Activate() => IsActive = true;
            public override void Deactivate() => IsActive = false;
        }

        // E로 조작하는 사물 (문·모니터 같은)
        private class Handle : MonoBehaviour, IInteractable
        {
            public string Prompt => string.Empty;
            public void Interact(PlayerInteractor who) { }
        }

        private static readonly Vector3 EyePosition = new Vector3(0f, 1.6f, 0f);
        private static readonly Vector3 Front = new Vector3(0f, 1.6f, 4f), Back = new Vector3(0f, 1.6f, -4f);
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
            PhotoSubjects.Forget();   // 적어 둔 모습·표시·구독이 다음 테스트로 넘어가지 않게
        }

        private GameObject New(string name)
        {
            var go = new GameObject(name);
            created.Add(go);
            return go;
        }

        // 켜진 이상현상 + 보이는 몸(자식 큐브, 충돌체 포함)
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

        // 셔터: 직전에 표시를 갱신하고(카메라 앱과 같은 순서) 판정
        private PhotoCaptureResult Shot()
        {
            Physics.SyncTransforms();
            return PhotoJudge.Capture(eye, rules, PhotoSubjects.Refresh());
        }

        private IReadOnlyList<Anomaly> Capture() => Shot().Captured;

        private static int CountSeen(PhotoCaptureResult shot, Anomaly anomaly)
        {
            int count = 0;
            foreach (var ray in shot.Rays)
                if (ray.Seen == anomaly) count++;
            return count;
        }

        // ── 레이 ──

        [Test]
        public void Rays_GoFromTheCenterOut_InRingsOf8_12_16_InsideTheAimBox()
        {
            var shot = Shot();
            Assert.AreEqual(1 + 8 + 12 + 16, shot.Rays.Count);
            Assert.AreEqual(0, shot.Rays[0].Ring);
            Assert.AreEqual(new Vector2(0.5f, 0.5f), shot.Rays[0].Viewport);

            var perRing = new int[4];
            var radius = new float[4];
            int previous = 0;
            foreach (var ray in shot.Rays)
            {
                Assert.GreaterOrEqual(ray.Ring, previous, "가운데부터 바깥 순서");
                previous = ray.Ring;
                perRing[ray.Ring]++;
                Assert.IsTrue(rules.InAimBox(ray.Viewport), "조준 박스 안");
                Assert.IsNull(ray.Seen);
                Assert.IsTrue(float.IsPositiveInfinity(ray.Distance), "빗나간 레이의 거리");
                Assert.IsFalse(ray.UsedForCapture);
                radius[ray.Ring] = Mathf.Max(radius[ray.Ring], (ray.Viewport - new Vector2(0.5f, 0.5f)).magnitude);
            }
            CollectionAssert.AreEqual(new[] { 1, 8, 12, 16 }, perRing);
            Assert.Less(radius[1], radius[2]);
            Assert.Less(radius[2], radius[3]);
            Assert.AreEqual(rules.aimBox * 0.5f, radius[3], 1e-4f, "바깥 링은 조준 박스 끝");
            Assert.IsEmpty(shot.Captured);
        }

        [Test]
        public void Centered_InFront_IsCaptured_ByTheCenterRay()
        {
            var anomaly = AnomalyWithCube(Front);
            var shot = Shot();
            CollectionAssert.AreEqual(new Anomaly[] { anomaly }, shot.Captured);
            Assert.AreSame(anomaly, shot.Rays[0].Seen);
            Assert.AreEqual(3.5f, shot.Rays[0].Distance, 1e-3f, "앞면까지 거리");
            Assert.IsTrue(shot.Rays[0].UsedForCapture);
        }

        [Test]
        public void OutsideTheAimBox_IsNot()
        {
            AnomalyWithCube(new Vector3(2.5f, 1.6f, 4f));   // 뷰포트 x 0.85 넘음 (바깥 링 끝 0.7)
            Assert.IsEmpty(Capture());
            var near = AnomalyWithCube(new Vector3(0.5f, 1.6f, 4f));   // 가운데 바로 옆
            CollectionAssert.AreEqual(new Anomaly[] { near }, Capture());
        }

        [Test]
        public void BehindTheCamera_IsNot()
        {
            AnomalyWithCube(Back);
            Assert.IsEmpty(Capture());
        }

        [Test]
        public void SmallThings_NeedToBeCloser()
        {
            var small = AnomalyWithCube(Front, 0.2f);   // 대각선 0.35m → 인정 거리 2.08m
            Assert.IsEmpty(Capture(), "작은 것은 4m에서 안 찍힘");
            small.transform.GetChild(0).position = new Vector3(0f, 1.6f, 1.8f);
            CollectionAssert.AreEqual(new Anomaly[] { small }, Capture(), "2m 안이면 찍힘");
        }

        [Test]
        public void AWall_Blocks()
        {
            AnomalyWithCube(Front);
            Block(new Vector3(0f, 1.6f, 2f), new Vector3(6f, 4f, 0.2f));
            Assert.IsEmpty(Capture());
        }

        [Test]
        public void ASmallObstacle_OffTheCenter_DoesNotBlock()
        {
            var anomaly = AnomalyWithCube(Front);
            Block(new Vector3(0.289f, 1.6f, 2f), Vector3.one * 0.15f);
            CollectionAssert.AreEqual(new Anomaly[] { anomaly }, Capture());
        }

        // 가운데 레이만 막혀도 첫 링이 보면 찍힌다 (안쪽 링은 1발)
        [Test]
        public void ABlockedCenter_IsFine_WhenTheFirstRingSees()
        {
            var anomaly = AnomalyWithCube(Front);
            Block(new Vector3(0f, 1.6f, 2f), Vector3.one * 0.1f);
            var shot = Shot();
            Assert.IsNull(shot.Rays[0].Seen, "가운데는 막힘");
            CollectionAssert.AreEqual(new Anomaly[] { anomaly }, shot.Captured);
        }

        // 바깥 링에서만 보이면 여러 발이 맞아야 찍힌다. 링 하나 4발(45°·135°·225°·315°)로 오른쪽 두 발만 맞는 판
        [Test]
        public void SeenOnlyByOuterRings_NeedsSeveralRays()
        {
            rules.raysPerRing = new[] { 4 };
            rules.innerRings = 0;   // 가운데만 1발로 됨
            rules.outerMinHits = 2;
            var anomaly = New("Anomaly").AddComponent<FakeAnomaly>();
            var board = GameObject.CreatePrimitive(PrimitiveType.Cube);
            board.transform.SetParent(anomaly.transform, false);
            board.transform.position = new Vector3(1.15f, 1.6f, 4f);   // x 0.3~2.0: 가운데·왼쪽 레이는 빗나감
            board.transform.localScale = new Vector3(1.7f, 2.2f, 1f);
            anomaly.Activate();

            var shot = Shot();
            Assert.AreEqual(5, shot.Rays.Count);
            Assert.AreEqual(2, CountSeen(shot, anomaly));
            CollectionAssert.AreEqual(new Anomaly[] { anomaly }, shot.Captured, "바깥 2발 = 필요한 2발");

            rules.outerMinHits = 3;
            shot = Shot();
            Assert.IsEmpty(shot.Captured, "바깥 2발 < 3발");
            foreach (var ray in shot.Rays) Assert.IsFalse(ray.UsedForCapture);

            rules.innerRings = 1;
            CollectionAssert.AreEqual(new Anomaly[] { anomaly }, Capture(), "그 링을 안쪽으로 치면 1발이면 됨");
        }

        // 레이마다 가장 가까운 부품 하나. 그 부품이 너무 멀어도 뒤의 것으로 넘어가지 않는다
        [Test]
        public void EachRaySeesTheNearestPart_AndATooFarOne_DoesNotLetItThrough()
        {
            var near = AnomalyWithCube(new Vector3(0f, 1.6f, 3f));
            var behind = AnomalyWithCube(new Vector3(0f, 1.6f, 6f));
            CollectionAssert.AreEqual(new Anomaly[] { near }, Capture(), "앞의 것에 가린 뒤의 것은 안 찍힘");

            var small = near.transform.GetChild(0);
            small.localScale = Vector3.one * 0.2f;   // 작아져서 3m는 너무 멂
            Object.DestroyImmediate(small.GetComponent<Collider>());   // 충돌체로 막는 것과 구분
            var shot = Shot();
            var center = shot.Rays[0];
            Assert.AreSame(near, center.Seen, "가운데 레이는 너무 먼 작은 것에서 멈춤 (본 것은 남김)");
            Assert.IsTrue(center.TooFar);
            Assert.AreEqual(2.9f, center.Distance, 1e-3f);
            Assert.IsFalse(center.UsedForCapture);
            CollectionAssert.AreEqual(new Anomaly[] { behind }, shot.Captured, "작은 것을 비켜 간 레이로 뒤의 것");
        }

        // 06_04r 레이 점 색: 찍힘에 쓰임 자홍 / 봤지만 부족·거리 초과 주황 / 못 봄 흰색 반투명. 거리 초과 레이는 찍힘에 쓰이지 않음
        [Test]
        public void RayDotColors_FollowTheRayResult()
        {
            var anomaly = AnomalyWithCube(Front);
            var middle = new Vector2(0.5f, 0.5f);
            var used = new PhotoRayResult(middle, 0, anomaly, 3f, true);
            var short_ = new PhotoRayResult(middle, 3, anomaly, 3f, false);
            var far = new PhotoRayResult(middle, 0, anomaly, 9f, true, tooFar: true);
            var none = new PhotoRayResult(middle, 0, null, float.PositiveInfinity, false);

            Assert.IsFalse(far.UsedForCapture, "거리 초과는 찍힘에 안 쓰임");
            Assert.AreEqual(new Color(1f, 0f, 1f, 1f), PhotoDebugView.RayColor(used));
            Assert.AreEqual(new Color(1f, 0.5f, 0f, 1f), PhotoDebugView.RayColor(short_));
            Assert.AreEqual(new Color(1f, 0.5f, 0f, 1f), PhotoDebugView.RayColor(far));
            Assert.AreEqual(new Color(1f, 1f, 1f, 0.45f), PhotoDebugView.RayColor(none));
        }

        [Test]
        public void PhotoPassThroughGlass_DoesNotBlock()
        {
            var anomaly = AnomalyWithCube(Front);
            var glass = Block(new Vector3(0f, 1.6f, 2f), new Vector3(6f, 4f, 0.05f));
            Assert.IsEmpty(Capture(), "보통 벽은 막음");
            glass.AddComponent<PhotoPassThrough>();
            CollectionAssert.AreEqual(new Anomaly[] { anomaly }, Capture(), "유리는 통과");
        }

        // 모니터처럼 주인(이상현상 오브젝트·칸 물체)의 충돌체가 부품을 감싸도 막지 않는다. 같은 자리의 남의 충돌체는 막는다
        [Test]
        public void TheOwnersColliders_DoNotHideItsParts_OthersDo()
        {
            var anomaly = New("Monitor anomaly").AddComponent<FakeAnomaly>();
            anomaly.transform.position = Front;
            anomaly.gameObject.AddComponent<BoxCollider>().size = new Vector3(2f, 2f, 1f);   // z 3.5~4.5
            var screen = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(screen.GetComponent<Collider>());
            screen.transform.SetParent(anomaly.transform, false);
            screen.transform.localPosition = new Vector3(0f, 0f, 0.3f);
            screen.transform.localScale = new Vector3(1f, 1f, 0.05f);
            anomaly.Activate();
            CollectionAssert.AreEqual(new Anomaly[] { anomaly }, Capture(), "자기 충돌체는 안 막음");

            Block(Front, new Vector3(2f, 2f, 1f));
            Assert.IsEmpty(Capture(), "남의 충돌체는 막음");
        }

        // ── 자기 몸: 셔터 순간 그 자리 ──

        [Test]
        public void AMovingBody_IsCaughtWhereItIsAtTheShutter()
        {
            var anomaly = AnomalyWithCube(Back);
            Assert.IsEmpty(Capture());
            Assert.IsTrue(PhotoSubjects.HasAppeared(anomaly), "보이는 몸이 있으면 나타난 것 (카메라에 담겼는지와 상관없음)");
            anomaly.transform.GetChild(0).position = Front;
            CollectionAssert.AreEqual(new Anomaly[] { anomaly }, Capture());
        }

        [Test]
        public void AHiddenBody_AppearsOnlyOnceShown()
        {
            var anomaly = AnomalyWithCube(Front);
            var body = anomaly.transform.GetChild(0).gameObject;
            body.SetActive(false);
            Assert.IsEmpty(Capture());
            Assert.IsFalse(PhotoSubjects.HasAppeared(anomaly));
            body.SetActive(true);
            CollectionAssert.AreEqual(new Anomaly[] { anomaly }, Capture());
            Assert.IsTrue(PhotoSubjects.HasAppeared(anomaly));
        }

        [Test]
        public void InactiveAnomaly_IsNotASubject()
        {
            var anomaly = AnomalyWithCube(Front, active: false);
            Assert.IsEmpty(PhotoSubjects.Refresh());
            Assert.IsEmpty(Capture());
            anomaly.Activate();
            Assert.AreEqual(1, PhotoSubjects.Refresh().Count);
            Assert.AreEqual("자기 몸", PhotoSubjects.Active()[0].Where);
        }

        // ── 칸에 든 물체 중 달라진 것 ──

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

        // 층 시작 흉내 (PatrolRun 순서): 정상 모습 적기 → 이상현상 켜기 → 첫 표시 → (점검 항목) → 표시 안 된 것 다시 적기
        private void StartFloor(params Anomaly[] anomalies)
        {
            PhotoSubjects.BeginFloor();
            foreach (var anomaly in anomalies) anomaly.Activate();
            PhotoSubjects.FindChanged();
            PhotoSubjects.Settle();
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

        // 자판기: 층 중간에 만든 캔도 찍힌다. 첫 캔이 지워져도 다음 캔으로
        [Test]
        public void ThingsMadeLater_IntoAList_AreFound_EvenAfterTheFirstIsGone()
        {
            var anomaly = New("VendingMachineAnomaly").AddComponent<SpawningAnomaly>();
            StartFloor(anomaly);
            Assert.IsEmpty(Capture());
            Assert.IsFalse(PhotoSubjects.HasAppeared(anomaly), "아직 아무것도 안 나옴");

            created.Add(anomaly.Spawn(Back));
            Assert.IsEmpty(Capture(), "뒤에 나온 캔");
            Assert.IsTrue(PhotoSubjects.HasAppeared(anomaly));
            anomaly.DropOldest();
            created.Add(anomaly.Spawn(Front));
            CollectionAssert.AreEqual(new Anomaly[] { anomaly }, Capture(), "새로 나온 캔");
            StringAssert.Contains("SodaCan(Clone)", PhotoSubjects.Active()[0].Where);
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

        // 여러 부분으로 된 물체를 통째로 옮겼으면 부분마다 찍히는 물체가 된다 (레이는 부분마다)
        [Test]
        public void WholeObjectMoved_EveryPartCounts()
        {
            var chair = New("Chair");
            chair.transform.position = Back;
            MapCube("Seat", Back, chair.transform);
            MapCube("Back", Back + Vector3.up * 0.6f, chair.transform);
            var anomaly = Changing("Ceiling chair", chair);
            anomaly.change = _ => chair.transform.position = Front;
            StartFloor(anomaly);

            var subject = PhotoSubjects.Active()[0];
            Assert.AreEqual(2, subject.Shapes.Count);
            Assert.AreEqual("Seat·Back", subject.Where);
            CollectionAssert.AreEqual(new Anomaly[] { anomaly }, Capture());
        }

        // 맵 물체를 카메라로만 보이게(CameraOnly 레이어로) 바꾼 이상현상: 레이어가 바뀐 것도 달라진 것
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

        // 조명 구역처럼 여러 부분으로 된 물체를 들고 있어도, 달라진 부분(등 하나)만 찍히는 물체
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

        // 깜빡이는 등: 켜질 때는 그대로였다가 층 중간에 달라진다. 한 번 표시되면 정상 모습으로 돌아온 순간에도 찍힌다
        [Test]
        public void APartThatChangesLater_IsFound_AndStaysFoundWhenItLooksNormalAgain()
        {
            var lamp = MapCube("Lamp", Front);
            var anomaly = Changing("Flicker", lamp);   // 켤 때는 그대로
            StartFloor(anomaly);
            Assert.IsEmpty(Capture(), "아직 안 달라짐");
            Assert.IsFalse(PhotoSubjects.HasAppeared(anomaly));

            var renderer = lamp.GetComponent<Renderer>();
            renderer.enabled = false;   // 꺼진 순간에 표시 (0.1초마다 갱신)
            PhotoSubjects.Refresh();
            renderer.enabled = true;    // 다시 켜져 정상 모습
            CollectionAssert.AreEqual(new Anomaly[] { anomaly }, Capture());
            Assert.IsTrue(PhotoSubjects.HasAppeared(anomaly));
        }

        // 점검 항목이 층 시작 때 바꾼 것(켠 모니터)은 정상 모습으로 다시 적는다
        [Test]
        public void WhatTheChoresChangeAtFloorStart_IsTheNormal()
        {
            var monitor = MapCube("Monitor", Front);
            var anomaly = Changing("Holds the monitor", monitor);
            PhotoSubjects.BeginFloor();
            anomaly.Activate();
            PhotoSubjects.FindChanged();
            Repaint(monitor);   // 점검 항목 (첫 표시 뒤, 다시 적기 전)
            PhotoSubjects.Settle();
            Assert.IsEmpty(Capture());
        }

        // ── 빼는 것 ──

        private static void RaiseInteracted(PlayerInteractor interactor, IInteractable target)
        {
            var field = typeof(PlayerInteractor).GetField("Interacted", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "PlayerInteractor.Interacted 이벤트");
            ((System.Action<IInteractable>)field.GetValue(interactor))?.Invoke(target);
        }

        private PlayerInteractor MakeInteractor()
        {
            LogAssert.Expect(LogType.Error, new Regex("PlayerInteractor에 PlayerSettings"));
            return New("Interactor").AddComponent<PlayerInteractor>();
        }

        // 플레이어가 E로 바꾼 것은 2초 동안 표시하지 않고, 2초 뒤 그 모습을 정상으로 적는다. 그 뒤 이상현상이 바꾸면 다시 찾는다
        [UnityTest]
        public IEnumerator WhatThePlayerChangesWithE_IsLeftOut_ThenBecomesTheNormal()
        {
            var interactor = MakeInteractor();
            var door = MapCube("Door", Front);
            var handle = door.AddComponent<Handle>();
            var anomaly = Changing("Holds the door", door);   // 들고만 있다
            StartFloor(anomaly);

            RaiseInteracted(interactor, handle);
            door.transform.position += Vector3.right * 0.3f;   // 플레이어가 연 문
            Assert.IsEmpty(Capture(), "플레이어가 바꾼 것은 이상현상 탓이 아님");
            yield return new WaitForSeconds(PhotoSubjects.InteractionSeconds + 0.1f);
            Assert.IsEmpty(Capture(), "2초 뒤 그때 모습이 정상");

            Repaint(door);
            CollectionAssert.AreEqual(new Anomaly[] { anomaly }, Capture(), "그 뒤 이상현상이 바꾼 것은 찾음");
        }

        // 이미 표시된 것(이상현상 모니터)은 플레이어가 E로 만져도 그대로 찍힌다
        [Test]
        public void AMarkedPart_StaysWhenThePlayerTouchesIt()
        {
            var interactor = MakeInteractor();
            var monitor = MapCube("Monitor", Front);
            var handle = monitor.AddComponent<Handle>();
            var anomaly = Changing("Red monitor", monitor);
            anomaly.change = _ => Repaint(monitor);
            StartFloor(anomaly);

            RaiseInteracted(interactor, handle);
            CollectionAssert.AreEqual(new Anomaly[] { anomaly }, Capture());
        }

        // 스위치(LightZone.Changed)로 켠 등은 이상현상 탓이 아님
        [Test]
        public void ALightSwitch_IsLeftOut()
        {
            var zoneObject = New("Zone");
            var lamp = new GameObject("Light_00");
            lamp.transform.SetParent(zoneObject.transform, false);
            lamp.transform.position = new Vector3(0f, 1.6f, 1.5f);
            lamp.AddComponent<Light>();
            var zone = zoneObject.AddComponent<LightZone>();
            zone.SetOn(false);   // 꺼진 채 층 시작
            var anomaly = Changing("Holds the zone", zoneObject);
            StartFloor(anomaly);

            zone.SetOn(true);
            Assert.IsEmpty(Capture());
        }

        // 권투 글러브처럼 플레이어를 칸에 들고 있어도, 플레이어 손의 물건은 찍히는 물체가 아님
        [Test]
        public void ThePlayersOwnThings_AreNeverMarked()
        {
            var player = New("Player");
            player.AddComponent<PlayerControl>();
            var flashlight = MapCube("Flashlight", Front, player.transform);
            var glove = MapCube("Glove", Back);
            glove.SetActive(false);
            var anomaly = New("BoxingGloveAnomaly").AddComponent<GloveAnomaly>();
            anomaly.player = player.transform;
            anomaly.glove = glove;
            StartFloor(anomaly);

            Repaint(flashlight);
            Assert.IsEmpty(Capture(), "플레이어 칸의 물건");
            Assert.IsFalse(PhotoSubjects.HasAppeared(anomaly));

            glove.SetActive(true);   // 다가가면 튀어나옴
            LookBack();
            CollectionAssert.AreEqual(new Anomaly[] { anomaly }, Capture());
        }

        [Test]
        public void TheFirstPersonLayer_IsNeverMarked()
        {
            int firstPerson = LayerMask.NameToLayer("FirstPerson");
            Assert.GreaterOrEqual(firstPerson, 0, "FirstPerson 레이어 없음");
            var held = MapCube("Phone", Front);
            held.layer = firstPerson;
            var anomaly = Changing("Holds a first-person thing", held);
            anomaly.change = _ => Repaint(held);
            StartFloor(anomaly);
            Assert.IsEmpty(Capture());
        }

        // ── 같은 물체를 둘이 들고 있으면 ──

        // 먼저 표시한 쪽이 가진 부분은 다른 쪽 것이 되지 않는다 (나중에 깜빡인 등은 깜빡임 쪽)
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
            var renderer = second.GetComponent<Renderer>();
            renderer.enabled = false;
            PhotoSubjects.Refresh();
            renderer.enabled = true;

            CollectionAssert.AreEqual(new Anomaly[] { color }, Capture());
            LookBack();
            CollectionAssert.AreEqual(new Anomaly[] { flicker }, Capture());
        }

        // 둘 다 켜지는 순간에 바꿨어도 한쪽이 다 가져가지 않고 하나씩 (이름 순)
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
            second.GetComponent<Renderer>().enabled = true;   // 깜빡임이 다시 켬

            Assert.AreEqual(2, PhotoSubjects.Refresh().Count);
            CollectionAssert.AreEqual(new Anomaly[] { color }, Capture());
            LookBack();
            CollectionAssert.AreEqual(new Anomaly[] { flicker }, Capture());
        }

        // ── 방 전체에 붙은 이상현상 (강의실, 06_04s) ──

        // 강의실처럼 방(벽·문 포함)에 붙고, 켜면 미리 놓아 둔 꾸밈을 보여 주는 이상현상. 문은 z=2 (4×4m), 꾸밈은 처음에 꺼져 있다
        private ChangingAnomaly Room(out GameObject door, out GameObject set, Vector3 setPosition, Vector3 setScale)
        {
            var room = New("527").AddComponent<ChangingAnomaly>();
            door = MapCube("Door", new Vector3(0f, 1.6f, 2f), room.transform);
            door.transform.localScale = new Vector3(4f, 4f, 0.2f);
            var shown = MapCube("BirthdayParty_Combined", setPosition, room.transform);
            shown.transform.localScale = setScale;
            shown.SetActive(false);
            room.change = _ => shown.SetActive(true);
            set = shown;
            return room;
        }

        [Test]
        public void ARoomWideAnomaly_OnlyTheSetItShowsCounts_AndTheRoomsOwnDoorBlocks()
        {
            var room = Room(out var door, out _, Front, Vector3.one);
            StartFloor(room);
            Assert.IsEmpty(Capture(), "원래 있던 문은 찍어도 안 되고, 문 너머 꾸밈도 가려짐");
            Assert.AreEqual("자기 몸", PhotoSubjects.Active()[0].Where);
            Assert.AreEqual(1, PhotoSubjects.Active()[0].Shapes.Count, "찍히는 부품은 나타난 꾸밈 하나");

            door.SetActive(false);   // 문을 열고 들여다봄
            CollectionAssert.AreEqual(new Anomaly[] { room }, Capture());
        }

        // 방을 다 덮는 큰 꾸밈 안에 서 있으면 (눈이 부품 상자 안) 어느 쪽을 찍어도 그 이상현상
        [Test]
        public void StandingInsideARoomWideSet_AnyShotCounts()
        {
            var room = Room(out var door, out var set, EyePosition, new Vector3(8f, 4f, 8f));
            Object.DestroyImmediate(set.GetComponent<Collider>());
            door.SetActive(false);
            StartFloor(room);
            CollectionAssert.AreEqual(new Anomaly[] { room }, Capture(), "앞");

            eye.transform.rotation = Quaternion.LookRotation(Vector3.down + Vector3.forward * 0.2f);
            CollectionAssert.AreEqual(new Anomaly[] { room }, Capture(), "바닥 쪽");
        }

        // 이상현상 스크립트가 칸에 자기 아래 물체(꾸밈)를 들고 있으면 그 아래만 자기 몸:
        // 다른 코드가 방에 켠 것(문 위 유리창 불빛, RoomAnomalyAccess)은 찍히는 부품이 아니다
        [Test]
        public void WhenTheRoomHoldsItsSets_OnlyThoseCount_NotTheGlowOtherCodeTurnsOn()
        {
            var room = Room(out var door, out var set, Back, Vector3.one);   // 꾸밈은 뒤 (z −4)
            room.holds = new[] { set };
            door.SetActive(false);
            var transom = MapCube("Transom", Front, room.transform);           // 앞 (z 4): 문 위 유리창
            StartFloor(room);
            Repaint(transom);   // 방 불이 켜지며 다른 코드가 유리창을 밝힘 (층 시작 뒤)

            Assert.IsEmpty(Capture(), "유리창 불빛은 찍히는 부품이 아님");
            LookBack();
            CollectionAssert.AreEqual(new Anomaly[] { room }, Capture(), "꾸밈은 찍힘");
        }

        // 방을 채우는 꾸밈이 벽을 몇 cm 뚫고 나와도 (캠핑 꾸밈 15cm) 닫힌 문 밖에서는 안 찍힌다: 큰 부품은 가장자리 20cm를 빼고 본다
        [Test]
        public void ARoomWideSet_PokingAFewCentimetersPastTheDoor_IsStillHiddenByIt()
        {
            // 문 z 1.9~2.1, 꾸밈 z 1.85~5.85 (문보다 5cm 앞까지 나옴)
            var room = Room(out var door, out _, new Vector3(0f, 1.6f, 3.85f), new Vector3(4f, 3f, 4f));
            StartFloor(room);
            Assert.IsEmpty(Capture(), "줄인 상자는 문 뒤에서 시작");

            door.SetActive(false);
            CollectionAssert.AreEqual(new Anomaly[] { room }, Capture(), "문을 열면 찍힘");
        }

        // 큰 꾸밈 안에서도 상자 끝보다 가까운 다른 이상현상은 그 레이가 먼저 본다
        [Test]
        public void InsideARoomWideSet_ANearerAnomalyInTheRoom_IsStillCaught()
        {
            var room = Room(out var door, out var set, EyePosition, new Vector3(10f, 4f, 10f));
            Object.DestroyImmediate(set.GetComponent<Collider>());
            door.SetActive(false);
            var monitor = AnomalyWithCube(new Vector3(0f, 1.6f, 3f), active: false);
            StartFloor(room, monitor);

            var shot = Shot();
            CollectionAssert.AreEquivalent(new Anomaly[] { room, monitor }, shot.Captured, "가운데는 모니터, 둘레는 방");
            Assert.AreEqual(monitor, shot.Rays[0].Seen, "가운데 레이는 상자 끝(약 5m)보다 가까운 모니터(2.5m)");
        }

        // 켜도 모습이 그대로인 자기 몸은 (달라진 부품을 못 찾으면) 예전처럼 보이는 몸 전부가 찍힌다
        [Test]
        public void AnOwnBodyThatLooksTheSameWhenTurnedOn_StillCounts()
        {
            var anomaly = AnomalyWithCube(Front, active: false);
            StartFloor(anomaly);
            CollectionAssert.AreEqual(new Anomaly[] { anomaly }, Capture());
            Assert.AreEqual("자기 몸", PhotoSubjects.Active()[0].Where);
        }

        // 달라진 부품과 원래 있던 부품이 같은 부모 아래면, 그 부모의 충돌체는 달라진 부품을 가리지 않는다
        [Test]
        public void TheSharedParentsCollider_DoesNotHideTheChangedPart()
        {
            var anomaly = New("Trash can anomaly").AddComponent<ChangingAnomaly>();
            var model = New("Model");
            model.transform.SetParent(anomaly.transform, false);
            model.transform.position = Front;
            model.AddComponent<BoxCollider>().size = new Vector3(3f, 3f, 3f);   // z 2.5~5.5, 두 부품을 감쌈
            MapCube("Can", Front + Vector3.down, model.transform);                // 원래 있던 몸 (가운데 레이 아래)
            var glove = MapCube("Glove", Front, model.transform);
            glove.SetActive(false);
            anomaly.change = _ => glove.SetActive(true);
            StartFloor(anomaly);

            CollectionAssert.AreEqual(new Anomaly[] { anomaly }, Capture());
            Assert.AreEqual(1, PhotoSubjects.Active()[0].Shapes.Count, "원래 있던 쓰레기통은 찍히는 부품이 아님");
        }

        // ── 그 밖 ──

        // 층 시작을 거치지 않으면(적어 둔 모습이 없으면) 바깥 물체는 찾지 않는다 — 자기 몸·PhotoTarget만
        [Test]
        public void WithoutABaseline_OutsideObjectsAreNotSearched()
        {
            var monitor = MapCube("Monitor", Front);
            var anomaly = Changing("Red monitor", monitor);
            anomaly.change = _ => Repaint(monitor);
            anomaly.Activate();
            Assert.IsEmpty(PhotoSubjects.Refresh());
            var notYet = new List<Anomaly>();
            PhotoSubjects.Active(notYet);
            CollectionAssert.AreEqual(new Anomaly[] { anomaly }, notYet, "아직 안 나타난 것");
        }

        [Test]
        public void TurnedOff_IsDropped_AndFoundAgainWhenTurnedOn()
        {
            var monitor = MapCube("Monitor", Front);
            var anomaly = Changing("Red monitor", monitor);
            anomaly.change = _ => Repaint(monitor);
            StartFloor(anomaly);
            Assert.AreEqual(1, PhotoSubjects.Refresh().Count);

            anomaly.Deactivate();
            Assert.IsEmpty(PhotoSubjects.Refresh());
            Assert.IsEmpty(Capture(), "꺼진 이상현상은 안 찍힘");
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
            Assert.IsTrue(PhotoSubjects.HasAppeared(anomaly), "PhotoTarget이 있으면 켜진 순간부터 나타난 것");
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

        // 얇은 벽(2cm) 너머의 얼룩은 앞쪽 확인으로 막는다
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

        // 정상 모습 다시 적기 (표시 안 된 것만)와 지워진 물체 잊기
        [Test]
        public void LookBaseline_RememberMakesTheNowLookNormal_AndDestroyedThingsAreForgotten()
        {
            var cube = MapCube("Cube", Front);
            var renderer = cube.GetComponent<Renderer>();
            var baseline = LookBaseline.Capture();
            int count = baseline.Count;
            Repaint(cube);
            Assert.IsTrue(baseline.Changed(renderer, false));
            baseline.Remember(renderer);
            Assert.IsFalse(baseline.Changed(renderer, true));

            Object.DestroyImmediate(cube);
            baseline.PruneDestroyed();
            Assert.AreEqual(count - 1, baseline.Count);
        }
    }
}
