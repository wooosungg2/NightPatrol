using System;
using System.Collections.Generic;
using System.Linq;
using NightPatrol.Patrol;

namespace NightPatrol.Debugging
{
    /// <summary>점검 사물 한 줄 (메뉴 Night Patrol → 점검 사물 목록, Scene 뷰 상태 색)</summary>
    public sealed class PatrolObjectRow
    {
        public string Id = string.Empty;
        public string TypeName = string.Empty;
        public string Path = string.Empty;
        /// <summary>오브젝트가 켜져 있고 컴포넌트도 켜짐. 꺼진 사물은 판정에 안 들어감</summary>
        public bool Active;
        public bool Normal;
        public bool Anomaly;
        public bool Exception;
        /// <summary>판정 통과 (PatrolObject.IsClear)</summary>
        public bool Clear;
        /// <summary>상태를 읽다가 난 오류 (없으면 null). 그 줄만 오류로 보이고 다른 줄은 그대로</summary>
        public string Error;
        public PatrolObject Target;
        /// <summary>Scene 뷰 표시 위치: 켜진 렌더러가 있으면 그 중심, 없으면 사물 위치</summary>
        public UnityEngine.Vector3 Position;
    }

    /// <summary>Scene 뷰 상태 색: 초록 정상 / 빨강 비정상 / 노랑 예외·이상현상 / 회색 꺼짐 / 자홍 오류</summary>
    public enum PatrolColor { Normal, Abnormal, Flagged, Off, Error }

    /// <summary>점검 사물 목록의 순수 함수 (EditMode 테스트용). 사물을 바꾸지 않고 읽기만 한다</summary>
    public static class PatrolObjectList
    {
        /// <summary>씬 점검이 모은 사물 하나로 줄을 만들고 상태를 읽는다</summary>
        public static PatrolObjectRow Row(PatrolFact fact)
        {
            var row = new PatrolObjectRow
            {
                Id = fact?.Id ?? string.Empty,
                Path = fact?.Path ?? string.Empty,
                Active = fact != null && fact.Active,
                Target = fact?.Target as PatrolObject,
            };
            row.TypeName = row.Target != null ? row.Target.GetType().Name : string.Empty;
            Read(row);
            return row;
        }

        /// <summary>상태만 다시 읽는다. 사물 코드에서 오류가 나면 그 줄의 Error에 적고 넘어간다</summary>
        public static void Read(PatrolObjectRow row)
        {
            if (row == null) return;
            row.Error = null;
            var obj = row.Target;
            if (obj == null)
            {
                row.Error = "사물이 사라짐";
                return;
            }
            try
            {
                row.Id = obj.Id ?? string.Empty;
                row.Active = obj.enabled && obj.gameObject.activeInHierarchy;   // 씬 점검과 같은 기준 (편집 중에도)
                row.Anomaly = obj.HasAnomaly;
                row.Exception = obj.IsException;
                row.Normal = obj.IsNormal;
                row.Clear = obj.IsClear;
                var renderer = obj.GetComponentInChildren<UnityEngine.Renderer>();
                row.Position = renderer != null && renderer.enabled ? renderer.bounds.center : obj.transform.position;
            }
            catch (Exception e)
            {
                row.Error = $"{e.GetType().Name}: {e.Message}";
            }
        }

        /// <summary>상태 글: "정상", "비정상", "비정상 · 예외 지시 → 통과", "정상 · 이상현상 → 걸림", "꺼짐 (판정 안 함)", "오류: …"</summary>
        public static string StateText(PatrolObjectRow row)
        {
            if (row == null) return string.Empty;
            if (row.Error != null) return "오류: " + row.Error;
            if (!row.Active) return "꺼짐 (판정 안 함)";
            var parts = new List<string> { row.Normal ? "정상" : "비정상" };
            if (row.Exception) parts.Add("예외 지시");
            if (row.Anomaly) parts.Add("이상현상");
            string text = string.Join(" · ", parts);
            return row.Exception || row.Anomaly ? $"{text} → {(row.Clear ? "통과" : "걸림")}" : text;
        }

        public static PatrolColor ColorOf(PatrolObjectRow row)
        {
            if (row == null || row.Error != null) return PatrolColor.Error;
            if (!row.Active) return PatrolColor.Off;
            if (row.Exception || row.Anomaly) return PatrolColor.Flagged;
            return row.Normal ? PatrolColor.Normal : PatrolColor.Abnormal;
        }

        /// <summary>엘리베이터를 타면 걸리는 줄 (켜져 있고 통과 못 함) 또는 오류</summary>
        public static bool IsProblem(PatrolObjectRow row) => row != null && (row.Error != null || (row.Active && !row.Clear));

        /// <summary>Id 순(같으면 경로 순)으로, 검색어(Id·종류, 대소문자 무시)와 "문제만"으로 거른다</summary>
        public static List<PatrolObjectRow> Filter(IEnumerable<PatrolObjectRow> rows, string search, bool problemsOnly)
        {
            var list = (rows ?? Enumerable.Empty<PatrolObjectRow>()).Where(r => r != null);
            if (!string.IsNullOrWhiteSpace(search))
            {
                string s = search.Trim();
                list = list.Where(r => r.Id.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                       r.TypeName.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0);
            }
            if (problemsOnly) list = list.Where(IsProblem);
            return list.OrderBy(r => r.Id, StringComparer.Ordinal).ThenBy(r => r.Path, StringComparer.Ordinal).ToList();
        }

        /// <summary>예: "점검 사물 89개 (켜진 것 89개) · 판정에 걸림 3개 · 예외 0 · 이상현상 0 · 오류 0"</summary>
        public static string Summary(IReadOnlyCollection<PatrolObjectRow> rows)
        {
            rows = rows ?? Array.Empty<PatrolObjectRow>();
            int active = rows.Count(r => r.Error == null && r.Active);
            int failing = rows.Count(r => r.Error == null && r.Active && !r.Clear);
            int exceptions = rows.Count(r => r.Error == null && r.Active && r.Exception);
            int anomalies = rows.Count(r => r.Error == null && r.Active && r.Anomaly);
            int errors = rows.Count(r => r.Error != null);
            return $"점검 사물 {rows.Count}개 (켜진 것 {active}개) · 판정에 걸림 {failing}개 · 예외 {exceptions} · 이상현상 {anomalies} · 오류 {errors}";
        }
    }
}
