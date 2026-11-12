using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NightPatrol.Anomalies;
using UnityEngine;

namespace NightPatrol.Photo
{
    /// <summary>찍어야 할 것 하나 = 켜진 이상현상 하나. 모양 중 하나라도 찍히면 찍힌 것</summary>
    public sealed class PhotoSubject
    {
        public readonly Anomaly Source;
        public readonly IReadOnlyList<PhotoShape> Shapes;
        /// <summary>찍을 곳을 어디로 잡았는지 (F1 "사진"용): "PhotoTarget", "자기 아래", 또는 달라진 물체 이름</summary>
        public readonly string Where;

        public PhotoSubject(Anomaly source, IReadOnlyList<PhotoShape> shapes, string where = "")
        {
            Source = source;
            Shapes = shapes;
            Where = where ?? "";
        }

        public string Name => Source != null ? Source.name : "?";
    }

    /// <summary>
    /// 지금 찍어야 할 것 목록. 판정·촬영 장수·플레이 기록이 모두 이 목록 하나를 쓴다.
    /// 이상현상은 공통 부모 Anomaly를 상속하기만 하면 된다 — 켜져 있으면(IsActive) 목록에 들어가고, 찍을 곳은 시스템이 찾는다:
    /// 1. 그 이상현상을 맡은 PhotoTarget (직접 정한 자리 — 보이는 물체가 없거나 자동으로 못 찾을 때만)
    /// 2. 이상현상 오브젝트와 자식 중 켜진 렌더러 (이미지·여학생처럼 보이는 게 자기 아래에 있는 것)
    /// 3. 이상현상이 바꾼 바깥 물체 (모니터·벨·의자·등·얼룩) — 이상현상이 칸(필드)에 들고 있는 씬 물체 중
    ///    켜지기 전과 모습이 달라진 것. 들고만 있고 안 바꾼 물체(점검 대상 문 등)는 달라지지 않았으니 빠진다.
    /// 그래서 PatrolRun이 층을 시작할 때 BeginFloor(켜지기 전) → FindChanged(켜진 직후) → Settle(층 준비 끝) 순서로 부른다
    /// </summary>
    public static class PhotoSubjects
    {
        /// <summary>층 준비가 끝난 뒤 이만큼(초)은 조금 뒤에 달라지는 이상현상(깜빡임)을 지켜본다</summary>
        public const float WatchSeconds = 3f;
        // 층 전체처럼 너무 큰 묶음을 가리키는 칸은 "바꾼 물체"로 보지 않는다
        private const int MaxRenderersPerObject = 512;
        private const BindingFlags AnyField = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        // 바꾼 물체 한 곳: 이상현상이 가리킨 물체(Root) 아래에서 달라진 부분
        // (Part — 일부만 달라졌으면 Root의 자식 하나, 물체 전체가 달라졌으면 Root 자신)
        private sealed class Spot
        {
            public Transform Root, Part;
            public readonly List<Renderer> Renderers = new List<Renderer>();
            public readonly List<Light> Lights = new List<Light>();
        }

        private static readonly Dictionary<Anomaly, List<Spot>> spots = new Dictionary<Anomaly, List<Spot>>();
        private static readonly Dictionary<Type, FieldInfo[]> objectFields = new Dictionary<Type, FieldInfo[]>();
        private static readonly HashSet<Anomaly> warned = new HashSet<Anomaly>();
        private static LookBaseline baseline;
        private static float watchUntil;

        /// <summary>층 시작, 이상현상이 켜지기 직전: 지금 모습을 적어 둔다 (무엇을 바꿨는지 찾을 기준)</summary>
        public static void BeginFloor()
        {
            spots.Clear();
            watchUntil = 0f;
            baseline = LookBaseline.Capture();
        }

        /// <summary>이상현상이 켜진 직후: 각 이상현상이 바꾼 물체를 찾아 둔다. 점검 항목이 사물을 바꾸기 전에 부를 것</summary>
        public static void FindChanged()
        {
            if (baseline == null) return;
            var active = ActiveAnomalies();
            foreach (var anomaly in active)
                if (!spots.ContainsKey(anomaly)) spots[anomaly] = Find(anomaly, countMoves: true, active);
        }

        /// <summary>
        /// 층 준비가 끝났을 때: 기준을 지금 모습으로 다시 잡고(점검 항목이 바꾼 것이 섞이지 않게),
        /// 아직 못 찾은 이상현상은 watchSeconds 동안 Watch가 다시 찾는다
        /// </summary>
        public static void Settle(float watchSeconds = WatchSeconds)
        {
            if (baseline == null) return;
            baseline = LookBaseline.Capture();
            watchUntil = Time.time + Mathf.Max(0f, watchSeconds);
        }

        /// <summary>
        /// 지켜보는 동안 매 프레임 부른다: 켜질 때는 안 달라졌다가 조금 뒤에 달라지는 이상현상(깜빡이는 등)을 찾는다.
        /// 새로 찾았으면 true. 자리만 바뀐 것은 치지 않는다 (열리는 문처럼 저절로 움직이는 것이 섞이지 않게)
        /// </summary>
        public static bool Watch()
        {
            if (baseline == null || Time.time >= watchUntil) return false;
            bool found = false;
            var active = ActiveAnomalies();
            foreach (var anomaly in active)
            {
                bool first = !spots.TryGetValue(anomaly, out var list);   // 방금 켜진 이상현상은 자리 바뀜도 본다
                if (!first && list.Count > 0) continue;
                var late = Find(anomaly, countMoves: first, active);
                spots[anomaly] = late;
                found |= late.Count > 0;
            }
            return found;
        }

        /// <summary>적어 둔 기준과 찾아 둔 것을 버린다 (1층 도착, 테스트 정리)</summary>
        public static void Forget()
        {
            spots.Clear();
            baseline = null;
            watchUntil = 0f;
        }

        /// <summary>
        /// 켜진 이상현상 중 찍을 곳이 있는 것 (이름 순). 찍을 곳을 못 찾은 이상현상은 요구하지 않고 missing에 넣는다
        /// (통과할 수 없는 층을 만들지 않게). 씬 전체를 찾으므로 매 프레임 부르지 말 것
        /// </summary>
        public static List<PhotoSubject> Active(List<Anomaly> missing = null)
        {
            var targets = new Dictionary<Anomaly, List<PhotoTarget>>();
            foreach (var target in UnityEngine.Object.FindObjectsByType<PhotoTarget>(FindObjectsInactive.Include))
            {
                var owner = target != null ? target.Anomaly : null;
                if (owner == null) continue;
                if (!targets.TryGetValue(owner, out var list)) targets.Add(owner, list = new List<PhotoTarget>());
                list.Add(target);
            }

            var active = ActiveAnomalies();
            DropInactive(active);
            var result = new List<PhotoSubject>();
            foreach (var anomaly in active)
            {
                var shapes = new List<PhotoShape>();
                string where = "";
                if (targets.TryGetValue(anomaly, out var list))
                {
                    foreach (var target in list) shapes.Add(target.Shape());
                    where = "PhotoTarget";
                }
                else if (PhotoShape.TryFromRenderers(anomaly.gameObject, out var own))
                {
                    shapes.Add(own);
                    where = "자기 아래";
                }
                else where = ChangedShapes(anomaly, shapes, active);

                if (shapes.Count > 0) result.Add(new PhotoSubject(anomaly, shapes, where));
                else
                {
                    missing?.Add(anomaly);
                    if (Time.time >= watchUntil) WarnOnce(anomaly);   // 지켜보는 동안은 아직 찾는 중
                }
            }
            return result;
        }

        // 켜진 이상현상 (이름 순 — 같은 물체를 두 이상현상이 들고 있으면 달라진 부분을 앞에서부터 하나씩 가져가므로 순서를 고정)
        private static List<Anomaly> ActiveAnomalies()
        {
            var active = new List<Anomaly>();
            foreach (var anomaly in UnityEngine.Object.FindObjectsByType<Anomaly>(FindObjectsInactive.Exclude))
                if (anomaly != null && anomaly.IsActive) active.Add(anomaly);
            active.Sort((a, b) => StringComparer.Ordinal.Compare(a.name, b.name));
            return active;
        }

        // 꺼진 이상현상이 찾아 둔 것은 버린다 (다시 켜지면 새로 찾게)
        private static void DropInactive(List<Anomaly> active)
        {
            if (spots.Count == 0) return;
            var stale = new List<Anomaly>();
            foreach (var anomaly in spots.Keys)
                if (!active.Contains(anomaly)) stale.Add(anomaly);
            foreach (var anomaly in stale) spots.Remove(anomaly);
        }

        // 그 이상현상이 바꾼 바깥 물체의 모양들. 처음 보는 이상현상(층 중간에 켜진 것)은 지금 한 번 찾는다
        private static string ChangedShapes(Anomaly anomaly, List<PhotoShape> shapes, List<Anomaly> active)
        {
            if (!spots.TryGetValue(anomaly, out var list))
            {
                if (baseline == null) return "";
                spots[anomaly] = list = Find(anomaly, countMoves: true, active);
            }
            var names = new List<string>();
            foreach (var spot in list)
            {
                if (!TryBounds(spot, out var bounds)) continue;
                shapes.Add(new PhotoShape(bounds.center, bounds.size.magnitude, spot.Root));
                if (names.Count < 3 && spot.Part != null) names.Add(spot.Part.name);
            }
            return string.Join("·", names);
        }

        private static bool TryBounds(Spot spot, out Bounds bounds)
        {
            bounds = new Bounds();
            bool any = false;
            foreach (var renderer in spot.Renderers)
            {
                if (renderer == null) continue;
                // 꺼진 오브젝트의 렌더러는 바운드를 믿을 수 없어 자리만 쓴다
                var part = renderer.gameObject.activeInHierarchy ? renderer.bounds : new Bounds(renderer.transform.position, Vector3.zero);
                if (any) bounds.Encapsulate(part);
                else bounds = part;
                any = true;
            }
            foreach (var light in spot.Lights)
            {
                if (light == null) continue;
                var point = new Bounds(light.transform.position, Vector3.zero);
                if (any) bounds.Encapsulate(point);
                else bounds = point;
                any = true;
            }
            return any;
        }

        // 이상현상이 칸에 들고 있는 씬 물체들 아래에서, 기준과 모습이 달라진 렌더러·빛을 부분별로 모은다
        private static List<Spot> Find(Anomaly anomaly, bool countMoves, List<Anomaly> active)
        {
            var found = new List<Spot>();
            foreach (var root in ReferencedObjects(anomaly))
            {
                var renderers = root.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length > MaxRenderersPerObject) continue;
                var changedRenderers = new List<Renderer>();
                foreach (var renderer in renderers)
                    if (baseline.Changed(renderer, countMoves) && !BelongsToAnother(renderer, anomaly)) changedRenderers.Add(renderer);
                var changedLights = new List<Light>();
                foreach (var light in root.GetComponentsInChildren<Light>(true))
                    if (baseline.Changed(light) && !BelongsToAnother(light, anomaly)) changedLights.Add(light);

                // 물체 전체가 달라졌으면(옮긴 의자) 한 덩어리, 일부만 달라졌으면(조명 구역의 등 하나) 달라진 부분별로.
                // 같은 물체를 다른 켜진 이상현상도 들고 있으면(조명 구역 하나에 색·깜빡임) 늘 부분별로 보고 하나만 가져간다 — 나머지는 그쪽 몫
                bool shared = HeldByAnother(root, anomaly, active);
                bool whole = !shared && renderers.Length > 0 && changedRenderers.Count == renderers.Length;
                var here = new List<Spot>();
                foreach (var renderer in changedRenderers)
                    SpotFor(here, root.transform, whole ? root.transform : renderer.transform).Renderers.Add(renderer);
                foreach (var light in changedLights)
                    SpotFor(here, root.transform, whole ? root.transform : light.transform).Lights.Add(light);
                if (shared && here.Count > 1) here.RemoveRange(1, here.Count - 1);
                found.AddRange(here);
            }
            return found;
        }

        private static bool HeldByAnother(GameObject root, Anomaly anomaly, List<Anomaly> active)
        {
            foreach (var other in active)
                if (other != anomaly && ReferencedObjects(other).Contains(root)) return true;
            return false;
        }

        private static Spot SpotFor(List<Spot> found, Transform root, Transform changed)
        {
            var part = changed;
            while (part != root && part.parent != null && part.parent != root) part = part.parent;
            foreach (var spot in found)
                if (spot.Root == root && spot.Part == part) return spot;
            var created = new Spot { Root = root, Part = part };
            found.Add(created);
            return created;
        }

        // 다른 이상현상 오브젝트 아래에 있거나, 다른 이상현상이 이미 "내가 바꾼 것"으로 찾아 둔 것
        private static bool BelongsToAnother(Component changed, Anomaly anomaly)
        {
            var owner = changed.GetComponentInParent<Anomaly>(true);
            if (owner != null && owner != anomaly) return true;
            foreach (var pair in spots)
            {
                if (pair.Key == anomaly) continue;
                foreach (var spot in pair.Value)
                    if (changed is Renderer renderer ? spot.Renderers.Contains(renderer) : spot.Lights.Contains(changed as Light)) return true;
            }
            return false;
        }

        // 이상현상(과 부모 클래스)의 칸에 든 씬 물체. 프리팹 에셋처럼 씬 밖의 것은 뺀다
        private static List<GameObject> ReferencedObjects(Anomaly anomaly)
        {
            var result = new List<GameObject>();
            foreach (var field in ObjectFields(anomaly.GetType()))
            {
                object value;
                try { value = field.GetValue(anomaly); }
                catch (Exception) { continue; }
                Collect(value, result);
            }
            return result;
        }

        private static void Collect(object value, List<GameObject> result)
        {
            GameObject found = null;
            if (value is GameObject gameObject) found = gameObject;
            else if (value is Component component) found = component != null ? component.gameObject : null;
            else if (value is IEnumerable items)
            {
                foreach (var item in items) Collect(item, result);
                return;
            }
            if (found != null && found.scene.IsValid() && !result.Contains(found)) result.Add(found);
        }

        private static FieldInfo[] ObjectFields(Type type)
        {
            if (objectFields.TryGetValue(type, out var cached)) return cached;
            var fields = new List<FieldInfo>();
            for (var current = type; current != null && current != typeof(MonoBehaviour); current = current.BaseType)
                foreach (var field in current.GetFields(AnyField))
                    if (HoldsSceneObjects(field.FieldType)) fields.Add(field);
            return objectFields[type] = fields.ToArray();
        }

        private static bool HoldsSceneObjects(Type type)
        {
            if (typeof(GameObject).IsAssignableFrom(type) || typeof(Component).IsAssignableFrom(type)) return true;
            if (type.IsArray) return HoldsSceneObjects(type.GetElementType());
            return type.IsGenericType && type.GetGenericArguments().Length == 1 &&
                   typeof(IEnumerable).IsAssignableFrom(type) && HoldsSceneObjects(type.GetGenericArguments()[0]);
        }

        // 찍을 곳을 못 찾은 이상현상은 플레이어가 통과할 수 없게 만들지 않도록 요구에서 빼고, 만든 사람에게 한 번 알린다 (F1 "사진"에도 보임)
        private static void WarnOnce(Anomaly anomaly)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (warned.Add(anomaly))
                Debug.LogWarning($"[Photo] {anomaly.name}: 찍을 곳을 못 찾아 판정에서 뺌 — 자기 아래에 켜진 렌더러가 없고, 칸에 든 물체 중 모습이 달라진 것도 없음. " +
                                 "바꾼 물체를 칸(필드)에 들고 있으면 저절로 찾고, 보이는 물체가 없는 자리면 PhotoTarget을 둔다", anomaly);
#endif
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            warned.Clear();
            Forget();
        }
    }
}
