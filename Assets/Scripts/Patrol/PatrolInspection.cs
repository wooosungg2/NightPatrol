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
                if (obj != null && obj.isActiveAndEnabled && obj.gameObject.activeInHierarchy && !obj.IsClear)
                    result.Add(obj);
            }
            result.Sort((a, b) => StringComparer.Ordinal.Compare(a.Id, b.Id));
            return result;
        }

        // Scene-wide inspection: do not call every frame.
        public static List<PatrolObject> FindAbnormal() => FindAbnormal(
            UnityEngine.Object.FindObjectsByType<PatrolObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None));

        public static bool IsAllClear() => FindAbnormal().Count == 0;
    }
}
