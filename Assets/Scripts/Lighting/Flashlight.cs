using System;
using NightPatrol.Run;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NightPatrol.Lighting
{
	/// <summary>
	/// 손전등. 플레이어(또는 카메라)에 붙이면 Spot Light를 만들어 시점을 살짝 늦게 따라가게 한다. F키로 켜고 끔.
	/// 다른 코드(이상현상 등)는 SetOn / Flicker로 조작할 수 있다.
	///
	/// 배터리: 켜 둔 동안만 닳는다. 내려갈 때는 이어지고, 실패·처음 시작이면 다시 가득 (refillOnRestart).
	/// 얼마 안 남으면 빛이 약해지고 가끔 끊긴다 (화면 UI 없이 빛으로만 알림). 다 닳으면 깜빡이다 꺼지고 다시 켜지지 않음.
	/// 충전: Recharge(0~1) / Refill() — FlashlightCharger가 부른다.
	/// 이상현상용: IsLighting(점) — 그 점을 지금 손전등이 비추는지 (LightQuery.IsInFlashlight가 씀).
	/// </summary>
	[DisallowMultipleComponent]
	public sealed class Flashlight : MonoBehaviour
	{
		[Tooltip("비워두면 자식에서 Camera를 찾음")] [SerializeField] private Camera playerCamera;
		[Tooltip("비워두면 실행할 때 Spot Light를 만듦")] [SerializeField] private Light spot;
		[SerializeField] private bool startOn = false;
		[Tooltip("손전등 켜고 끄기 입력. 비워두면 F키")] [SerializeField] private InputActionReference toggleAction;

		[Header("빛")]
		public float range = 14f;
		[Range(10f, 120f)] public float spotAngle = 55f;
		[Range(5f, 120f)] public float innerSpotAngle = 28f;
		public float intensity = 4f;
		public Color color = new Color(1f, 0.95f, 0.85f);
		public LightShadows shadows = LightShadows.Soft;

		[Header("움직임")]
		[Tooltip("카메라 기준 손 위치 (오른쪽 아래)")] public Vector3 handOffset = new Vector3(0.22f, -0.22f, 0.15f);
		[Tooltip("시점을 따라가는 빠르기. 작을수록 늦게 따라옴 (0 = 바로)")] public float followSpeed = 14f;

		[Header("배터리")]
		[Tooltip("끄면 배터리 없이 계속 켤 수 있음 (예전 동작)")] public bool useBattery = true;
		[Tooltip("가득 찬 배터리로 켜 둘 수 있는 시간(초)")] [Min(1f)] public float batterySeconds = 180f;
		[Tooltip("이 아래로 내려가면 빛이 약해지고 가끔 끊김 (0~1)")] [Range(0f, 1f)] public float lowBattery = 0.25f;
		[Tooltip("다 닳기 직전 밝기 비율")] [Range(0.05f, 1f)] public float lowIntensityScale = 0.35f;
		[Tooltip("배터리가 적을 때 1초에 끊길 확률")] [Range(0f, 3f)] public float lowDipsPerSecond = 0.6f;
		[Tooltip("다 닳을 때 깜빡이는 시간(초), 끝나면 꺼짐")] [Min(0f)] public float dyingSeconds = 0.8f;
		[Tooltip("처음 시작·실패·개발자 메뉴로 층을 새로 시작하면 가득 채움. 내려갈 때는 그대로")] public bool refillOnRestart = true;

		public bool IsOn => spot != null && spot.enabled;
		/// <summary>남은 배터리 (0~1). useBattery가 꺼져 있으면 항상 1</summary>
		public float Battery01 => useBattery ? battery : 1f;
		public bool IsEmpty => useBattery && battery <= 0f;
		public bool IsLow => useBattery && battery < lowBattery;
		/// <summary>켜짐/꺼짐이 바뀔 때 (소리·UI 연결용)</summary>
		public event Action<bool> Toggled;
		/// <summary>켜 둔 채 배터리가 다 닳아 꺼질 때</summary>
		public event Action Depleted;
		/// <summary>배터리가 없는데 켜려고 했을 때 (빈 딸깍 소리용)</summary>
		public event Action EmptyPressed;

		/// <summary>씬의 플레이어 손전등 (먼저 켜진 것, 없으면 null)</summary>
		public static Flashlight Current { get; private set; }

		Transform holder;
		float flickerUntil;
		float battery = 1f;
		bool dying;
		float dipUntil;
		PatrolRun run;

		void Awake()
		{
			if (playerCamera == null) playerCamera = GetComponentInChildren<Camera>();
			if (playerCamera == null) playerCamera = Camera.main;
			if (spot == null)
			{
				holder = new GameObject("Flashlight_Holder").transform;
				holder.SetParent(transform, false);
				var lgo = new GameObject("Flashlight_Spot");
				lgo.transform.SetParent(holder, false);
				spot = lgo.AddComponent<Light>();
				spot.type = LightType.Spot;
			}
			else holder = spot.transform;
			Apply();
			spot.enabled = startOn;
			Snap();
		}

		void OnEnable()
		{
			if (Current == null) Current = this;
			if (toggleAction != null) toggleAction.action?.Enable();
			run = FindAnyObjectByType<PatrolRun>();
			if (run != null) run.FloorReset += OnFloorReset;
		}

		void OnDisable()
		{
			if (Current == this) Current = null;
			if (toggleAction != null) toggleAction.action?.Disable();
			if (run != null) run.FloorReset -= OnFloorReset;
		}

		void OnFloorReset(int floor)
		{
			if (refillOnRestart && run != null && run.LastStart != FloorStartReason.Descend) Refill();
		}

		void Apply()
		{
			spot.range = range;
			spot.spotAngle = spotAngle;
			spot.innerSpotAngle = Mathf.Min(innerSpotAngle, spotAngle);
			spot.intensity = intensity;
			spot.color = color;
			spot.shadows = shadows;
		}

		void OnValidate()
		{
			if (spot != null) Apply();
		}

		void Update()
		{
			var action = toggleAction != null ? toggleAction.action : null;
			bool pressed = action != null
				? action.WasPressedThisFrame()
				: Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame;
			if (pressed && Cursor.lockState == CursorLockMode.Locked && Time.timeScale > 0f)
				Toggle();

			Drain(Time.deltaTime);
			if (spot != null && spot.enabled) spot.intensity = CurrentIntensity();
		}

		void Drain(float dt)
		{
			if (!useBattery || !IsOn || dying || dt <= 0f) return;
			battery = Mathf.Max(0f, battery - dt / batterySeconds);
			if (battery > 0f) return;
			// 다 닳음: 잠깐 깜빡이다 꺼짐
			dying = true;
			Flicker(dyingSeconds);
		}

		float CurrentIntensity()
		{
			float k = 1f;
			if (useBattery && battery < lowBattery && lowBattery > 0f)
				k = Mathf.Lerp(lowIntensityScale, 1f, battery / lowBattery);

			if (dying && Time.time >= flickerUntil)
			{
				dying = false;
				SetOn(false);
				Depleted?.Invoke();
				return 0f;
			}
			if (Time.time < flickerUntil)   // 깜빡임 (이상현상·배터리 끝)
				return intensity * k * (UnityEngine.Random.value < 0.25f ? 0.05f : UnityEngine.Random.Range(0.6f, 1f));

			if (IsLow)   // 배터리 적음: 가끔 짧게 끊김
			{
				if (Time.time >= dipUntil && UnityEngine.Random.value < lowDipsPerSecond * Time.deltaTime)
					dipUntil = Time.time + UnityEngine.Random.Range(0.05f, 0.18f);
				if (Time.time < dipUntil) k *= 0.12f;
			}
			return intensity * k;
		}

		void LateUpdate()
		{
			if (playerCamera == null || holder == null) return;
			var cam = playerCamera.transform;
			holder.position = cam.TransformPoint(handOffset);
			float t = followSpeed <= 0f ? 1f : 1f - Mathf.Exp(-followSpeed * Time.deltaTime);
			holder.rotation = Quaternion.Slerp(holder.rotation, cam.rotation, t);
		}

		void Snap()
		{
			if (playerCamera == null || holder == null) return;
			holder.position = playerCamera.transform.TransformPoint(handOffset);
			holder.rotation = playerCamera.transform.rotation;
		}

		public void Toggle() => SetOn(!IsOn);

		public void SetOn(bool on)
		{
			if (spot == null || spot.enabled == on) return;
			if (on && IsEmpty)
			{
				EmptyPressed?.Invoke();
				return;
			}
			if (!on) dying = false;
			spot.enabled = on;
			if (on) spot.intensity = CurrentIntensity();
			Toggled?.Invoke(on);
		}

		/// <summary>seconds 동안 불규칙하게 깜빡임</summary>
		public void Flicker(float seconds) => flickerUntil = Time.time + Mathf.Max(0f, seconds);

		// ── 배터리 ──
		/// <summary>배터리를 amount01(0~1)만큼 채움</summary>
		public void Recharge(float amount01) => SetBattery(battery + Mathf.Max(0f, amount01));
		/// <summary>가득 채움</summary>
		public void Refill() => SetBattery(1f);
		/// <summary>배터리를 정해진 값으로 (개발자 메뉴·테스트용)</summary>
		public void SetBattery(float value01)
		{
			battery = Mathf.Clamp01(value01);
			if (battery > 0f) dying = false;
		}

		// ── 이상현상용 ──
		/// <summary>지금 손전등이 이 점을 비추는지 (켜짐 + 거리 + 원뿔 각도, occlusion이면 가려졌는지도)</summary>
		public bool IsLighting(Vector3 point, bool occlusion = true, float rangeScale = 0.9f)
		{
			if (!IsOn || spot == null || spot.intensity <= intensity * 0.1f) return false;
			var t = spot.transform;
			Vector3 d = point - t.position;
			float dist = d.magnitude;
			if (dist > range * rangeScale || dist < 0.0001f) return false;
			if (Vector3.Angle(t.forward, d) > spotAngle * 0.5f) return false;
			if (!occlusion) return true;
			// 대상 바로 앞까지만 검사 (대상 자신의 콜라이더에 걸리지 않게)
			return !Physics.Raycast(t.position, d / dist, dist - 0.15f, ~0, QueryTriggerInteraction.Ignore);
		}

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		static void ResetStatics() => Current = null;
	}
}
