using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NightPatrol.Lighting
{
	/// <summary>
	/// 손전등. 플레이어(또는 카메라)에 붙이면 Spot Light를 만들어 시점을 살짝 늦게 따라가게 한다. F키로 켜고 끔.
	/// 다른 코드(이상현상 등)는 SetOn / Flicker로 조작할 수 있다.
	/// </summary>
	[DisallowMultipleComponent]
	public sealed class Flashlight : MonoBehaviour
	{
		[Tooltip("비워두면 자식에서 Camera를 찾음")] [SerializeField] private Camera playerCamera;
		[Tooltip("비워두면 실행할 때 Spot Light를 만듦")] [SerializeField] private Light spot;
		[SerializeField] private bool startOn = false;

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

		public bool IsOn => spot != null && spot.enabled;
		/// <summary>켜짐/꺼짐이 바뀔 때 (소리·UI 연결용)</summary>
		public event Action<bool> Toggled;

		Transform holder;
		float flickerUntil;

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
			var kb = Keyboard.current;
			if (kb != null && kb.fKey.wasPressedThisFrame && Cursor.lockState == CursorLockMode.Locked && Time.timeScale > 0f)
				Toggle();

			// 깜빡임 (이상현상용)
			if (spot != null && Time.time < flickerUntil)
				spot.intensity = intensity * (UnityEngine.Random.value < 0.25f ? 0.05f : UnityEngine.Random.Range(0.6f, 1f));
			else if (spot != null && !Mathf.Approximately(spot.intensity, intensity))
				spot.intensity = intensity;
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
			spot.enabled = on;
			Toggled?.Invoke(on);
		}

		/// <summary>seconds 동안 불규칙하게 깜빡임</summary>
		public void Flicker(float seconds) => flickerUntil = Time.time + Mathf.Max(0f, seconds);
	}
}
