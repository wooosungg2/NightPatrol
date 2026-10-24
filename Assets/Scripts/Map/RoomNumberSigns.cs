using System.Collections.Generic;
using UnityEngine;

namespace NightPatrol
{
	/// <summary>
	/// 방 표찰 호수를 지금 층에 맞춤 (5층 맵을 모든 층에 다시 쓰므로 521 → 421 → 321 …).
	/// 생성기가 FloorGenerator에 붙인다. 층이 시작될 때(PatrolRun.FloorStarted) 바뀜.
	/// 점검 사물 Id(R21.Door.01 등)에는 층 번호가 없어서 영향 없음.
	/// </summary>
	[DisallowMultipleComponent]
	public sealed class RoomNumberSigns : MonoBehaviour
	{
		readonly List<TMPro.TMP_Text> texts = new List<TMPro.TMP_Text>();
		readonly List<string> originals = new List<string>();
		NightPatrol.Run.PatrolRun run;

		void Awake() => Collect();

		void OnEnable()
		{
			run = FindAnyObjectByType<NightPatrol.Run.PatrolRun>();
			if (run != null) run.FloorStarted += SetFloor;
		}

		void OnDisable()
		{
			if (run != null) run.FloorStarted -= SetFloor;
		}

		void Collect()
		{
			texts.Clear(); originals.Clear();
			foreach (var t in GetComponentsInChildren<TMPro.TMP_Text>(true))
			{
				var p = t.transform.parent;
				if (t.gameObject.name != "Number" || p == null || !p.name.StartsWith("Nameplate_")) continue;
				texts.Add(t);
				originals.Add(t.text);
			}
		}

		/// <summary>표찰을 이 층 번호로 (예: 4 → 521이 421로)</summary>
		public void SetFloor(int floor)
		{
			for (int i = 0; i < texts.Count; i++)
			{
				if (texts[i] == null) continue;
				string o = originals[i];
				// 세 자리 숫자 호수만 앞자리를 바꿈 (계단실·화장실 등 다른 이름은 그대로)
				bool roomNo = o.Length == 3 && char.IsDigit(o[0]) && char.IsDigit(o[1]) && char.IsDigit(o[2]);
				texts[i].text = roomNo ? floor + o.Substring(1) : o;
			}
		}
	}
}
