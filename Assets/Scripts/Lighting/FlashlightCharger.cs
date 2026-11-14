using NightPatrol.Interaction;
using NightPatrol.Run;
using UnityEngine;

namespace NightPatrol.Lighting
{
	/// <summary>
	/// 손전등 충전 거치대. E로 손전등 배터리를 채운다. 한 층에 쓸 수 있는 횟수가 정해져 있고 층이 시작되면 다시 채워짐.
	/// 콜라이더가 있는 오브젝트에 붙일 것 (조준용). 어디에 몇 개 둘지가 난이도 — 예: 경비 데스크 1개, 3층 계단실 옆 1개.
	/// </summary>
	public sealed class FlashlightCharger : MonoBehaviour, IInteractable
	{
		[Tooltip("한 번에 채우는 양 (0~1)")] [Range(0.05f, 1f)] [SerializeField] private float amount = 0.5f;
		[Tooltip("한 층에서 쓸 수 있는 횟수 (0 = 무제한)")] [Min(0)] [SerializeField] private int usesPerFloor = 1;
		[Tooltip("남은 횟수에 따라 색이 바뀌는 표시등 (없어도 됨)")] [SerializeField] private Renderer indicator;
		[SerializeField] private Color readyColor = new Color(0.2f, 1f, 0.35f), usedColor = new Color(0.9f, 0.15f, 0.1f);

		int used;
		PatrolRun run;
		MaterialPropertyBlock mpb;

		bool Ready => usesPerFloor == 0 || used < usesPerFloor;

		public string Prompt
		{
			get
			{
				var f = Flashlight.Current;
				if (f == null || !f.useBattery) return string.Empty;
				if (!Ready) return "충전기 (방전됨)";
				if (f.Battery01 >= 0.99f) return "손전등 가득 참";
				return $"E - 손전등 충전 ({Mathf.RoundToInt(f.Battery01 * 100f)}%)";
			}
		}

		public void Interact(PlayerInteractor who)
		{
			var f = Flashlight.Current;
			if (f == null || !Ready || f.Battery01 >= 0.99f) return;
			f.Recharge(amount);
			used++;
			Sync();
		}

		void OnEnable()
		{
			run = FindAnyObjectByType<PatrolRun>();
			if (run != null) run.FloorReset += OnFloorReset;
			Sync();
		}

		void OnDisable()
		{
			if (run != null) run.FloorReset -= OnFloorReset;
		}

		void OnFloorReset(int floor)
		{
			used = 0;
			Sync();
		}

		void Sync()
		{
			if (indicator == null) return;
			if (mpb == null) mpb = new MaterialPropertyBlock();
			Color c = Ready ? readyColor : usedColor;
			indicator.GetPropertyBlock(mpb);
			mpb.SetColor("_BaseColor", c);
			mpb.SetColor("_EmissionColor", c * 2f);
			indicator.SetPropertyBlock(mpb);
		}
	}
}
