using NightPatrol.Debugging;
using UnityEditor;
using UnityEngine;

namespace NightPatrol.Editor
{
    // 메뉴 Night Patrol → 점검 상태 표시 (기본 꺼짐): Scene 뷰에서 점검 사물마다 상태 색 점.
    // 초록 정상 / 빨강 비정상 / 노랑 예외 지시·이상현상 / 회색 꺼짐 / 자홍 오류. 정상이 아닌 사물에는 Id·상태 글도.
    // 목록은 PatrolObjectCache (점검 사물 목록 창과 같이 씀). 그리기만 하고 씬·에셋은 바꾸지 않는다.
    // 켜고 끈 상태는 이 PC의 EditorPrefs에만 (프로젝트 파일에 안 씀).
    // 이 네임스페이스 안에서 Menu는 NightPatrol.Menu(메뉴 화면 코드)로 잡히므로 UnityEditor.Menu로 쓴다
    [InitializeOnLoad]
    internal static class PatrolStateColors
    {
        internal const string MenuPath = "Night Patrol/점검 상태 표시";
        internal const string PrefKey = "NightPatrol.PatrolStateColors";
        private static readonly object User = new object();
        private static GUIStyle labelStyle;

        static PatrolStateColors()
        {
            EditorApplication.delayCall += () => SetEnabled(EditorPrefs.GetBool(PrefKey, false));
        }

        internal static bool Enabled { get; private set; }

        [MenuItem(MenuPath, priority = 2)]
        private static void Toggle()
        {
            EditorPrefs.SetBool(PrefKey, !Enabled);
            SetEnabled(!Enabled);
        }

        [MenuItem(MenuPath, true)]
        private static bool ToggleValidate()
        {
            UnityEditor.Menu.SetChecked(MenuPath, Enabled);
            return true;
        }

        internal static void SetEnabled(bool on)
        {
            if (on != Enabled)
            {
                Enabled = on;
                if (on)
                {
                    PatrolObjectCache.AddUser(User);
                    PatrolObjectCache.Changed += RepaintScenes;
                    SceneView.duringSceneGui += Draw;
                }
                else
                {
                    SceneView.duringSceneGui -= Draw;
                    PatrolObjectCache.Changed -= RepaintScenes;
                    PatrolObjectCache.RemoveUser(User);
                }
            }
            UnityEditor.Menu.SetChecked(MenuPath, on);
            RepaintScenes();
        }

        private static void RepaintScenes() => SceneView.RepaintAll();

        private static void Draw(SceneView view)
        {
            if (Event.current.type != EventType.Repaint) return;
            if (labelStyle == null)
            {
                labelStyle = new GUIStyle(EditorStyles.miniBoldLabel) { richText = false };
                labelStyle.normal.textColor = Color.white;
            }
            Color before = Handles.color;
            foreach (var row in PatrolObjectCache.Rows)
            {
                if (row.Target == null) continue;
                var color = PatrolObjectList.ColorOf(row);
                Vector3 position = row.Position;
                float size = HandleUtility.GetHandleSize(position) * 0.12f;
                Handles.color = PatrolColors.Of(color);
                Handles.SphereHandleCap(0, position, Quaternion.identity, size, EventType.Repaint);
                if (color != PatrolColor.Normal && color != PatrolColor.Off)
                    Handles.Label(position + view.camera.transform.up * size, $"{row.Id} {PatrolObjectList.StateText(row)}", labelStyle);
            }
            Handles.color = before;
        }
    }
}
