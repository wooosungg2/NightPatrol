using NightPatrol.Interaction;
using NightPatrol.Patrol;
using UnityEngine;

namespace NightPatrol
{
	/// <summary>
	/// 창문 한 칸의 아래 미서기창 (점검 대상, 예: R21.Window.03). 블라인드는 WindowBlind가 따로 맡음.
	/// 정상 = 닫히고 잠김. E: 잠김 → 열기 / 열림 → 닫기 / 닫힘(안 잠김) → 잠그기
	/// </summary>
	public class WindowUnit : PatrolObject
	{
		[Tooltip("창이 있는 방 호수")] public string roomLabel;

		[Header("처음 상태 (ResetToDefault)")]
		public bool defaultLocked = true;
		public bool defaultOpen;

		[Header("지금 상태")]
		[SerializeField] bool locked = true;
		[SerializeField] bool open;

		[Header("움직임 (생성기가 채움)")]
		[Tooltip("여닫는 창짝 (로컬 X로 미끄러짐)")] public Transform sash;
		public float sashClosedX, sashOpenX;
		[Tooltip("초당 이동(m)")] public float speed = 1.2f;

		public event System.Action<WindowUnit> StateChanged;

		public bool IsLocked => locked;
		public bool IsOpen => open;

		// ── PatrolObject ──
		public override bool IsNormal => locked && !open;
		public override string Prompt => open ? "E - 창문 닫기" : locked ? "E - 창문 열기" : "E - 창문 잠그기";

		public override void Interact(PlayerInteractor who)
		{
			if (open) Close();
			else if (locked) { SetLocked(false); TryOpen(); }
			else SetLocked(true);
		}

		public override void ResetToDefault() => SetStateImmediate(defaultLocked, defaultOpen);

		/// <summary>점검 항목으로 뽑혔을 때: 잠금이 풀리고 열린 채로 둠</summary>
		public override bool TrySetAbnormal()
		{
			if (sash == null) return false;
			SetStateImmediate(false, true);
			return true;
		}

		void Awake() => ResetToDefault();

		// ── 직접 조작 ──
		public bool TryOpen()
		{
			if (locked) return false;
			if (!open) { open = true; StateChanged?.Invoke(this); }
			return true;
		}

		public void Close()
		{
			if (!open) return;
			open = false;
			StateChanged?.Invoke(this);
		}

		/// <summary>잠금. 열린 창은 먼저 닫힘</summary>
		public void SetLocked(bool value)
		{
			if (value) open = false;
			locked = value;
			StateChanged?.Invoke(this);
		}

		public void SetStateImmediate(bool isLocked, bool isOpen)
		{
			locked = isLocked;
			open = !isLocked && isOpen;
			Apply(true);
			StateChanged?.Invoke(this);
		}

		void Apply(bool instant)
		{
			if (sash == null) return;
			var p = sash.localPosition;
			p.x = Mathf.MoveTowards(p.x, open ? sashOpenX : sashClosedX, instant ? float.MaxValue : speed * Time.deltaTime);
			sash.localPosition = p;
		}

		void Update() => Apply(false);

		void OnValidate()
		{
			if (!Application.isPlaying) Apply(true);
		}
	}
}
