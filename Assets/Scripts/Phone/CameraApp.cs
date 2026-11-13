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
    /// 카메라 앱: 이상현상은 고치지 않고 찍는다. 가운데 조준 박스에 대상을 넣고 좌클릭 또는 E로 찍는다.
    /// 무엇이 찍혔는지는 보여 주지 않는다 — 엘리베이터 판정 때만 안다 (찍는 순간 알려 주면 탐지기가 된다).
    /// 한 층에 찍을 수 있는 장수가 정해져 있고, 이상현상이 아닌 것을 찍어도 장수만 준다.
    /// 전화·메시지 다음 독 세 번째 칸. 찍으면 그 사진을 잠깐(1.5초) 보여 주고 뷰파인더로 돌아온다 (우클릭이면 바로).
    /// 지난 사진은 갤러리 앱에서 본다 (홈에서 연다).
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

        /// <summary>갤러리의 사진 한 장 (찍힌 대상은 판정용이라 여기 없음 — PatrolRun.Photos)</summary>
        public sealed class Picture
        {
            public Texture2D Image;
            public int Floor;
            /// <summary>찍은 시각 (분, 자정부터)</summary>
            public int Minutes;
        }

        public const int Width = 360, Height = 240;

        [Tooltip("촬영 판정 (조준 박스·인정 거리·가림)")]
        [SerializeField] private PhotoRules rules = new PhotoRules();
        [Tooltip("층마다 찍을 수 있는 장수를 정하는 방식")]
        [SerializeField] private ShotLimit limit = ShotLimit.PerFloor;
        [Tooltip("PerFloor: 층별 장수. 표에 없는 층은 Default Shots")]
        [SerializeField] private FloorShots[] perFloor = new FloorShots[0];
        [Tooltip("PerFloor에서 표에 없는 층, Fixed에서는 모든 층의 장수. 임시값 12 = 한 층 이상현상 최대 10개 + 여유 2 (못 깨는 층이 없게, 플레이테스트 뒤 팀이 정함)")]
        [SerializeField, Min(0)] private int defaultShots = 12;
        [Tooltip("CountPlusSlack: 그 층 이상현상 수에 더할 여유")]
        [SerializeField, Min(0)] private int slack = 2;
        [Tooltip("셔터 번쩍임 시간(초)")]
        [SerializeField, Min(0f)] private float flashSeconds = 0.15f;
        [Tooltip("다음 셔터를 받기까지(초). 번쩍임과 별개")]
        [SerializeField, Min(0f)] private float retakeSeconds = 0.3f;
        [Tooltip("찍은 뒤 그 사진을 보여 주는 시간(초). 지나거나 우클릭하면 뷰파인더로. 0이면 안 보여 줌")]
        [SerializeField, Min(0f)] private float reviewSeconds = 1.5f;
        [Tooltip("눈(찍는 위치·방향). 비어 있으면 휴대폰이 달린 카메라(PlayerCamera)")]
        [SerializeField] private Camera eye;

        private readonly List<Picture> pictures = new List<Picture>();
        private PatrolRun run;
        private Camera lens;
        private RenderTexture preview;
        private RectTransform viewfinder;
        private RawImage live, review;
        private Image flash;
        private TextMeshProUGUI count, footer, title;
        private float flashLeft, retakeLeft, emptyBlink, reviewLeft;
        private Picture reviewed;

        public override string Title => "카메라";
        public override bool InDock => true;
        public override int Order => 2;
        public override bool Landscape => true;
        public override Color IconTop => new Color(0.7f, 0.72f, 0.76f);
        public override Color IconBottom => new Color(0.24f, 0.25f, 0.29f);
        public override bool HasSymbol => true;
        public override bool SymbolAt(Vector2 p) => IconShape(p);

        public PhotoRules Rules => rules;
        /// <summary>이번 층에 찍은 사진 (오래된 것이 앞, 갤러리가 보여 준다)</summary>
        public IReadOnlyList<Picture> Pictures => pictures;
        /// <summary>방금 찍은 사진을 보여 주는 중인지 (아니면 뷰파인더)</summary>
        public bool IsReviewing => reviewed != null;
        /// <summary>보여 주는 사진 (뷰파인더면 null)</summary>
        public Picture Reviewed => reviewed;
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

        // 사진은 그 층의 증거 — 층이 바뀌면 사진도 비운다 (장수·찍힌 기록은 PatrolRun.Photos가 비움)
        private void OnFloorChanged(int floor) => ResetFloor();
        private void OnCleared() => ResetFloor();

        private void ResetFloor()
        {
            reviewed = null;
            ClearPictures();
            Redraw();
        }

        protected override void Build(RectTransform root)
        {
            var content = LandscapeRoot(root, "LandscapeCamera");
            PhoneUI.Image(content, null, new Color(0.08f, 0.08f, 0.09f));
            title = PhoneUI.Label(content, "Title", 8f, 2f, 120f, 22f, Title, 13f, Color.white, TextAlignmentOptions.Left);
            viewfinder = PhoneUI.Box(content, "Viewfinder", 0f, 0f, Width, Height);
            live = PictureBox(viewfinder, "Live");
            AimBox(viewfinder);
            review = PictureBox(content, "Review");
            review.gameObject.SetActive(false);
            count = PhoneUI.Label(content, "Count", 198f, 2f, 154f, 22f, "", 13f, Color.white, TextAlignmentOptions.Right);
            PhoneUI.Image(PhoneUI.Box(content, "TopShade", 0f, 0f, Width, 26f), null, new Color(0f, 0f, 0f, 0.55f));
            PhoneUI.Image(PhoneUI.Box(content, "BottomShade", 0f, 212f, Width, 28f), null, new Color(0f, 0f, 0f, 0.55f));
            title.transform.SetAsLastSibling();
            count.transform.SetAsLastSibling();
            var flashRect = PhoneUI.Fill(content, "Flash");
            flash = PhoneUI.Image(flashRect, null, new Color(1f, 1f, 1f, 0f));
            footer = PhoneUI.Label(content, "Footer", 4f, 214f, 352f, 24f, "", 12f, Color.white, TextAlignmentOptions.Center);
        }

        /// <summary>가로 화면 바탕 (Width×Height, -90도 돌림): 상태 표시줄이 숨는 자리까지 씀. 갤러리도 쓴다</summary>
        internal static RectTransform LandscapeRoot(RectTransform root, string name)
        {
            var content = PhoneUI.Rect(root, name);
            content.anchorMin = content.anchorMax = new Vector2(0.5f, 0.5f);
            content.pivot = new Vector2(0.5f, 0.5f);
            content.sizeDelta = new Vector2(Width, Height);
            content.anchoredPosition = new Vector2(0f, PhoneScreen.StatusHeight / 2f);
            content.localRotation = Quaternion.Euler(0f, 0f, -90f);
            return content;
        }

        /// <summary>사진 한 장 크기(Width×Height)의 그림 칸</summary>
        internal static RawImage PictureBox(RectTransform parent, string name)
        {
            var rect = PhoneUI.Box(parent, name, 0f, 0f, Width, Height);
            var image = rect.gameObject.AddComponent<RawImage>();
            image.raycastTarget = false;
            image.color = Color.white;
            return image;
        }

        // 조준 박스: 판정과 같은 비율(rules.aimBox)로 뷰파인더 가운데에 얇은 테두리 네 줄
        private void AimBox(RectTransform parent)
        {
            float w = Width * rules.aimBox, h = Height * rules.aimBox, x = (Width - w) / 2f, y = (Height - h) / 2f, t = 1.5f;
            var color = new Color(1f, 1f, 1f, 0.85f);
            PhoneUI.Image(PhoneUI.Box(parent, "AimTop", x, y, w, t), null, color);
            PhoneUI.Image(PhoneUI.Box(parent, "AimBottom", x, y + h - t, w, t), null, color);
            PhoneUI.Image(PhoneUI.Box(parent, "AimLeft", x, y, t, h), null, color);
            PhoneUI.Image(PhoneUI.Box(parent, "AimRight", x + w - t, y, t, h), null, color);
        }

        protected override void OnOpen()
        {
            EnsureLens();
            Redraw();
        }

        protected override void OnClose()
        {
            reviewed = null;
            DropLens();
        }

        // 좌클릭: 뷰파인더면 찍기, 사진을 보여 주는 중이면 찍지 않고 뷰파인더로 (안 보고 찍히지 않게)
        protected override void OnConfirm()
        {
            if (reviewed != null) EndReview();
            else Take();
        }

        // 우클릭: 사진을 보여 주는 중이면 바로 뷰파인더로, 아니면 홈
        protected override bool OnBack()
        {
            if (reviewed == null) return false;
            EndReview();
            return true;
        }

        /// <summary>찍은 사진 보여 주기를 끝내고 뷰파인더로</summary>
        public void EndReview()
        {
            if (reviewed == null) return;
            reviewed = null;
            Redraw();
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
            if (IsOpen && reviewSeconds > 0f)
            {
                reviewed = picture;
                reviewLeft = reviewSeconds;
            }

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
            lens.cullingMask = CameraOnlyLayer.With(eye.cullingMask);   // 카메라로만 보이는 것(CameraOnly 레이어)은 뷰파인더에만
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
            if (reviewed != null && Time.timeScale > 0f)   // ESC 메뉴 동안은 멈춤
            {
                reviewLeft -= Time.deltaTime;
                if (reviewLeft <= 0f) EndReview();
            }
            if (lens == null || reviewed != null || !IsOpen || Time.timeScale <= 0f) return;
            if (Phone == null || !Phone.IsInHand || Screen.IsCallShown) return;
            lens.Render();
        }

        private void Redraw()
        {
            if (footer == null) return;   // 아직 한 번도 안 열어 봄
            string interact = "좌클릭", back = "우클릭";
            title.text = Title;
            count.text = $"남은 촬영 {ShotsLeft}장";
            viewfinder.gameObject.SetActive(reviewed == null);
            review.gameObject.SetActive(reviewed != null);
            review.texture = reviewed != null ? reviewed.Image : null;
            footer.text = reviewed != null ? $"{back} 카메라" : $"{interact} 찍기 · {back} 홈";
        }
    }
}
