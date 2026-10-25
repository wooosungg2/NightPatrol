using System;
using UnityEngine;

namespace NightPatrol
{
	/// <summary>
	/// 엘리베이터 문 여닫기 + 층 표시. Elevator_Base 프리팹 루트에 붙인다 (자식 이름으로 문짝·표시판을 찾음).
	/// 순서(닫힘 → 하행 판정 → 열림)는 Elevator 쪽에서 Open / Close / SetFloor를 불러서 정함.
	/// 층이 시작될 때(PatrolRun.FloorStarted) 층 표시는 자동으로 바뀜.
	/// </summary>
	[DisallowMultipleComponent]
	public sealed class ElevatorDoors : MonoBehaviour
	{
		[Header("문짝 (비워두면 자식 이름으로 찾음)")]
		[SerializeField] private Transform doorLeft;
		[SerializeField] private Transform doorRight;
		[Tooltip("문짝 하나가 옆으로 밀리는 거리(m). 0이면 문짝 폭만큼")] public float slideDistance = 0f;
		[Tooltip("다 열리거나 닫히는 데 걸리는 시간(초)")] public float moveSeconds = 1.2f;
		[Tooltip("시작할 때 열려 있음 (하행 흐름을 연결하기 전 테스트용)")] public bool startOpen = true;

		[Header("층 표시 (비워두면 FloorSign 자식에 글자를 만듦)")]
		[SerializeField] private TMPro.TMP_Text floorText;
		[Tooltip("엘리베이터 안 버튼판 위에도 층 표시")] public bool insideSign = true;
		[SerializeField] private TMPro.TMP_Text insideText;

		/// <summary>0 = 닫힘, 1 = 열림</summary>
		public float OpenAmount { get; private set; }
		public bool IsOpen => OpenAmount >= 0.999f;
		public bool IsClosed => OpenAmount <= 0.001f;
		public bool IsMoving => !Mathf.Approximately(OpenAmount, target);
		public int Floor { get; private set; } = 5;

		public event Action Opened;
		public event Action Closed;

		float target;
		Vector3 leftClosed, rightClosed;
		float leftSlide, rightSlide;
		NightPatrol.Run.PatrolRun run;

		void Awake()
		{
			if (doorLeft == null) doorLeft = FindChild(transform, "Door_Left");
			if (doorRight == null) doorRight = FindChild(transform, "Door_Right");
			if (doorLeft != null) { leftClosed = doorLeft.localPosition; leftSlide = SlideOf(doorLeft); }
			if (doorRight != null) { rightClosed = doorRight.localPosition; rightSlide = SlideOf(doorRight); }

			if (floorText == null) floorText = MakeSign(FindChild(transform, "FloorSign"), false);
			if (insideSign && insideText == null) insideText = MakeSign(FindChild(transform, "CarPanel_Main"), true);

			target = OpenAmount = startOpen ? 1f : 0f;
			Pose();
			SetFloor(Floor);
		}

		void OnEnable()
		{
			run = FindAnyObjectByType<NightPatrol.Run.PatrolRun>();
			if (run != null) { run.FloorStarted += SetFloor; run.Cleared += OnCleared; }
		}

		// 마지막 층(1층) 도착 = 클리어. 이때는 FloorStarted가 없어서 따로 층 표시를 바꿈
		void OnCleared()
		{
			if (run != null) SetFloor(run.CurrentFloor);
		}

		void OnDisable()
		{
			if (run != null) { run.FloorStarted -= SetFloor; run.Cleared -= OnCleared; }
		}

		public void Open() => target = 1f;
		public void Close() => target = 0f;

		/// <summary>애니메이션 없이 바로 (층 재시작 등)</summary>
		public void SetOpenImmediate(bool open)
		{
			target = OpenAmount = open ? 1f : 0f;
			Pose();
		}

		public void SetFloor(int floor)
		{
			Floor = floor;
			string t = floor.ToString();
			if (floorText != null) floorText.text = t;
			if (insideText != null) insideText.text = t;
		}

		void Update()
		{
			if (Mathf.Approximately(OpenAmount, target)) return;
			bool wasOpen = IsOpen, wasClosed = IsClosed;
			OpenAmount = Mathf.MoveTowards(OpenAmount, target, Time.deltaTime / Mathf.Max(0.05f, moveSeconds));
			Pose();
			if (IsOpen && !wasOpen) Opened?.Invoke();
			if (IsClosed && !wasClosed) Closed?.Invoke();
		}

		void Pose()
		{
			float e = Mathf.SmoothStep(0f, 1f, OpenAmount);
			if (doorLeft != null) doorLeft.localPosition = leftClosed + Vector3.left * leftSlide * e;
			if (doorRight != null) doorRight.localPosition = rightClosed + Vector3.right * rightSlide * e;
		}

		float SlideOf(Transform door)
		{
			if (slideDistance > 0.01f) return slideDistance;
			var r = door.GetComponent<Renderer>();
			if (r == null) return 0.45f;
			// 문짝 폭 (부모 기준 X 크기)
			Vector3 size = transform.InverseTransformVector(r.bounds.size);
			return Mathf.Max(0.2f, Mathf.Abs(size.x) * 0.95f);
		}

		static TMPro.TMP_Text MakeSign(Transform at, bool inside)
		{
			if (at == null) return null;
			var go = new GameObject("FloorNumber");
			go.transform.SetParent(at, false);
			// 바깥 표시판은 복도(-Z)를 봄, 안쪽은 엘리베이터 안(+Z)을 봄
			go.transform.localPosition = inside ? new Vector3(0f, 0.55f, 0.02f) : new Vector3(0f, 0f, -0.02f);
			go.transform.localRotation = inside ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity;
			var tmp = go.AddComponent<TMPro.TextMeshPro>();
			tmp.alignment = TMPro.TextAlignmentOptions.Center;
			tmp.enableAutoSizing = true;
			tmp.fontSizeMin = 0.1f;
			tmp.fontSizeMax = 3f;
			tmp.color = new Color(1f, 0.35f, 0.2f);   // 주황빨강 숫자
			tmp.rectTransform.sizeDelta = new Vector2(0.18f, 0.14f);
			return tmp;
		}

		static Transform FindChild(Transform t, string name)
		{
			foreach (Transform c in t)
			{
				if (c.name == name) return c;
				var r = FindChild(c, name);
				if (r != null) return r;
			}
			return null;
		}
	}
}
