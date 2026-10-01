using UnityEngine;

namespace NightPatrol
{
	/// <summary>
	/// 창문 한 칸의 상태 (미서기창 + 롤 블라인드). 생성기가 강의실 바깥벽에 만들 때 붙인다.
	/// ※ 초안: Door와 같은 방식. 상호작용은 TryOpen / Close / SetLocked / SetBlind, 판정은 IsLocked·IsOpen·IsBlindDown.
	/// </summary>
	public class WindowUnit : MonoBehaviour
	{
		[Tooltip("창이 있는 방 호수")] public string roomLabel;
		[Tooltip("근무일지 점검 대상인지")] public bool isInspectionTarget = true;

		[Header("상태")]
		[SerializeField] bool locked = true;
		[SerializeField] bool open;
		[SerializeField] bool blindDown;

		[Header("움직임 (생성기가 채움)")]
		[Tooltip("여닫는 창짝 (로컬 X로 미끄러짐)")] public Transform sash;
		public float sashClosedX, sashOpenX;
		[Tooltip("블라인드 천 (로컬 Y 스케일로 내려옴)")] public Transform blind;
		public float blindUpHeight = 0.05f, blindDownHeight = 1.4f;
		[Tooltip("초당 이동(m)")] public float speed = 1.2f;

		public event System.Action<WindowUnit> StateChanged;

		public bool IsLocked => locked;
		public bool IsOpen => open;
		public bool IsBlindDown => blindDown;

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

		public bool Toggle() => open ? CloseAndTrue() : TryOpen();
		bool CloseAndTrue() { Close(); return true; }

		/// <summary>잠금. 열린 창은 먼저 닫힘</summary>
		public void SetLocked(bool value)
		{
			if (value) open = false;
			locked = value;
			StateChanged?.Invoke(this);
		}

		public void SetBlind(bool down)
		{
			if (blindDown == down) return;
			blindDown = down;
			StateChanged?.Invoke(this);
		}

		public void ToggleBlind() => SetBlind(!blindDown);

		/// <summary>움직임 없이 바로 (층 재시작용)</summary>
		public void SetStateImmediate(bool isLocked, bool isOpen, bool isBlindDown)
		{
			locked = isLocked;
			open = !isLocked && isOpen;
			blindDown = isBlindDown;
			Apply(true);
			StateChanged?.Invoke(this);
		}

		void Apply(bool instant)
		{
			float step = instant ? float.MaxValue : speed * Time.deltaTime;
			if (sash != null)
			{
				var p = sash.localPosition;
				p.x = Mathf.MoveTowards(p.x, open ? sashOpenX : sashClosedX, step);
				sash.localPosition = p;
			}
			if (blind != null)
			{
				// 천의 윗변은 고정, 아래로 늘어남 (기준점이 윗변)
				var sc = blind.localScale;
				sc.y = Mathf.MoveTowards(sc.y, blindDown ? blindDownHeight : blindUpHeight, step);
				blind.localScale = sc;
			}
		}

		void Update() => Apply(false);

		void OnValidate()
		{
			if (!Application.isPlaying) Apply(true);
		}
	}
}
