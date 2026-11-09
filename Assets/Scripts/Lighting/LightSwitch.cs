using NightPatrol.Interaction;
using UnityEngine;

namespace NightPatrol.Lighting
{
	/// <summary>벽 스위치. E로 연결된 LightZone을 켜고 끔 (점검 판정과는 무관). 정전 구역(PowerOutage)이면 눌러도 안 켜짐</summary>
	public sealed class LightSwitch : MonoBehaviour, IInteractable
	{
		[SerializeField] private LightZone zone;
		[Tooltip("눌리는 판 (켜짐/꺼짐에 따라 기울어짐)")] [SerializeField] private Transform rocker;
		[SerializeField] private float rockerAngle = 12f;

		public LightZone Zone => zone;

		public void Setup(LightZone z, Transform r)
		{
			zone = z;
			rocker = r;
			Sync();
		}

		public string Prompt => zone == null ? string.Empty
			: PowerOutage.IsCut(zone) ? "E - 스위치 (전기가 안 들어옴)"
			: zone.IsOn ? "E - 불 끄기" : "E - 불 켜기";

		public void Interact(PlayerInteractor who)
		{
			if (zone == null) return;
			if (PowerOutage.IsCut(zone))
			{
				// 판만 딸깍 넘어갔다 돌아옴 (불은 그대로)
				if (rocker != null) rocker.localRotation = Quaternion.Euler(-rocker.localRotation.eulerAngles.x, 0f, 0f);
				CancelInvoke(nameof(Sync));
				Invoke(nameof(Sync), 0.25f);
				return;
			}
			zone.Toggle();
		}

		void OnEnable()
		{
			if (zone != null) zone.Changed += OnChanged;
			Sync();
		}

		void OnDisable()
		{
			if (zone != null) zone.Changed -= OnChanged;
		}

		void OnChanged(LightZone z, bool on) => Sync();

		void Sync()
		{
			if (rocker != null && zone != null)
				rocker.localRotation = Quaternion.Euler(zone.IsOn ? -rockerAngle : rockerAngle, 0f, 0f);
		}
	}
}
