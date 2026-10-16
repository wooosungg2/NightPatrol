using System;
using System.Collections.Generic;
using System.Linq;
using NightPatrol.Debugging;
using NUnit.Framework;

namespace NightPatrol.Tests
{
    // 10/12 야간 9번: 메뉴 Night Patrol → 개발 빌드의 계획 (출력 폴더·실행 파일 이름·씬 목록 검사·결과 한 줄)
    public class DevBuildPlanTests
    {
        private static readonly DateTime Now = new DateTime(2026, 10, 12, 7, 5, 9);

        [Test]
        public void DefaultScenes_IsTheSharedFloor5Only()
        {
            CollectionAssert.AreEqual(new[] { "Assets/Scenes/Floor5.unity" }, DevBuildPlan.DefaultScenes);
        }

        [Test]
        public void OutputFolder_IsDateAndTime_UnderBuilds()
        {
            Assert.AreEqual("Builds/2026-10-12_0705", DevBuildPlan.OutputFolder(Now));
            Assert.AreEqual("Out/2026-10-12_0705", DevBuildPlan.OutputFolder(Now, "Out/"));
            Assert.AreEqual("Builds/2026-10-12_0705", DevBuildPlan.OutputFolder(Now, ""));
        }

        [Test]
        public void OutputFolder_SameMinuteTwice_GetsANumber()
        {
            var taken = new HashSet<string> { "Builds/2026-10-12_0705", "Builds/2026-10-12_0705_2" };
            Assert.AreEqual("Builds/2026-10-12_0705_3", DevBuildPlan.OutputFolder(Now, "Builds", taken.Contains));
        }

        [Test]
        public void OutputFolder_AlwaysTaken_StillEnds()
        {
            StringAssert.StartsWith("Builds/2026-10-12_0705_", DevBuildPlan.OutputFolder(Now, "Builds", _ => true));
        }

        [Test]
        public void ExePath_UsesTheProductName_WithoutBadCharacters()
        {
            Assert.AreEqual("Builds/x/NightPatrol.exe", DevBuildPlan.ExePath("Builds/x", "NightPatrol"));
            Assert.AreEqual("Builds/x/Night_Patrol.exe", DevBuildPlan.ExePath("Builds/x", "Night:Patrol"));
            Assert.AreEqual("Builds/x/NightPatrol.exe", DevBuildPlan.ExePath("Builds/x", "  "));
        }

        [Test]
        public void Check_GoodScene_HasNoProblems()
        {
            var (errors, warnings) = DevBuildPlan.Check(new[] { "Assets/Scenes/Floor5.unity" }, _ => true);
            Assert.IsEmpty(errors);
            Assert.IsEmpty(warnings);
        }

        [Test]
        public void Check_EmptyList_IsAnError()
        {
            Assert.AreEqual(1, DevBuildPlan.Check(new string[0], _ => true).errors.Count);
            Assert.AreEqual(1, DevBuildPlan.Check(null, _ => true).errors.Count);
        }

        [Test]
        public void Check_MissingFile_NotAScene_Blank_Duplicate_AreErrors()
        {
            var scenes = new[] { "Assets/Scenes/Nope.unity", "Assets/Scenes/Floor5.prefab", " ", "Assets/Scenes/Floor5.unity", "assets/scenes/floor5.unity" };
            var (errors, _) = DevBuildPlan.Check(scenes, p => !p.Contains("Nope"));
            Assert.AreEqual(4, errors.Count, string.Join(" / ", errors));
            Assert.IsTrue(errors.Any(e => e.Contains("씬 파일이 없음") && e.Contains("Nope")));
            Assert.IsTrue(errors.Any(e => e.Contains("씬 파일이 아님")));
            Assert.IsTrue(errors.Any(e => e.Contains("빈 씬 경로")));
            Assert.IsTrue(errors.Any(e => e.Contains("같은 씬이 2번")));
        }

        [Test]
        public void Check_DevFolderScene_IsOnlyAWarning()
        {
            var (errors, warnings) = DevBuildPlan.Check(new[] { "Assets/_Dev/woosung/Scenes/Test_00_04_Floor5.unity" }, _ => true);
            Assert.IsEmpty(errors);
            Assert.AreEqual(1, warnings.Count);
            StringAssert.Contains("로컬 시험 빌드", warnings[0]);
        }

        [Test]
        public void Summary_SaysWhatWasBuilt()
        {
            var scenes = new[] { "Assets/Scenes/Floor5.unity" };
            Assert.AreEqual("[Build] 개발 빌드 성공 · 씬 1개 (Assets/Scenes/Floor5.unity) · 대상 StandaloneWindows64 · Builds/x/NightPatrol.exe · 1.5MB · 75초",
                DevBuildPlan.Summary(true, scenes, "StandaloneWindows64", "Builds/x/NightPatrol.exe", 1572864, TimeSpan.FromSeconds(75), 0));
            Assert.AreEqual("[Build] 개발 빌드 실패 · 씬 1개 (Assets/Scenes/Floor5.unity) · 대상 StandaloneWindows64 · Builds/x/NightPatrol.exe · 오류 3개 · 2초",
                DevBuildPlan.Summary(false, scenes, "StandaloneWindows64", "Builds/x/NightPatrol.exe", 0, TimeSpan.FromSeconds(2), 3));
        }
    }
}
