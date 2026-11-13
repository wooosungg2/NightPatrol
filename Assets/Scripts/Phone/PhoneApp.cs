using System;
using UnityEngine;

namespace NightPatrol.Phone
{
    /// <summary>
    /// 전화 앱의 바탕. 새 앱(보고·지도·퍼즐 등)은 이것을 상속해 Title·아이콘·Build를 채우고,
    /// Player 프리팹 PlayerCamera/Phone/Apps 아래 오브젝트에 붙이면 홈 화면에 나온다 (실행 중에는 PhoneScreen.Install).
    /// 조작은 화면이 넘겨준다: 휠 = OnMove, E = OnConfirm, Q(홈 버튼) = OnBack (false면 홈으로).
    /// OnInstalled는 앱 자신의 Awake보다 먼저 올 수 있다 — 거기서 쓰는 것은 Awake에 기대지 말 것
    /// </summary>
    public abstract class PhoneApp : MonoBehaviour
    {
        [Tooltip("아이콘 그림. 비어 있으면 코드가 그린 아이콘 (앱 색 + 기호, 기호가 없으면 이름 첫 글자)")]
        [SerializeField] private Sprite icon;

        /// <summary>홈 화면 이름 (아이콘 아래)</summary>
        public abstract string Title { get; }
        /// <summary>아래 줄(독)에 둘지. 독은 4칸 — 넘치면 위 격자로</summary>
        public virtual bool InDock => false;
        /// <summary>홈 화면 순서 (작을수록 앞, 같으면 이름순)</summary>
        public virtual int Order => 100;
        /// <summary>가로 화면 앱인지 (카메라·갤러리): 열면 휴대폰이 화면 가운데로 와서 가로로 돌고 상태 표시줄이 숨는다.
        /// Build의 root는 그대로 세로 240×342 — 내용은 CameraApp.LandscapeRoot 안에 그린다</summary>
        public virtual bool Landscape => false;
        /// <summary>코드 아이콘 바탕 위쪽 색</summary>
        public virtual Color IconTop => new Color(0.62f, 0.66f, 0.74f);
        /// <summary>코드 아이콘 바탕 아래쪽 색</summary>
        public virtual Color IconBottom => new Color(0.26f, 0.29f, 0.36f);
        /// <summary>코드 아이콘에 흰 기호를 그릴지 (false면 이름 첫 글자)</summary>
        public virtual bool HasSymbol => false;
        /// <summary>흰 기호 모양: p는 -1~1 (위가 +), 칠할 곳이면 true. PhoneArt.Handset·Bubble·Globe 참고</summary>
        public virtual bool SymbolAt(Vector2 p) => false;
        /// <summary>아이콘 위 빨간 숫자 (부재중·안 읽음). 0이면 안 보인다</summary>
        public virtual int Badge => 0;
        /// <summary>인스펙터에서 넣은 아이콘 그림 (없으면 null)</summary>
        public Sprite IconSprite => icon;

        /// <summary>설치된 화면 (설치 전에는 null)</summary>
        public PhoneScreen Screen { get; private set; }
        /// <summary>이 앱이 깔린 전화 (걸기·키 이름 등)</summary>
        public PhoneHolder Phone => Screen != null ? Screen.Phone : null;
        /// <summary>지금 열려 있는지</summary>
        public bool IsOpen => Screen != null && Screen.OpenApp == this;
        /// <summary>화면을 만들었는지 (처음 열 때 만든다)</summary>
        public bool IsBuilt { get; private set; }

        /// <summary>설치될 때 (이벤트 구독 등). Screen·Phone을 쓸 수 있다</summary>
        protected virtual void OnInstalled() { }
        /// <summary>지워질 때 (구독 풀기 등)</summary>
        protected virtual void OnUninstalled() { }
        /// <summary>앱 화면을 만든다 (처음 열 때 한 번). root = 상태 표시줄 아래 전체 (240×342). PhoneUI·Screen.Art를 쓴다</summary>
        protected abstract void Build(RectTransform root);
        /// <summary>열릴 때 (배지 지우기 등)</summary>
        protected virtual void OnOpen() { }
        /// <summary>닫힐 때 (홈으로·전화를 넣을 때)</summary>
        protected virtual void OnClose() { }
        /// <summary>휠 한 칸: +1 아래, -1 위</summary>
        protected virtual void OnMove(int step) { }
        /// <summary>E</summary>
        protected virtual void OnConfirm() { }
        /// <summary>Q(홈 버튼): 앱 안에서 한 단계 뒤로 갔으면 true. false면 홈 화면으로</summary>
        protected virtual bool OnBack() => false;

        /// <summary>상속한 앱이 OnDestroy를 쓰면 base.OnDestroy()를 부를 것 (화면에서 빠진다)</summary>
        protected virtual void OnDestroy()
        {
            if (Screen != null) Screen.Uninstall(this);
        }

        // ── PhoneScreen이 부른다 (앱 코드의 예외가 전화 전체를 멈추지 않게) ──

        internal void Attach(PhoneScreen screen)
        {
            Screen = screen;
            Safe(OnInstalled);
        }

        internal void Detach()
        {
            Safe(OnUninstalled);
            Screen = null;
            IsBuilt = false;   // 화면은 PhoneScreen이 지웠다 — 다시 설치되면 새로 만든다
        }

        internal void BuildInto(RectTransform root)
        {
            if (IsBuilt) return;
            IsBuilt = true;
            try { Build(root); }
            catch (Exception e) { Debug.LogException(e, this); }
        }

        internal void Opened() => Safe(OnOpen);
        internal void Closed() => Safe(OnClose);
        internal void Confirmed() => Safe(OnConfirm);

        internal void Moved(int step)
        {
            try { OnMove(step); }
            catch (Exception e) { Debug.LogException(e, this); }
        }

        internal bool WentBack()
        {
            try { return OnBack(); }
            catch (Exception e)
            {
                Debug.LogException(e, this);
                return false;
            }
        }

        private void Safe(Action action)
        {
            try { action(); }
            catch (Exception e) { Debug.LogException(e, this); }
        }
    }
}
