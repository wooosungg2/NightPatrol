using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NightPatrol.Audio;
using NightPatrol.Interaction;
using NightPatrol.Journal;
using NightPatrol.Lighting;
using NightPatrol.Phone;
using NightPatrol.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace NightPatrol.Tests
{
    // 06_01: 손에 드는 전화 — 꺼내기·받기·넣기, 한 손(일지), 부재중 뒤 손 상태(00_17e), 조작 막기, 연출 잠금, 메뉴 정지, 손전등은 그대로
    public class PhoneHolderTests
    {
        private GameObject player;
        private PlayerInteractor interactor;
        private JournalHolder journal;
        private SubtitleView subtitles;
        private SoundEmitter sounds;
        private PhoneHolder phone;
        private AudioClip vibrateClip, bellClip;
        private readonly List<(PhoneCall call, bool finished)> ended = new List<(PhoneCall, bool)>();
        private readonly List<PhoneCall> missed = new List<PhoneCall>();
        private float timeScaleBefore;

        [SetUp]
        public void SetUp()
        {
            timeScaleBefore = Time.timeScale;
            player = new GameObject("Player");
            LogAssert.Expect(LogType.Error, new Regex("PlayerInteractor에 PlayerSettings"));
            interactor = player.AddComponent<PlayerInteractor>();
            journal = Child("Clipboard").AddComponent<JournalHolder>();
            subtitles = Child("HUD").AddComponent<SubtitleView>();

            var phoneObject = Child("Phone");
            sounds = phoneObject.AddComponent<SoundEmitter>();
            vibrateClip = AudioClip.Create("vibrate", 441, 1, 44100, false);
            bellClip = AudioClip.Create("bell", 441, 1, 44100, false);
            var cues = new[] { PhoneHolder.VibrateSound, PhoneHolder.BellSound, PhoneHolder.RingbackSound,
                    PhoneHolder.AnswerSound, PhoneHolder.HangUpSound, PhoneHolder.StaticSound }
                .Select(name => new SoundEmitter.Cue
                {
                    name = name,
                    clips = name == PhoneHolder.VibrateSound ? new[] { vibrateClip }
                        : name == PhoneHolder.BellSound ? new[] { bellClip } : new AudioClip[0],
                })
                .ToArray();
            SetField(sounds, "cues", cues);

            phone = phoneObject.AddComponent<PhoneHolder>();
            SetField(phone, "missSeconds", 0.3f);
            SetField(phone, "ringInterval", 0.1f);
            ended.Clear();
            missed.Clear();
            phone.CallEnded += (call, finished) => ended.Add((call, finished));
            phone.Missed += missed.Add;
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = timeScaleBefore;
            Object.DestroyImmediate(player);
            Object.DestroyImmediate(vibrateClip);
            Object.DestroyImmediate(bellClip);
        }

        private GameObject Child(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(player.transform);
            return go;
        }

        private static void SetField(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        private static PhoneCall Call(string id, params string[] lines) => new PhoneCall
        {
            id = id,
            lines = lines.Select(text => new PhoneLine { text = text, seconds = 0.5f }).ToArray(),
        };

        // ── 꺼내기·넣기, 한 손 ──

        [Test]
        public void TakeOut_BlocksInteraction_StowUnblocks()
        {
            phone.TakeOut();
            Assert.AreEqual(PhoneState.Held, phone.State);
            Assert.IsTrue(phone.IsInHand);
            Assert.IsTrue(interactor.IsBlocked, "전화를 든 동안 E(사물·엘리베이터) 막힘");
            phone.Stow();
            Assert.AreEqual(PhoneState.Stowed, phone.State);
            Assert.IsFalse(interactor.IsBlocked);
        }

        [Test]
        public void TakeOut_LowersTheJournal()
        {
            journal.SetRaised(true);
            phone.TakeOut();
            Assert.IsFalse(journal.IsRaised);
            Assert.AreEqual(PhoneState.Held, phone.State);
        }

        // 같은 왼손: 휴대폰을 든 동안 일지는 화면 밖으로 치우고, 넣으면 되돌린다
        [UnityTest]
        public IEnumerator HoldingThePhone_PutsTheJournalAway()
        {
            yield return null;
            phone.TakeOut();
            yield return null;
            Assert.IsTrue(journal.IsHidden);
            phone.Stow();
            yield return null;
            Assert.IsFalse(journal.IsHidden);
        }

        [Test]
        public void RaisingTheJournal_StowsThePhone()
        {
            phone.TakeOut();
            journal.SetRaised(true);
            Assert.IsTrue(journal.IsRaised);
            Assert.AreEqual(PhoneState.Stowed, phone.State);
            Assert.IsFalse(phone.IsInHand);
        }

        // ── 수신·받기·통화 ──

        [Test]
        public void Ring_InPocket_ShowsHowToAnswer_AndLeavesInteractionFree()
        {
            Assert.IsTrue(phone.Ring(Call("C01", "첫 줄")));
            Assert.AreEqual(PhoneState.Ringing, phone.State);
            Assert.IsFalse(phone.IsInHand);
            Assert.IsFalse(interactor.IsBlocked, "주머니에서 울리는 동안은 사물 조작 가능");
            StringAssert.Contains("Q 받기", subtitles.Current);
            Assert.IsFalse(phone.Ring(Call("C02", "또")), "울리는 중에는 새 수신 안 됨");
        }

        [Test]
        public void Answer_TakesItOut_ShowsTheFirstLine_AndBlocksTheJournal()
        {
            phone.Ring(Call("C01", "첫 줄", "둘째 줄"));
            phone.Answer();
            Assert.AreEqual(PhoneState.Talking, phone.State);
            Assert.IsTrue(phone.IsInHand);
            Assert.AreEqual("관리실: 첫 줄", subtitles.Current);
            journal.SetRaised(true);
            Assert.IsFalse(journal.IsRaised, "통화 중에는 Tab으로 일지를 들지 못함 (실수로 끊기지 않게)");
        }

        [UnityTest]
        public IEnumerator Call_PlaysEveryLine_ThenStowsItself()
        {
            var call = Call("C01", "첫 줄", "둘째 줄");
            phone.Ring(call);
            phone.Answer();
            yield return new WaitForSeconds(0.6f);
            Assert.AreEqual("관리실: 둘째 줄", subtitles.Current);
            yield return new WaitForSeconds(0.6f);
            Assert.AreEqual(PhoneState.Stowed, phone.State);
            Assert.AreEqual(string.Empty, subtitles.Current);
            Assert.AreEqual(1, ended.Count);
            Assert.AreSame(call, ended[0].call);
            Assert.IsTrue(ended[0].finished);
            Assert.IsFalse(interactor.IsBlocked);
            journal.SetRaised(true);
            Assert.IsTrue(journal.IsRaised, "통화가 끝나면 일지를 다시 들 수 있음");
        }

        [Test]
        public void Stow_DuringACall_HangsUp()
        {
            phone.Ring(Call("C01", "첫 줄"));
            phone.Answer();
            phone.Stow();
            Assert.AreEqual(PhoneState.Stowed, phone.State);
            Assert.AreEqual(1, ended.Count);
            Assert.IsFalse(ended[0].finished);
            Assert.AreEqual(string.Empty, subtitles.Current);
        }

        [Test]
        public void GarbledCall_ShowsBlocks()
        {
            var call = Call("C07", "신입 씨 들려요 내려와요");
            call.garbled = true;
            phone.Ring(call);
            phone.Answer();
            StringAssert.Contains("█", subtitles.Current);
            StringAssert.StartsWith("관리실: ", subtitles.Current);
        }

        // ── 부재중 (손은 지금 그대로) ──

        [UnityTest]
        public IEnumerator NotAnswered_InPocket_IsMissed_AndStaysInThePocket()
        {
            var call = Call("C01", "첫 줄");
            phone.Ring(call);
            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual(PhoneState.Stowed, phone.State);
            CollectionAssert.AreEqual(new[] { call }, missed);
            Assert.AreEqual(string.Empty, subtitles.Current);
        }

        [UnityTest]
        public IEnumerator NotAnswered_InHand_StaysInHand()
        {
            phone.TakeOut();
            phone.Ring(Call("C01", "첫 줄"));
            Assert.IsTrue(phone.IsInHand);
            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual(PhoneState.Held, phone.State);
            Assert.AreEqual(1, missed.Count);
        }

        // 00_17e: 울리는 중 일지를 들었다가 부재중이 되면 전화가 다시 손에 오지 않는다 (일지와 같이 들리지 않게)
        [UnityTest]
        public IEnumerator Ringing_JournalRaised_ThenMissed_PhoneDoesNotComeBack()
        {
            phone.TakeOut();
            phone.Ring(Call("C01", "첫 줄"));
            journal.SetRaised(true);
            Assert.AreEqual(PhoneState.Ringing, phone.State, "주머니에서 계속 울림");
            Assert.IsFalse(phone.IsInHand);
            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual(PhoneState.Stowed, phone.State);
            Assert.IsFalse(phone.IsInHand);
            Assert.IsTrue(journal.IsRaised);
        }

        // ── 걸기 ──

        [UnityTest]
        public IEnumerator Dial_UsesTheOutgoingCall_RingbackFirst()
        {
            phone.OutgoingCall = () => new PhoneCall
            {
                id = "C06",
                ringbackSeconds = 0.3f,
                lines = new[] { new PhoneLine { text = "왜요?", seconds = 1f } },
            };
            Assert.IsFalse(phone.Dial(), "들고 있을 때만");
            phone.TakeOut();
            Assert.IsTrue(phone.Dial());
            Assert.AreEqual(PhonePlayback.Stage.Ringback, phone.Playback.Phase);
            Assert.AreEqual(string.Empty, subtitles.Current);
            yield return new WaitForSeconds(0.45f);
            Assert.AreEqual("관리실: 왜요?", subtitles.Current);
        }

        [Test]
        public void Dial_WithoutAProvider_OnlyRingsBack()
        {
            phone.TakeOut();
            phone.Dial();
            Assert.AreEqual(PhoneState.Talking, phone.State);
            Assert.AreEqual(PhonePlayback.Stage.Ringback, phone.Playback.Phase);
            Assert.AreEqual("응답 없음", phone.CurrentCall.id);
        }

        // ── 막힘·끊기·정리 ──

        [Test]
        public void Blocked_EndsTheCall_AndIgnoresRingAndTakeOut_UntilReleased()
        {
            var call = Call("C01", "첫 줄");
            phone.Ring(call);
            phone.Answer();
            var owner = new object();
            phone.SetBlocked(owner, true);
            Assert.AreEqual(PhoneState.Stowed, phone.State);
            Assert.IsFalse(ended[0].finished);
            Assert.IsFalse(phone.Ring(call));
            phone.TakeOut();
            Assert.AreEqual(PhoneState.Stowed, phone.State);
            phone.SetBlocked(owner, false);
            phone.TakeOut();
            Assert.AreEqual(PhoneState.Held, phone.State);
        }

        [Test]
        public void Blocked_WhileRinging_IsMissed()
        {
            phone.Ring(Call("C01", "첫 줄"));
            phone.SetBlocked(new object(), true);
            Assert.AreEqual(1, missed.Count);
            Assert.AreEqual(PhoneState.Stowed, phone.State);
        }

        [Test]
        public void HangUp_RingingIsMissed_TalkingEnds()
        {
            phone.Ring(Call("C01", "첫 줄"));
            phone.HangUp();
            Assert.AreEqual(1, missed.Count);
            phone.Ring(Call("C02", "첫 줄"));
            phone.Answer();
            phone.HangUp();
            Assert.AreEqual(1, ended.Count);
            Assert.IsFalse(ended[0].finished);
        }

        // 층이 바뀔 때(PhoneCalls): 이벤트 없이 끝내고 손은 그대로
        [Test]
        public void ResetCall_EndsQuietly_AndKeepsTheHand()
        {
            phone.TakeOut();
            phone.Ring(Call("C01", "첫 줄"));
            phone.ResetCall();
            Assert.AreEqual(PhoneState.Held, phone.State);
            phone.Ring(Call("C02", "첫 줄"));
            phone.Answer();
            phone.ResetCall();
            Assert.AreEqual(PhoneState.Held, phone.State);
            Assert.IsNull(phone.CurrentCall);
            Assert.IsEmpty(missed);
            Assert.IsEmpty(ended);
            Assert.AreEqual(string.Empty, subtitles.Current);
        }

        // 층 이동·깨어나기 연출(PlayerControl.Lock)은 전화를 넣고 끊는다. 메뉴(includeJournal false)는 그대로 둔다
        [Test]
        public void PlayerControlLock_StowsAndHangsUp_ButTheMenuLockDoesNot()
        {
            var control = player.AddComponent<PlayerControl>();
            phone.Ring(Call("C01", "첫 줄"));
            phone.Answer();
            var menu = new object();
            control.Lock(menu, true, includeJournal: false);
            Assert.AreEqual(PhoneState.Talking, phone.State);
            control.Lock(menu, false, includeJournal: false);

            var cutscene = new object();
            control.Lock(cutscene, true);
            Assert.AreEqual(PhoneState.Stowed, phone.State);
            Assert.IsTrue(phone.IsBlocked);
            control.Lock(cutscene, false);
            Assert.IsFalse(phone.IsBlocked);
        }

        [Test]
        public void Disable_ReleasesEverything()
        {
            phone.Ring(Call("C01", "첫 줄"));
            phone.Answer();
            phone.enabled = false;
            Assert.IsFalse(interactor.IsBlocked);
            journal.SetRaised(true);
            Assert.IsTrue(journal.IsRaised);
            Assert.AreEqual(string.Empty, subtitles.Current);
        }

        // ── 시간·소리·손전등 ──

        // ESC 메뉴(시간 정지) 동안 울림 시간도 멈춘다. 메뉴가 클립보드를 쓰는 동안 전화는 넣은 자세로만 보인다
        [UnityTest]
        public IEnumerator Menu_PausesTheRing_AndShowsThePhoneStowed()
        {
            phone.TakeOut();
            phone.Ring(Call("C01", "첫 줄"));
            journal.SetMenuOpen(true);
            Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.AreEqual(PhoneState.Ringing, phone.State, "시간 정지 중에는 부재중이 되지 않음");
            Assert.That(Vector3.Distance(phone.transform.localPosition, new Vector3(-0.12f, -0.42f, 0.22f)), Is.LessThan(0.001f));
            Time.timeScale = 1f;
            journal.SetMenuOpen(false);
            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual(1, missed.Count);
        }

        [UnityTest]
        public IEnumerator RingSound_VibratesInPocket_BellsInHand()
        {
            SetField(phone, "missSeconds", 5f);   // 프레임이 느려도 벨 전에 부재중이 되지 않게
            phone.Ring(Call("C01", "첫 줄"));
            yield return null;
            yield return null;
            Assert.AreSame(vibrateClip, sounds.LastClip);
            phone.TakeOut();
            yield return new WaitForSeconds(0.15f);
            Assert.AreSame(bellClip, sounds.LastClip);
        }

        // ── 키 입력 나누기 (Route — 키 장치 없이) ──

        // 화면이 없는 전화: Q 꺼내기, 꺼낸 프레임의 E는 무시, 다음 프레임 E = 관리실에 걸기, 통화 중 Q = 끊고 넣기
        [UnityTest]
        public IEnumerator Route_NoScreen_QTakesOut_ENextFrameDials_QHangsUp()
        {
            phone.Route(true, false, 0f, 0f);
            Assert.AreEqual(PhoneState.Held, phone.State);
            phone.Route(false, true, 0f, 0f);
            Assert.AreEqual(PhoneState.Held, phone.State, "꺼낸 프레임의 E는 꺼낸 누름과 같이 온 것");
            yield return null;
            phone.Route(false, true, 0f, 0f);
            Assert.AreEqual(PhoneState.Talking, phone.State);
            phone.Route(true, false, 0f, 0f);
            Assert.AreEqual(PhoneState.Stowed, phone.State);
        }

        [Test]
        public void Route_PressedTogether_PhoneKeyWins()
        {
            phone.Route(true, true, -120f, 0f);
            Assert.AreEqual(PhoneState.Held, phone.State, "Q만 — 같은 프레임의 E로 걸지 않음");
            Assert.IsNull(phone.CurrentCall);
        }

        [UnityTest]
        public IEnumerator Route_RingingInThePocket_EAndWheelIgnored_QAnswers()
        {
            phone.Ring(Call("C01", "첫 줄"));
            yield return null;
            phone.Route(false, true, 0f, 0f);
            Assert.AreEqual(PhoneState.Ringing, phone.State, "주머니에 있을 때 E는 사물 조작 키");
            phone.Route(false, false, -120f, 0f);
            Assert.AreEqual(PhoneState.Ringing, phone.State);
            phone.Route(true, false, 0f, 0f);
            Assert.AreEqual(PhoneState.Talking, phone.State);
        }

        [UnityTest]
        public IEnumerator Route_RingingInHand_EAnswers()
        {
            phone.TakeOut();
            phone.Ring(Call("C01", "첫 줄"));
            yield return null;
            phone.Route(false, true, 0f, 0f);
            Assert.AreEqual(PhoneState.Talking, phone.State);
        }

        // 06_03: 메시지 진동은 넣어 두었거나 들고 있을 때만 (통화 중에는 안 낸다)
        [Test]
        public void Buzz_VibratesOnlyWhenNotInACall()
        {
            phone.Ring(Call("C01", "첫 줄"));
            phone.Answer();
            phone.Buzz();
            Assert.IsNull(sounds.LastClip, "통화 중에는 진동 없음");
            phone.HangUp();
            phone.Buzz();
            Assert.AreSame(vibrateClip, sounds.LastClip);
        }

        // 06_03: 최근 기록용 — 받으면 (통화, false), 걸면 (통화, true)
        [Test]
        public void CallStarted_TellsIncomingFromOutgoing()
        {
            var started = new List<(PhoneCall call, bool outgoing)>();
            phone.CallStarted += (call, outgoing) => started.Add((call, outgoing));
            var incoming = Call("C01", "첫 줄");
            phone.Ring(incoming);
            phone.Answer();
            phone.HangUp();
            phone.TakeOut();
            phone.Dial();
            Assert.AreEqual(2, started.Count);
            Assert.AreSame(incoming, started[0].call);
            Assert.IsFalse(started[0].outgoing);
            Assert.IsTrue(started[1].outgoing);
        }

        // 사용자 결정 (11/02): 손전등은 전화와 상관없이 그대로 (전화는 왼손, 손전등은 오른손)
        [Test]
        public void Flashlight_StaysOn_ThroughThePhone()
        {
            var flashlight = player.AddComponent<Flashlight>();
            flashlight.SetOn(true);
            phone.TakeOut();
            phone.Stow();
            phone.Ring(Call("C01", "첫 줄"));
            phone.Answer();
            phone.SetBlocked(new object(), true);
            Assert.IsTrue(flashlight.IsOn);
        }
    }
}
