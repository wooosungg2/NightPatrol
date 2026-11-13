using System;
using System.Collections.Generic;
using NightPatrol.Run;
using TMPro;
using UnityEngine;

namespace NightPatrol.Phone
{
    /// <summary>
    /// 메시지 앱 (독 둘째 칸): 받은 메시지 목록(새것이 위, 안 읽음은 굵게 + 파란 점) → E로 읽기 → Q로 목록.
    /// 층이 시작되면 층·회차가 맞는 메시지를 N초 뒤에 받는다 (PatrolRun.Schedule, Id마다 한 판에 한 번). Receive로 어디서든 보낼 수 있다
    /// </summary>
    public sealed class MessagesApp : PhoneApp
    {
        /// <summary>예약해서 받는 메시지 한 줄 (층·회차 조건 + 층 시작 뒤 시간)</summary>
        [Serializable]
        public sealed class Entry
        {
            [Tooltip("메시지 Id (F1·로그). 한 판에 한 번만 받는 기준 — 비우면 내용으로")]
            public string id = "";
            [Tooltip("보낸 사람")]
            public string sender = CallApp.Contact;
            [Tooltip("내용")]
            [TextArea] public string text = "";
            [Tooltip("층 (0이면 모든 층)")]
            public int floor;
            [Tooltip("이 회차부터")]
            [Min(1)] public int fromAttempt = 1;
            [Tooltip("이 회차까지 (0이면 끝없음)")]
            [Min(0)] public int toAttempt;
            [Tooltip("층 시작 뒤 받는 시간(초)")]
            [Min(0f)] public float delay = 3f;

            public bool Matches(int currentFloor, int attempt) =>
                (floor == 0 || floor == currentFloor) && attempt >= fromAttempt && (toAttempt <= 0 || attempt <= toAttempt);

            /// <summary>한 판에 한 번 판단하는 키 (Id, 없으면 내용)</summary>
            public string Key => string.IsNullOrEmpty(id) ? text : id;
        }

        /// <summary>받은 메시지</summary>
        public sealed class Message
        {
            public string Id;
            public string Sender;
            public string Text;
            /// <summary>받은 시각 (분, 자정부터)</summary>
            public int Minutes;
            public bool Read;
        }

        [Tooltip("층·회차가 맞으면 층 시작 뒤 받는 메시지 (내용은 팀이 정함). 비어 있으면 오지 않음")]
        [SerializeField] private List<Entry> schedule = new List<Entry>();

        private readonly List<Message> inbox = new List<Message>();
        private readonly HashSet<string> delivered = new HashSet<string>();
        private readonly List<PhoneListItem> items = new List<PhoneListItem>();
        private PatrolRun run;
        private PhoneList list;
        private RectTransform listView, readView, bubble;
        private TextMeshProUGUI title, footer, bubbleText, bubbleTime;
        private int selected;

        public override string Title => "메시지";
        public override bool InDock => true;
        public override int Order => 1;
        public override Color IconTop => new Color(0.66f, 0.92f, 0.38f);
        public override Color IconBottom => new Color(0.22f, 0.64f, 0.1f);
        public override bool HasSymbol => true;
        public override bool SymbolAt(Vector2 p) => PhoneArt.Bubble(p);

        public override int Badge
        {
            get
            {
                int count = 0;
                foreach (var message in inbox)
                    if (!message.Read) count++;
                return count;
            }
        }

        /// <summary>받은 메시지 (새것이 앞)</summary>
        public IReadOnlyList<Message> Inbox => inbox;
        /// <summary>읽는 중인 메시지 (목록이면 null)</summary>
        public Message Reading { get; private set; }
        /// <summary>목록에서 고른 줄</summary>
        public int Selected => selected;
        /// <summary>예약해서 받는 메시지 목록 (비어 있으면 오지 않음)</summary>
        public IReadOnlyList<Entry> Entries => schedule != null ? (IReadOnlyList<Entry>)schedule : Array.Empty<Entry>();

        /// <summary>메시지를 받는다 (목록 맨 위, 안 읽음). 전화가 진동하고, 주머니에 있으면 자막 줄에 잠깐 알림</summary>
        public Message Receive(string sender, string text, string id = null)
        {
            var message = new Message
            {
                Id = id ?? string.Empty,
                Sender = string.IsNullOrEmpty(sender) ? "알 수 없음" : sender,
                Text = text ?? string.Empty,
                Minutes = Screen != null ? Screen.ClockMinutes : 0,
            };
            inbox.Insert(0, message);
            selected = Reading == null ? 0 : Mathf.Min(selected + 1, inbox.Count - 1);   // 읽는 중이면 목록 자리 유지
            if (Phone != null)
            {
                Phone.Buzz();
                Phone.Notify($"{message.Sender} 메시지 — {Phone.PhoneKey}로 확인");   // 주머니에 있을 때 자막 줄 2초 (B5)
            }
            Redraw();
            return message;
        }

        protected override void OnInstalled()
        {
            run = FindAnyObjectByType<PatrolRun>();
            if (run != null) run.FloorStarted += OnFloorStarted;
        }

        protected override void OnUninstalled()
        {
            if (run != null)
            {
                run.FloorStarted -= OnFloorStarted;
                run.Schedule.CancelAll(this);
            }
            run = null;
        }

        // 층이 바뀌면 남은 예약은 버려진다 → 못 받은 메시지는 다음에 조건이 맞을 때 다시 예약 (받은 것만 한 번으로 친다)
        private void OnFloorStarted(int floor)
        {
            foreach (var entry in Entries)
            {
                if (entry == null || !entry.Matches(floor, run.Attempt) || delivered.Contains(entry.Key)) continue;
                var due = entry;
                run.Schedule.After(due.delay, () => Deliver(due), this, (string.IsNullOrEmpty(due.id) ? "메시지" : due.id) + " 메시지");
            }
        }

        private void Deliver(Entry entry)
        {
            if (delivered.Add(entry.Key)) Receive(entry.sender, entry.text, entry.id);
        }

        protected override void Build(RectTransform root)
        {
            title = PhoneUI.NavBar(root, Screen.Art, Title);
            listView = PhoneUI.Content(root, "List");
            list = new PhoneList(listView, Screen.Art, 7, "메시지 없음");
            readView = PhoneUI.Content(root, "Read");
            bubble = PhoneUI.Box(readView, "Bubble", 10f, 14f, 204f, 60f);
            PhoneUI.Image(bubble, Screen.Art.Rounded(14), new Color(0.87f, 0.88f, 0.91f));
            bubbleText = PhoneUI.Label(bubble, "Text", 12f, 8f, 180f, 44f, "", 16f, PhoneUI.Ink, TextAlignmentOptions.TopLeft, wrap: true);
            bubbleTime = PhoneUI.Label(readView, "Time", 14f, 80f, 120f, 16f, "", 12f, PhoneUI.Gray, TextAlignmentOptions.MidlineLeft);
            footer = PhoneUI.Footer(root, Screen.Art, "");
            readView.gameObject.SetActive(false);
        }

        protected override void OnOpen()
        {
            Reading = null;
            selected = 0;
            Redraw();
        }

        protected override void OnClose() => Reading = null;

        protected override void OnMove(int step)
        {
            if (Reading != null || inbox.Count == 0) return;
            selected = PhoneScreen.Wrap(selected, step, inbox.Count);
            Redraw();
        }

        protected override void OnConfirm()
        {
            if (Reading != null || selected < 0 || selected >= inbox.Count) return;
            Reading = inbox[selected];
            Reading.Read = true;
            Redraw();
        }

        protected override bool OnBack()
        {
            if (Reading == null) return false;
            Reading = null;
            Redraw();
            return true;
        }

        private void Redraw()
        {
            if (list == null) return;   // 아직 한 번도 안 열어 봄
            string interact = Phone != null ? Phone.InteractKey : "E", home = Phone != null ? Phone.PhoneKey : "Q";
            bool reading = Reading != null;
            listView.gameObject.SetActive(!reading);
            readView.gameObject.SetActive(reading);
            if (reading)
            {
                title.text = Reading.Sender;
                bubbleText.text = Reading.Text;
                float textHeight = Mathf.Clamp(bubbleText.GetPreferredValues(Reading.Text, 180f, 0f).y, 20f, 230f);
                bubble.sizeDelta = new Vector2(204f, textHeight + 16f);
                ((RectTransform)bubbleText.transform).sizeDelta = new Vector2(180f, textHeight);
                ((RectTransform)bubbleTime.transform).anchoredPosition = new Vector2(14f, -(14f + textHeight + 16f + 4f));
                bubbleTime.text = PatrolRun.ClockText(Reading.Minutes);
                footer.text = $"{home} 목록";
                return;
            }
            title.text = Title;
            items.Clear();
            foreach (var message in inbox)
                items.Add(new PhoneListItem
                {
                    Title = message.Sender,
                    Detail = message.Text,
                    Right = PatrolRun.ClockText(message.Minutes),
                    Unread = !message.Read,
                });
            list.Show(items, inbox.Count > 0 ? selected : -1);
            footer.text = inbox.Count > 0 ? $"휠 고르기 · {interact} 읽기 · {home} 홈" : $"{home} 홈";
        }
    }
}
