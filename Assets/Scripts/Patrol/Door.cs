using NightPatrol.Interaction;
using NightPatrol.Patrol;
using UnityEngine;

namespace NightPatrol
{
	/// <summary>
	/// 문 하나 (점검 대상). 생성기가 DoorSlot 자리에 만들 때 붙이고 Id도 채운다 (예: R21.Door.01).
	/// 정상 = 닫히고 잠김. E: 잠김 → 열쇠로 열기 / 열림 → 닫기 / 닫힘(안 잠김) → 잠그기
	/// </summary>
	public class Door : PatrolObject
	{
		[Tooltip("문이 달린 방 호수 (예: 521)")] public string roomLabel;
		public DoorStyle style;

		[Header("처음 상태 (ResetToDefault)")]
		public bool defaultLocked = true;
		public bool defaultOpen;

		[Header("지금 상태")]
		[SerializeField] bool locked = true;
		[SerializeField] bool open;

		[Header("열림 동작")]
		[Tooltip("다 열렸을 때 각도")] public float openAngle = 95f;
		[Tooltip("초당 회전 각도")] public float swingSpeed = 180f;
		[Tooltip("문짝 (경첩 위치가 기준점). 생성기가 채움")] public Transform[] leaves;
		[Tooltip("문짝마다 여는 방향 (+1 / -1). 생성기가 채움")] public float[] swingSigns;

		/// <summary>잠금·열림이 바뀔 때마다 (판정·소리 연결용)</summary>
		public event System.Action<Door> StateChanged;

		public bool IsLocked => locked;
		public bool IsOpen => open;

		// ── PatrolObject ──
		public override bool IsNormal => locked && !open;
		public override string Prompt => open ? "E - 문 닫기" : locked ? "E - 문 열기" : "E - 문 잠그기";

		public override void Interact(PlayerInteractor who)
		{
			if (open) Close();
			else if (locked) { SetLocked(false); TryOpen(); }
			else SetLocked(true);
		}

		public override void ResetToDefault() => SetStateImmediate(defaultLocked, defaultOpen);

		void Awake() => ResetToDefault();

		// ── 직접 조작 ──
		/// <summary>잠겨 있으면 false (열리지 않음)</summary>
		public bool TryOpen()
		{
			if (locked) return false;
			SetOpen(true);
			return true;
		}

		public void Close() => SetOpen(false);

		/// <summary>잠금. 열린 문은 먼저 닫힘</summary>
		public void SetLocked(bool value)
		{
			if (value) open = false;
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
