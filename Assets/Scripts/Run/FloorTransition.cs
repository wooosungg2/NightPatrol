using System;
using System.Collections;
using NightPatrol.Interaction;
using NightPatrol.Journal;
using NightPatrol.Player;
using TMPro;
using UnityEngine;

namespace NightPatrol.Run
{
    /// <summary>
    /// 층 이동 연출. 엘리베이터 E → 조작 잠금 → 문 닫힘 → 화면 어두워짐 → (어두운 동안) 판정·층 바꾸기 → "N층" →
    /// 화면 밝아짐 → 문 열림 → 잠금 해제. 실패하면 어두운 동안 시작 자리로 옮겨진 뒤 졸다 깨는 연출(기획서 3.3).
    /// 잠그는 것: 이동·시점·E·일지(들고 있었으면 내림). Player 프리팹의 HUD에 붙는다.
    /// 판정·층 진행 규칙은 PatrolRun 그대로 — 여기는 그 바깥을 감싸기만 한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FloorTransition : MonoBehaviour
    {
        [Tooltip("비어 있으면 씬에서 찾는다")]
        [SerializeField] private PatrolRun run;
        [Tooltip("비어 있으면 씬에서 찾는다")]
        [SerializeField] private PlayerMovement player;
        [Tooltip("비어 있으면 씬에서 찾는다. 없으면 화면 연출 없이 진행")]
        [SerializeField] private ScreenFader fader;
        [Tooltip("비어 있으면 씬에서 찾는다. 없으면 문 연출 없이 진행")]
        [SerializeField] private ElevatorDoors doors;
        [Tooltip("도착할 때 \"N층\"을 띄울 글자. 없으면 안 띄움")]
        [SerializeField] private TMP_Text floorCaption;
        [Tooltip("어두워지는·밝아지는 시간(초)")]
        [SerializeField] private float fadeSeconds = 0.6f;
        [Tooltip("어두운 화면에 \"N층\"을 보여주는 시간(초)")]
        [SerializeField] private float holdSeconds = 0.8f;
        [Tooltip("졸다 깨는 연출 시간(초)")]
        [SerializeField] private float wakeSeconds = 1.6f;
        [Tooltip("깨어날 때 처음 시선 각도 (+ = 아래, 책상을 보고 졸던 자세)")]
        [SerializeField] private float wakeStartPitch = 35f;
        [Tooltip("게임 시작 때 검은 화면에서 밝아짐")]
        [SerializeField] private bool fadeInOnStart = true;
        private PlayerLook look;
        private PlayerInteractor interactor;
        private JournalHolder journal;
        private PatrolRun subscribedRun;
        private Coroutine startFade;

        /// <summary>연출 중인지 (이 동안 엘리베이터는 다시 안 눌림)</summary>
        public bool IsBusy { get; private set; }
        /// <summary>하행 연출 시작 (예: 문 닫히는 소리, 버튼음)</summary>
        public event Action DescendStarted;
        /// <summary>하행 성공 후 "N층"을 띄우기 직전, 도착한 층 번호 (예: "띵" 소리)</summary>
        public event Action<int> Arrived;
        /// <summary>졸다 깨는 연출 시작 (예: 숨 들이쉬는 소리)</summary>
        public event Action WakeStarted;

        // 꺼진 문(Awake 전)은 닫힘 위치를 모르므로 건드리지 않는다
        private bool HasDoors => doors != null && doors.isActiveAndEnabled;

        private void Awake()
        {
            if (run == null) run = UnityEngine.Object.FindAnyObjectByType<PatrolRun>();
            if (player == null) player = UnityEngine.Object.FindAnyObjectByType<PlayerMovement>();
            if (fader == null) fader = UnityEngine.Object.FindAnyObjectByType<ScreenFader>();
            if (doors == null) doors = UnityEngine.Object.FindAnyObjectByType<ElevatorDoors>();
            if (player != null)
            {
                look = player.GetComponent<PlayerLook>();
                interactor = player.GetComponent<PlayerInteractor>();
                journal = player.GetComponentInChildren<JournalHolder>(true);
            }
        }

        private void OnEnable()
        {
            subscribedRun = run;
            if (subscribedRun != null) subscribedRun.Failed += OnFailed;
        }

        private void OnDisable()
        {
            if (subscribedRun != null) subscribedRun.Failed -= OnFailed;
            subscribedRun = null;
            StopAllCoroutines();
            startFade = null;
            if (IsBusy && HasDoors) doors.SetOpenImmediate(true);   // 문이 닫힌 채로 멈추지 않게
            End();
            SetCaption(string.Empty);
            if (fader != null) fader.SetAlpha(0f);
        }

        private void Start()
        {
            if (!fadeInOnStart || fader == null) return;
            fader.SetAlpha(1f);
            startFade = StartCoroutine(fader.FadeTo(0f, fadeSeconds));
        }

        /// <summary>엘리베이터 하행 연출 (엘리베이터가 부름). 연출 중·클리어 뒤·이 컴포넌트가 꺼져 있으면 무시</summary>
        public void Descend()
        {
            if (IsBusy || !isActiveAndEnabled || run == null || run.IsCleared) return;
            StartCoroutine(DescendRoutine());
        }

        private IEnumerator DescendRoutine()
        {
            Begin();
            SafeEvent.Raise(DescendStarted, this);
            if (HasDoors) yield return doors.Close();
            if (fader != null) yield return fader.FadeTo(1f, fadeSeconds);

            if (JudgeInTheDark())
            {
                if (HasDoors) doors.SetOpenImmediate(true);   // 다음 하행을 위해 문은 열어 둠
                yield return WakeUp();
            }
            else
            {
                SafeEvent.Raise(Arrived, run.CurrentFloor, this);
                SetCaption($"{run.CurrentFloor}층");
                yield return new WaitForSeconds(holdSeconds);
                SetCaption(string.Empty);
                if (fader != null) yield return fader.FadeTo(0f, fadeSeconds);
                if (HasDoors) yield return doors.Open();
            }
            End();
        }

        // 어두운 동안 판정·층 바꾸기 (실패면 시작 자리로 순간이동까지). 실패했으면 true.
        // 판정 중 예외(사물 코드 오류 등)가 나도 연출은 끝까지 가서 잠금이 풀리게 한다 — 그때는 층 그대로
        private bool JudgeInTheDark()
        {
            int attemptBefore = run.Attempt;
            try { run.TryDescend(); }
            catch (Exception e) { Debug.LogException(e, this); }
            return run.Attempt != attemptBefore;
        }

        // 개발자 메뉴 F3처럼 엘리베이터 밖에서 실패가 나도 같은 깨어나기 연출
        private void OnFailed(int failedFloor)
        {
            if (IsBusy) return;   // 하행 연출 중의 실패는 DescendRoutine이 처리
            StartCoroutine(WakeFromOutside());
        }

        private IEnumerator WakeFromOutside()
        {
            Begin();
            if (fader != null) fader.SetAlpha(1f);
            yield return WakeUp();
            End();
        }

        // 검은 화면에서 시작해 깜빡이며 밝아지고, 시선이 아래에서 정면으로 올라온다
        private IEnumerator WakeUp()
        {
            SafeEvent.Raise(WakeStarted, this);
            if (look != null) look.SetPitch(wakeStartPitch);
            for (float t = 0f; t < wakeSeconds; t += Time.deltaTime)
            {
                float k = Mathf.Clamp01(t / wakeSeconds);
                if (look != null) look.SetPitch(Mathf.Lerp(wakeStartPitch, 0f, Mathf.SmoothStep(0f, 1f, k)));
                if (fader != null) fader.SetAlpha(BlinkAlpha(k));
                yield return null;
            }
            if (look != null) look.SetPitch(0f);
            if (fader != null) fader.SetAlpha(0f);
        }

        /// <summary>
        /// 깨어나기 진행도(0~1) → 화면 어두움: 조금 밝아짐(1→0.5) → 다시 감음(0.5→1) → 눈 뜸(1→0)
        /// </summary>
        public static float BlinkAlpha(float k)
        {
            k = Mathf.Clamp01(k);
            if (k < 0.3f) return Mathf.Lerp(1f, 0.5f, k / 0.3f);
            if (k < 0.45f) return Mathf.Lerp(0.5f, 1f, (k - 0.3f) / 0.15f);
            return Mathf.Lerp(1f, 0f, (k - 0.45f) / 0.55f);
        }

        private void Begin()
        {
            if (startFade != null)
            {
                StopCoroutine(startFade);   // 게임 시작 페이드와 겹치지 않게
                startFade = null;
            }
            IsBusy = true;
            Lock(true);
        }

        private void End()
        {
            Lock(false);
            IsBusy = false;
        }

        private void SetCaption(string text)
        {
            if (floorCaption != null) floorCaption.text = text;
        }

        private void Lock(bool locked)
        {
            if (player != null) player.SetBlocked(this, locked);
            if (look != null) look.SetBlocked(this, locked);
            if (interactor != null) interactor.SetBlocked(this, locked);
            if (journal != null) journal.SetBlocked(this, locked);
        }
    }
}
