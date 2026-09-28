using UnityEngine;

namespace NightPatrol
{
	/// <summary>
	/// 문 하나의 상태 (잠금·열림). 생성기가 DoorSlot 자리에 문을 만들 때 붙인다.
	///
	/// ※ 초안: 판정 시스템(양우성 님)과 합의 전. 플레이어 상호작용은 컨트롤러 쪽에서
	///    TryOpen / Close / SetLocked를 부르고, 판정은 IsLocked·IsInspectionTarget을 읽는 방식을 가정함.
	/// </summary>
	public class Door : MonoBehaviour
	{
		[Tooltip("문이 달린 방 호수 (예: 521)")] public string roomLabel;
		public DoorStyle style;
		[Tooltip("근무일지 '모든 출입문 잠금' 점검 대상인지")] public bool isInspectionTarget = true;

		[Header("상태")]
		[SerializeField] bool locked = true;
		[SerializeField] bool open;

		[Header("열림 동작")]
		[Tooltip("다 열렸을 때 각도")] public float openAngle = 95f;
		[Tooltip("초당 회전 각도")] public float swingSpeed = 180f;
		[Tooltip("문짝 (경첩 위치가 기준점). 생성기가 채움")] public Transform[] leaves;
		[Tooltip("문짝마다 여는 방향 (+1 / -1). 생성기가 채움")] public float[] swingSigns;

		/// <summary>잠금·열림이 바뀔 때마다 (판정·소리·UI 연결용)</summary>
		public event System.Action<Door> StateChanged;

		public bool IsLocked => locked;
		public bool IsOpen => open;

		/// <summary>잠겨 있으면 false (열리지 않음)</summary>
		public bool TryOpen()
		{
			if (locked) return false;
			SetOpen(true);
			return true;
		}

		public void Close() => SetOpen(false);

		/// <summary>열려 있으면 닫고, 닫혀 있으면 열기 시도</summary>
		public bool Toggle() => open ? CloseAndReturnTrue() : TryOpen();

		/// <summary>잠금. 열린 문은 먼저 닫힘</summary>
		public void SetLocked(bool value)
		{
			if (value && open) open = false;
			if (locked == value) { StateChanged?.Invoke(this); return; }
			locked = value;
			StateChanged?.Invoke(this);
		}

		/// <summary>애니메이션 없이 바로 그 상태로 (층 재시작·점검 항목 재생성용)</summary>
		public void SetStateImmediate(bool isLocked, bool isOpen)
		{
			locked = isLocked;
			open = !isLocked && isOpen;
			for (int i = 0; leaves != null && i < leaves.Length; i++)
				if (leaves[i] != null) leaves[i].localRotation = Quaternion.Euler(0f, TargetAngle(i), 0f);
			StateChanged?.Invoke(this);
		}

		bool CloseAndReturnTrue() { SetOpen(false); return true; }

		void SetOpen(bool value)
		{
			if (open == value) return;
			open = value;
			StateChanged?.Invoke(this);
		}

		float TargetAngle(int i)
		{
			float sgn = swingSigns != null && i < swingSigns.Length ? swingSigns[i] : 1f;
			return open ? sgn * openAngle : 0f;
		}

		void Update()
		{
			if (leaves == null) return;
			for (int i = 0; i < leaves.Length; i++)
			{
				var l = leaves[i];
				if (l == null) continue;
				float cur = Mathf.DeltaAngle(0f, l.localEulerAngles.y);
				float next = Mathf.MoveTowards(cur, TargetAngle(i), swingSpeed * Time.deltaTime);
				if (!Mathf.Approximately(cur, next)) l.localRotation = Quaternion.Euler(0f, next, 0f);
			}
		}

		void OnValidate()
		{
			if (!Application.isPlaying) SetStateImmediate(locked, open);
		}
	}
}
