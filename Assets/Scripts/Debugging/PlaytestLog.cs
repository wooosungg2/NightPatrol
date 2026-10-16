using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.IO;
using System.Text;
using NightPatrol.Interaction;
using NightPatrol.Journal;
using NightPatrol.Run;
#endif

namespace NightPatrol.Debugging
{
    /// <summary>
    /// 플레이테스트 기록 (재미 검증용, 기획서 3.4·7.3). 층마다 한 줄씩 CSV로 남긴다:
    /// 회차, 층, 결과(통과·실패·중단, 개발자 키로 넘기면 키 이름), 걸린 시간, E 누른 횟수, 일지 본 횟수, 점검 항목.
    /// 에디터·개발 빌드에서만 기록한다. 파일 위치: persistentDataPath/playtest/ (개발자 메뉴 F5로 폴더 열기)
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlaytestLog : MonoBehaviour
    {
        // 릴리스 빌드에서는 기록하지 않아 안 쓰이는 필드가 있다. 직렬화 구조는 빌드마다 같아야 해서 #if 밖에 둔다
#pragma warning disable 0414, 0649
        [Tooltip("끄면 파일은 안 쓰고 메모리(Rows)에만 쌓음 (테스트용)")]
        [SerializeField] private bool writeFile = true;
        private readonly List<string> rows = new List<string>();
        private bool open;
        private float segmentStart;
#pragma warning restore 0414, 0649

        /// <summary>지금까지 쓴 줄 (머리줄 제외)</summary>
        public IReadOnlyList<string> Rows => rows;
        /// <summary>지금 층에서 보낸 시간(초). 기록 중이 아니면 0</summary>
        public float CurrentFloorSeconds => open ? Time.time - segmentStart : 0f;
        /// <summary>
        /// null이 아니면 이때 끝나는 층 기록의 결과 칸을 "통과"·"실패" 대신 이 글로 적는다.
        /// 개발자 메뉴가 키를 처리하는 동안만 넣는다 (예: "F4 재시작") — 개발자 키로 넘긴 층이 실제 결과에 섞이지 않게
        /// </summary>
        public string ResultOverride { get; set; }

        /// <summary>CSV 한 줄: 쉼표·따옴표·줄바꿈이 있는 칸은 따옴표로 감싸고, 따옴표는 두 번 쓴다</summary>
        public static string CsvRow(params string[] cells)
        {
            if (cells == null) return string.Empty;
            return string.Join(",", cells.Select(cell =>
            {
                if (string.IsNullOrEmpty(cell)) return string.Empty;
                return cell.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0
                    ? "\"" + cell.Replace("\"", "\"\"") + "\"" : cell;
            }));
        }

        /// <summary>초를 소수 첫째 자리까지 (컴퓨터 언어 설정과 상관없이 소수점은 ".")</summary>
        public static string Seconds(float seconds) => seconds.ToString("F1", CultureInfo.InvariantCulture);

        /// <summary>초를 "분:초"로 (F1 표시용)</summary>
        public static string Clock(float seconds)
        {
            int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return $"{total / 60}:{total % 60:00}";
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private PatrolRun run;
        private FloorTasks tasks;
        private PlayerInteractor interactor;
        private JournalHolder journal;
        private PatrolRun subscribedRun;
        private FloorTasks subscribedTasks;
        private PlayerInteractor subscribedInteractor;
        private JournalHolder subscribedJournal;
        private int attempt, floor, presses, journalViews;
        private string taskText = string.Empty;
        private float runStart;
        private string path;

        private void Awake()
        {
            run = Object.FindAnyObjectByType<PatrolRun>();
            tasks = Object.FindAnyObjectByType<FloorTasks>();
            interactor = GetComponent<PlayerInteractor>();
            if (interactor == null) interactor = Object.FindAnyObjectByType<PlayerInteractor>();
            journal = GetComponentInChildren<JournalHolder>(true);
            if (journal == null) journal = Object.FindAnyObjectByType<JournalHolder>();
            runStart = Time.time;
        }

        private void OnEnable()
        {
            if (run == null) return;
            subscribedRun = run;
            subscribedRun.FloorStarted += OnFloorStarted;
            subscribedRun.Failed += OnFailed;
            subscribedRun.Cleared += OnCleared;
            if (tasks != null)
            {
                subscribedTasks = tasks;
                subscribedTasks.TasksChanged += OnTasksChanged;
            }
            if (interactor != null)
            {
                subscribedInteractor = interactor;
                subscribedInteractor.Interacted += OnInteracted;
            }
            if (journal != null)
            {
                subscribedJournal = journal;
                subscribedJournal.RaisedChanged += OnJournalRaised;
            }
        }

        private void OnDisable()
        {
            // Unity 오브젝트가 파괴된 뒤에도 C# 이벤트에서는 구독을 해제할 수 있다.
            if (!ReferenceEquals(subscribedRun, null))
            {
                subscribedRun.FloorStarted -= OnFloorStarted;
                subscribedRun.Failed -= OnFailed;
                subscribedRun.Cleared -= OnCleared;
            }
            if (!ReferenceEquals(subscribedTasks, null)) subscribedTasks.TasksChanged -= OnTasksChanged;
            if (!ReferenceEquals(subscribedInteractor, null)) subscribedInteractor.Interacted -= OnInteracted;
            if (!ReferenceEquals(subscribedJournal, null)) subscribedJournal.RaisedChanged -= OnJournalRaised;
            subscribedRun = null;
            subscribedTasks = null;
            subscribedInteractor = null;
            subscribedJournal = null;
            Close("중단");   // 테스터가 도중에 끄면 마지막 층(포기한 층) 기록이 남게
        }

        private void OnApplicationQuit() => Close("중단");

        private void OnFloorStarted(int startedFloor)
        {
            Close("통과");   // 앞 구간이 실패로 닫히지 않았다면 하행 성공
            open = true;
            attempt = run.Attempt;
            floor = startedFloor;
            segmentStart = Time.time;
            presses = 0;
            journalViews = 0;
            taskText = string.Empty;
        }

        private void OnFailed(int failedFloor) => Close("실패");

        private void OnCleared()
        {
            Close("통과");
            Write(CsvRow(run.Attempt.ToString(), run.CurrentFloor.ToString(), "클리어",
                Seconds(Time.time - runStart), "", "", ""));
        }

        private void OnTasksChanged(IReadOnlyList<PatrolTask> list)
        {
            if (open) taskText = string.Join(" / ", list.Where(t => t != null).Select(t => t.Label));
        }

        private void OnInteracted(IInteractable _)
        {
            if (open) presses++;
        }

        private void OnJournalRaised(bool raised)
        {
            if (open && raised) journalViews++;
        }

        private void Close(string result)
        {
            if (!open) return;
            open = false;
            Write(CsvRow(attempt.ToString(), floor.ToString(), ResultOverride ?? result, Seconds(Time.time - segmentStart),
                presses.ToString(), journalViews.ToString(), taskText));
        }

        private void Write(string row)
        {
            rows.Add(row);
            if (!writeFile) return;
            try
            {
                if (path == null)
                {
                    string folder = Path.Combine(Application.persistentDataPath, "playtest");
                    Directory.CreateDirectory(folder);
                    // 같은 초에 두 번 시작해도 덮어쓰지 않게 _1, _2를 붙인다
                    string stem = System.DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
                    path = Path.Combine(folder, stem + ".csv");
                    for (int i = 1; File.Exists(path); i++) path = Path.Combine(folder, $"{stem}_{i}.csv");
                    // BOM을 넣어야 엑셀에서 한글이 안 깨진다
                    File.WriteAllText(path, CsvRow("회차", "층", "결과", "소요(초)", "E 횟수", "일지 본 횟수", "점검 항목") + "\n",
                        new UTF8Encoding(true));
                    Debug.Log($"[Playtest] 기록 파일: {path}", this);
                }
                File.AppendAllText(path, row + "\n", new UTF8Encoding(false));
            }
            catch (System.Exception e)
            {
                writeFile = false;   // 파일을 못 쓰면 메모리에만 남기고 게임은 계속
                Debug.LogWarning($"[Playtest] 기록 파일을 쓸 수 없어 끕니다: {e.Message}", this);
            }
        }
#endif
    }
}
