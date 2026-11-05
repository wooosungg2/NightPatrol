using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NightPatrol.Debugging;
using NUnit.Framework;

namespace NightPatrol.Tests
{
    // 10/12 야간 9번: 메뉴 Night Patrol → 개발 빌드의 계획 (출력 폴더·실행 파일 이름·씬 목록 검사·결과 한 줄)
    public class DevBuildPlanTests
    {
        private static readonly DateTime Now = new DateTime(2026, 10, 12, 7, 5, 9);

        [Test]
        public void DefaultScenes_TitleFirst_ThenTheSharedFloor5()
        {
            CollectionAssert.AreEqual(new[] { "Assets/Scenes/Title.unity", "Assets/Scenes/Floor5.unity" }, DevBuildPlan.DefaultScenes);
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

        // 00_12: 메뉴 Night Patrol → 발표용 빌드 (일반 빌드: "Development Build" 글씨·개발자 도구 없음)
        [Test]
        public void ReleaseBuild_HasItsOwnFolderAndSummary()
        {
            Assert.AreEqual("Builds/2026-10-12_0705_release", DevBuildPlan.OutputFolder(Now, "Builds", null, DevBuildPlan.ReleaseSuffix));
            var taken = new HashSet<string> { "Builds/2026-10-12_0705_release" };
            Assert.AreEqual("Builds/2026-10-12_0705_release_2",
                DevBuildPlan.OutputFolder(Now, "Builds", taken.Contains, DevBuildPlan.ReleaseSuffix));
            Assert.AreEqual("[Build] 발표용 빌드 성공 · 씬 1개 (Assets/Scenes/Floor5.unity) · 대상 StandaloneWindows64 · Builds/x/NightPatrol.exe · 1.5MB · 75초",
                DevBuildPlan.Summary(true, new[] { "Assets/Scenes/Floor5.unity" }, "StandaloneWindows64", "Builds/x/NightPatrol.exe",
                    1572864, TimeSpan.FromSeconds(75), 0, release: true));
        }

        // 00_13c: 빌드 편의 — 공유 설정 되돌리기, build.txt, zip, 빌드 정리
        [Test]
        public void SharedSettingsFiles_AllExist()
        {
            Assert.AreEqual(6, DevBuildPlan.SharedSettingsFiles.Count);
            foreach (string path in DevBuildPlan.SharedSettingsFiles)
                Assert.IsTrue(File.Exists(path), $"되돌릴 공유 설정 파일이 없음 (이름이 바뀌었으면 목록도): {path}");
        }

        [Test]
        public void RestoreTargets_KeepSavedEdits_ButNotLineEndingOnlyChanges()
        {
            byte[] crlf = Encoding.UTF8.GetBytes("a: 1\r\nb: 2\r\n");
            byte[] lf = Encoding.UTF8.GetBytes("a: 1\nb: 2\n");
            byte[] edited = Encoding.UTF8.GetBytes("a: 1\nb: 3\n");
            var beforeSave = new Dictionary<string, byte[]> { ["eol"] = crlf, ["edit"] = crlf, ["same"] = crlf };
            var afterSave = new Dictionary<string, byte[]> { ["eol"] = lf, ["edit"] = edited, ["same"] = crlf, ["new"] = lf };
            var targets = DevBuildPlan.RestoreTargets(beforeSave, afterSave);
            Assert.AreEqual(4, targets.Count);
            Assert.AreSame(crlf, targets["eol"], "저장이 줄바꿈만 바꿨으면 저장 전 그대로");
            Assert.AreSame(edited, targets["edit"], "일부러 바꾼 내용은 저장한 대로");
            Assert.AreSame(crlf, targets["same"]);
            Assert.AreSame(lf, targets["new"]);
            Assert.IsFalse(DevBuildPlan.SameIgnoringLineEndings(crlf, null));
        }

        [Test]
        public void ChangedFiles_AreTheOnesThatDifferOrAreGone()
        {
            var targets = new Dictionary<string, byte[]>
            {
                ["same"] = new byte[] { 1, 2 },
                ["changed"] = new byte[] { 1, 2 },
                ["gone"] = new byte[] { 3 },
            };
            var now = new Dictionary<string, byte[]> { ["same"] = new byte[] { 1, 2 }, ["changed"] = new byte[] { 1, 2, 3 } };
            CollectionAssert.AreEquivalent(new[] { "changed", "gone" },
                DevBuildPlan.ChangedFiles(targets, path => now.TryGetValue(path, out var bytes) ? bytes : null));
            Assert.IsEmpty(DevBuildPlan.ChangedFiles(new Dictionary<string, byte[]>(), _ => null));
            StringAssert.StartsWith("[Build] 빌드 중 다시 저장된 공유 설정 2개를 빌드 전 내용으로 되돌림: ",
                DevBuildPlan.RestoredLine(new[] { "a", "b" }));
        }

        [Test]
        public void GitHead_ReadsBranchAndShortCommit_WithoutGit()
        {
            const string hash = "08456f0c1d2e3f4a5b6c7d8e9f0a1b2c3d4e5f60";
            var loose = new Dictionary<string, string> { ["HEAD"] = "ref: refs/heads/woosung\n", ["refs/heads/woosung"] = hash + "\n" };
            Assert.AreEqual(("woosung", "08456f0"), DevBuildPlan.GitHead(path => loose.TryGetValue(path, out var text) ? text : null));

            var packed = new Dictionary<string, string>
            {
                ["HEAD"] = "ref: refs/heads/feature/menu\r\n",
                ["packed-refs"] = "# pack-refs with: peeled fully-peeled sorted\n" +
                                  "1111111111111111111111111111111111111111 refs/heads/develop\n" +
                                  hash + " refs/heads/feature/menu\n" +
                                  "^2222222222222222222222222222222222222222\n",
            };
            Assert.AreEqual(("feature/menu", "08456f0"), DevBuildPlan.GitHead(path => packed.TryGetValue(path, out var text) ? text : null));

            Assert.AreEqual(("", "08456f0"), DevBuildPlan.GitHead(path => path == "HEAD" ? hash : null), "커밋을 꺼내 놓은 상태");
            Assert.AreEqual(("woosung", ""), DevBuildPlan.GitHead(path => path == "HEAD" ? "ref: refs/heads/woosung" : null), "커밋이 하나도 없음");
            Assert.AreEqual(("", ""), DevBuildPlan.GitHead(_ => null), ".git 없음");
        }

        [Test]
        public void Stamp_SaysKindTimeBranchCommitAndUncommittedChanges()
        {
            var at = new DateTime(2026, 10, 23, 14, 5, 30);
            Assert.AreEqual(-1, DevBuildPlan.UncommittedCount(null));
            Assert.AreEqual(0, DevBuildPlan.UncommittedCount(""));
            Assert.AreEqual(2, DevBuildPlan.UncommittedCount(" M Assets/Scripts/Run/PatrolRun.cs\n?? Assets/Scripts/Records/\n"));

            Assert.AreEqual("개발 빌드 2026-10-23 14:05 · woosung 08456f0", DevBuildPlan.Stamp(false, at, "woosung", "08456f0", 0));
            Assert.AreEqual("발표용 빌드 2026-10-23 14:05 · woosung 08456f0 · 커밋 안 한 변경 3개", DevBuildPlan.Stamp(true, at, "woosung", "08456f0", 3));
            Assert.AreEqual("개발 빌드 2026-10-23 14:05 · 08456f0 · 변경 여부 모름", DevBuildPlan.Stamp(false, at, "", "08456f0", -1));
            Assert.AreEqual("개발 빌드 2026-10-23 14:05 · 커밋 모름", DevBuildPlan.Stamp(false, at, "woosung", "", 5));
        }

        [Test]
        public void InfoText_StartsWithTheStamp_ThatTheGameLogReads()
        {
            const string stamp = "개발 빌드 2026-10-23 14:05 · woosung 08456f0";
            string text = DevBuildPlan.InfoText(stamp, new[] { "Assets/Scenes/Floor5.unity" }, "StandaloneWindows64", "6000.4.6f1", "0.1.0",
                DevBuildPlan.LogFolder("DefaultCompany", "NightPatrol"));
            StringAssert.StartsWith(stamp + "\n", text);
            StringAssert.Contains("씬: Assets/Scenes/Floor5.unity", text);
            StringAssert.Contains("Unity: 6000.4.6f1", text);
            StringAssert.Contains(@"%USERPROFILE%\AppData\LocalLow\DefaultCompany\NightPatrol\logs", text);

            Assert.AreEqual(stamp, DevBuildPlan.StampFromInfo(text));
            Assert.AreEqual(stamp, DevBuildPlan.StampFromInfo("﻿\r\n" + stamp + "\r\n씬: x"), "BOM·빈 줄·CRLF");
            Assert.AreEqual("", DevBuildPlan.StampFromInfo(null));
            Assert.AreEqual("", DevBuildPlan.StampFromInfo("\n  \n"));
        }

        [Test]
        public void Zip_IsNextToTheFolder_WithoutDoNotShipFolders()
        {
            Assert.AreEqual("Builds/NightPatrol_2026-10-23_1405.zip", DevBuildPlan.ZipPath("Builds/2026-10-23_1405", "NightPatrol"));
            Assert.AreEqual(@"Builds\Night_Patrol_2026-10-23_1405_release.zip", DevBuildPlan.ZipPath(@"Builds\2026-10-23_1405_release\", "Night:Patrol"));
            Assert.IsTrue(DevBuildPlan.ShipsInZip("NightPatrol.exe"));
            Assert.IsTrue(DevBuildPlan.ShipsInZip("build.txt"));
            Assert.IsTrue(DevBuildPlan.ShipsInZip("NightPatrol_Data/globalgamemanagers"));
            Assert.IsFalse(DevBuildPlan.ShipsInZip("NightPatrol_BurstDebugInformation_DoNotShip/Data/lib_burst_generated.txt"));
            Assert.IsFalse(DevBuildPlan.ShipsInZip(@"NightPatrol_BackUpThisFolder_ButDontShipItWithYourGame\x.cpp"));
        }

        [Test]
        public void OldBuilds_KeepTheNewestOfEachKind_AndOnlyBuildMenuNames()
        {
            var names = new[]
            {
                "2026-10-12_1543", "2026-10-12_0723", "2026-10-12_0908", "2026-10-12_0919", "2026-10-12_1542",
                "NightPatrol_2026-10-12_0908.zip",
                "2026-10-01_0900_release", "NightPatrol_2026-10-01_0900_release.zip",
                "발표_최종", "notes.zip", "2026-10-12",
            };
            CollectionAssert.AreEqual(new[] { "2026-10-12_0723", "2026-10-12_0908", "NightPatrol_2026-10-12_0908.zip" },
                DevBuildPlan.OldBuilds(names, DevBuildPlan.KeepBuilds));
            Assert.IsEmpty(DevBuildPlan.OldBuilds(new[] { "2026-10-12_0723", "2026-10-12_0723_2" }, 3));
            CollectionAssert.AreEqual(new[] { "2026-10-12_0723" }, DevBuildPlan.OldBuilds(new[] { "2026-10-12_0723", "2026-10-12_0723_2" }, 1));
            Assert.IsEmpty(DevBuildPlan.OldBuilds(null, 3));
        }
    }
}
