using UnityEngine;
using UnityEngine.InputSystem;

namespace NightPatrol
{
	/// <summary>
	/// 문 동작 확인용 임시 스크립트 (상호작용 시스템이 들어오면 지울 것).
	/// 카메라(또는 플레이어)에 붙이고 Play → 문을 바라보고 E: 열기/닫기, L: 잠금/해제.
	/// </summary>
	public class DoorInteractTest : MonoBehaviour
	{
		[Tooltip("비워두면 Main Camera")] public Camera cam;
		[Tooltip("문까지 닿는 거리(m)")] public float reach = 3f;

		// 바라보는 문. 열린 양문은 가운데가 비어서 광선이 통과하므로, 못 찾으면 앞쪽 가까운 문을 고름
		Door FindDoor(Transform eye)
		{
			if (Physics.Raycast(eye.position, eye.forward, out var hit, reach))
			{
				var d = hit.collider.GetComponentInParent<Door>();
				if (d != null) return d;
			}
			Door best = null;
			float bestDist = reach;
			foreach (var d in FindObjectsByType<Door>(FindObjectsSortMode.None))
			{
				Vector3 to = d.transform.position + Vector3.up - eye.position; // 문 구멍 가운데 높이 1m
				float dist = to.magnitude;
				if (dist > bestDist || Vector3.Dot(to / dist, eye.forward) < 0.6f) continue;
				best = d; bestDist = dist;
			}
			return best;
		}

		void Update()
		{
			var kb = Keyboard.current;
			if (kb == null) return;
			bool use = kb.eKey.wasPressedThisFrame, lockKey = kb.lKey.wasPressedThisFrame;
			if (!use && !lockKey) return;

			var c = cam != null ? cam : Camera.main;
			if (c == null) return;
			var door = FindDoor(c.transform);
			if (door == null) return;

			if (lockKey)
			{
				door.SetLocked(!door.IsLocked);
				Debug.Log($"[DoorTest] {door.roomLabel}: {(door.IsLocked ? "잠금" : "잠금 해제")}", door);
			}
			else if (!door.Toggle())
				Debug.Log($"[DoorTest] {door.roomLabel}: 잠겨 있어서 안 열림 (L로 해제)", door);
			else
				Debug.Log($"[DoorTest] {door.roomLabel}: {(door.IsOpen ? "열림" : "닫힘")} (문짝 {door.leaves?.Length ?? 0}개)", door);
		}
	}
}