using System;
using System.Collections.Generic;
using UnityEngine;

namespace NightPatrol.Patrol
{
    public static class PatrolInspection
    {
        public static List<PatrolObject> FindAbnormal(IEnumerable<PatrolObject> objects)
        {
            var result = new List<PatrolObject>();
            if (objects == null) return result;
            foreach (var obj in objects)
            {
                if (obj != null && obj.isActiveAndEnabled && !obj.IsClear)
                    result.Add(obj);
            }
            result.Sort((a, b) => StringComparer.Ordinal.Compare(a.Id, b.Id));
            return result;
        }

        /// <summary>씬의 켜진 점검 사물 중 비정상인 것 (Id 순). 씬 전체를 검색하므로 매 프레임 부르지 말 것</summary>
        public static List<PatrolObject> FindAbnormal() => FindAbnormal(
            UnityEngine.Object.FindObjectsByType<PatrolObject>(FindObjectsInactive.Exclude));

        /// <summary>켜진 점검 사물이 전부 정상인지 (엘리베이터 판정)</summary>
        public static bool IsAllClear() => FindAbnormal().Count == 0;

        // Id → 사물. 기본은 켜진 사물만. Id가 빈 사물은 건너뛰고, 중복 Id는 경고 후 먼저 찾은 것만 쓴다
        public static Dictionary<string, PatrolObject> MapById(bool includeInactive = false)
        {
            var map = new Dictionary<string, PatrolObject>(StringComparer.Ordinal);
            var all = UnityEngine.Object.FindObjectsByType<PatrolObject>(
                includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude);
            foreach (var obj in all)
            {
                if (obj == null || string.IsNullOrEmpty(obj.Id)) continue;
                if (!includeInactive && !obj.isActiveAndEnabled) continue;
                if (map.TryGetValue(obj.Id, out var first))
                    Debug.LogWarning($"[Patrol] 중복 Id: {obj.Id} ({first.name}, {obj.name})", obj);
                else
                    map.Add(obj.Id, obj);
            }
            return map;
        }

        // 생성기가 만든 사물은 직접 참조하지 말고 Id로 찾는다 (Generate Floor를 다시 하면 참조가 끊김). 꺼진 사물도 찾는다
        public static PatrolObject FindById(string id) =>
            !string.IsNullOrEmpty(id) && MapById(true).TryGetValue(id, out var obj) ? obj : null;
    }
}
