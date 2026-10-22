using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NightPatrol.Audio;
using NightPatrol.Run;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace NightPatrol.Tests
{
    // 01_10: 사물 소리 SoundEmitter — 이름으로 내기, 같은 파일 연달아 안 냄, 높낮이 범위, 층 시작 중 조용, 저절로 울릴 순간, 3D 설정
    public class SoundEmitterTests
    {
        private readonly List<Object> created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = created.Count - 1; i >= 0; i--)
                if (created[i] != null) Object.DestroyImmediate(created[i]);
            created.Clear();
        }

        private T Track<T>(T obj) where T : Object
        {
            created.Add(obj);
            return obj;
        }

        private AudioClip Clip(string clipName) => Track(AudioClip.Create(clipName, 441, 1, 44100, false));

        private static SoundEmitter.Cue Cue(string cueName, float pitchJitter, params AudioClip[] clips) =>
            new SoundEmitter.Cue { name = cueName, clips = clips, pitchJitter = pitchJitter };

        private SoundEmitter Make(params SoundEmitter.Cue[] cues)
        {
            var go = Track(new GameObject("Door"));
            go.SetActive(false);
            var emitter = go.AddComponent<SoundEmitter>();
            typeof(SoundEmitter).GetField("cues", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(emitter, cues);
            go.SetActive(true);
            return emitter;
        }

        [UnityTest]
        public IEnumerator Play_ByName_NeverRepeatsTheSameClipTwiceInARow()
        {
            yield return null;   // 씬을 막 불러온 순간이 아니게
            var emitter = Make(Cue("열림", 0f, Clip("a"), Clip("b"), Clip("c")));

            AudioClip previous = null;
            var used = new HashSet<AudioClip>();
            for (int i = 0; i < 30; i++)
            {
                emitter.Play("열림");
                Assert.AreNotSame(previous, emitter.LastClip, $"{i}번째에 같은 파일이 연달아 나옴");
                previous = emitter.LastClip;
                used.Add(previous);
            }
            Assert.AreEqual(30, emitter.PlayCount);
            Assert.That(used.Count, Is.GreaterThanOrEqualTo(2));
            Assert.IsTrue(emitter.Has("열림"));
            Assert.IsFalse(emitter.Has("닫힘"));
        }

        [UnityTest]
        public IEnumerator PitchJitter_StaysInRange_ZeroKeepsOriginalPitch()
        {
            yield return null;
            var emitter = Make(Cue("삐걱", 0.1f, Clip("creak")), Cue("띵", 0f, Clip("ding")));

            bool varied = false;
            for (int i = 0; i < 20; i++)
            {
                emitter.Play("삐걱");
                Assert.That(emitter.LastPitch, Is.InRange(0.9f, 1.1f));
                if (!Mathf.Approximately(emitter.LastPitch, 1f)) varied = true;
            }
            Assert.IsTrue(varied, "높낮이가 한 번도 바뀌지 않음");

            emitter.Play("띵");
            Assert.AreEqual(1f, emitter.LastPitch, "흔들림 0이면 원래 높이");
        }

        [UnityTest]
        public IEnumerator UnknownName_WarnsOncePerName_AndPlaysNothing()
        {
            yield return null;
            var emitter = Make(Cue("열림", 0f, Clip("a")));
            LogAssert.Expect(LogType.Warning, new Regex("소리 묶음 '열린'이 없음"));

            emitter.Play("열린");
            emitter.Play("열린");

            Assert.AreEqual(0, emitter.PlayCount);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator EmptyCue_AndInactiveObject_AreSilent()
        {
            yield return null;
            var empty = Make(Cue("열림", 0f));
            empty.Play("열림");
            Assert.AreEqual(0, empty.PlayCount, "파일이 아직 없으면 조용");

            var inactive = Make(Cue("열림", 0f, Clip("a")));
            inactive.gameObject.SetActive(false);
            inactive.Play("열림");
            Assert.AreEqual(0, inactive.PlayCount, "꺼진 오브젝트는 조용");
        }

        [UnityTest]
        public IEnumerator QuietWhileAFloorStarts_ThenPlaysAgain()
        {
            var run = Track(new GameObject("PatrolRun")).AddComponent<PatrolRun>();
            yield return null;   // Start: 첫 층 시작
            var emitter = Make(Cue("열림", 0f, Clip("a")));
            bool quietDuring = false;
            run.FloorStarted += _ =>
            {
                quietDuring = SoundEmitter.IsQuiet;
                emitter.Play("열림");   // 층 초기화 때 문 상태가 바뀌어 소리를 내려는 경우
            };

            run.RestartFloor();

            Assert.IsTrue(quietDuring);
            Assert.AreEqual(0, emitter.PlayCount, "층이 시작되는 동안은 조용");
            Assert.IsFalse(run.IsStartingFloor);
            Assert.IsFalse(SoundEmitter.IsQuiet);

            emitter.Play("열림");
            Assert.AreEqual(1, emitter.PlayCount, "층이 시작된 뒤에는 소리를 냄");
        }

        [UnityTest]
        public IEnumerator Moment_PlaysOnlyTheCuesSetForIt()
        {
            yield return null;
            AudioClip descend = Clip("descend"), arrive = Clip("arrive"), open = Clip("open");
            var emitter = Make(
                new SoundEmitter.Cue { name = "하행", clips = new[] { descend }, pitchJitter = 0f, playOn = SoundEmitter.Moment.DescendStarted },
                new SoundEmitter.Cue { name = "도착", clips = new[] { arrive }, pitchJitter = 0f, playOn = SoundEmitter.Moment.Arrived },
                Cue("열림", 0f, open));

            emitter.PlayMoment(SoundEmitter.Moment.Arrived);
            Assert.AreEqual(1, emitter.PlayCount);
            Assert.AreSame(arrive, emitter.LastClip);

            emitter.PlayMoment(SoundEmitter.Moment.DescendStarted);
            Assert.AreEqual(2, emitter.PlayCount);
            Assert.AreSame(descend, emitter.LastClip);

            emitter.PlayMoment(SoundEmitter.Moment.None);
            Assert.AreEqual(2, emitter.PlayCount);
        }

        [UnityTest]
        public IEnumerator Sources_Are3D_OnePerCue()
        {
            yield return null;
            var emitter = Make(Cue("열림", 0.05f, Clip("a")), Cue("닫힘", 0.05f, Clip("b")));

            emitter.Play("열림");
            emitter.Play("열림");
            var sources = emitter.GetComponents<AudioSource>();
            Assert.AreEqual(1, sources.Length, "같은 묶음은 같은 소스");

            emitter.Play("닫힘");
            sources = emitter.GetComponents<AudioSource>();
            Assert.AreEqual(2, sources.Length, "묶음마다 따로 (높낮이가 섞이지 않게)");
            foreach (var source in sources)
            {
                Assert.AreEqual(1f, source.spatialBlend, "방향·거리가 있는 3D");
                Assert.AreEqual(0f, source.dopplerLevel);
                Assert.AreEqual(AudioRolloffMode.Linear, source.rolloffMode);
                Assert.AreEqual(1f, source.minDistance);
                Assert.AreEqual(20f, source.maxDistance);
                Assert.IsFalse(source.playOnAwake);
            }
        }
    }
}
