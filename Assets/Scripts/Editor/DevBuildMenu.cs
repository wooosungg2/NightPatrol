using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using NightPatrol.Debugging;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace NightPatrol.Editor
{
    // 메뉴 Night Patrol → 개발 빌드 / 발표용 빌드: 씬 목록·빌드 종류·출력 폴더 Builds/날짜_시각/을 한 번에.
    // 개발 빌드 = Development Build (F1~F5 개발자 도구·플레이테스트 기록, 화면 오른쪽 아래 "Development Build" 글씨·오류 화면 콘솔).
    // 발표용 빌드 = 일반 빌드 (개발자 도구·기록·글씨 없음, 촬영·발표용). 폴더 끝에 _release.
    // Build Settings(ProjectSettings/EditorBuildSettings.asset)는 바꾸지 않고 DevBuildPlan.DefaultScenes를 넘긴다.
    // 빌드 중 Unity가 다시 저장한 공유 설정(DevBuildPlan.SharedSettingsFiles)은 빌드 전 내용으로 되돌리고,
    // 빌드 폴더에 build.txt(종류·날짜·브랜치·커밋)를 남긴다. "빌드 + zip"을 고르면 폴더 옆에 공유용 zip.
    // 메뉴 빌드 정리 = Builds의 오래된 빌드 폴더·zip 지우기 (종류마다 최근 DevBuildPlan.KeepBuilds개 남김).
    // 다른 씬으로 시험할 때는 코드에서 DevBuildMenu.Build(new[] { "Assets/_Dev/이름/Scenes/...unity" })
    public static class DevBuildMenu
    {
        [MenuItem("Night Patrol/개발 빌드", priority = 20)]
        private static void BuildDevelopmentFromMenu() => BuildFromMenu(release: false);

        [MenuItem("Night Patrol/발표용 빌드", priority = 21)]
        private static void BuildReleaseFromMenu() => BuildFromMenu(release: true);

        [MenuItem("Night Patrol/빌드 정리", priority = 22)]
        private static void CleanFromMenu()
        {
            string root = DevBuildPlan.DefaultRoot;
            var names = Directory.Exists(root)
                ? Directory.GetDirectories(root).Concat(Directory.GetFiles(root, "*.zip")).Select(Path.GetFileName).ToList()
                : new List<string>();
            var old = DevBuildPlan.OldBuilds(names, DevBuildPlan.KeepBuilds);
            string rule = $"개발·발표용 빌드는 종류마다 최근 {DevBuildPlan.KeepBuilds}개를 남깁니다.";
            if (old.Count == 0)
            {
                EditorUtility.DisplayDialog("빌드 정리", $"{root}/에 지울 빌드가 없습니다. {rule}", "확인");
                return;
            }
            var paths = old.Select(name => $"{root}/{name}").ToList();
            long bytes = paths.Sum(SizeOf);
            string list = string.Join("\n", old.Take(12)) + (old.Count > 12 ? $"\n… 외 {old.Count - 12}개" : "");
            if (!EditorUtility.DisplayDialog("빌드 정리",
                    $"{root}/의 오래된 빌드 폴더·zip {old.Count}개(약 {DevBuildPlan.SizeLabel(bytes)})를 지웁니다. {rule}\n\n{list}",
                    "지우기", "취소"))
                return;
            var failed = paths.Where(path => !FileUtil.DeleteFileOrDirectory(path)).Select(Path.GetFileName).ToList();
            string line = $"[Build] 빌드 정리: {old.Count - failed.Count}개 지움 (약 {DevBuildPlan.SizeLabel(bytes)})";
            if (failed.Count == 0) Debug.Log(line);
            else Debug.LogWarning($"{line}, 못 지움 {failed.Count}개 (그 빌드가 실행 중이면 끄고 다시): {string.Join(", ", failed)}");
        }

        private static void BuildFromMenu(bool release)
        {
            var scenes = DevBuildPlan.DefaultScenes.ToArray();
            string folder = DevBuildPlan.OutputFolder(DateTime.Now, DevBuildPlan.DefaultRoot, Directory.Exists,
                release ? DevBuildPlan.ReleaseSuffix : string.Empty);
            string kind = release
                ? "발표용 빌드(일반 빌드)로 빌드합니다. 개발자 도구(F1~F5)·플레이테스트 기록·\"Development Build\" 글씨가 없습니다."
                : "Development Build로 빌드합니다.";
            int choice = EditorUtility.DisplayDialogComplex(release ? "발표용 빌드" : "개발 빌드",
                $"씬: {string.Join(", ", scenes)}\n대상: {EditorUserBuildSettings.activeBuildTarget}\n출력: {folder}/\n\n{kind} Build Settings는 바꾸지 않습니다.\n" +
                "빌드 중 Unity가 다시 저장한 공유 설정(URP·ProjectSettings 등)은 빌드 전 내용으로 되돌립니다.\n\n" +
                "빌드 + zip: 빌드 폴더 옆에 공유용 zip도 만듭니다.",
                "빌드", "취소", "빌드 + zip");
            if (choice == 1) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var report = Build(scenes, DevBuildPlan.DefaultRoot, release);
            if (report == null || report.summary.result != BuildResult.Succeeded) return;
            string built = report.summary.outputPath;   // 실행 파일 경로 (Builds/날짜_시각/NightPatrol.exe)
            string zip = choice == 2
                ? Zip(built.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? Path.GetDirectoryName(built) : built)
                : null;
            EditorUtility.RevealInFinder(zip != null ? Path.GetFullPath(zip) : built);
        }

        /// <summary>
        /// 빌드. release가 false면 개발 빌드(Development), true면 발표용 일반 빌드. 씬 목록에 오류가 있거나 Play·컴파일 중이면 빌드하지 않고 null.
        /// 빌드 직전에 저장(SaveAssets)하고, 빌드 중 다시 저장된 공유 설정은 그 내용으로 되돌린다. 성공하면 빌드 폴더에 build.txt.
        /// 결과는 콘솔에 [Build] 한 줄. 실패한 빌드의 폴더는 남는다 (로그 확인용)
        /// </summary>
        public static BuildReport Build(string[] scenes, string root = DevBuildPlan.DefaultRoot, bool release = false)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            {
                Debug.LogError("[Build] Play·컴파일 중에는 빌드하지 않음");
                return null;
            }
            var (errors, warnings) = DevBuildPlan.Check(scenes, File.Exists);
            foreach (var warning in warnings) Debug.LogWarning("[Build] " + warning);
            if (errors.Count > 0)
            {
                Debug.LogError("[Build] 빌드 안 함: " + string.Join(" / ", errors));
                return null;
            }

            var now = DateTime.Now;
            string folder = DevBuildPlan.OutputFolder(now, root, Directory.Exists,
                release ? DevBuildPlan.ReleaseSuffix : string.Empty);
            string exe = DevBuildPlan.ExePath(folder, PlayerSettings.productName);
            Directory.CreateDirectory(folder);
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = exe,
                target = EditorUserBuildSettings.activeBuildTarget,
                targetGroup = BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget),
                options = release ? BuildOptions.None : BuildOptions.Development,
            };

            // 일부러 바꾸고 저장 안 한 설정은 먼저 저장해서 "빌드 전 내용"에 넣는다 (되돌리지 않게)
            var beforeSave = ReadSharedSettings();
            AssetDatabase.SaveAssets();
            var settingsBefore = DevBuildPlan.RestoreTargets(beforeSave, ReadSharedSettings());
            BuildReport report;
            try { report = BuildPipeline.BuildPlayer(options); }
            finally { RestoreSharedSettings(settingsBefore); }

            var summary = report.summary;
            bool succeeded = summary.result == BuildResult.Succeeded;
            if (succeeded) WriteInfo(folder, release, now, scenes, summary.platform.ToString());
            string line = DevBuildPlan.Summary(succeeded, scenes, summary.platform.ToString(),
                exe, summary.totalSize, summary.totalTime, summary.totalErrors, release);
            if (succeeded) Debug.Log(line);
            else Debug.LogError(line + $" ({summary.result})");
            return report;
        }

        /// <summary>빌드 폴더를 공유용 zip 하나로 (폴더 옆 제품이름_폴더이름.zip, "배포하지 말 것" 폴더는 뺌). 실패하면 null</summary>
        public static string Zip(string folder)
        {
            string zipPath = DevBuildPlan.ZipPath(folder, PlayerSettings.productName);
            try
            {
                string full = Path.GetFullPath(folder);
                var files = Directory.GetFiles(full, "*", SearchOption.AllDirectories);
                if (File.Exists(zipPath)) File.Delete(zipPath);
                using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
                {
                    for (int i = 0; i < files.Length; i++)
                    {
                        string entry = files[i].Substring(full.Length).TrimStart('\\', '/').Replace('\\', '/');
                        if (!DevBuildPlan.ShipsInZip(entry)) continue;
                        EditorUtility.DisplayProgressBar("빌드 zip", entry, (float)i / files.Length);
                        archive.CreateEntryFromFile(files[i], entry, System.IO.Compression.CompressionLevel.Optimal);
                    }
                }
                Debug.Log($"[Build] zip: {zipPath} · {DevBuildPlan.SizeLabel(new FileInfo(zipPath).Length)}");
                return zipPath;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Build] zip 실패: {e.Message}");
                try { if (File.Exists(zipPath)) File.Delete(zipPath); }
                catch (Exception) { }
                return null;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static Dictionary<string, byte[]> ReadSharedSettings()
        {
            var files = new Dictionary<string, byte[]>();
            foreach (string path in DevBuildPlan.SharedSettingsFiles)
            {
                byte[] bytes = TryReadBytes(path);
                if (bytes != null) files[path] = bytes;
            }
            return files;
        }

        private static byte[] TryReadBytes(string path)
        {
            try
            {
                return File.Exists(path) ? File.ReadAllBytes(path) : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        // Assets/ 아래 파일은 되돌린 뒤 다시 불러와서 에디터 안의 값도 빌드 전으로 (ProjectSettings는 GitHub Desktop의 Discard와 같음)
        private static void RestoreSharedSettings(Dictionary<string, byte[]> targets)
        {
            var restored = new List<string>();
            foreach (string path in DevBuildPlan.ChangedFiles(targets, TryReadBytes))
            {
                try
                {
                    File.WriteAllBytes(path, targets[path]);
                    if (path.StartsWith("Assets/", StringComparison.Ordinal))
                        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                    restored.Add(path);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Build] 공유 설정을 되돌리지 못함: {path} ({e.Message}) — 일부러 바꾼 게 아니면 GitHub Desktop에서 Discard");
                }
            }
            if (restored.Count > 0) Debug.Log(DevBuildPlan.RestoredLine(restored));
        }

        // build.txt: 브랜치·커밋은 .git 폴더의 파일에서, 커밋 안 한 변경 수는 git 프로그램으로 (없으면 "변경 여부 모름")
        private static void WriteInfo(string folder, bool release, DateTime builtAt, string[] scenes, string target)
        {
            try
            {
                var (branch, commit) = DevBuildPlan.GitHead(ReadGitFile);
                int uncommitted = string.IsNullOrEmpty(commit) ? -1 : DevBuildPlan.UncommittedCount(GitStatus());
                string stamp = DevBuildPlan.Stamp(release, builtAt, branch, commit, uncommitted);
                string text = DevBuildPlan.InfoText(stamp, scenes, target, Application.unityVersion, PlayerSettings.bundleVersion,
                    DevBuildPlan.LogFolder(PlayerSettings.companyName, PlayerSettings.productName));
                File.WriteAllText(Path.Combine(folder, DevBuildPlan.InfoFileName), text, new UTF8Encoding(true));   // BOM: 메모장에서 한글이 안 깨지게
                Debug.Log($"[Build] {DevBuildPlan.InfoFileName}: {stamp}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Build] {DevBuildPlan.InfoFileName}을 못 씀: {e.Message}");
            }
        }

        private static string ReadGitFile(string relativePath)
        {
            try
            {
                string path = Path.Combine(".git", relativePath);
                return File.Exists(path) ? File.ReadAllText(path) : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        // git status --porcelain 출력. git이 없거나 실패하거나 5초 안에 안 끝나면 null
        private static string GitStatus()
        {
            try
            {
                var start = new ProcessStartInfo("git", "--no-optional-locks status --porcelain")
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                };
                using (var process = Process.Start(start))
                {
                    if (process == null) return null;
                    var output = process.StandardOutput.ReadToEndAsync();
                    _ = process.StandardError.ReadToEndAsync();   // 오류 출력도 비워야 git이 멈추지 않는다
                    if (!process.WaitForExit(5000))
                    {
                        try { process.Kill(); }
                        catch (Exception) { }
                        return null;
                    }
                    return process.ExitCode == 0 ? output.Result : null;
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static long SizeOf(string path)
        {
            try
            {
                if (File.Exists(path)) return new FileInfo(path).Length;
                return Directory.Exists(path)
                    ? Directory.GetFiles(path, "*", SearchOption.AllDirectories).Sum(file => new FileInfo(file).Length)
                    : 0;
            }
            catch (Exception)
            {
                return 0;
            }
        }
    }
}
