using NightPatrol.Run;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NightPatrol.Phone
{
    /// <summary>
    /// 갤러리 앱: 카메라로 이번 층에 찍은 사진을 본다 (넘겨 보기만, 지우기 없음). 카메라처럼 가로 화면.
    /// 홈 화면에서 연다. 우클릭은 홈으로.
    /// 사진은 카메라 앱이 들고 있다 (층이 바뀌면 비움) — 같은 휴대폰에 카메라가 없으면 늘 "사진 없음".
    /// </summary>
    public sealed class GalleryApp : PhoneApp
    {
        private RawImage shown;
        private TextMeshProUGUI title, caption, footer, empty;
        private int index, shownCount = -1;

        public override string Title => "갤러리";
        public override bool InDock => true;
        public override int Order => 3;
        public override bool Landscape => true;
        public override Color IconTop => new Color(0.98f, 0.78f, 0.36f);
        public override Color IconBottom => new Color(0.86f, 0.42f, 0.2f);
        public override bool HasSymbol => true;
        public override bool SymbolAt(Vector2 p) => IconShape(p);

        /// <summary>사진을 들고 있는 카메라 (같은 휴대폰에 깔린 것, 없으면 null)</summary>
        public CameraApp Source
        {
            get
            {
                if (Screen == null) return null;
                foreach (var app in Screen.Installed)
                    if (app is CameraApp camera) return camera;
                return null;
            }
        }

        /// <summary>이번 층 사진 수</summary>
        public int Count => Source != null ? Source.Pictures.Count : 0;
        /// <summary>보고 있는 사진 (0 = 가장 오래된 것). 사진이 없으면 -1</summary>
        public int Index => Count > 0 ? Mathf.Clamp(index, 0, Count - 1) : -1;
        /// <summary>보고 있는 사진 (없으면 null)</summary>
        public CameraApp.Picture Current => Index >= 0 ? Source.Pictures[Index] : null;

        /// <summary>아이콘: 사진 틀 + 해 + 산</summary>
        public static bool IconShape(Vector2 p)
        {
            bool inside = Mathf.Abs(p.x) <= 0.62f && Mathf.Abs(p.y) <= 0.48f;
            bool frame = Mathf.Abs(p.x) <= 0.78f && Mathf.Abs(p.y) <= 0.64f && !inside;
            bool sun = (p - new Vector2(0.3f, 0.2f)).magnitude <= 0.14f;
            bool hill = p.y <= -0.48f + (0.62f - Mathf.Abs(p.x + 0.12f)) * 0.9f;
            return frame || inside && (sun || hill);
        }

        protected override void Build(RectTransform root)
        {
            var content = CameraApp.LandscapeRoot(root, "LandscapeGallery");
            PhoneUI.Image(content, null, new Color(0.08f, 0.08f, 0.09f));
            shown = CameraApp.PictureBox(content, "Photo");
            empty = PhoneUI.Label(content, "Empty", 0f, 100f, CameraApp.Width, 40f, "사진 없음", 16f,
                new Color(0.7f, 0.72f, 0.76f), TextAlignmentOptions.Center);
            PhoneUI.Image(PhoneUI.Box(content, "TopShade", 0f, 0f, CameraApp.Width, 26f), null, new Color(0f, 0f, 0f, 0.55f));
            PhoneUI.Image(PhoneUI.Box(content, "BottomShade", 0f, 212f, CameraApp.Width, 28f), null, new Color(0f, 0f, 0f, 0.55f));
            title = PhoneUI.Label(content, "Title", 8f, 2f, 160f, 22f, Title, 13f, Color.white, TextAlignmentOptions.Left);
            caption = PhoneUI.Label(content, "Caption", 168f, 2f, 184f, 22f, "", 13f, Color.white, TextAlignmentOptions.Right);
            footer = PhoneUI.Label(content, "Footer", 4f, 214f, 352f, 24f, "", 12f, Color.white, TextAlignmentOptions.Center);
        }

        // 열 때마다 마지막(가장 최근) 사진부터
        protected override void OnOpen()
        {
            index = Count - 1;
            Redraw();
        }

        protected override void OnClose()
        {
            if (shown != null) shown.texture = null;
        }

        // 휠 아래(+1) = 더 오래된 사진, 위(-1) = 최근 쪽. 끝에서는 멈춘다
        protected override void OnMove(int step)
        {
            if (Count == 0) return;
            index = Mathf.Clamp(Index + (step > 0 ? -1 : 1), 0, Count - 1);
            Redraw();
        }

        // 보는 중에 층이 바뀌어 사진이 지워지거나 새로 생기면 다시 그린다
        private void LateUpdate()
        {
            if (!IsOpen || footer == null) return;
            if (Count != shownCount)
            {
                if (Count < shownCount || index >= Count) index = Count - 1;
                Redraw();
            }
        }

        private void Redraw()
        {
            if (footer == null) return;   // 아직 한 번도 안 열어 봄
            shownCount = Count;
            const string back = "우클릭 홈";
            var picture = Current;
            shown.texture = picture != null ? picture.Image : null;
            shown.enabled = picture != null;
            empty.gameObject.SetActive(picture == null);
            if (picture == null)
            {
                title.text = Title;
                caption.text = string.Empty;
                footer.text = back;
                return;
            }
            title.text = $"사진 {Index + 1}/{Count}";
            caption.text = $"{picture.Floor}층 · {PatrolRun.ClockText(picture.Minutes)}";
            footer.text = Count > 1 ? $"휠 넘기기 · {back}" : back;
        }
    }
}
