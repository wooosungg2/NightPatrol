using System.Collections.Generic;
using System.Linq;
using NightPatrol.Debugging;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NightPatrol.Editor
{
    // 메뉴 Night Patrol → 씬 점검. 열린(활성) 씬 하나를 읽기만 한다: 씬·에셋을 바꾸지 않고 TrySetAbnormal도 부르지 않는다.
    // 점검 로직은 런타임 어셈블리의 SceneCheck (EditMode 테스트). 이 창은 결과를 보여 주고 줄을 누르면 그 오브젝트를 선택만 한다.
    // 이 네임스페이스 안에서 UnityEditor.Editor 클래스를 쓸 때는 전체 이름으로 (NightPatrol.Editor와 이름이 같음)
    public sealed class SceneCheckWindow : EditorWindow
    {
        private List<CheckIssue> issues = new List<CheckIssue>();
        private string checkedScene = string.Empty;
        private string note = "다시 점검을 누르면 열린 씬을 점검합니다";
        private bool showInfo = true;
        private Vector2 scroll;
        private GUIStyle rowStyle;

        [MenuItem("Night Patrol/씬 점검", priority = 0)]
        private static void Open()
        {
            var window = GetWindow<SceneCheckWindow>("씬 점검");
            window.Run();
        }

        private void Run()
        {
            issues = new List<CheckIssue>();
            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                checkedScene = string.Empty;
                note = "프리팹 편집 화면에서는 점검하지 않음 — 씬으로 돌아간 뒤 다시 점검";
                return;
            }
            var facts = SceneCheck.Gather(SceneManager.GetActiveScene());
            checkedScene = facts.SceneName;
            issues = SceneCheck.Check(facts);
            note = string.Empty;
        }

        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("다시 점검", EditorStyles.toolbarButton, GUILayout.Width(70))) Run();
                showInfo = GUILayout.Toggle(showInfo, "정보", EditorStyles.toolbarButton, GUILayout.Width(40));
                GUILayout.FlexibleSpace();
                GUILayout.Label($"오류 {Count(CheckLevel.Error)} · 경고 {Count(CheckLevel.Warning)} · 정보 {Count(CheckLevel.Info)}", EditorStyles.miniLabel);
            }
            if (note.Length > 0)
            {
                EditorGUILayout.HelpBox(note, MessageType.Info);
                return;
            }
            EditorGUILayout.LabelField("씬", string.IsNullOrEmpty(checkedScene) ? "(저장 안 된 씬)" : checkedScene);
            EditorGUILayout.LabelField("줄을 누르면 그 오브젝트를 선택합니다. 씬은 바꾸지 않습니다", EditorStyles.miniLabel);
            if (rowStyle == null) rowStyle = new GUIStyle(EditorStyles.label) { wordWrap = true, richText = false };
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (var issue in issues)
            {
                if (issue.Level == CheckLevel.Info && !showInfo) continue;
                if (GUILayout.Button(new GUIContent(issue.Message, Icon(issue.Level)), rowStyle) && issue.Target != null)
                {
                    var target = issue.Target is Component component ? component.gameObject : issue.Target;
                    Selection.activeObject = target;
                    EditorGUIUtility.PingObject(target);
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private int Count(CheckLevel level) => issues.Count(i => i.Level == level);

        private static Texture Icon(CheckLevel level) => EditorGUIUtility.IconContent(
            level == CheckLevel.Error ? "console.erroricon.sml" :
            level == CheckLevel.Warning ? "console.warnicon.sml" : "console.infoicon.sml").image;
    }
}
