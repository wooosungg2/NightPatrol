using System;
using System.Collections.Generic;
using NightPatrol.Debugging;
using UnityEditor;
using UnityEngine;

namespace NightPatrol.Editor
{
    // 메뉴 Night Patrol → 점검 사물 목록. 열린(활성) 씬의 점검 사물 Id·종류·상태(정상·비정상·이상현상·예외 지시·꺼짐·오류).
    // 줄을 누르면 그 사물을 선택. Play 중에만 "기본 상태로"(ResetToDefault — 예외 지시·이상현상은 안 지움, 층 재시작 F4와 다름)와
    // "비정상 만들기"(TrySetAbnormal). 에셋·씬 저장과 이상현상 지우기는 없음. 목록은 PatrolObjectCache (Scene 뷰 상태 색과 같이 씀)
    public sealed class PatrolObjectsWindow : EditorWindow
    {
        private string search = string.Empty;
        private bool problemsOnly;
        private Vector2 scroll;
        private List<PatrolObjectRow> view = new List<PatrolObjectRow>();
        private GUIStyle rowStyle;

        [MenuItem("Night Patrol/점검 사물 목록", priority = 1)]
        private static void Open() => GetWindow<PatrolObjectsWindow>("점검 사물 목록");

        private void OnEnable()
        {
            PatrolObjectCache.AddUser(this);
            PatrolObjectCache.Changed += Repaint;
        }

        private void OnDisable()
        {
            PatrolObjectCache.Changed -= Repaint;
            PatrolObjectCache.RemoveUser(this);
        }

        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("새로 고침", EditorStyles.toolbarButton, GUILayout.Width(60))) PatrolObjectCache.Refresh();
                problemsOnly = GUILayout.Toggle(problemsOnly, new GUIContent("문제만", "엘리베이터를 타면 걸리는 사물과 오류만"), EditorStyles.toolbarButton, GUILayout.Width(50));
                GUILayout.Space(6);
                search = EditorGUILayout.TextField(search, EditorStyles.toolbarSearchField);
            }
            if (PatrolObjectCache.Note.Length > 0)
            {
                EditorGUILayout.HelpBox(PatrolObjectCache.Note, MessageType.Info);
                return;
            }

            // Layout 때만 다시 거름: 같은 프레임의 Layout과 Repaint가 같은 줄 수를 그리게
            if (Event.current.type == EventType.Layout) view = PatrolObjectList.Filter(PatrolObjectCache.Rows, search, problemsOnly);

            string scene = string.IsNullOrEmpty(PatrolObjectCache.SceneName) ? "(저장 안 된 씬)" : PatrolObjectCache.SceneName;
            EditorGUILayout.LabelField($"씬 {scene}: {PatrolObjectList.Summary(PatrolObjectCache.Rows)}", EditorStyles.miniLabel);
            EditorGUILayout.LabelField(Application.isPlaying
                ? "기본 상태로 = ResetToDefault (예외 지시·이상현상은 그대로, 층 재시작 F4와 다름: 점검 항목도 그대로)"
                : "편집 중에는 씬에 저장된 상태 그대로입니다 (Play를 시작하면 층 시작 때 기본 상태로 바뀜). 상태를 바꾸는 버튼은 Play 중에만", EditorStyles.miniLabel);
            if (rowStyle == null) rowStyle = new GUIStyle(EditorStyles.label) { richText = false };

            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (var row in view) DrawRow(row);
            EditorGUILayout.EndScrollView();
        }

        private void DrawRow(PatrolObjectRow row)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var dot = GUILayoutUtility.GetRect(12f, 16f, GUILayout.Width(12f), GUILayout.Height(16f));
                EditorGUI.DrawRect(new Rect(dot.x + 2f, dot.y + 4f, 8f, 8f), PatrolColors.Of(PatrolObjectList.ColorOf(row)));
                string text = $"{(row.Id.Length > 0 ? row.Id : "(빈 Id)")}  ·  {row.TypeName}  ·  {PatrolObjectList.StateText(row)}";
                if (GUILayout.Button(new GUIContent(text, row.Path), rowStyle)) Select(row);
                if (Application.isPlaying && row.Target != null)
                {
                    if (GUILayout.Button(new GUIContent("기본 상태로", "ResetToDefault — 예외 지시·이상현상은 안 지움. 층 재시작(F4)과 다름: 점검 항목은 그대로"),
                            EditorStyles.miniButtonLeft, GUILayout.Width(72)))
                        ResetToDefault(row);
                    if (GUILayout.Button(new GUIContent("비정상 만들기", "TrySetAbnormal — 지원하지 않는 사물은 그대로"),
                            EditorStyles.miniButtonRight, GUILayout.Width(84)))
                        MakeAbnormal(row);
                }
            }
        }

        private static void Select(PatrolObjectRow row)
        {
            if (row.Target == null) return;
            Selection.activeObject = row.Target.gameObject;
            EditorGUIUtility.PingObject(row.Target.gameObject);
        }

        /// <summary>Play 중에만: 사물의 ResetToDefault (예외 지시·이상현상은 그대로)</summary>
        internal void ResetToDefault(PatrolObjectRow row)
        {
            if (!Application.isPlaying || row?.Target == null) return;
            try { row.Target.ResetToDefault(); }
            catch (Exception e) { Debug.LogException(e, row.Target); }
            PatrolObjectCache.ReadNow(row);
        }

        /// <summary>Play 중에만: 사물의 TrySetAbnormal. 지원하지 않거나 못 바꾸면 false (창에 알림)</summary>
        internal bool MakeAbnormal(PatrolObjectRow row)
        {
            if (!Application.isPlaying || row?.Target == null) return false;
            bool ok = false;
            try { ok = row.Target.TrySetAbnormal(); }
            catch (Exception e) { Debug.LogException(e, row.Target); }
            if (!ok) ShowNotification(new GUIContent($"{row.Id}: 비정상으로 못 바꿈 (지원 안 함 또는 이미 비정상)"));
            PatrolObjectCache.ReadNow(row);
            return ok;
        }
    }
}
