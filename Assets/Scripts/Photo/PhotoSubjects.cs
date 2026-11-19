using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NightPatrol.Anomalies;
using NightPatrol.Interaction;
using NightPatrol.Lighting;
using NightPatrol.Player;
using UnityEngine;

namespace NightPatrol.Photo
{
    /// <summary>지금 찍히는 이상현상 하나와 셔터 순간 부품 바운드.</summary>
    public sealed class PhotoSubject
    {
        public readonly Anomaly Source;
        public readonly IReadOnlyList<PhotoShape> Shapes;
        public readonly string Where;

        public PhotoSubject(Anomaly source, IReadOnlyList<PhotoShape> shapes, string where = "")
        {
            Source = source;
            Shapes = shapes;
            Where = where ?? "";
        }

        public string Name => Source != null ? Source.name : "?";
    }

    /// <summary>층 동안 바뀐 부품을 표시하고, 현재 보이는 부품과 한 번이라도 나타난 이상현상을 따로 관리한다.</summary>
    public static class PhotoSubjects
    {
        public const float RefreshSeconds = 0.1f;
        public const float InteractionSeconds = 2f;
        private const int MaxRenderersPerObject = 512;
        private const BindingFlags AnyField = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        private sealed class Spot
        {
            public Transform Root, Part;
            public readonly List<Renderer> Renderers = new List<Renderer>();
            public readonly List<Light> Lights = new List<Light>();
        }

        private static readonly Dictionary<Anomaly, List<Spot>> spots = new Dictionary<Anomaly, List<Spot>>();
        private static readonly Dictionary<Type, FieldInfo[]> objectFields = new Dictionary<Type, FieldInfo[]>();
        private static readonly HashSet<Anomaly> appeared = new HashSet<Anomaly>();
        private static readonly HashSet<Anomaly> warned = new HashSet<Anomaly>();
        private static readonly Dictionary<Component, float> suppressed = new Dictionary<Component, float>();
        private static readonly Dictionary<Transform, float> suppressedRoots = new Dictionary<Transform, float>();
        private static readonly HashSet<PlayerInteractor> interactors = new HashSet<PlayerInteractor>();
        private static readonly HashSet<LightZone> zones = new HashSet<LightZone>();
        private static LookBaseline baseline;
        private static bool listening;

        public static IReadOnlyCollection<Anomaly> Appeared => appeared;
        public static bool HasAppeared(Anomaly anomaly) => anomaly != null && appeared.Contains(anomaly);

        /// <summary>FloorStarted 직전 정상 모습을 저장한다. 이전 층 구독과 기록은 먼저 비운다.</summary>
        public static void BeginFloor()
        {
            Forget();
            baseline = LookBaseline.Capture();
        }

        /// <summary>FloorStarted 직후, 점검 항목이 물체를 바꾸기 전에 첫 표시를 한다.</summary>
        public static void FindChanged() => Refresh();

        /// <summary>점검 항목 생성 후, 표시되지 않은 부품만 정상 기준을 다시 적고 플레이어 조작 구독을 시작한다.</summary>
        public static void Settle()
        {
            if (baseline == null) return;
            foreach (var renderer in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include))
                if (!IsMarked(renderer)) baseline.Remember(renderer);
            foreach (var light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include))
                if (!IsMarked(light)) baseline.Remember(light);
            listening = true;
            SubscribeNewObjects();
        }

        /// <summary>0.1초마다와 셔터 직전에 호출한다. 필드·List와 바운드는 매번 현재 값으로 읽는다.</summary>
        public static List<PhotoSubject> Refresh()
        {
            var active = ActiveAnomalies();
            DropInactive(active);
            if (baseline != null)
            {
                if (listening) SubscribeNewObjects();
                FinishInteractions();
                baseline.PruneDestroyed();
                var references = new Dictionary<Anomaly, List<GameObject>>();
                foreach (var anomaly in active) references[anomaly] = ReferencedObjects(anomaly);
                foreach (var anomaly in active) MarkChanged(anomaly, references);
            }
            var result = BuildSubjects(active);
            foreach (var subject in result) appeared.Add(subject.Source);
            return result;
        }

        /// <summary>표시를 늘리지 않고 현재 바운드만 읽는다. missing에는 이번 층에 한 번도 안 나타난 것을 넣는다.</summary>
        public static List<PhotoSubject> Active(List<Anomaly> missing = null)
        {
            var active = ActiveAnomalies();
            DropInactive(active);
            if (missing != null)
                foreach (var anomaly in active)
                    if (!HasAppeared(anomaly)) missing.Add(anomaly);
            return BuildSubjects(active);
        }

        /// <summary>IsActive인 이상현상을 이름순으로 반환한다. 오브젝트 활성화 여부와 별개로 논리 상태를 따른다.</summary>
        public static List<Anomaly> ActiveAnomalies()
        {
            var result = new List<Anomaly>();
            foreach (var anomaly in UnityEngine.Object.FindObjectsByType<Anomaly>(FindObjectsInactive.Include))
                if (anomaly != null && anomaly.IsActive) result.Add(anomaly);
            result.Sort((a, b) =>
            {
                int name = StringComparer.Ordinal.Compare(a.name, b.name);
                return name != 0 ? name : a.GetEntityId().CompareTo(b.GetEntityId());
            });
            return result;
        }

        public static void WarnUnseen()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            foreach (var anomaly in ActiveAnomalies())
                if (!HasAppeared(anomaly) && warned.Add(anomaly))
                    Debug.LogWarning($"[Photo] {anomaly.name}: 이번 층에 찍힐 물체가 한 번도 없어 요구하지 않음", anomaly);
#endif
        }

        /// <summary>1층 도착·씬 종료 때 기록과 이벤트 구독을 모두 비운다.</summary>
        public static void Forget()
        {
            foreach (var interactor in interactors)
                if (!ReferenceEquals(interactor, null)) interactor.Interacted -= OnInteracted;
            foreach (var zone in zones)
                if (!ReferenceEquals(zone, null)) zone.Changed -= OnLightChanged;
            interactors.Clear();
            zones.Clear();
            spots.Clear();
            appeared.Clear();
            warned.Clear();
            suppressed.Clear();
            suppressedRoots.Clear();
            baseline = null;
            listening = false;
        }

        private static void SubscribeNewObjects()
        {
            interactors.RemoveWhere(item => item == null);
            zones.RemoveWhere(item => item == null);
            foreach (var interactor in UnityEngine.Object.FindObjectsByType<PlayerInteractor>(FindObjectsInactive.Include))
                if (interactors.Add(interactor)) interactor.Interacted += OnInteracted;
            foreach (var zone in UnityEngine.Object.FindObjectsByType<LightZone>(FindObjectsInactive.Include))
                if (zones.Add(zone)) zone.Changed += OnLightChanged;
        }

        private static void OnInteracted(IInteractable target)
        {
            if (target is Component component && component != null) Suppress(component.gameObject);
        }

        private static void OnLightChanged(LightZone zone, bool on)
        {
            if (zone != null) Suppress(zone.gameObject);
        }

        private static void Suppress(GameObject root)
        {
            if (baseline == null) return;
            float until = Time.time + InteractionSeconds;
            suppressedRoots[root.transform] = until;
            SuppressParts(root, until);
        }

        private static void SuppressParts(GameObject root, float until)
        {
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                SuppressPart(renderer, until);
            foreach (var light in root.GetComponentsInChildren<Light>(true))
                SuppressPart(light, until);
        }

        private static void SuppressPart(Component component, float until)
        {
            if (!IsMarked(component))
                suppressed[component] = suppressed.TryGetValue(component, out float previous) ? Mathf.Max(previous, until) : until;
        }

        private static void FinishInteractions()
        {
            // 유예 시간 안에 그 아래 새로 생긴 부품도 제외한다. 겹친 구역은 더 늦은 만료를 따른다.
            var expiredRoots = new List<Transform>();
            foreach (var pair in suppressedRoots)
            {
                if (pair.Key != null) SuppressParts(pair.Key.gameObject, pair.Value);
                if (pair.Key == null || Time.time >= pair.Value) expiredRoots.Add(pair.Key);
            }
            foreach (var root in expiredRoots) suppressedRoots.Remove(root);
            var expired = new List<Component>();
            foreach (var pair in suppressed)
            {
                if (pair.Key != null && Time.time < pair.Value) continue;
                if (pair.Key != null && !IsMarked(pair.Key))
                {
                    if (pair.Key is Renderer renderer) baseline.Remember(renderer);
                    else if (pair.Key is Light light) baseline.Remember(light);
                }
                expired.Add(pair.Key);
            }
            foreach (var component in expired) suppressed.Remove(component);
        }

        private static List<PhotoSubject> BuildSubjects(List<Anomaly> active)
        {
            var targets = new Dictionary<Anomaly, List<PhotoTarget>>();
            foreach (var target in UnityEngine.Object.FindObjectsByType<PhotoTarget>(FindObjectsInactive.Include))
            {
                if (target == null || target.Anomaly == null) continue;
                if (!targets.TryGetValue(target.Anomaly, out var list)) targets[target.Anomaly] = list = new List<PhotoTarget>();
                list.Add(target);
            }
            var result = new List<PhotoSubject>();
            foreach (var anomaly in active)
            {
                var shapes = new List<PhotoShape>();
                var names = new List<string>();
                var namedParts = new HashSet<Transform>();
                var ownRenderers = new HashSet<Renderer>();
                bool own = false, explicitTarget = false;
                foreach (var renderer in anomaly.GetComponentsInChildren<Renderer>(true))
                {
                    if (!Visible(renderer) || Excluded(renderer, anomaly)) continue;
                    shapes.Add(new PhotoShape(renderer.bounds, anomaly.transform));
                    ownRenderers.Add(renderer);
                    own = true;
                }
                if (spots.TryGetValue(anomaly, out var marked))
                {
                    foreach (var spot in marked)
                    {
                        bool visible = false;
                        foreach (var renderer in spot.Renderers)
                        {
                            if (!Visible(renderer) || Excluded(renderer, anomaly) || ownRenderers.Contains(renderer)) continue;
                            shapes.Add(new PhotoShape(renderer.bounds, spot.Root));
                            visible = true;
                        }
                        foreach (var light in spot.Lights)
                        {
                            if (light == null || !light.isActiveAndEnabled || Excluded(light, anomaly)) continue;
                            shapes.Add(new PhotoShape(new Bounds(light.transform.position, Vector3.one * 0.3f), spot.Root));
                            visible = true;
                        }
                        if (visible && spot.Part != null && namedParts.Add(spot.Part)) names.Add(spot.Part.name);
                    }
                }
                if (targets.TryGetValue(anomaly, out var assigned))
                {
                    foreach (var target in assigned) shapes.Add(target.Shape());
                    explicitTarget = true;
                }
                if (shapes.Count == 0) continue;
                var descriptions = new List<string>();
                if (own) descriptions.Add("자기 몸");
                if (names.Count > 0)
                    descriptions.Add(string.Join("·", names.GetRange(0, Math.Min(2, names.Count))) + (names.Count > 2 ? $" 외 {names.Count - 2}" : ""));
                if (explicitTarget) descriptions.Add("PhotoTarget");
                result.Add(new PhotoSubject(anomaly, shapes, string.Join(" / ", descriptions)));
            }
            return result;
        }

        private static bool Visible(Renderer renderer) => renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy;

        private static bool Excluded(Component component, Anomaly anomaly)
        {
            if (component == null || !component.gameObject.scene.IsValid()) return true;
            var player = PlayerControl.Current;
            if (player != null && component.transform.IsChildOf(player.transform)) return true;
            int firstPerson = LayerMask.NameToLayer("FirstPerson");
            if (firstPerson >= 0 && component.gameObject.layer == firstPerson) return true;
            var owner = component.GetComponentInParent<Anomaly>(true);
            return owner != null && owner != anomaly;
        }

        private static void DropInactive(List<Anomaly> active)
        {
            var stale = new List<Anomaly>();
            foreach (var pair in spots)
            {
                if (!active.Contains(pair.Key)) { stale.Add(pair.Key); continue; }
                foreach (var spot in pair.Value)
                {
                    spot.Renderers.RemoveAll(item => item == null);
                    spot.Lights.RemoveAll(item => item == null);
                }
                pair.Value.RemoveAll(spot => spot.Root == null || spot.Part == null || spot.Renderers.Count + spot.Lights.Count == 0);
            }
            foreach (var anomaly in stale) spots.Remove(anomaly);
        }

        private static void MarkChanged(Anomaly anomaly, Dictionary<Anomaly, List<GameObject>> references)
        {
            if (!spots.TryGetValue(anomaly, out var marked)) spots[anomaly] = marked = new List<Spot>();
            foreach (var root in references[anomaly])
            {
                var renderers = root.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length > MaxRenderersPerObject) continue;
                bool shared = false;
                foreach (var pair in references)
                    if (pair.Key != anomaly && pair.Value.Contains(root)) { shared = true; break; }
                var candidates = new List<Spot>();
                foreach (var renderer in renderers)
                    if (CanMark(renderer, anomaly) && baseline.Changed(renderer, true))
                        SpotFor(candidates, root.transform, renderer.transform).Renderers.Add(renderer);
                foreach (var light in root.GetComponentsInChildren<Light>(true))
                    if (CanMark(light, anomaly) && baseline.Changed(light))
                        SpotFor(candidates, root.transform, light.transform).Lights.Add(light);

                // 공용 칸은 이상현상마다 바로 아래 자식 하나만 맡는다. 기존 배정은 꺼질 때까지 유지한다.
                Transform assigned = null;
                if (shared)
                    foreach (var spot in marked)
                        if (spot.Root == root.transform) { assigned = spot.Part; break; }
                foreach (var candidate in candidates)
                {
                    if (shared)
                    {
                        if (AssignedToAnother(candidate.Root, candidate.Part, anomaly)) continue;
                        if (assigned != null && candidate.Part != assigned) continue;
                        assigned = candidate.Part;
                    }
                    var spot = SpotFor(marked, candidate.Root, candidate.Part);
                    spot.Renderers.AddRange(candidate.Renderers);
                    spot.Lights.AddRange(candidate.Lights);
                }
            }
        }

        private static bool CanMark(Component component, Anomaly anomaly) =>
            !Excluded(component, anomaly) && !suppressed.ContainsKey(component) && !IsMarked(component);

        private static bool IsMarked(Component component)
        {
            foreach (var list in spots.Values)
                foreach (var spot in list)
                    if (component is Renderer renderer ? spot.Renderers.Contains(renderer) : spot.Lights.Contains(component as Light)) return true;
            return false;
        }

        private static bool AssignedToAnother(Transform root, Transform part, Anomaly anomaly)
        {
            foreach (var pair in spots)
            {
                if (pair.Key == anomaly) continue;
                foreach (var spot in pair.Value)
                    if (spot.Root == root && spot.Part == part) return true;
            }
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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Forget();
    }
}
