using System.Collections.Generic;
using NightPatrol.Menu;
using NUnit.Framework;
using UnityEngine;

namespace NightPatrol.Tests
{
    public class ScreenOptionsTests
    {
        private static Resolution Res(int width, int height) => new Resolution { width = width, height = height };

        private static readonly List<Vector2Int> Common = new List<Vector2Int>
        {
            new Vector2Int(1280, 720), new Vector2Int(1600, 900), new Vector2Int(1920, 1080),
        };

        [Test]
        public void ResolutionOptions_DistinctSorted_SmallOnesDropped()
        {
            var supported = new[] { Res(1920, 1080), Res(640, 480), Res(1280, 720), Res(1920, 1080), Res(1600, 900) };
            var options = ScreenOptions.ResolutionOptions(supported, new Vector2Int(1920, 1080));
            CollectionAssert.AreEqual(Common, options);
        }

        [Test]
        public void ResolutionOptions_AddsTheCurrentSizeIfMissing()
        {
            var options = ScreenOptions.ResolutionOptions(new[] { Res(1920, 1080) }, new Vector2Int(1366, 768));
            CollectionAssert.AreEqual(new[] { new Vector2Int(1366, 768), new Vector2Int(1920, 1080) }, options);
            Assert.IsEmpty(ScreenOptions.ResolutionOptions(null, Vector2Int.zero));
        }

        [Test]
        public void StepResolution_MovesOneAndStopsAtTheEnds()
        {
            Assert.AreEqual(new Vector2Int(1600, 900), ScreenOptions.StepResolution(Common, new Vector2Int(1920, 1080), -1));
            Assert.AreEqual(new Vector2Int(1920, 1080), ScreenOptions.StepResolution(Common, new Vector2Int(1920, 1080), 1));
            Assert.AreEqual(new Vector2Int(1280, 720), ScreenOptions.StepResolution(Common, new Vector2Int(1280, 720), -1));
            Assert.AreEqual(new Vector2Int(800, 600), ScreenOptions.StepResolution(new List<Vector2Int>(), new Vector2Int(800, 600), 1));
        }

        [Test]
        public void StepResolution_FromAnUnlistedSize_StartsAtTheNearest()
        {
            // 1700×950 is closest to 1600×900 by pixel count
            Assert.AreEqual(1, ScreenOptions.NearestIndex(Common, new Vector2Int(1700, 950)));
            Assert.AreEqual(new Vector2Int(1920, 1080), ScreenOptions.StepResolution(Common, new Vector2Int(1700, 950), 1));
        }

        [Test]
        public void WindowedSize_ShrinksOnlyAFullDisplaySize()
        {
            var display = new Vector2Int(1920, 1080);
            Assert.AreEqual(new Vector2Int(1600, 900), ScreenOptions.WindowedSize(Common, display, display));
            Assert.AreEqual(new Vector2Int(1280, 720), ScreenOptions.WindowedSize(Common, new Vector2Int(1280, 720), display));
        }

        [Test]
        public void Labels()
        {
            Assert.AreEqual("1920×1080", ScreenOptions.ResolutionLabel(new Vector2Int(1920, 1080)));
            Assert.AreEqual("전체 화면", ScreenOptions.ScreenModeLabel(true));
            Assert.AreEqual("창 모드", ScreenOptions.ScreenModeLabel(false));
            Assert.IsTrue(ScreenOptions.IsFullScreen(FullScreenMode.FullScreenWindow));
            Assert.IsTrue(ScreenOptions.IsFullScreen(FullScreenMode.ExclusiveFullScreen));
            Assert.IsFalse(ScreenOptions.IsFullScreen(FullScreenMode.Windowed));
        }

        // 01_05d: 에디터 Game 뷰는 Screen.SetResolution을 무시하므로 메뉴 값 뒤에 안내
        [Test]
        public void Labels_InEditor_AddTheBuildNote()
        {
            Assert.AreEqual("1920×1080 (빌드에서 적용)", ScreenOptions.ResolutionLabel(new Vector2Int(1920, 1080), true));
            Assert.AreEqual("1920×1080", ScreenOptions.ResolutionLabel(new Vector2Int(1920, 1080), false));
            Assert.AreEqual("전체 화면 (빌드에서 적용)", ScreenOptions.ScreenModeLabel(true, true));
            Assert.AreEqual("창 모드 (빌드에서 적용)", ScreenOptions.ScreenModeLabel(false, true));
            Assert.AreEqual("창 모드", ScreenOptions.ScreenModeLabel(false, false));
        }

        [Test]
        public void ChangeLog_ShowsTheRequestAndWhatTheScreenReallyIs()
        {
            Assert.AreEqual("[Menu] 화면: 요청 1280×720 창 모드 → 실제 1280×720 Windowed",
                ScreenOptions.ChangeLog(new Vector2Int(1280, 720), false, new Vector2Int(1280, 720), FullScreenMode.Windowed));
            Assert.AreEqual("[Menu] 화면: 요청 1920×1080 전체 화면 → 실제 1600×900 FullScreenWindow",
                ScreenOptions.ChangeLog(new Vector2Int(1920, 1080), true, new Vector2Int(1600, 900), FullScreenMode.FullScreenWindow));
        }
    }
}
