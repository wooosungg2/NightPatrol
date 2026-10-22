using System;
using System.IO;
using System.Linq;
using NightPatrol.Debugging;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NightPatrol.Editor
{
    // 메뉴 Night Patrol → 개발 빌드: 씬 목록·Development Build·출력 폴더 Builds/날짜_시각/을 한 번에.
    // Build Settings(ProjectSettings/EditorBuildSettings.asset)는 바꾸지 않고 DevBuildPlan.DefaultScenes를 넘긴다.
    // 다른 씬으로 시험할 때는 코드에서 DevBuildMenu.Build(new[] { "Assets/_Dev/이름/Scenes/...unity" })
    public static class DevBuildMenu
    {
        [MenuItem("Night Patrol/개발 빌드", priority = 20)]
        private static void BuildFromMenu()
        {
            var scenes = DevBuildPlan.DefaultScenes.ToArray();
            string folder = DevBuildPlan.OutputFolder(DateTime.Now, DevBuildPlan.DefaultRoot, Directory.Exists);
            if (!EditorUtility.DisplayDialog("개발 빌드",
                    $"씬: {string.Join(", ", scenes)}\n대상: {EditorUserBuildSettings.activeBuildTarget}\n출력: {folder}/\n\nDevelopment Build로 빌드합니다. Build Settings는 바꾸지 않습니다.",
                    "빌드", "취소"))
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var report = Build(scenes, DevBuildPlan.DefaultRoot);
            if (report != null && report.summary.result == BuildResult.Succeeded)
                EditorUtility.RevealInFinder(report.summary.outputPath);
        }

        /// <summary>
        /// 개발 빌드 (Development). 씬 목록에 오류가 있거나 Play·컴파일 중이면 빌드하지 않고 null.
        /// 결과는 콘솔에 [Build] 한 줄. 실패한 빌드의 폴더는 남는다 (로그 확인용)
        /// </summary>
        public static BuildReport Build(string[] scenes, string root = DevBuildPlan.DefaultRoot)
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

            string folder = DevBuildPlan.OutputFolder(DateTime.Now, root, Directory.Exists);
            string exe = DevBuildPlan.ExePath(folder, PlayerSettings.productName);
            Directory.CreateDirectory(folder);
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = exe,
                target = EditorUserBuildSettings.activeBuildTarget,
                targetGroup = BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget),
                options = BuildOptions.Development,
            };
            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            string line = DevBuildPlan.Summary(summary.result == BuildResult.Succeeded, scenes, summary.platform.ToString(),
                exe, summary.totalSize, summary.totalTime, summary.totalErrors);
            if (summary.result == BuildResult.Succeeded) Debug.Log(line);
            else Debug.LogError(line + $" ({summary.result})");
            return report;
        }
    }
}
