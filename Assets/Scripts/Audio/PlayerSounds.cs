using NightPatrol.Interaction;
using NightPatrol.Journal;
using NightPatrol.Menu;
using NightPatrol.Run;
using UnityEngine;

namespace NightPatrol.Audio
{
    /// <summary>
    /// 플레이어 쪽 소리 연결점. 소리 파일을 비워 두면 그 소리만 안 난다 — 사운드 방향(공동 결정)이 정해지면 파일만 넣으면 된다.
    /// 발소리(걸은 거리마다), E 조작, 일지·메뉴 들기·내리기, 엘리베이터 하행 시작·도착, 깨어나기.
    /// Player 루트에 붙인다. 전체 크기는 일시정지 메뉴의 "소리 크기"를 따른다 (AudioListener.volume)
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public sealed class PlayerSounds : MonoBehaviour
    {
        [Header("발소리")]
        [Tooltip("여러 개면 무작위로 (같은 소리가 연달아 나지 않게)")]
        [SerializeField] private AudioClip[] footsteps = new AudioClip[0];
        [Tooltip("이만큼(m) 걸을 때마다 한 걸음")]
        [SerializeField, Min(0.1f)] private float stepDistance = 0.9f;
        [Tooltip("발소리 음량 (0~1). 설정 메뉴의 전체 음량과 곱해진다")]
        [SerializeField, Range(0f, 1f)] private float footstepVolume = 0.5f;
        [Tooltip("걸음마다 높낮이를 이만큼 무작위로 (반복이 덜 거슬리게)")]
        [SerializeField, Range(0f, 0.3f)] private float footstepPitchJitter = 0.06f;

        [Header("조작")]
        [Tooltip("E로 무언가를 조작했을 때 (짧은 딸깍)")]
        [SerializeField] private AudioClip interact;
        [Tooltip("일지(클립보드)를 들 때. ESC 메뉴를 열 때도")]
        [SerializeField] private AudioClip journalRaise;
        [Tooltip("일지를 내릴 때. ESC 메뉴를 닫을 때도")]
        [SerializeField] private AudioClip journalLower;

        [Header("엘리베이터·실패")]
        [Tooltip("하행 시작 (문 닫힘·구동음)")]
        [SerializeField] private AudioClip descend;
        [Tooltip("도착 (띵)")]
        [SerializeField] private AudioClip arrive;
        [Tooltip("졸다 깸 (숨 들이쉼)")]
        [SerializeField] private AudioClip wake;
        [Tooltip("효과음 음량 (조작·일지·엘리베이터·실패·깨어남, 0~1). 설정 메뉴의 전체 음량과 곱해진다")]
        [SerializeField, Range(0f, 1f)] private float effectVolume = 0.8f;

        // 한 프레임에 이만큼(m) 넘게 움직이면 순간이동(실패 후 데스크로)이라 발소리를 내지 않는다
        private const float TeleportDistance = 1f;

        private AudioSource source;       // 효과음. 메뉴 소리가 나게 일시정지를 무시
        private AudioSource stepSource;   // 발소리. 일시정지 때 멈추고 높낮이를 따로 바꿈
        private CharacterController controller;
        private PlayerInteractor interactor;
        private JournalHolder journal;
        private PauseMenu menu;
        private FloorTransition transition;
        private Vector3 lastPosition;
        private float walked;
        private int lastFootstep = -1;

        private void Awake()
        {
            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.ignoreListenerPause = true;
            stepSource = gameObject.AddComponent<AudioSource>();
            stepSource.playOnAwake = false;
            stepSource.spatialBlend = 0f;

            controller = GetComponent<CharacterController>();
            interactor = GetComponent<PlayerInteractor>();
            journal = GetComponentInChildren<JournalHolder>(true);
            menu = GetComponentInChildren<PauseMenu>(true);
            transition = GetComponentInChildren<FloorTransition>(true);
            if (transition == null) transition = UnityEngine.Object.FindAnyObjectByType<FloorTransition>();
        }

        private void OnEnable()
        {
            lastPosition = transform.position;
            walked = 0f;
            if (interactor != null) interactor.Interacted += OnInteracted;
            if (journal != null) journal.RaisedChanged += OnRaised;
            if (menu != null) menu.OpenChanged += OnRaised;
            if (transition != null)
            {
                transition.DescendStarted += OnDescend;
                transition.Arrived += OnArrived;
                transition.WakeStarted += OnWake;
            }
        }

        private void OnDisable()
        {
            if (interactor != null) interactor.Interacted -= OnInteracted;
            if (journal != null) journal.RaisedChanged -= OnRaised;
            if (menu != null) menu.OpenChanged -= OnRaised;
            if (transition != null)
            {
                transition.DescendStarted -= OnDescend;
                transition.Arrived -= OnArrived;
                transition.WakeStarted -= OnWake;
            }
        }

        private void Update()
        {
            Vector3 position = transform.position;
            Vector3 moved = position - lastPosition;
            lastPosition = position;
            moved.y = 0f;
            bool grounded = controller == null || controller.isGrounded;
            if (Step(ref walked, grounded ? moved.magnitude : 0f, stepDistance, TeleportDistance)) PlayFootstep();
        }

        /// <summary>
        /// 걸은 거리를 쌓다가 stepDistance를 넘으면 한 걸음(true). 한 번에 teleportDistance 넘게 움직이면 순간이동으로 보고 무시
        /// </summary>
        public static bool Step(ref float walked, float moved, float stepDistance, float teleportDistance)
        {
            if (moved >= teleportDistance)
            {
                walked = 0f;
                return false;
            }
            walked += Mathf.Max(0f, moved);
            if (stepDistance <= 0f || walked < stepDistance) return false;
            walked = Mathf.Repeat(walked - stepDistance, stepDistance);
            return true;
        }

        /// <summary>count개 중 하나를 고르되 바로 전(last)과 다르게. roll은 0 이상 아무 정수(무작위)</summary>
        public static int PickIndex(int count, int last, int roll)
        {
            if (count <= 0) return -1;
            if (count == 1) return 0;
            roll = Mathf.Abs(roll);
            if (last < 0 || last >= count) return roll % count;
            int index = roll % (count - 1);
            return index >= last ? index + 1 : index;
        }

        private void PlayFootstep()
        {
            if (footsteps == null || footsteps.Length == 0) return;
            int index = PickIndex(footsteps.Length, lastFootstep, Random.Range(0, int.MaxValue));
            lastFootstep = index;
            var clip = footsteps[index];
            if (clip == null) return;
            stepSource.pitch = 1f + Random.Range(-footstepPitchJitter, footstepPitchJitter);
            stepSource.PlayOneShot(clip, footstepVolume);
        }

        private void Play(AudioClip clip)
        {
            if (clip != null) source.PlayOneShot(clip, effectVolume);
        }

        private void OnInteracted(IInteractable _) => Play(interact);
        private void OnRaised(bool raised) => Play(raised ? journalRaise : journalLower);
        private void OnDescend() => Play(descend);
        private void OnArrived(int floor) => Play(arrive);
        private void OnWake() => Play(wake);
    }
}
