using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace NightPatrol.Debugging
{
    /// <summary>
    /// 메뉴 Night Patrol → 개발 빌드·발표용 빌드·빌드 정리의 계획 (순수 함수, EditMode 테스트용). 빌드 자체는 에디터 어셈블리의 DevBuildMenu.
    /// Build Settings 파일은 바꾸지 않고 여기 씬 목록을 넘긴다
    /// </summary>
    public static class DevBuildPlan
    {
        /// <summary>
        /// 메뉴가 빌드하는 씬: 시작 화면(첫 씬 = 실행하면 나옴) → 공용 게임 씬(근무 시작으로 불러옴, TitleScreen의 Game Scene과 같아야 함).
        /// 씬 담당이 Floor5에 Player·PatrolSystems를 놓은 뒤에 쓴다
        /// </summary>
        public static readonly IReadOnlyList<string> DefaultScenes = new[] { "Assets/Scenes/Title.unity", "Assets/Scenes/Floor5.unity" };

        public const string DefaultRoot = "Builds";
        /// <summary>발표용 빌드 폴더 이름 끝 (Builds/2026-10-12_0725_release)</summary>
        public const string ReleaseSuffix = "_release";
        /// <summary>빌드 정보 파일 (빌드 폴더, 실행 파일 옆). 게임 로그 머리글이 첫 줄을 읽는다</summary>
        public const string InfoFileName = "build.txt";
        /// <summary>빌드 정리에서 종류(개발·발표용)마다 남기는 최근 빌드 수</summary>
        public const int KeepBuilds = 3;
        private const string DevFolder = "Assets/_Dev/";

        /// <summary>
        /// 빌드하면 Unity·URP가 다시 저장하는 공유 설정 파일 (10/12 빌드에서 확인). 빌드 메뉴가 빌드 전 내용으로 되돌린다.
        /// 빌드 직전에 저장부터 하므로 일부러 바꾼 설정은 되돌리지 않는다
        /// </summary>
        public static readonly IReadOnlyList<string> SharedSettingsFiles = new[]
        {
            "Assets/Settings/PC_RPAsset.asset",
            "Assets/Settings/DefaultVolumeProfile.asset",
            "Assets/Settings/UniversalRenderPipelineGlobalSettings.asset",
            "ProjectSettings/ProjectSettings.asset",
            "ProjectSettings/UnityConnectSettings.asset",
            "ProjectSettings/Packages/com.unity.probuilder/Settings.json",
        };

        // 빌드 메뉴가 만드는 이름: 폴더 2026-10-12_0705(_release)(_2), zip 제품이름_폴더이름.zip
        private const string BuildStem = @"\d{4}-\d{2}-\d{2}_\d{4}(?:_release)?(?:_\d+)?";

        /// <summary>출력 폴더: Builds/2026-10-12_0725 (날짜_시각 + suffix, 같은 분에 두 번이면 _2, _3 …)</summary>
        public static string OutputFolder(DateTime now, string root = DefaultRoot, Func<string, bool> exists = null, string suffix = "")
        {
            string folder = $"{(string.IsNullOrEmpty(root) ? DefaultRoot : root.TrimEnd('/', '\\'))}/{now:yyyy-MM-dd_HHmm}{suffix}";
            if (exists == null || !exists(folder)) return folder;
            for (int i = 2; i < 1000; i++)
                if (!exists($"{folder}_{i}")) return $"{folder}_{i}";
            return $"{folder}_{now:ssfff}";
        }

        /// <summary>실행 파일 경로: 폴더/제품이름.exe (파일 이름에 못 쓰는 글자는 _로)</summary>
        public static string ExePath(string folder, string productName) => $"{folder}/{SafeFileName(productName)}.exe";

        /// <summary>
        /// 빌드 전에 씬 목록을 본다. 오류가 있으면 빌드하지 않는다: 빈 목록, 없는 씬 파일, .unity가 아닌 경로, 같은 씬 두 번.
        /// 경고(빌드는 함): 개인 개발 폴더(Assets/_Dev/) 씬 — 로컬 시험 빌드용
        /// </summary>
        public static (List<string> errors, List<string> warnings) Check(IReadOnlyList<string> scenes, Func<string, bool> sceneExists)
        {
            var errors = new List<string>();
            var warnings = new List<string>();
            if (scenes == null || scenes.Count == 0)
            {
                errors.Add("빌드할 씬이 없음");
                return (errors, warnings);
            }
            foreach (var path in scenes)
            {
                if (string.IsNullOrWhiteSpace(path)) { errors.Add("빈 씬 경로"); continue; }
                if (!path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)) { errors.Add($"씬 파일이 아님: {path}"); continue; }
                if (sceneExists != null && !sceneExists(path)) errors.Add($"씬 파일이 없음: {path}");
                if (path.StartsWith(DevFolder, StringComparison.OrdinalIgnoreCase))
                    warnings.Add($"개인 개발 폴더의 씬 — 로컬 시험 빌드로만 쓸 것: {path}");
            }
            foreach (var group in scenes.Where(p => !string.IsNullOrWhiteSpace(p))
                         .GroupBy(p => p, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
                errors.Add($"같은 씬이 {group.Count()}번: {group.Key}");
            return (errors, warnings);
        }

        /// <summary>빌드 결과 한 줄 (콘솔·보고용). release면 "발표용 빌드"</summary>
        public static string Summary(bool succeeded, IReadOnlyList<string> scenes, string target, string exePath, ulong totalBytes, TimeSpan duration, int errorCount,
            bool release = false)
        {
            string kind = release ? "발표용 빌드" : "개발 빌드";
            string head = $"[Build] {kind} {(succeeded ? "성공" : "실패")}";
            string size = succeeded ? $" · {SizeLabel(totalBytes)}" : $" · 오류 {errorCount}개";
            return $"{head} · 씬 {scenes?.Count ?? 0}개 ({string.Join(", ", scenes ?? Array.Empty<string>())}) · 대상 {target} · {exePath}{size} · {duration.TotalSeconds.ToString("0", CultureInfo.InvariantCulture)}초";
        }

        /// <summary>크기 글: 1572864 → "1.5MB"</summary>
        public static string SizeLabel(double bytes) => $"{(bytes / (1024.0 * 1024.0)).ToString("0.0", CultureInfo.InvariantCulture)}MB";

        /// <summary>
        /// 빌드 뒤 되돌릴 내용 = 빌드 직전 저장(SaveAssets) 뒤의 내용. 저장이 줄바꿈(CRLF↔LF)만 바꿨으면 저장 전 그대로
        /// (git에 줄바꿈만 바뀐 파일로 뜨지 않게). 저장 뒤에 없는 파일은 넣지 않는다
        /// </summary>
        public static Dictionary<string, byte[]> RestoreTargets(IReadOnlyDictionary<string, byte[]> beforeSave, IReadOnlyDictionary<string, byte[]> afterSave)
        {
            var targets = new Dictionary<string, byte[]>();
            if (afterSave == null) return targets;
            foreach (var pair in afterSave)
            {
                byte[] old = null;
                bool keepOld = beforeSave != null && beforeSave.TryGetValue(pair.Key, out old) && SameIgnoringLineEndings(old, pair.Value);
                targets[pair.Key] = keepOld ? old : pair.Value;
            }
            return targets;
        }

        /// <summary>줄바꿈 글자(CR)만 다르고 나머지가 같은가</summary>
        public static bool SameIgnoringLineEndings(byte[] a, byte[] b) =>
            a != null && b != null && a.Where(x => x != (byte)'\r').SequenceEqual(b.Where(x => x != (byte)'\r'));

        /// <summary>되돌릴 내용(targets)과 지금 내용이 다른 파일. readNow: 경로 → 지금 내용, 없으면 null (null도 다른 것)</summary>
        public static List<string> ChangedFiles(IReadOnlyDictionary<string, byte[]> targets, Func<string, byte[]> readNow)
        {
            var changed = new List<string>();
            if (targets == null) return changed;
            foreach (var pair in targets)
            {
                byte[] now = readNow(pair.Key);
                if (now == null || !now.SequenceEqual(pair.Value)) changed.Add(pair.Key);
            }
            return changed;
        }

        /// <summary>공유 설정을 되돌린 뒤 콘솔 한 줄</summary>
        public static string RestoredLine(IReadOnlyList<string> restored) =>
            $"[Build] 빌드 중 다시 저장된 공유 설정 {restored.Count}개를 빌드 전 내용으로 되돌림: {string.Join(", ", restored)}";

        /// <summary>
        /// .git 폴더의 파일로 브랜치·커밋(앞 7자리)을 읽는다 (git 프로그램 없이). readGitFile: .git 안 상대 경로 → 내용, 없으면 null.
        /// 브랜치가 아니라 커밋을 꺼내 놓은 상태면 브랜치 "", 모르면 ""
        /// </summary>
        public static (string branch, string commit) GitHead(Func<string, string> readGitFile)
        {
            string head = readGitFile?.Invoke("HEAD")?.Trim();
            if (string.IsNullOrEmpty(head)) return ("", "");
            const string refPrefix = "ref:";
            if (!head.StartsWith(refPrefix, StringComparison.Ordinal)) return ("", ShortHash(head));

            string reference = head.Substring(refPrefix.Length).Trim();
            const string branchPrefix = "refs/heads/";
            string branch = reference.StartsWith(branchPrefix, StringComparison.Ordinal) ? reference.Substring(branchPrefix.Length) : reference;
            string hash = readGitFile(reference)?.Trim();
            if (string.IsNullOrEmpty(hash)) hash = PackedRef(readGitFile("packed-refs"), reference);
            return (branch, ShortHash(hash));
        }

        // packed-refs 한 줄: "<해시> refs/heads/이름" (^로 시작하는 줄은 태그용이라 건너뜀)
        private static string PackedRef(string packedRefs, string reference)
        {
            if (string.IsNullOrEmpty(packedRefs)) return "";
            foreach (string line in packedRefs.Split('\n'))
            {
                string[] parts = line.Trim().Split(' ');
                if (parts.Length == 2 && parts[1] == reference) return parts[0];
            }
            return "";
        }

        private static string ShortHash(string hash) =>
            !string.IsNullOrEmpty(hash) && hash.Length >= 7 && hash.All(Uri.IsHexDigit) ? hash.Substring(0, 7).ToLowerInvariant() : "";

        /// <summary>git status --porcelain 출력 → 커밋 안 한 파일 수. null(git이 없거나 실패)이면 -1</summary>
        public static int UncommittedCount(string porcelain) =>
            porcelain == null ? -1 : porcelain.Split('\n').Count(line => line.Trim().Length > 0);

        /// <summary>
        /// 빌드 한 줄: "개발 빌드 2026-10-23 14:05 · woosung 08456f0 · 커밋 안 한 변경 3개".
        /// 커밋을 모르면 "커밋 모름", uncommitted가 음수면 "변경 여부 모름", 0이면 생략
        /// </summary>
        public static string Stamp(bool release, DateTime builtAt, string branch, string commit, int uncommitted)
        {
            string head = $"{(release ? "발표용 빌드" : "개발 빌드")} {builtAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)}";
            if (string.IsNullOrEmpty(commit)) return $"{head} · 커밋 모름";
            string source = string.IsNullOrEmpty(branch) ? commit : $"{branch} {commit}";
            string changes = uncommitted < 0 ? " · 변경 여부 모름" : uncommitted == 0 ? "" : $" · 커밋 안 한 변경 {uncommitted}개";
            return $"{head} · {source}{changes}";
        }

        /// <summary>build.txt 내용. 첫 줄은 Stamp (게임 로그 머리글에 들어감), 그다음 씬·대상·버전과 받은 사람 안내</summary>
        public static string InfoText(string stamp, IReadOnlyList<string> scenes, string target, string unityVersion, string gameVersion, string logFolder) =>
            $"{stamp}\n" +
            $"씬: {string.Join(", ", scenes ?? Array.Empty<string>())}\n" +
            $"대상: {target}\n" +
            $"Unity: {unityVersion}\n" +
            $"게임 버전: {gameVersion}\n" +
            "\n" +
            "문제가 생기면 이 build.txt와 그때의 게임 로그(가장 최근 game_….log)를 시스템 담당에게 보내 주세요.\n" +
            $"게임 로그 위치: {logFolder} (개발 빌드는 F5로 열림)\n";

        /// <summary>빌드(Windows)의 게임 로그 폴더 = persistentDataPath/logs. 이 PC의 사용자 이름 대신 %USERPROFILE%</summary>
        public static string LogFolder(string companyName, string productName) =>
            $@"%USERPROFILE%\AppData\LocalLow\{companyName}\{productName}\logs";

        /// <summary>build.txt 내용 → 첫 줄 (게임 로그 머리글용). 없으면 ""</summary>
        public static string StampFromInfo(string infoText) =>
            (infoText ?? "").Split('\n').Select(line => line.Trim().TrimStart('\uFEFF').Trim()).FirstOrDefault(line => line.Length > 0) ?? "";

        /// <summary>공유용 zip 경로: 빌드 폴더 옆 제품이름_폴더이름.zip (Builds/NightPatrol_2026-10-23_1405.zip)</summary>
        public static string ZipPath(string folder, string productName)
        {
            string trimmed = (folder ?? "").TrimEnd('/', '\\');
            int cut = trimmed.LastIndexOfAny(new[] { '/', '\\' });
            return $"{trimmed.Substring(0, cut + 1)}{SafeFileName(productName)}_{trimmed.Substring(cut + 1)}.zip";
        }

        /// <summary>zip에 넣을 파일인가 (빌드 폴더 기준 경로). Unity가 "배포하지 말 것"으로 만드는 폴더(…_DoNotShip, …_ButDontShipItWithYourGame)는 뺀다</summary>
        public static bool ShipsInZip(string relativePath)
        {
            string top = (relativePath ?? "").Replace('\\', '/').Split('/')[0];
            return !top.EndsWith("_DoNotShip", StringComparison.OrdinalIgnoreCase)
                && !top.EndsWith("_ButDontShipItWithYourGame", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 빌드 정리에서 지울 이름 (Builds 안의 폴더·zip 이름). 빌드 메뉴가 만든 이름만 보고, 개발·발표용 빌드를 종류마다 최근 keep개 남긴다.
        /// 같은 빌드의 폴더와 zip은 함께 남기거나 지운다. 이름순 = 시간순. 직접 만든 다른 이름은 건드리지 않는다
        /// </summary>
        public static List<string> OldBuilds(IEnumerable<string> names, int keep)
        {
            var builds = new List<(string name, string stem)>();
            foreach (string name in names ?? Enumerable.Empty<string>())
            {
                if (string.IsNullOrEmpty(name)) continue;
                if (Regex.IsMatch(name, $"^{BuildStem}$")) { builds.Add((name, name)); continue; }
                var zip = Regex.Match(name, $"^.+_({BuildStem})\\.zip$", RegexOptions.IgnoreCase);
                if (zip.Success) builds.Add((name, zip.Groups[1].Value));
            }
            var kept = new HashSet<string>(builds.Select(b => b.stem).Distinct()
                .GroupBy(stem => stem.Contains(ReleaseSuffix))
                .SelectMany(kind => kind.OrderByDescending(stem => stem, StringComparer.Ordinal).Take(Math.Max(0, keep))));
            return builds.Where(b => !kept.Contains(b.stem)).Select(b => b.name).OrderBy(name => name, StringComparer.Ordinal).ToList();
        }

        private static string SafeFileName(string productName)
        {
            string name = string.IsNullOrWhiteSpace(productName) ? "NightPatrol" : productName.Trim();
            foreach (char c in System.IO.Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return name;
        }
    }
}
