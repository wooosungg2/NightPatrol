using System.Collections;
using NightPatrol.Player;
using NightPatrol.Run;
using UnityEngine;

namespace NightPatrol
{
	/// <summary>
	/// 1층 엔딩 연출: 2층에서 판정을 통과해 1층에 도착하면(클리어) 조작을 잠그고
	/// 엘리베이터에서 나와 → 동쪽으로 돌아 → 유리문을 열고 → 밖으로 걸어 나간 뒤 FloorTransition.EndShift() ("근무 종료").
	/// 1층 구간(GroundFloor_1F)에 붙고 생성기가 경로·문을 채운다. 1층에서만 켜지므로 다른 층에서는 동작하지 않음.
	/// 경로는 층 루트 기준 좌표 (이 오브젝트가 층 루트 원점에 있음).
	/// </summary>
	[DisallowMultipleComponent]
	public sealed class EndingWalk : MonoBehaviour
	{
		[Tooltip("층 루트 기준 경유점 (엘리베이터 앞 → 출구 앞 → 바깥)")] [SerializeField] private Vector3[] path;
		[Tooltip("열고 지나갈 출구 문")] [SerializeField] private Door exitDoor;
		[Tooltip("이 거리 안으로 들어오면 문을 엶(m)")] [SerializeField] private float openDistance = 2.5f;
		[SerializeField] private float walkSpeed = 1.2f;
		[Tooltip("방향을 트는 빠르기 (도/초)")] [SerializeField] private float turnSpeed = 120f;
		[Tooltip("문이 열린 뒤·나가기 전 잠깐 멈춤(초)")] [SerializeField] private float pause = 0.6f;
		[Tooltip("시선 아래로 (도)")] [SerializeField] private float lookDown = 4f;

		PatrolRun run;
		FloorTransition transition;
		Coroutine routine;

		public void Setup(Vector3[] points, Door door, float speed)
		{
			path = points; exitDoor = door; walkSpeed = Mathf.Max(0.3f, speed);
		}

		void OnEnable()
		{
			if (run == null) run = FindAnyObjectByType<PatrolRun>();
			if (transition == null) transition = FindAnyObjectByType<FloorTransition>();
			if (run != null && run.IsCleared && path != null && path.Length > 0) routine = StartCoroutine(Play());
		}

		void OnDisable()
		{
			if (routine != null) StopCoroutine(routine);
			routine = null;
			var pc = PlayerControl.Current;
			if (pc != null) pc.Lock(this, false);
		}

		IEnumerator Play()
		{
			// 도착 연출(밝아짐·엘리베이터 문 열림)이 끝날 때까지
			while (transition != null && transition.IsBusy) yield return null;
			var pc = PlayerControl.Current;
			if (pc == null) yield break;
			pc.Lock(this, true);
			var cc = pc.GetComponent<CharacterController>();
			var look = pc.GetComponent<PlayerLook>();
			if (look != null) look.SetPitch(lookDown);
			yield return new WaitForSeconds(0.4f);

			bool opened = false;
			for (int i = 0; i < path.Length; i++)
			{
				Vector3 target = transform.TransformPoint(path[i]);
				while (true)
				{
					Vector3 pos = pc.transform.position;
					Vector3 to = target - pos; to.y = 0f;
					float dist = to.magnitude;
					if (dist < 0.08f) break;
					// 방향: 목표 쪽으로 천천히 돌고, 많이 틀어져 있으면 제자리에서 돌기만
					Quaternion want = Quaternion.LookRotation(to / dist, Vector3.up);
					pc.transform.rotation = Quaternion.RotateTowards(pc.transform.rotation, want, turnSpeed * Time.deltaTime);
					float angle = Quaternion.Angle(pc.transform.rotation, want);
					float speed = angle > 45f ? 0f : walkSpeed * (1f - angle / 90f);
					Vector3 step = to / dist * Mathf.Min(dist, speed * Time.deltaTime) + Vector3.down * 2f * Time.deltaTime;
					if (cc != null && cc.enabled) cc.Move(step); else pc.transform.position += new Vector3(step.x, 0f, step.z);

					if (!opened && exitDoor != null && Vector3.Distance(pc.transform.position, exitDoor.transform.position) < openDistance)
					{
						opened = true;
						exitDoor.SetLocked(false);
						exitDoor.TryOpen();
						yield return new WaitForSeconds(pause);
					}
					yield return null;
				}
			}
			yield return new WaitForSeconds(pause);
			if (transition != null && !transition.IsBusy) transition.EndShift();
			routine = null;
		}
	}
}
