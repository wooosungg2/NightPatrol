using TMPro;
using UnityEngine;

namespace NightPatrol.Phone
{
    /// <summary>인터넷 앱 (독 셋째 칸): 건물 안에서는 인터넷이 안 된다 — 늘 "페이지를 열 수 없음"</summary>
    public sealed class BrowserApp : PhoneApp
    {
        private TextMeshProUGUI footer;

        public override string Title => "인터넷";
        public override bool InDock => true;
        public override int Order => 4;
        public override Color IconTop => new Color(0.5f, 0.76f, 1f);
        public override Color IconBottom => new Color(0.08f, 0.34f, 0.84f);
        public override bool HasSymbol => true;
        public override bool SymbolAt(Vector2 p) => PhoneArt.Globe(p);

        protected override void Build(RectTransform root)
        {
            var bar = PhoneUI.Box(root, "AddressBar", 0f, 0f, PhoneUI.AppWidth, PhoneUI.NavHeight);
            PhoneUI.Image(bar, Screen.Art.Gradient(PhoneUI.BarTop, PhoneUI.BarBottom), Color.white);
            var field = PhoneUI.Box(bar, "Address", 8f, 5f, PhoneUI.AppWidth - 16f, 22f);
            PhoneUI.Image(field, Screen.Art.Rounded(6), Color.white);
            PhoneUI.Label(field, "Text", 8f, 0f, PhoneUI.AppWidth - 32f, 22f, "http://", 13f, PhoneUI.Gray, TextAlignmentOptions.MidlineLeft);
            var page = PhoneUI.Content(root, "Page");
            PhoneUI.Label(page, "Title", 12f, 96f, PhoneUI.AppWidth - 24f, 28f, "페이지를 열 수 없음", 18f, PhoneUI.Ink, TextAlignmentOptions.Center, bold: true);
            PhoneUI.Label(page, "Detail", 16f, 128f, PhoneUI.AppWidth - 32f, 44f, "인터넷에 연결되어 있지 않습니다.", 13f, PhoneUI.Gray,
                TextAlignmentOptions.Top, wrap: true);
            footer = PhoneUI.Footer(root, Screen.Art, "");
        }

        protected override void OnOpen()
        {
            if (footer != null) footer.text = $"{(Phone != null ? Phone.PhoneKey : "Q")} 홈";
        }
    }
}
