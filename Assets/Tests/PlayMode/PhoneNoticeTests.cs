using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NightPatrol.Interaction;
using NightPatrol.Journal;
using NightPatrol.Phone;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace NightPatrol.Tests
{
    // B5 (11/03): 메시지가 오면 전화가 주머니에 있을 때만 자막 줄에 2초 "관리실 메시지 — Q로 확인".
    // 울림·통화 자막은 건드리지 않고, 꺼내면 바로 지운다. 시간은 TickNotice를 직접 불러 진행한다
    public class PhoneNoticeTests
    {
        private readonly List<GameObject> created = new List<GameObject>();
        private PhoneHolder phone;
        private PhoneScreen screen;
        private SubtitleView subtitles;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            var player = New("Player");
            LogAssert.Expect(LogType.Error, new Regex("PlayerInteractor에 PlayerSettings"));
            player.AddComponent<PlayerInteractor>();
            Child(player, "Clipboard").AddComponent<JournalHolder>();
            subtitles = Child(player, "HUD").AddComponent<SubtitleView>();
            var phoneObject = Child(player, "Phone");
            phoneObject.layer = LayerMask.NameToLayer("FirstPerson");
            phoneObject.SetActive(false);
            phone = phoneObject.AddComponent<PhoneHolder>();
            screen = phoneObject.AddComponent<PhoneScreen>();
            phoneObject.SetActive(true);
            yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in created)
                if (go != null) Object.DestroyImmediate(go);
            created.Clear();
        }

        private GameObject New(string name)
        {
            var go = new GameObject(name);
            created.Add(go);
            return go;
        }

        private static GameObject Child(GameObject parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform);
            return go;
        }

        private MessagesApp Messages()
        {
            foreach (var app in screen.Installed)
                if (app is MessagesApp messages) return messages;
            return null;
        }

        [Test]
        public void AMessage_InThePocket_ShowsANoticeForTwoSeconds()
        {
            Messages().Receive("관리실", "3층은 전화가 안 될 거예요.");
            Assert.AreEqual("관리실 메시지 — Q로 확인", subtitles.Current);
            phone.TickNotice(1.9f);
            Assert.AreEqual("관리실 메시지 — Q로 확인", subtitles.Current);
            phone.TickNotice(0.2f);
            Assert.AreEqual(string.Empty, subtitles.Current);
            Assert.IsNull(phone.Notice);
        }

        [Test]
        public void InHand_NoNotice_TakingItOut_ClearsTheNotice()
        {
            phone.TakeOut();
            Messages().Receive("관리실", "들고 있을 때");
            Assert.AreEqual(string.Empty, subtitles.Current, "들고 있으면 화면에 보이므로 자막 없음");
            phone.Stow();
            Messages().Receive("관리실", "주머니에 있을 때");
            Assert.IsNotNull(phone.Notice);
            phone.TakeOut();
            Assert.AreEqual(string.Empty, subtitles.Current, "꺼내면 바로 지움");
        }

        [Test]
        public void ACallSubtitle_IsNeverReplacedOrCleared()
        {
            phone.Ring(new PhoneCall { id = "시험", lines = new[] { new PhoneLine { speaker = "관리실", text = "여보세요", seconds = 2f } } });
            string ringing = subtitles.Current;
            StringAssert.Contains("받기", ringing);
            Messages().Receive("관리실", "울리는 중");
            Assert.AreEqual(ringing, subtitles.Current, "울림 자막 그대로");

            phone.ResetCall();
            Messages().Receive("관리실", "알림");
            phone.Ring(new PhoneCall { id = "시험2", lines = new[] { new PhoneLine { speaker = "관리실", text = "여보세요", seconds = 2f } } });
            string second = subtitles.Current;
            phone.TickNotice(3f);
            Assert.AreEqual(second, subtitles.Current, "알림 시간이 지나도 그사이 뜬 울림 자막은 안 지움");
        }
    }
}
