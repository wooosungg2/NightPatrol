using System;
using System.Collections.Generic;
using NightPatrol.Photo;
using NightPatrol.Run;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NightPatrol.Phone
{
    /// <summary>
    /// 카메라 앱 (06_04, 기획서 v3 04): 이상현상은 고치지 않고 찍는다. 뷰파인더 가운데 조준 박스에 대상을 넣고 E로 찍는다.
    /// 무엇이 찍혔는지는 보여 주지 않는다 — 엘리베이터 판정 때만 안다 (찍는 순간 알려 주면 탐지기가 된다).
    /// 한 층에 찍을 수 있는 장수가 정해져 있고, 이상현상이 아닌 것을 찍어도 장수만 준다.
    /// 독 첫 칸이라 휴대폰을 꺼내면 골라져 있다 (Q → E 열기 → E 찍기). 휠로 이번 층 사진첩(넘겨 보기만, 지우기 없음)
    /// </summary>
    public sealed class CameraApp : PhoneApp
    {
        /// <summary>촬영 장수를 정하는 방식 (기획서 v3 M01 — 팀이 정하면 고르기만)</summary>
        public enum ShotLimit { PerFloor, CountPlusSlack, Fixed }

        [Serializable]
        public struct FloorShots
        {
            public int floor;
            [Min(0)] public int shots;
        }

        /// <summary>사진첩의 사진 한 장 (찍힌 대상은 판정용이라 여기 없음 — PatrolRun.Photos)</summary>
        public sealed class Picture
        {
            public Texture2D Image;
            public int Floor;
            /// <summary>찍은 시각 (분, 자정부터)</summary>
            public int Minutes;
        }

        public const int Width = 240, Height = 180;

        [Tooltip("촬영 판정 (조준 박스·인정 거리·가림)")]
        [SerializeField] private PhotoRules rules = new PhotoRules();
        [Tooltip("층마다 찍을 수 있는 장수를 정하는 방식")]
        [SerializeField] private ShotLimit limit = ShotLimit.PerFloor;
        [Tooltip("PerFloor: 층별 장수. 표에 없는 층은 Default Shots")]
        [SerializeField] private FloorShots[] perFloor = new FloorShots[0];
        [Tooltip("PerFloor에서 표에 없는 층, Fixed에서는 모든 층의 장수 (임시값, 팀이 정함)")]
        [SerializeField, Min(0)] private int defaultShots = 6;
        [Tooltip("CountPlusSlack: 그 층 이상현상 수에 더할 여유")]
        [SerializeField, Min(0)] private int slack = 2;
        [Tooltip("셔터 번쩍임 시간(초)")]
        [SerializeField, Min(0f)] private float flashSeconds = 0.15f;
        [Tooltip("다음 셔터를 받기까지(초). 번쩍임과 별개")]
        [SerializeField, Min(0f)] private float retakeSeconds = 0.3f;
        [Tooltip("눈(찍는 위치·방향). 비어 있으면 휴대폰이 달린 카메라(PlayerCamera)")]
        [SerializeField] private Camera eye;
        [Tooltip("독 첫 칸 (휴대폰을 꺼내면 카메라가 골라져 있음). 끄면 독 마지막 칸")]
        [SerializeField] private bool firstInDock = true;

        private readonly List<Picture> pictures = new List<Picture>();
        private PatrolRun run;
        private Camera lens;
        private RenderTexture preview;
        private RectTransform viewfinder, album;
        private RawImage live, shown;
        private Image flash;
        private TextMeshProUGUI count, caption, footer, title;
        private bool browsing;
        private int index;
        private float flashLeft, retakeLeft, emptyBlink;

        public override string Title => "카메라";
        public override bool InDock => true;
        public override int Order => firstInDock ? -1 : 3;   // 독 첫 칸 = 꺼낼 때 골라져 있음 (전화 0, 메시지 1, 인터넷 2)
        public override Color IconTop => new Color(0.7f, 0.72f, 0.76f);
        public override Color IconBottom => new Color(0.24f, 0.25f, 0.29f);
        public override bool HasSymbol => true;
        public override bool SymbolAt(Vector2 p) => IconShape(p);

        public PhotoRules Rules => rules;
        /// <summary>이번 층 사진첩 (오래된 것이 앞)</summary>
        public IReadOnlyList<Picture> Pictures => pictures;
        /// <summary>사진첩을 보는 중인지 (아니면 뷰파인더)</summary>
        public bool IsBrowsing => browsing;
        /// <summary>뷰파인더 카메라가 있는지 (앱을 닫으면 지움)</summary>
        public bool HasLens => lens != null;
        /// <summary>뷰파인더·판정에 쓰는 카메라 (없으면 null)</summary>
        public Camera Lens => lens;
        /// <summary>이번 층에 찍을 수 있는 장수</summary>
        public int ShotsAllowed => ShotsFor(limit, run != null ? run.CurrentFloor : 0, run != null ? run.Photos.RequiredAtStart : 0);
        /// <summary>이번 층 남은 장수</summary>
        public int ShotsLeft => Mathf.Max(0, ShotsAllowed - (run != null ? run.Photos.Shots : pictures.Count));

        /// <summary>찍었을 때 (사진, 그 장에 찍힌 이상현상 수 — 판정용이라 화면에는 안 씀)</summary>
        public event Action<Picture, int> Taken;

        /// <summary>그 방식으로 그 층에 찍을 수 있는 장수</summary>
        public int ShotsFor(ShotLimit mode, int floor, int required)
        {
            switch (mode)
            {
                case ShotLimit.CountPlusSlack: return Mathf.Max(0, required) + slack;
                case ShotLimit.Fixed: return defaultShots;
                default:
                    foreach (var row in perFloor)
                        if (row.floor == floor) return row.shots;
                    return defaultShots;
            }
        }

        /// <summary>코드에서 장수 설정을 바꾼다 (테스트·튜닝용)</summary>
        public void SetShotLimit(ShotLimit mode, int shots, int extra = 2, params FloorShots[] table)
        {
            limit = mode;
            defaultShots = Mathf.Max(0, shots);
            slack = Mathf.Max(0, extra);
            perFloor = table ?? new FloorShots[0];
        }

        /// <summary>아이콘: 카메라 몸통(둥근 네모) + 렌즈 구멍 + 위 돌기</summary>
        public static bool IconShape(Vector2 p)
        {
            bool body = Mathf.Abs(p.x) <= 0.78f && p.y >= -0.5f && p.y <= 0.38f;
            bool bump = Mathf.Abs(p.x + 0.3f) <= 0.18f && p.y > 0.38f && p.y <= 0.52f;
            float r = (p - new Vector2(0f, -0.06f)).magnitude;
            bool hole = r <= 0.34f && r >= 0.2f;   // 렌즈 테두리만 남기고 비움
            bool ring = r < 0.2f;
            return (body || bump) && !hole || ring;
        }

        protected override void OnInstalled()
        {
            run = FindAnyObjectByType<PatrolRun>();
            if (run == null) return;
            run.FloorStarted += OnFloorChanged;
            run.Cleared += OnCleared;
        }

        protected override void OnUninstalled()
        {
            if (run != null)
            {
                run.FloorStarted -= OnFloorChanged;
                run.Cleared -= OnCleared;
            }
            run = null;
            DropLens();
            ClearPictures();
        }

        protected override void OnDestroy()
        {
            DropLens();
            ClearPictures();
            base.OnDestroy();
        }

        // 사진은 그 층의 증거 — 층이 바뀌면 사진첩도 비운다 (장수·찍힌 기록은 PatrolRun.Photos가 비움)
        private void OnFloorChanged(int floor) => ResetFloor();
        private void OnCleared() => ResetFloor();

        private void ResetFloor()
        {
            ClearPictures();
            browsing = false;
            Redraw();
        }

        protected override void Build(RectTransform root)
        {
            title = PhoneUI.NavBar(root, Screen.Art, Title);
            var content = PhoneUI.Content(root, "Camera");
            content.GetComponent<Image>().color = new Color(0.08f, 0.08f, 0.09f);   // Content가 붙인 종이 색 대신 어두운 바탕
            float height = PhoneUI.AppHeight - PhoneUI.NavHeight - PhoneUI.FooterHeight;
            viewfinder = PhoneUI.Box(content, "Viewfinder", 0f, 0f, PhoneUI.AppWidth, height);
            live = PictureBox(viewfinder, "Live");
            AimBox(viewfinder);
            count = PhoneUI.Label(viewfinder, "Count", 10f, 200f, 220f, 20f, "", 14f, Color.white, TextAlignmentOptions.Center);
            album = PhoneUI.Box(content, "Album", 0f, 0f, PhoneUI.AppWidth, height);
            shown = PictureBox(album, "Photo");
            caption = PhoneUI.Label(album, "Caption", 10f, 196f, 220f, 40f, "", 13f, Color.white, TextAlignmentOptions.Top, wrap: true);
            var flashRect = PhoneUI.Box(content, "Flash", 0f, 0f, PhoneUI.AppWidth, PhoneUI.AppHeight);
            flash = PhoneUI.Image(flashRect, null, new Color(1f, 1f, 1f, 0f));
            footer = PhoneUI.Footer(root, Screen.Art, "");
        }

        private static RawImage PictureBox(RectTransform parent, string name)
        {
            var rect = PhoneUI.Box(parent, name, 0f, 10f, Width, Height);
            var image = rect.gameObject.AddComponent<RawImage>();
            image.raycastTarget = false;
            image.color = Color.white;
            return image;
        }

        // 조준 박스: 판정과 같은 비율(rules.aimBox)로 뷰파인더 가운데에 얇은 테두리 네 줄
        private void AimBox(RectTransform parent)
        {
            float w = Width * rules.aimBox, h = Height * rules.aimBox, x = (Width - w) / 2f, y = 10f + (Height - h) / 2f, t = 1.5f;
            var color = new Color(1f, 1f, 1f, 0.85f);
            PhoneUI.Image(PhoneUI.Box(parent, "AimTop", x, y, w, t), null, color);
            PhoneUI.Image(PhoneUI.Box(parent, "AimBottom", x, y + h - t, w, t), null, color);
            PhoneUI.Image(PhoneUI.Box(parent, "AimLeft", x, y, t, h), null, color);
            PhoneUI.Image(PhoneUI.Box(parent, "AimRight", x + w - t, y, t, h), null, color);
        }

        protected override void OnOpen()
        {
            browsing = false;
            EnsureLens();
            Redraw();
        }

        protected override void OnClose()
        {
            browsing = false;
            DropLens();
        }

        // 휠: 뷰파인더 → 사진첩(마지막 사진부터) → 넘기기, 끝에서 한 번 더 넘기면 뷰파인더로
        protected override void OnMove(int step)
        {
            if (pictures.Count == 0) return;
            if (!browsing)
            {
                browsing = true;
                index = pictures.Count - 1;
            }
            else
            {
                int next = index + (step < 0 ? 1 : -1);   // 휠 위 = 최근 쪽
                if (next < 0 || next >= pictures.Count) browsing = false;
                else index = next;
            }
            Redraw();
        }

        protected override void OnConfirm()
        {
            if (!browsing) Take();
        }

        protected override bool OnBack()
        {
            if (!browsing) return false;
            browsing = false;
            Redraw();
            return true;
        }

        /// <summary>
        /// 지금 시선으로 한 장 찍는다 (뷰파인더가 없으면 잠깐 만들어 찍음). 장수가 없거나 다시 찍기 대기 중이거나 눈이 없으면 null.
        /// 찍힌 이상현상은 PatrolRun.Photos에 기록한다 (화면에는 안 보임)
        /// </summary>
        public Picture Take()
        {
            if (retakeLeft > 0f) return null;
            if (ShotsLeft <= 0)
            {
                emptyBlink = 0.6f;
                Redraw();
                return null;
            }
            bool temporary = lens == null;
            if (!EnsureLens()) return null;
            lens.Render();
            var picture = new Picture
            {
                Image = new Texture2D(Width, Height, TextureFormat.RGB24, false),
                Floor = run != null ? run.CurrentFloor : 0,
                Minutes = run != null ? run.ClockMinutes : Screen != null ? Screen.ClockMinutes : 0,
            };
            var previous = RenderTexture.active;
            RenderTexture.active = preview;
            picture.Image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            picture.Image.Apply();
            RenderTexture.active = previous;

            var inShot = PhotoJudge.Capture(lens, rules, PhotoSubjects.Active());
            if (run != null) run.Photos.Record(inShot, run.PlaySeconds);
            pictures.Add(picture);
            if (temporary && !IsOpen) DropLens();

            flashLeft = flashSeconds;
            retakeLeft = retakeSeconds;
            if (Phone != null) Phone.PlaySound(PhoneHolder.ShutterSound);
            Taken?.Invoke(picture, inShot.Count);
            Redraw();
            return picture;
        }

        private bool EnsureLens()
        {
            if (lens != null) return true;
            if (eye == null) eye = GetComponentInParent<Camera>();
            if (eye == null) eye = Camera.main;
            if (eye == null) return false;
            preview = new RenderTexture(Width, Height, 16, RenderTextureFormat.ARGB32) { name = "CameraAppPreview" };
            var go = new GameObject("CameraAppLens");
            go.transform.SetParent(eye.transform, false);
            lens = go.AddComponent<Camera>();
            lens.CopyFrom(eye);
            lens.targetTexture = preview;
            lens.aspect = (float)Width / Height;   // 판정도 이 카메라로 하므로 뷰파인더와 조준 박스가 같은 자리
            lens.enabled = false;   // 보일 때만 손으로 그린다
            if (live != null) live.texture = preview;
            return true;
        }

        private void DropLens()
        {
            if (lens != null) Destroy(lens.gameObject);
            lens = null;
            if (preview != null)
            {
                preview.Release();
                Destroy(preview);
            }
            preview = null;
            if (live != null) live.texture = null;
        }

        private void ClearPictures()
        {
            foreach (var picture in pictures)
                if (picture.Image != null) Destroy(picture.Image);
            pictures.Clear();
        }

        // 뷰파인더는 실제로 보일 때만 그린다: 열림 + 손에 듦 + 통화 화면 아님 + 시간이 감(ESC 메뉴 아님)
        private void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            if (retakeLeft > 0f && Time.timeScale > 0f) retakeLeft -= Time.deltaTime;
            if (flashLeft > 0f && flash != null)
            {
                flashLeft -= dt;
                flash.color = new Color(1f, 1f, 1f, flashSeconds > 0f ? Mathf.Clamp01(flashLeft / flashSeconds) * 0.8f : 0f);
            }
            if (emptyBlink > 0f)
            {
                emptyBlink -= dt;
                if (count != null) count.color = Mathf.Repeat(emptyBlink, 0.2f) < 0.1f ? PhoneUI.Alert : Color.white;
                if (emptyBlink <= 0f && count != null) count.color = Color.white;
            }
            if (lens == null || browsing || !IsOpen || Time.timeScale <= 0f) return;
            if (Phone == null || !Phone.IsInHand || Screen.IsCallShown) return;
            lens.Render();
        }

        private void Redraw()
        {
            if (footer == null) return;   // 아직 한 번도 안 열어 봄
            string interact = Phone != null ? Phone.InteractKey : "E", back = Phone != null ? Phone.PhoneKey : "Q";
            viewfinder.gameObject.SetActive(!browsing);
            album.gameObject.SetActive(browsing);
            if (!browsing)
            {
                title.text = Title;
                count.text = $"남은 촬영 {ShotsLeft}장";
                footer.text = pictures.Count > 0 ? $"{interact} 찍기 · 휠 사진첩 · {back} 홈" : $"{interact} 찍기 · {back} 홈";
                return;
            }
            var picture = pictures[index];
            title.text = $"사진 {index + 1}/{pictures.Count}";
            shown.texture = picture.Image;
            caption.text = $"{picture.Floor}층 · {PatrolRun.ClockText(picture.Minutes)}";
            footer.text = $"휠 넘기기 · {back} 카메라";
        }
    }
}
