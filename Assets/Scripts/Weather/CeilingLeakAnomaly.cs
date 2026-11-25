using NightPatrol.Anomalies;
using NightPatrol.Photo;
using UnityEngine;

namespace NightPatrol.Weather
{
	/// <summary>
	/// 이상현상: 천장에서 새던 빗물이 젤리빈으로 바뀌어 바닥에 흩어진다. 정상 상태 = 물이 똑똑 샘.
	///
	/// 연결 (이상현상 담당):
	/// 1. 비 새는 자리 오브젝트(CeilingDrip이 붙은 것)나 그 부모에 이 컴포넌트를 붙인다.
	/// 2. Patrol Target에 점검 항목 PatrolObject를 넣는다 (다른 이상현상과 같은 규칙).
	/// 3. AnomalyManager에 후보 칸을 하나 추가해서 이걸 넣는다 (예: "젤리빈 누수 이상현상 후보").
	/// 촬영: 웅덩이 자리에 PhotoTarget을 자동으로 붙인다 (이미 자식에 있으면 그대로 씀).
	/// </summary>
	[DisallowMultipleComponent]
	public sealed class CeilingLeakAnomaly : Anomaly
	{
		[Tooltip("비워두면 자식에서 찾음")]
		[SerializeField] private CeilingDrip drip;
		[Tooltip("웅덩이 자리에 촬영 대상 자동 추가")]
		[SerializeField] private bool addPhotoTarget = true;
		[SerializeField, Min(0.1f)] private float photoSize = 0.8f;

		void Awake()
		{
			if (drip == null) drip = GetComponentInChildren<CeilingDrip>(true);
			if (drip == null) Debug.LogWarning("[CeilingLeakAnomaly] CeilingDrip이 없음 — 자식에 비 새는 자리를 두거나 칸에 연결", this);
		}

		void Start()
		{
			if (!addPhotoTarget || drip == null || GetComponentInChildren<PhotoTarget>(true) != null) return;
			var go = new GameObject("CeilingLeak_PhotoTarget");
			go.transform.SetParent(transform, false);
			go.transform.position = drip.FloorPoint + Vector3.up * 0.05f;
			go.transform.rotation = Quaternion.LookRotation(Vector3.up);   // 파란 화살표(앞)가 위 = 바닥 면
			go.AddComponent<PhotoTarget>().Setup(this, photoSize, true);
		}

		public override void Activate()
		{
			if (IsActive) return;
			if (drip == null)
			{
				Debug.LogWarning("[CeilingLeakAnomaly] CeilingDrip 연결 안 됨", this);
				return;
			}
			drip.UseCandy();
			IsActive = true;
			RegisterAnomaly();
		}

		public override void Deactivate()
		{
			Restore();
			IsActive = false;
			UnregisterAnomaly();
		}

		public override void ResetAfterFloorReset()
		{
			Restore();
			IsActive = false;
			ForgetRegistrationAfterFloorReset();
		}

		void Restore()
		{
			if (drip == null) return;
			drip.UseWater();
			drip.ClearPile();
		}
	}
}
