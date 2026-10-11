using NightPatrol.Interaction;
using NightPatrol.Patrol;
using UnityEngine;

namespace NightPatrol
{
	/// <summary>
	/// 롤 블라인드 하나 (점검 대상, 예: R21.Blind.01). 이 오브젝트의 기준점이 블라인드 윗변이고,
	/// 로컬 Y 스케일만큼 천이 아래로 내려온다. E: 올리기 ↔ 내리기
	/// </summary>
	public class WindowBlind : PatrolObject
	{
		[Tooltip("창이 있는 방 호수")] public string roomLabel;
		[Tooltip("규정상 내려 있어야 정상인지")] public bool normalDown = true;
		[Tooltip("처음 상태 (ResetToDefault)")] public bool defaultDown = true;
		[SerializeField] bool down = true;

		[Header("높이 (생성기가 채움)")]
		public float upHeight = 0.04f, downHeight = 1.3f;
		[Tooltip("초당 이동(m)")] public float speed = 1.2f;

		public event System.Action<WindowBlind> StateChanged;

		public bool IsDown => down;

		// ── PatrolObject ──
		public override bool IsNormal => down == normalDown;
		public override string Prompt => down ? "E - 블라인드 올리기" : "E - 블라인드 내리기";
		public override void Interact(PlayerInteractor who) => SetDown(!down);
		public override void ResetToDefault() => SetStateImmediate(defaultDown);

		/// <summary>점검 항목으로 뽑혔을 때: 규정과 반대 상태 (기본은 올라가 있음)</summary>
		public override bool TrySetAbnormal()
		{
			SetStateImmediate(!normalDown);
			return true;
		}

		void Awake() => ResetToDefault();

		public void SetDown(bool value)
		{
			if (down == value) return;
			down = value;
			StateChanged?.Invoke(this);
		}

		public void SetStateImmediate(bool isDown)
		{
			down = isDown;
			Apply(true);
			StateChanged?.Invoke(this);
		}

		void Apply(bool instant)
		{
			var sc = transform.localScale;
			sc.y = Mathf.MoveTowards(sc.y, down ? downHeight : upHeight, instant ? float.MaxValue : speed * Time.deltaTime);
			transform.localScale = sc;
		}

		void Update() => Apply(false);

		void OnValidate()
		{
			if (!Application.isPlaying) Apply(true);
		}
	}
}
