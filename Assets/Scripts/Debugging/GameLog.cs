using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace NightPatrol.Debugging
{
    /// <summary>
    /// 게임 로그 파일. 실행 한 번마다 콘솔 메시지(Log·경고·오류·예외)를 시각과 함께 파일 하나에 남긴다.
    /// 씬 세팅 없이 자동으로 시작한다 (에디터 Play, 개발·릴리스 빌드 모두).
    /// 위치: persistentDataPath/logs/game_날짜_시각.log — 개발자 메뉴 F5로 폴더를 연다.
    /// Unity의 Player.log는 실행할 때마다 덮어쓰고 줄마다 시각이 없어서 따로 둔다.
    /// </summary>
    public static class GameLog
    {
        /// <summary>남겨 둘 최근 파일 수 (이번 실행 포함)</summary>
        public const int KeepFiles = 20;
        /// <summary>파일 하나의 최대 크기. 넘으면 생략 줄을 쓰고 그만 쓴다 (매 프레임 오류 대비)</summary>
        public const long MaxBytes = 5L * 1024 * 1024;

        private static readonly object gate = new object();
        private static StreamWriter writer;
        private static Stopwatch clock;
        private static bool full;

        /// <summary>로그 폴더 (persistentDataPath/logs)</summary>
        public static string Folder { get; private set; }
        /// <summary>이번 실행의 로그 파일. 꺼져 있으면 null</summary>
        public static string FilePath { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Begin()
        {
            Stop();   // 에디터에서 도메인 리로드를 끄면 static이 남아 있으므로 먼저 정리
            try
            {
                Folder = Path.Combine(Application.persistentDataPath, "logs");
                Directory.CreateDirectory(Folder);
                var existing = Directory.GetFiles(Folder, "game_*.log").Select(Path.GetFileName);
                foreach (string old in FilesToDelete(existing, KeepFiles - 1))
                    TryDelete(Path.Combine(Folder, old));

                FilePath = NewFilePath(Folder, DateTime.Now);
                lock (gate)
                {
                    writer = new StreamWriter(FilePath, false, new UTF8Encoding(false)) { AutoFlush = true };
                    clock = Stopwatch.StartNew();
                    full = false;
                    writer.Write(Header());
                }
            }
            catch (Exception)
            {
                Stop();   // 폴더·파일을 못 만들면 조용히 끈다 (게임 진행에 영향 없음)
                return;
            }
            Application.logMessageReceivedThreaded += OnLog;
            Application.quitting += OnQuit;
            Debug.Log($"[Log] 기록 파일: {FilePath}");
        }

        // 다른 스레드에서도 불린다. 여기서 Debug.Log를 부르면 자기 자신이 다시 불리므로 절대 부르지 않는다
        private static void OnLog(string message, string stackTrace, LogType type)
        {
            lock (gate)
            {
                if (writer == null || full) return;
                try
                {
                    if (writer.BaseStream.Position > MaxBytes)
                    {
                        writer.Write("… 로그가 너무 커서 이후는 생략\n");
                        full = true;
                        return;
                    }
                    writer.Write(FormatLine(DateTime.Now, clock.Elapsed, type, message, stackTrace));
                }
                catch (Exception)
                {
                    CloseWriter();
                }
            }
        }

        private static void OnQuit()
        {
            lock (gate)
            {
                try
                {
                    if (writer != null && !full)
                        writer.Write(FormatLine(DateTime.Now, clock.Elapsed, LogType.Log, "세션 종료", null));
                }
                catch (Exception) { }
            }
            Stop();
        }

        private static void Stop()
        {
            Application.logMessageReceivedThreaded -= OnLog;
            Application.quitting -= OnQuit;
            lock (gate) CloseWriter();
        }

        private static void CloseWriter()
        {
            try { writer?.Dispose(); }
            catch (Exception) { }
            writer = null;
            FilePath = null;
        }

        private static void TryDelete(string path)
        {
            try { File.Delete(path); }
            catch (Exception) { }
        }

        // 같은 초에 두 번 시작하면 덮어쓰지 않게 _1, _2를 붙인다 (이름순 = 시간순 유지)
        private static string NewFilePath(string folder, DateTime now)
        {
            string stem = $"game_{now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture)}";
            string path = Path.Combine(folder, stem + ".log");
            for (int i = 1; File.Exists(path); i++) path = Path.Combine(folder, $"{stem}_{i}.log");
            return path;
        }

        private static string Header()
        {
            string build = Application.isEditor ? "에디터" : Debug.isDebugBuild ? "개발 빌드" : "릴리스 빌드";
            return "=== 야간순찰 게임 로그 ===\n" +
                   $"게임: {Application.productName} {Application.version}\n" +
                   $"Unity: {Application.unityVersion} ({build})\n" +
                   BuildStampLine() +
                   $"OS: {SystemInfo.operatingSystem}\n" +
                   $"CPU: {SystemInfo.processorType} ({SystemInfo.processorCount}코어)\n" +
                   $"GPU: {SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsMemorySize} MB)\n" +
                   $"메모리: {SystemInfo.systemMemorySize} MB\n" +
                   $"화면: {Screen.width}×{Screen.height}\n" +
                   $"시작: {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)}\n" +
                   "==========================\n";
        }

        // 빌드 메뉴가 실행 파일 옆에 남긴 build.txt의 첫 줄 (어느 빌드의 로그인지). 에디터이거나 파일이 없으면 줄 없음
        private static string BuildStampLine()
        {
            if (Application.isEditor) return string.Empty;
            try
            {
                string path = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? string.Empty, DevBuildPlan.InfoFileName);
                string stamp = File.Exists(path) ? DevBuildPlan.StampFromInfo(File.ReadAllText(path)) : string.Empty;
                return stamp.Length > 0 ? $"빌드: {stamp}\n" : string.Empty;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// 한 줄 형식: "[시:분:초.밀리초 +경과분:초.1/10초] 종류 메시지".
        /// 메시지의 두 번째 줄부터, 그리고 오류·예외의 스택은 4칸 들여써서 다음 줄들에. 끝에 줄바꿈 포함
        /// </summary>
        public static string FormatLine(DateTime time, TimeSpan sinceStart, LogType type, string message, string stackTrace)
        {
            var text = new StringBuilder();
            text.Append('[').Append(time.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture))
                .Append(" +").Append(((int)sinceStart.TotalMinutes).ToString("00", CultureInfo.InvariantCulture))
                .Append(':').Append(sinceStart.Seconds.ToString("00", CultureInfo.InvariantCulture))
                .Append('.').Append(sinceStart.Milliseconds / 100)
                .Append("] ").Append(TypeLabel(type)).Append(' ');

            string[] lines = SplitLines(message);
            text.Append(lines.Length > 0 ? lines[0] : string.Empty);
            for (int i = 1; i < lines.Length; i++) text.Append("\n    ").Append(lines[i]);

            if (type != LogType.Log && type != LogType.Warning)
                foreach (string line in SplitLines(stackTrace))
                    if (line.Length > 0) text.Append("\n    ").Append(line);
            return text.Append('\n').ToString();
        }

        /// <summary>LogType → 파일에 쓰는 종류 글자</summary>
        public static string TypeLabel(LogType type)
        {
            switch (type)
            {
                case LogType.Warning: return "WARN";
                case LogType.Error: return "ERROR";
                case LogType.Assert: return "ASSERT";
                case LogType.Exception: return "EXCEPTION";
                default: return "INFO";
            }
        }

        /// <summary>지울 파일 이름: 이름순(= 시간순)으로 오래된 것부터, keep개를 남기고 나머지</summary>
        public static List<string> FilesToDelete(IEnumerable<string> fileNames, int keep)
        {
            var sorted = (fileNames ?? Enumerable.Empty<string>())
                .Where(name => !string.IsNullOrEmpty(name))
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToList();
            int remove = sorted.Count - Math.Max(0, keep);
            return remove > 0 ? sorted.GetRange(0, remove) : new List<string>();
        }

        private static string[] SplitLines(string text) =>
            string.IsNullOrEmpty(text) ? Array.Empty<string>() : text.Replace("\r\n", "\n").TrimEnd('\n').Split('\n');
    }
}
