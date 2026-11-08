using System.Globalization;
using System.Text;
using NightPatrol.Run;
using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine.InputSystem;
#endif

namespace NightPatrol.Debugging
{
    /// <summary>
    /// 개발자 키 (에디터·개발 빌드만). F1 개발자 정보 표시(InteractionDebugView)가 켜진 동안에만 동작한다 —
    /// 플레이테스트 빌드에서 테스터가 F3(강제 실패)·F5(폴더 열기)를 실수로 누르지 않게.
    /// 키 안내는 F1 패널의 "개발자 키" 구역으로 보인다 (DevInfo)
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DevMenu : MonoBehaviour
    {
#pragma warning disable 0414   // 릴리스 빌드에서는 안 쓰지만 직렬화 구조를 빌드마다 같게 두려고 #if 밖에 둔다
        [Tooltip("F1 패널에 개발자 키 안내 구역을 넣을지")]
        [SerializeField] private bool showHelp = true;
#pragma warning restore 0414

        /// <summary>F1 패널의 개발자 키 안내 (PatrolRun이 없으면 F5만)</summary>
        public static string HelpText(bool hasRun) => hasRun
            ? "F1 표시 전환 · F2 하행 · F3 실패 · F4 층 재시작\nF5 기록 폴더 · F6 예약 바로 실행 · PgUp/PgDn 층 이동"
            : "F1 표시 전환 · F5 기록 폴더  (PatrolRun 없음)";

        private const int MaxScheduleLines = 5;

        /// <summary>F1 "예약" 구역: 구간·이번 구간에서 흐른 시간, 남은 예약을 남은 시간 순으로 5줄까지</summary>
        public static string ScheduleText(FloorSchedule schedule)
        {
            if (schedule == null) return "PatrolRun 없음";
            var text = new StringBuilder($"구간 {schedule.Segment} · {PlaytestLog.Clock(schedule.Elapsed)}");
            var pending = schedule.Snapshot();
            if (pending.Count == 0) return text.Append("\n없음").ToString();
            int shown = Mathf.Min(MaxScheduleLines, pending.Count);
            for (int i = 0; i < shown; i++)
            {
                string label = string.IsNullOrEmpty(pending[i].Label) ? "이름 없음" : pending[i].Label;
                text.Append('\n').Append(label).Append(' ')
                    .Append(pending[i].Remaining.ToString("F1", CultureInfo.InvariantCulture)).Append('초');
            }
            if (pending.Count > shown) text.Append($"\n외 {pending.Count - shown}개");
            return text.ToString();
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private PatrolRun run;
        private FloorTransition transition;
        private PlaytestLog playtest;
        private InteractionDebugView debugView;

        /// <summary>개발자 키가 동작하는지: F1 디버그 표시가 켜져 있을 때만</summary>
        public bool KeysActive => debugView != null && debugView.IsVisible;

        private void Awake()
        {
            run = UnityEngine.Object.FindAnyObjectByType<PatrolRun>();
            transition = UnityEngine.Object.FindAnyObjectByType<FloorTransition>();
            playtest = UnityEngine.Object.FindAnyObjectByType<PlaytestLog>();
            debugView = GetComponent<InteractionDebugView>();
            if (debugView == null) debugView = UnityEngine.Object.FindAnyObjectByType<InteractionDebugView>();
        }

        private void OnEnable()
        {
            if (showHelp) DevInfo.Register(this, "개발자 키", () => HelpText(run != null), 200);
            if (run != null) DevInfo.Register(this, "예약", () => ScheduleText(run != null ? run.Schedule : null), 210);
        }

        private void OnDisable() => DevInfo.Unregister(this);

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || !KeysActive) return;
            if (keyboard.f5Key.wasPressedThisFrame)
            {
                // logs/(게임 로그)와 playtest/(플레이테스트 기록)가 있는 폴더
                Debug.Log($"[Dev] 기록 폴더 열기: {Application.persistentDataPath}", this);
                Application.OpenURL("file:///" + Application.persistentDataPath);
            }
            if (run == null) return;
            // 층 이동 연출 중에 층을 바꾸면 연출이 끝날 때 판정이 엉키므로 기다린다.
            // ESC 메뉴가 열린 동안(시간 정지)도: 실패 연출의 검은 화면이 메뉴를 덮어 버림
            if ((transition != null && transition.IsBusy) || Time.timeScale == 0f) return;
            if (keyboard.f2Key.wasPressedThisFrame)
            {
                Debug.Log("[Dev] 판정 무시 하행", this);
                AsDev("F2 하행", () => run.Descend(true));
            }
            if (keyboard.f3Key.wasPressedThisFrame)
            {
                Debug.Log("[Dev] 강제 실패", this);
                AsDev("F3 실패", run.Fail);
            }
            if (keyboard.f4Key.wasPressedThisFrame)
            {
                Debug.Log("[Dev] 현재 층 다시 시작", this);
                AsDev("F4 재시작", run.RestartFloor);
            }
            if (keyboard.f6Key.wasPressedThisFrame)
            {
                // 실행한 예약은 지워져 두 번 돌지 않는다. 이 로그가 게임 로그에 개발 개입으로 남는다
                string label = run.Schedule.RunNext();
                Debug.Log(label != null ? $"[Dev] 예약 바로 실행: {label}" : "[Dev] 남은 예약 없음", this);
            }
            if (keyboard.pageUpKey.wasPressedThisFrame) Jump(-1);
            if (keyboard.pageDownKey.wasPressedThisFrame) Jump(1);
        }

        private void Jump(int steps)
        {
            int before = run.CurrentFloor;
            AsDev("층 이동", () => run.JumpBy(steps));
            if (run.CurrentFloor != before) Debug.Log($"[Dev] 층 이동 → {run.CurrentFloor}층", this);
        }

        // 개발자 키로 끝난 층은 플레이테스트 기록에 "통과"·"실패" 대신 키 이름으로 남긴다
        private void AsDev(string result, System.Action action)
        {
            if (playtest != null) playtest.ResultOverride = result;
            try { action(); }
            finally
            {
                if (playtest != null) playtest.ResultOverride = null;
            }
        }
#endif
    }
}
