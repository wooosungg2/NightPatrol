using System;
using System.Collections.Generic;
using UnityEngine;

namespace NightPatrol.Patrol
{
    public static class PatrolInspection
    {
        private static readonly HashSet<PatrolObject> loggedErrors = new HashSet<PatrolObject>();
        private static Dictionary<string, PatrolObject> idMap;

        public static List<PatrolObject> FindAbnormal(IEnumerable<PatrolObject> objects) => Find(objects, IsClear);

        /// <summary>씬의 켜진 점검 사물 중 비정상인 것, 이상현상이 걸린 것 포함 (Id 순). 씬 전체를 검색하므로 매 프레임 부르지 말 것</summary>
        public static List<PatrolObject> FindAbnormal() => FindAbnormal(AllActive());

        /// <summary>켜진 점검 사물이 전부 정상이고 이상현상도 없는지 (개발자 정보·테스트용. 엘리베이터 판정은 PatrolRun.Judge)</summary>
        public static bool IsAllClear() => FindAbnormal().Count == 0;

        /// <summary>엘리베이터 업무 판정: 일이 남은 점검 사물 (Id 순). 이상현상이 걸린 사물은 빠진다 (PatrolObject.IsTaskClear)</summary>
        public static List<PatrolObject> FindUnfinished(IEnumerable<PatrolObject> objects) => Find(objects, IsTaskClear);

        public static List<PatrolObject> FindUnfinished() => FindUnfinished(AllActive());

        private static PatrolObject[] AllActive() => UnityEngine.Object.FindObjectsByType<PatrolObject>(FindObjectsInactive.Exclude);

        private static List<PatrolObject> Find(IEnumerable<PatrolObject> objects, Func<PatrolObject, bool> clear)
        {
            var result = new List<PatrolObject>();
            if (objects == null) return result;
            foreach (var obj in objects)
            {
                if (obj != null && obj.isActiveAndEnabled && !clear(obj))
                    result.Add(obj);
            }
            result.Sort((a, b) => StringComparer.Ordinal.Compare(a.Id, b.Id));
            return result;
        }

        // 판정과 점검 후보 선별에서 같은 오류 경계를 쓴다. 오류 사물은 정상으로 보지 않는다.
        internal static bool IsClear(PatrolObject obj) => Guard(obj, o => o.IsClear);

        internal static bool IsTaskClear(PatrolObject obj) => Guard(obj, o => o.IsTaskClear);

        private static bool Guard(PatrolObject obj, Func<PatrolObject, bool> check)
        {
            if (obj == null) return false;
            try { return check(obj); }
            catch (Exception e)
            {
                if (loggedErrors.Add(obj)) Debug.LogException(e, obj);
                return false;
            }
        }

        internal static void ResetErrorLog() => loggedErrors.Clear();

        internal static void RefreshIdMap() => idMap = MapById(true);

        // Id → 사물. 기본은 켜진 사물만. Id가 빈 사물은 건너뛰고, 중복 Id는 경고 후 먼저 찾은 것만 쓴다
        public static Dictionary<string, PatrolObject> MapById(bool includeInactive = false, bool warnDuplicates = true)
        {
            var map = new Dictionary<string, PatrolObject>(StringComparer.Ordinal);
            var all = UnityEngine.Object.FindObjectsByType<PatrolObject>(
                includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude);
            foreach (var obj in all)
            {
                if (obj == null || string.IsNullOrEmpty(obj.Id)) continue;
                if (!includeInactive && !obj.isActiveAndEnabled) continue;
                if (map.TryGetValue(obj.Id, out var first))
                {
                    if (warnDuplicates)
                        Debug.LogWarning($"[Patrol] 중복 Id: {obj.Id} ({first.name}, {obj.name})", obj);
                }
                else
                    map.Add(obj.Id, obj);
            }
            return map;
        }

        // 생성기가 만든 사물은 직접 참조하지 말고 Id로 찾는다 (Generate Floor를 다시 하면 참조가 끊김). 꺼진 사물도 찾는다
        public static PatrolObject FindById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (idMap != null && idMap.TryGetValue(id, out var cached) && cached != null) return cached;
            idMap = MapById(true, warnDuplicates: false);
            return idMap.TryGetValue(id, out var obj) && obj != null ? obj : null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            ResetErrorLog();
            idMap = null;
        }
    }
}
