using System.Collections.Generic;
using NightPatrol.Run;
using TMPro;
using UnityEngine;

namespace NightPatrol.Phone
{
    /// <summary>
    /// 전화 앱 (독 첫 칸): 연락처 "관리실"과 최근 기록(받은 전화·건 전화·부재중, 시각).
    /// 연락처가 관리실뿐이라 어느 줄에서나 E로 관리실에 건다. 부재중 배지는 앱을 열면 지워진다
    /// </summary>
    public sealed class CallApp : PhoneApp
    {
        /// <summary>연락처 이름 (통화의 caller가 비면 이 이름)</summary>
        public const string Contact = "관리실";
        private const int MaxRecords = 20;

        public enum Kind { Incoming, Outgoing, Missed }

        /// <summary>최근 기록 한 줄</summary>
        public readonly struct Record
        {
            public readonly string Caller;
            public readonly Kind Kind;
            /// <summary>시각 (분, 자정부터)</summary>
            public readonly int Minutes;

            public Record(string caller, Kind kind, int minutes)
            {
                Caller = caller;
                Kind = kind;
                Minutes = minutes;
            }
        }

        private readonly List<Record> records = new List<Record>();
        private readonly List<PhoneListItem> items = new List<PhoneListItem>();
        private PhoneHolder subscribed;
        private PhoneList list;
        private TextMeshProUGUI footer;
        private int unseenMissed;

        public override string Title => "전화";
        public override bool InDock => true;
        public override int Order => 0;
        public override Color IconTop => new Color(0.45f, 0.86f, 0.5f);
        public override Color IconBottom => new Color(0.09f, 0.55f, 0.2f);
        public override bool HasSymbol => true;
        public override bool SymbolAt(Vector2 p) => PhoneArt.Handset(p);
        public override int Badge => unseenMissed;

        /// <summary>최근 기록 (새것이 앞, 20개까지)</summary>
        public IReadOnlyList<Record> Records => records;
        /// <summary>고른 줄 (0 = 연락처, 1부터 기록)</summary>
        public int Selected { get; private set; }

        /// <summary>통화에 보일 이름 (caller가 비면 관리실)</summary>
        public static string CallerOf(PhoneCall call) => call != null && !string.IsNullOrEmpty(call.caller) ? call.caller : Contact;

        public static string KindName(Kind kind)
        {
            switch (kind)
            {
                case Kind.Outgoing: return "건 전화";
                case Kind.Missed: return "부재중";
                default: return "받은 전화";
            }
        }

        protected override void OnInstalled()
        {
            subscribed = Phone;
            if (subscribed == null) return;
            subscribed.CallStarted += OnCallStarted;
            subscribed.Missed += OnMissed;
        }

        protected override void OnUninstalled()
        {
            if (subscribed == null) return;
            subscribed.CallStarted -= OnCallStarted;
            subscribed.Missed -= OnMissed;
            subscribed = null;
        }

        protected override void Build(RectTransform root)
        {
            PhoneUI.NavBar(root, Screen.Art, Title);
            list = new PhoneList(PhoneUI.Content(root), Screen.Art, 7, "기록 없음");
            footer = PhoneUI.Footer(root, Screen.Art, "");
        }

        protected override void OnOpen()
        {
            unseenMissed = 0;
            Selected = 0;
            Redraw();
        }

        protected override void OnMove(int step)
        {
            Selected = PhoneScreen.Wrap(Selected, step, records.Count + 1);
            Redraw();
        }

        // 걸면 통화 화면이 앱 위에 뜨고, 통화가 끝나면 전화를 넣는다
        protected override void OnConfirm()
        {
            if (Phone != null) Phone.Dial();
        }

        private void OnCallStarted(PhoneCall call, bool outgoing) => Add(CallerOf(call), outgoing ? Kind.Outgoing : Kind.Incoming);

        private void OnMissed(PhoneCall call)
        {
            Add(CallerOf(call), Kind.Missed);
            if (!IsOpen) unseenMissed++;
        }

        private void Add(string caller, Kind kind)
        {
            records.Insert(0, new Record(caller, kind, Screen != null ? Screen.ClockMinutes : 0));
            if (records.Count > MaxRecords) records.RemoveAt(records.Count - 1);
            Redraw();
        }

        private void Redraw()
        {
            if (list == null) return;   // 아직 한 번도 안 열어 봄
            items.Clear();
            items.Add(new PhoneListItem { Title = Contact, Detail = "연락처" });
            foreach (var record in records)
                items.Add(new PhoneListItem
                {
                    Title = record.Caller,
                    Detail = KindName(record.Kind),
                    Right = PatrolRun.ClockText(record.Minutes),
                    Alert = record.Kind == Kind.Missed,
                });
            Selected = Mathf.Clamp(Selected, 0, items.Count - 1);
            list.Show(items, Selected);
            string interact = Phone != null ? Phone.InteractKey : "E", home = Phone != null ? Phone.PhoneKey : "Q";
            footer.text = $"휠 고르기 · {interact} 걸기 · {home} 홈";
        }
    }
}
