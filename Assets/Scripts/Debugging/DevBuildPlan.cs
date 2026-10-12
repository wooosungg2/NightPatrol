using System;
using System.Collections.Generic;
using System.Linq;

namespace NightPatrol.Debugging
{
    /// <summary>
    /// 메뉴 Night Patrol → 개발 빌드의 계획 (순수 함수, EditMode 테스트용). 빌드 자체는 에디터 어셈블리의 DevBuildMenu.
    /// Build Settings 파일은 바꾸지 않고 여기 씬 목록을 넘긴다
    /// </summary>
    public static class DevBuildPlan
    {
        /// <summary>메뉴가 빌드하는 씬 (공용 게임 씬). 씬 담당이 Floor5에 Player·PatrolSystems를 놓은 뒤에 쓴다</summary>
        public static readonly IReadOnlyList<string> DefaultScenes = new[] { "Assets/Scenes/Floor5.unity" };

        public const string DefaultRoot = "Builds";
        private const string DevFolder = "Assets/_Dev/";

        /// <summary>출력 폴더: Builds/2026-10-12_0725 (날짜_시각, 같은 분에 두 번이면 _2, _3 …)</summary>
        public static string OutputFolder(DateTime now, string root = DefaultRoot, Func<string, bool> exists = null)
        {
            string folder = $"{(string.IsNullOrEmpty(root) ? DefaultRoot : root.TrimEnd('/', '\\'))}/{now:yyyy-MM-dd_HHmm}";
            if (exists == null || !exists(folder)) return folder;
            for (int i = 2; i < 1000; i++)
                if (!exists($"{folder}_{i}")) return $"{folder}_{i}";
            return $"{folder}_{now:ssfff}";
        }

        /// <summary>실행 파일 경로: 폴더/제품이름.exe (파일 이름에 못 쓰는 글자는 _로)</summary>
        public static string ExePath(string folder, string productName)
        {
            string name = string.IsNullOrWhiteSpace(productName) ? "NightPatrol" : productName.Trim();
            foreach (char c in System.IO.Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return $"{folder}/{name}.exe";
        }

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

        /// <summary>빌드 결과 한 줄 (콘솔·보고용)</summary>
        public static string Summary(bool succeeded, IReadOnlyList<string> scenes, string target, string exePath, ulong totalBytes, TimeSpan duration, int errorCount)
        {
            string head = succeeded ? "[Build] 개발 빌드 성공" : "[Build] 개발 빌드 실패";
            var invariant = System.Globalization.CultureInfo.InvariantCulture;
            string size = succeeded ? $" · {(totalBytes / (1024.0 * 1024.0)).ToString("0.0", invariant)}MB" : $" · 오류 {errorCount}개";
            return $"{head} · 씬 {scenes?.Count ?? 0}개 ({string.Join(", ", scenes ?? Array.Empty<string>())}) · 대상 {target} · {exePath}{size} · {duration.TotalSeconds.ToString("0", invariant)}초";
        }
    }
}
