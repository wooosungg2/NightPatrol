using NightPatrol.Run;
using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine.InputSystem;
#endif

namespace NightPatrol.Debugging
{
    [DisallowMultipleComponent]
    public sealed class DevMenu : MonoBehaviour
    {
        [SerializeField] private bool showHelp = true;
        private PatrolRun run;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void Awake() => run = UnityEngine.Object.FindFirstObjectByType<PatrolRun>();

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (run == null || keyboard == null) return;
            if (keyboard.f2Key.wasPressedThisFrame)
            {
                Debug.Log("[Dev] 판정 무시 하행", this);
                run.Descend(true);
            }
            if (keyboard.f3Key.wasPressedThisFrame)
            {
                Debug.Log("[Dev] 강제 실패", this);
                run.Fail();
            }
            if (keyboard.f4Key.wasPressedThisFrame)
            {
                Debug.Log("[Dev] 현재 층 사물 초기화", this);
                run.ResetAllObjects();
            }
            if (keyboard.pageUpKey.wasPressedThisFrame)
            {
                run.JumpToFloor(run.CurrentFloor + 1);
                Debug.Log($"[Dev] 층 이동 → {run.CurrentFloor}층", this);
            }
            if (keyboard.pageDownKey.wasPressedThisFrame)
            {
                run.JumpToFloor(run.CurrentFloor - 1);
                Debug.Log($"[Dev] 층 이동 → {run.CurrentFloor}층", this);
            }
        }

        private void OnGUI()
        {
            if (!showHelp) return;
            string help = run == null ? "[DEV] PatrolRun 없음"
                : "[DEV] F2 하행  F3 실패  F4 초기화  PgUp/PgDn 층 이동";
            GUI.Label(new Rect(12f, Screen.height - 30f, Mathf.Max(100f, Screen.width - 24f), 24f), help);
        }
#endif
    }
}
