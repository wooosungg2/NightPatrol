using System;
using UnityEngine;

namespace NightPatrol.Player
{
    /// <summary>
    /// 걷는 느낌: 걸을 때 화면이 살짝 흔들리고(걸음마다 아래로, 두 걸음에 한 번 좌우) 발을 디딜 때마다 Stepped를 알린다
    /// (발소리는 PlayerSounds가 이걸 듣고 낸다 — 소리 파일은 PlayerSounds 한 곳).
    /// 달리면 흔들림이 커지고 시야가 조금 넓어진다. 값은 PlayerSettings (Head Bob·Step Length·Run Fov Boost).
    /// PlayerMovement가 실행 중에 붙인다 — 프리팹에 따로 붙일 필요 없음
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]   // PlayerLook(-100)·PlayerMovement가 이번 프레임 움직인 뒤
    public sealed class PlayerFeel : MonoBehaviour
    {
        private PlayerMovement movement;
        private Transform cameraTransform;
        private Camera eye;
        private NightPatrol.Phone.PhoneHolder phone;
        private Vector3 basePosition;
        private float phase, amplitude, fovKick, fovVelocity;
        private bool bound;

        /// <summary>설정 메뉴 "화면 흔들림" (GameSettings.HeadBob이 넣어 줌). 끄면 흔들지 않고 발소리는 그대로</summary>
        public static bool HeadBobEnabled { get; set; } = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => HeadBobEnabled = true;

        /// <summary>지금 카메라에 더한 흔들림 (카메라 부모 기준, m)</summary>
        public Vector3 Offset { get; private set; }
        /// <summary>지금 시야각에 더한 값 (도)</summary>
        public float FovKick { get; private set; }
        /// <summary>지금까지 디딘 걸음 수</summary>
        public int Steps { get; private set; }
        /// <summary>발을 디딜 때 (달리는 중이면 true). PlayerSounds가 발소리를 내고, 다른 소리·연출도 여기에 붙인다</summary>
        public event Action<bool> Stepped;

        private void Awake()
        {
            movement = GetComponent<PlayerMovement>();
            var look = GetComponent<PlayerLook>();
            Bind(look != null ? look.CameraTransform : null);
        }

        /// <summary>흔들 카메라를 정한다 (없으면 자식 카메라). 지금 위치가 흔들림 없는 기준</summary>
        public void Bind(Transform camera)
        {
            if (camera == null)
            {
                var child = GetComponentInChildren<Camera>(true);
                camera = child != null ? child.transform : null;
            }
            cameraTransform = camera;
            eye = camera != null ? camera.GetComponent<Camera>() : null;
            if (camera != null) basePosition = camera.localPosition;
            bound = camera != null;
        }

        /// <summary>순간이동·층 시작: 흔들림·시야를 바로 처음으로</summary>
        public void ResetFeel()
        {
            phase = amplitude = fovVelocity = 0f;
            Apply(Vector3.zero, 0f);
        }

        private void LateUpdate()
        {
            if (!bound || movement == null || movement.Settings == null || Time.deltaTime <= 0f) return;
            if (phone == null) phone = GetComponentInChildren<NightPatrol.Phone.PhoneHolder>(true);
            Tick(movement.Settings, movement.Speed, movement.IsRunning, movement.IsGrounded, phone == null || !phone.IsInHand, Time.deltaTime);
        }

        /// <summary>
        /// 한 프레임: speed = 지금 걷는 빠르기(m/s), running = 달리기 키, grounded = 땅에 닿음,
        /// allowFovKick = 시야를 넓혀도 되는지 (휴대폰 카메라를 든 동안은 뷰파인더와 어긋나지 않게 끔)
        /// </summary>
        public void Tick(PlayerSettings settings, float speed, bool running, bool grounded, bool allowFovKick, float dt)
        {
            if (settings == null || dt <= 0f) return;
            bool moving = grounded && speed > 0.1f;
            float strength = moving ? BobStrength(speed, settings.WalkSpeed, settings.RunSpeed, settings.RunBobScale) : 0f;
            // 멈추거나 떨어지면 흔들림이 부드럽게 0으로 (갑자기 제자리로 튀지 않게)
            amplitude = Mathf.MoveTowards(amplitude, settings.HeadBob && HeadBobEnabled ? strength : 0f, dt / 0.15f);

            if (moving)
            {
                float stepLength = running ? settings.RunStepLength : settings.WalkStepLength;
                int before = Mathf.FloorToInt(phase / Mathf.PI);
                phase += speed / Mathf.Max(0.1f, stepLength) * Mathf.PI * dt;
                int crossed = Mathf.FloorToInt(phase / Mathf.PI) - before;
                for (int i = 0; i < crossed; i++) Step(running);
            }

            var offset = BobOffset(phase, settings.BobHeight * amplitude, settings.BobSway * amplitude);
            float target = running && moving && allowFovKick ? settings.RunFovBoost : 0f;
            fovKick = Mathf.SmoothDamp(fovKick, target, ref fovVelocity, 0.2f, Mathf.Infinity, dt);
            if (Mathf.Abs(fovKick) < 0.001f && target == 0f) fovKick = 0f;
            Apply(offset, fovKick);
        }

        /// <summary>
        /// 걸음 위상(한 걸음 = π) → 흔들림: 발을 디딜 때(위상이 π의 배수) 가장 낮고 걸음 가운데서 가장 높다.
        /// 위아래는 부드러운 곡선(cos²)이라 디딜 때 툭 꺾이지 않는다. 좌우는 두 걸음에 한 번
        /// </summary>
        public static Vector3 BobOffset(float phase, float height, float sway)
        {
            float c = Mathf.Cos(phase);
            return new Vector3(Mathf.Sin(phase) * sway, -c * c * height, 0f);
        }

        /// <summary>
        /// 흔들림 세기: 걷는 속도까지는 빠르기에 비례해 0→1, 걷기와 달리기 사이에서는 1→runScale.
        /// 달릴 때 속도 비율(4.5/2.6 ≈ 1.7배)만큼 커지면 너무 흔들려서 따로 정한 배율까지만
        /// </summary>
        public static float BobStrength(float speed, float walkSpeed, float runSpeed, float runScale)
        {
            if (speed <= 0f || walkSpeed <= 0f) return 0f;
            if (speed <= walkSpeed || runSpeed <= walkSpeed) return Mathf.Clamp01(speed / walkSpeed);
            return Mathf.Lerp(1f, Mathf.Max(1f, runScale), Mathf.InverseLerp(walkSpeed, runSpeed, speed));
        }

        private void Step(bool running)
        {
            Steps++;
            SafeEvent.Raise(Stepped, running, this);
        }

        private void Apply(Vector3 offset, float kick)
        {
            if (cameraTransform != null) cameraTransform.localPosition = basePosition + offset;
            if (eye != null) eye.fieldOfView += kick - FovKick;   // 지난 프레임에 더한 만큼 빼고 새로 더함 (다른 곳에서 정한 시야각은 그대로)
            Offset = offset;
            FovKick = kick;
        }

        private void OnDisable() => ResetFeel();
    }
}
