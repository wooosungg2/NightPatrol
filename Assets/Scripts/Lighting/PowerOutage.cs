using System;
using System.Collections.Generic;
using NightPatrol.Run;
using UnityEngine;

namespace NightPatrol.Lighting
{
	/// <summary>
	/// 층별 정전 구역. 층이 시작될 때(FloorReset, LightZone이 처음 상태로 돌아간 다음) 정해 둔 조명 묶음을 끈다.
	/// 끊긴 구역은 벽 스위치로 켤 수 없다 (LightSwitch가 IsCut을 확인). 손전등으로만 다녀야 하는 구간.
	///
	/// 씬에 하나 (Systems 아래). 실행 순서를 PatrolRun보다 앞으로 둬서 첫 층부터 적용된다:
	/// LightZone.OnEnable(구독) → 이 컴포넌트 Start(구독) → PatrolRun.Start(첫 층) 순서라 FloorReset에서 LightZone보다 늦게 불린다.
	///
	/// 다른 코드용: PowerOutage.IsCut(zone), PowerOutage.Changed, Restore() (차단기·전화 안내 기믹에서 전기 복구).
	/// 이상현상은 끊긴 구역을 대상으로 고르지 말 것 (LightZone.StartFlicker를 부르면 정전인데 깜빡이게 됨).
	/// </summary>
	[DisallowMultipleComponent]
	[DefaultExecutionOrder(-50)]
	public sealed class PowerOutage : MonoBehaviour
	{
		[Serializable]
		public sealed class FloorCut
		{
			[Tooltip("층 번호")] public int floor;
			[Tooltip("끌 조명 묶음 Id. 끝이 *이면 앞부분이 같은 것 전부 (예: Corridor.Lights.*)")]
			public string[] zoneIds = new string[0];
			[Tooltip("*로 고른 것 중 켜 둘 것 (엘리베이터 앞 등)")]
			public string[] keepIds = new string[0];
		}

		[Tooltip("층마다 끊을 구역. 기본값은 예시 — 실제 Id는 Patrol Objects 창·씬에서 확인해서 맞출 것")]
		[SerializeField] private FloorCut[] cuts =
		{
			new FloorCut { floor = 3, zoneIds = new[] { "R24.Lights", "Corridor.Lights.03", "Corridor.Lights.04" } },
			new FloorCut { floor = 2, zoneIds = new[] { "Corridor.Lights.*", "R21.Lights", "R24.Lights" }, keepIds = new[] { "Corridor.Lights.00" } },
		};
		[Tooltip("첫 회차에도 정전을 적용할지. 끄면 한 번 실패한 뒤부터 정전")]
		[SerializeField] private bool onFirstAttempt = true;

		static readonly HashSet<LightZone> cut = new HashSet<LightZone>();
		/// <summary>지금 층에서 전기가 끊긴 조명 묶음인지</summary>
		public static bool IsCut(LightZone zone) => zone != null && cut.Contains(zone);
		/// <summary>지금 층에 정전 구역이 있는지</summary>
		public static bool HasOutage => cut.Count > 0;
		/// <summary>정전 구역이 바뀌었을 때 (층 시작·복구)</summary>
		public static event Action Changed;

		PatrolRun run;

		void Start()
		{
			run = PatrolRun.Current != null ? PatrolRun.Current : FindAnyObjectByType<PatrolRun>();
			if (run != null) run.FloorReset += OnFloorReset;
			else Debug.LogWarning("[PowerOutage] PatrolRun이 없어 정전을 적용하지 않음", this);
		}

		void OnDestroy()
		{
			if (run != null) run.FloorReset -= OnFloorReset;
			cut.Clear();
		}

		void OnFloorReset(int floor)
		{
			cut.Clear();
			if (onFirstAttempt || run == null || run.Attempt > 1)
				foreach (var c in cuts)
				{
					if (c == null || c.floor != floor) continue;
					foreach (var z in LightZone.All)
						if (z != null && Matches(z.Id, c.zoneIds) && !Matches(z.Id, c.keepIds)) cut.Add(z);
				}
			foreach (var z in cut) z.SetOn(false);
			if (cut.Count > 0) Debug.Log($"[PowerOutage] {floor}층 정전 구역 {cut.Count}개", this);
			Changed?.Invoke();
		}

		/// <summary>이 층의 전기를 다시 넣음 (차단기·전화 안내 기믹용). 처음부터 켜져 있던 구역만 켜짐</summary>
		public void Restore()
		{
			if (cut.Count == 0) return;
			var list = new List<LightZone>(cut);
			cut.Clear();
			foreach (var z in list) if (z != null) z.SetOn(z.startsOn);
			Changed?.Invoke();
		}

		internal static bool Matches(string id, string[] patterns)
		{
			if (string.IsNullOrEmpty(id) || patterns == null) return false;
			foreach (var p in patterns)
			{
				if (string.IsNullOrEmpty(p)) continue;
				if (p.EndsWith("*") ? id.StartsWith(p.Substring(0, p.Length - 1), StringComparison.Ordinal) : id == p) return true;
			}
			return false;
		}

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		static void ResetStatics()
		{
			cut.Clear();
			Changed = null;
		}
	}
}
