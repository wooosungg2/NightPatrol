using System.Collections.Generic;
using System.Linq;
using NightPatrol.Debugging;
using NightPatrol.Run;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NightPatrol.Editor
{
    // 점검 항목 표 Inspector: 칸은 기본 Inspector 그대로 그리고, 아래에 "미리보기 (열린 씬)"을 붙인다.
    // 열린(활성) 씬을 읽기만 한다: 씬·에셋을 바꾸지 않고 TrySetAbnormal도 부르지 않는다 (층 시작과 같은 SceneCheck.DescribeRow).
    // 씬 사실은 캐시: Hierarchy·활성 씬이 바뀌거나 "다시 읽기"를 누를 때만 다시 모은다 (최소 0.5초 간격).
    // 줄 계산도 캐시: 표가 바뀌거나(EditorUtility.GetDirtyCount) 층·씬 사실이 바뀔 때만. 미리보기 층·접힘은 SessionState에 (에셋에 안 씀)
    [CustomEditor(typeof(TaskTable))]
    [CanEditMultipleObjects]
    public sealed class TaskTableEditor : UnityEditor.Editor
    {
        private const string FloorKey = "NightPatrol.TaskTablePreview.Floor";
        private const string OpenKey = "NightPatrol.TaskTablePreview.Open";
        private const double GatherInterval = 0.5;

        /// <summary>미리보기 한 줄</summary>
        internal sealed class PreviewRow
        {
            public string Text = string.Empty;
            public bool HasCandidate;
            public Object Target;
            public List<CheckIssue> Problems = new List<CheckIssue>();
        }

        private SceneFacts facts;
        private Scene factsScene;
        private int factsActive;
        private int factsVersion;
        private bool factsStale = true;
        private bool repaintQueued;
        private double lastGather = double.NegativeInfinity;

        private List<PreviewRow> rows;
        private List<CheckIssue> tableIssues = new List<CheckIssue>();
        private int rowsDirtyCount = -1;
        private int rowsFloor = -1;
        private int rowsFactsVersion = -1;

        private GUIStyle rowStyle;
        private GUIStyle problemStyle;

        private void OnEnable()
        {
            EditorApplication.hierarchyChanged += MarkStale;
            EditorApplication.update += Tick;
        }

        private void OnDisable()
        {
            EditorApplication.hierarchyChanged -= MarkStale;
            EditorApplication.update -= Tick;
        }

        private void MarkStale() => factsStale = true;

        // 바뀐 뒤 간격이 지나면 한 번만 다시 그린다 (다시 모으는 건 그릴 때)
        private void Tick()
        {
            if (!factsStale || repaintQueued || EditorApplication.timeSinceStartup - lastGather < GatherInterval) return;
            repaintQueued = true;
            Repaint();
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            if (targets.Length != 1) return;
            EditorGUILayout.Space();
            bool open = SessionState.GetBool(OpenKey, true);
            bool nowOpen = EditorGUILayout.Foldout(open, "미리보기 (열린 씬)", true);
            if (nowOpen != open) SessionState.SetBool(OpenKey, nowOpen);
            if (nowOpen) DrawPreview((TaskTable)target);
        }

        private void DrawPreview(TaskTable table)
        {
            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                EditorGUILayout.HelpBox("프리팹 편집 화면에서는 미리보기를 하지 않음 — 씬으로 돌아가면 보임", MessageType.Info);
                return;
            }

            int floor = SessionState.GetInt(FloorKey, SceneCheck.DefaultPreviewFloor);
            using (new EditorGUILayout.HorizontalScope())
            {
                int picked = EditorGUILayout.IntSlider("미리보기 층", floor, 1, 9);
                if (picked != floor) SessionState.SetInt(FloorKey, floor = picked);
                if (GUILayout.Button("다시 읽기", GUILayout.Width(70)))
                {
                    factsStale = true;
                    lastGather = double.NegativeInfinity;
                    rowsDirtyCount = -1;
                }
            }

            // Layout 때만 갱신: 같은 프레임의 Layout과 Repaint가 같은 줄 수를 그리게
            if (Event.current.type == EventType.Layout || rows == null)
            {
                RefreshFacts();
                RefreshRows(table, floor);
            }

            if (rowStyle == null)
            {
                rowStyle = new GUIStyle(EditorStyles.label) { wordWrap = true, richText = false };
                problemStyle = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true, richText = false };
            }
            string scene = string.IsNullOrEmpty(facts.SceneName) ? "(저장 안 된 씬)" : facts.SceneName;
            EditorGUILayout.LabelField($"열린 씬 {scene}: 점검 사물 {facts.Objects.Count}개 (켜진 것 {factsActive}개)", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("층 시작 때처럼 맞춰 보기만 함 (사물은 안 바뀜). 줄을 누르면 예시 대상을 Hierarchy에서 깜빡임", EditorStyles.miniLabel);
            if (facts.Objects.Count == 0)
                EditorGUILayout.HelpBox("열린 씬에 점검 사물이 없음 — 이 표를 쓰는 씬을 열면 줄마다 맞는 사물이 나옴", MessageType.Info);

            foreach (var row in rows)
            {
                var content = new GUIContent(row.Text, row.HasCandidate ? null : Icon(CheckLevel.Warning));
                if (GUILayout.Button(content, rowStyle)) Ping(row.Target);
                foreach (var problem in row.Problems) Issue(problem);
            }
            foreach (var issue in tableIssues) Issue(issue);
        }

        private void Issue(CheckIssue issue)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(16);
                if (GUILayout.Button(new GUIContent(issue.Message, Icon(issue.Level)), problemStyle)) Ping(issue.Target);
            }
        }

        private void RefreshFacts()
        {
            var scene = SceneManager.GetActiveScene();
            bool sceneChanged = facts == null || scene != factsScene;
            bool due = factsStale && EditorApplication.timeSinceStartup - lastGather >= GatherInterval;
            if (!sceneChanged && !due) return;
            facts = SceneCheck.Gather(scene);
            factsScene = scene;
            factsActive = facts.Objects.Count(o => o.Active);
            factsVersion++;
            factsStale = false;
            repaintQueued = false;
            lastGather = EditorApplication.timeSinceStartup;
        }

        private void RefreshRows(TaskTable table, int floor)
        {
            int dirty = EditorUtility.GetDirtyCount(table);
            if (rows != null && dirty == rowsDirtyCount && floor == rowsFloor && factsVersion == rowsFactsVersion) return;
            rows = BuildRows(table, facts.Objects, floor);
            tableIssues = SceneCheck.TableIssues(table.name, table.Entries, table.AreaNames, table);
            rowsDirtyCount = dirty;
            rowsFloor = floor;
            rowsFactsVersion = factsVersion;
        }

        /// <summary>표의 줄마다 미리보기 한 줄 (그리기와 따로 확인할 수 있게)</summary>
        internal static List<PreviewRow> BuildRows(TaskTable table, IReadOnlyList<PatrolFact> objects, int floor)
        {
            var list = new List<PreviewRow>();
            if (table == null) return list;
            var entries = table.Entries;
            for (int i = 0; i < entries.Count; i++)
            {
                var info = SceneCheck.DescribeRow(entries[i], table.AreaNames, table.RoomFloor, objects, floor);
                list.Add(new PreviewRow
                {
                    Text = SceneCheck.RowSummary(i, entries[i], info),
                    HasCandidate = info.Active > 0,
                    Target = info.ExampleTarget,
                    Problems = info.Problems,
                });
            }
            return list;
        }

        // 선택은 바꾸지 않고 Hierarchy에서 깜빡이기만 (Inspector가 표에 머물게)
        private static void Ping(Object target)
        {
            if (target == null) return;
            EditorGUIUtility.PingObject(target is Component component ? component.gameObject : target);
        }

        private static Texture Icon(CheckLevel level) => EditorGUIUtility.IconContent(
            level == CheckLevel.Error ? "console.erroricon.sml" :
            level == CheckLevel.Warning ? "console.warnicon.sml" : "console.infoicon.sml").image;
    }
}
