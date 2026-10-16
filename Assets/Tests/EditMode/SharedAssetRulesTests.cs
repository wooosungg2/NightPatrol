using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;

namespace NightPatrol.Tests
{
    // 10/11 공용 폴더 이동: 시스템 공용 에셋(Player 프리팹·플레이어 설정·조작 키 목록·점검 항목 표·재질)은
    // 개인 개발 폴더 Assets/_Dev/ 안의 에셋을 가리키면 안 된다. _Dev는 개인 실험용이라 지우거나 옮기면
    // 공용 씬·빌드에서 연결이 조용히 끊긴다 (Missing). 프리팹 안 재질·폰트·중첩 프리팹까지 재귀로 본다
    public class SharedAssetRulesTests
    {
        private const string DevRoot = "Assets/_Dev/";

        // 있어야 하는 공용 에셋. 옮기거나 이름을 바꾸면 이 목록도 같이 고친다
        private static readonly string[] Required =
        {
            "Assets/Prefabs/Player.prefab",
            "Assets/Settings/PlayerSettings.asset",
            "Assets/Settings/KeyBindingList.asset",
        };

        // 생기면 검사하는 공용 에셋
        private static readonly string[] Optional =
        {
            "Assets/Prefabs/PatrolSystems.prefab",
        };

        // 폴더 안 에셋 전부 (하위 폴더 포함, 폴더가 아직 없으면 건너뜀)
        private static readonly string[] Folders =
        {
            "Assets/Data",
            "Assets/Materials/Player",
        };

        private static bool Exists(string path) => AssetDatabase.LoadMainAssetAtPath(path) != null;

        private static IEnumerable<string> Targets()
        {
            foreach (var path in Required.Where(Exists)) yield return path;
            foreach (var path in Optional.Where(Exists)) yield return path;
            foreach (var folder in Folders.Where(AssetDatabase.IsValidFolder))
                foreach (var path in AssetDatabase.FindAssets(string.Empty, new[] { folder })
                             .Select(AssetDatabase.GUIDToAssetPath)
                             .Where(p => !AssetDatabase.IsValidFolder(p)))
                    yield return path;
        }

        [Test]
        public void RequiredSharedAssets_Exist()
        {
            var missing = Required.Where(p => !Exists(p)).ToList();
            Assert.IsEmpty(missing, "공용 에셋이 없음 (옮겼다면 SharedAssetRulesTests의 목록도 고칠 것): " + string.Join(", ", missing));
        }

        [Test]
        public void SharedAssets_DoNotReferenceDevFolder()
        {
            var problems = new List<string>();
            foreach (var path in Targets().Distinct())
            {
                var dev = AssetDatabase.GetDependencies(path, true)
                    .Where(d => d.StartsWith(DevRoot, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (dev.Count > 0) problems.Add(path + " → " + string.Join(", ", dev));
            }
            Assert.IsEmpty(problems, "공용 에셋이 Assets/_Dev/를 참조함 (참조하는 에셋을 공용 폴더로 옮기고 다시 연결): " + string.Join(" / ", problems));
        }

        [Test]
        public void FolderScan_FindsThePlayerMaterials()
        {
            // 폴더 검색이 조용히 0개가 되면 위 검사가 아무것도 안 본다 (폴더를 옮기거나 검색 방식이 바뀔 때)
            Assert.IsTrue(Targets().Any(p => p.StartsWith("Assets/Materials/Player/", StringComparison.Ordinal)),
                "Assets/Materials/Player에서 에셋을 못 찾음 (폴더를 옮겼다면 SharedAssetRulesTests의 폴더 목록도 고칠 것)");
        }
    }
}
