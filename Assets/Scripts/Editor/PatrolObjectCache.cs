using System;
using System.Collections.Generic;
using System.Linq;
using NightPatrol.Debugging;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NightPatrol.Editor
{
    // 점검 사물 목록 창과 Scene 뷰 상태 색이 같이 쓰는 캐시.
    // 사물 찾기(활성 씬 전체, 씬 점검의 Gather)는 Hierarchy·활성 씬이 바뀔 때와 새로 고침 때만 (최소 0.5초 간격),
    // 상태 읽기는 찾아 둔 사물만 0.5초마다 (매 repaint 전체 검색 안 함). 읽기만 하고 사물·씬·에셋은 바꾸지 않는다.
    // 쓰는 곳이 없으면 이벤트를 떼어 둔다 (창을 여러 번 열고 닫아도 이벤트는 하나만)
    internal static class PatrolObjectCache
    {
        public const double Interval = 0.5;

        private static readonly HashSet<object> users = new HashSet<object>();
        private static List<PatrolObjectRow> rows = new List<PatrolObjectRow>();
        private static Scene scene;
        private static string note = string.Empty;
        private static bool listStale = true;
        private static double lastFind = double.NegativeInfinity;
        private static double lastRead = double.NegativeInfinity;

        /// <summary>목록이나 상태가 바뀌었을 때 (창·Scene 뷰가 다시 그림)</summary>
        public static event Action Changed;

        public static IReadOnlyList<PatrolObjectRow> Rows => rows;
        public static string SceneName => scene.IsValid() ? scene.name : string.Empty;
        /// <summary>목록을 만들지 않은 이유 (프리팹 편집 화면), 없으면 빈 문자열</summary>
        public static string Note => note;
        public static int UserCount => users.Count;

        public static void AddUser(object user)
        {
            if (user == null || !users.Add(user)) return;
            if (users.Count > 1) return;
            EditorApplication.hierarchyChanged += MarkStale;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.update += Tick;
            listStale = true;
            lastFind = double.NegativeInfinity;
        }

        public static void RemoveUser(object user)
        {
            if (user == null || !users.Remove(user) || users.Count > 0) return;
            EditorApplication.hierarchyChanged -= MarkStale;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.update -= Tick;
            rows = new List<PatrolObjectRow>();
        }

        /// <summary>지금 바로 다시 찾는다 (새로 고침 버튼)</summary>
        public static void Refresh()
        {
            listStale = true;
            lastFind = double.NegativeInfinity;
            Tick();
        }

        /// <summary>줄 하나의 상태만 바로 다시 읽는다 (버튼을 누른 뒤)</summary>
        public static void ReadNow(PatrolObjectRow row)
        {
            PatrolObjectList.Read(row);
            Changed?.Invoke();
        }

        private static void MarkStale() => listStale = true;

        // Play를 시작·끝내면 씬 오브젝트가 새로 만들어진다: 바로 다시 찾게 (간격 기다리지 않음)
        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            listStale = true;
            lastFind = double.NegativeInfinity;
        }

        private static void Tick()
        {
            double now = EditorApplication.timeSinceStartup;
            var active = SceneManager.GetActiveScene();
            if ((listStale || active != scene) && now - lastFind >= Interval)
            {
                Find(active, now);
                Changed?.Invoke();
            }
            else if (now - lastRead >= Interval)
            {
                lastRead = now;
                if (ReadAll()) Changed?.Invoke();
            }
        }

        private static void Find(Scene active, double now)
        {
            listStale = false;
            lastFind = lastRead = now;
            scene = active;
            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                rows = new List<PatrolObjectRow>();
                note = "프리팹 편집 화면에서는 목록을 만들지 않음 — 씬으로 돌아가면 보임";
                return;
            }
            note = string.Empty;
            rows = SceneCheck.Gather(active).Objects.Select(PatrolObjectList.Row).ToList();
        }

        // 상태 글이나 위치가 하나라도 바뀌었으면 true
        private static bool ReadAll()
        {
            bool changed = false;
            foreach (var row in rows)
            {
                string before = PatrolObjectList.StateText(row);
                Vector3 position = row.Position;
                PatrolObjectList.Read(row);
                if (PatrolObjectList.StateText(row) != before || row.Position != position) changed = true;
                if (row.Target == null) listStale = true;   // 사라진 사물이 있으면 다음 틱에 다시 찾기
            }
            return changed;
        }
    }

    /// <summary>상태 색 (목록 창의 점, Scene 뷰 표시가 같이 씀)</summary>
    internal static class PatrolColors
    {
        public static Color Of(PatrolColor color)
        {
            switch (color)
            {
                case PatrolColor.Normal: return new Color(0.25f, 0.8f, 0.3f);
                case PatrolColor.Abnormal: return new Color(0.92f, 0.25f, 0.2f);
                case PatrolColor.Flagged: return new Color(0.98f, 0.8f, 0.15f);
                case PatrolColor.Off: return new Color(0.55f, 0.55f, 0.55f);
                default: return new Color(0.85f, 0.3f, 0.85f);
            }
        }
    }
}
