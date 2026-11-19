using NightPatrol.Player;
using NightPatrol.Run;
using UnityEngine;

namespace NightPatrol
{
	/// <summary>
	/// 1층 출구로 나가면 퇴근. 생성기가 출구 프레임(문 가운데, 로컬 +Z = 바깥)에 붙이고 판정선을 채운다.
	/// 1층에 도착(클리어)한 뒤 플레이어가 유리문 밖으로 판정선을 넘으면 FloorTransition.EndShift()
	/// (기존처럼 엘리베이터를 다시 타도 퇴근됨 — 둘 중 먼저 한 쪽).
	/// 이 오브젝트는 1층에서만 켜지므로 다른 층에서는 동작하지 않는다.
	/// </summary>
	[DisallowMultipleComponent]
	public sealed class GroundExit : MonoBehaviour
	{
		[Tooltip("이 오브젝트 기준 z. 플레이어가 이보다 바깥(+Z)으로 나가면 퇴근")] [SerializeField] private float exitZ = 1.4f;
		[Tooltip("판정하는 좌우 폭의 절반 (로컬 X)")] [SerializeField] private float halfWidth = 2f;

		PatrolRun run;
		FloorTransition transition;
		Transform player;
		bool done;

		public void Setup(float z, float half)
		{
			exitZ = z; halfWidth = Mathf.Abs(half);
		}

		void OnEnable()
		{
			done = false;
			if (run == null) run = FindAnyObjectByType<PatrolRun>();
			if (transition == null) transition = FindAnyObjectByType<FloorTransition>();
			if (player == null)
			{
				var pc = FindAnyObjectByType<PlayerControl>();
				if (pc != null) player = pc.transform;
			}
		}

		void Update()
		{
			if (done || run == null || !run.IsCleared || player == null || transition == null) return;
			Vector3 p = transform.InverseTransformPoint(player.position);
			if (p.z < exitZ || Mathf.Abs(p.x) > halfWidth) return;
			if (transition.IsBusy) return;
			done = true;
			Debug.Log("[GroundExit] 1층 출구로 퇴근", this);
			transition.EndShift();
		}

		void OnDrawGizmosSelected()
		{
			Gizmos.matrix = transform.localToWorldMatrix;
			Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.8f);
			Gizmos.DrawLine(new Vector3(-halfWidth, 0.05f, exitZ), new Vector3(halfWidth, 0.05f, exitZ));
			Gizmos.DrawLine(new Vector3(-halfWidth, 2f, exitZ), new Vector3(halfWidth, 2f, exitZ));
		}
	}
}
