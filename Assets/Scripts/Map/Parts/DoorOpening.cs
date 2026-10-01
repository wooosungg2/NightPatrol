using UnityEngine;

namespace NightPatrol
{
	/// <summary>
	/// 방 프리팹 안의 문 자리 표시.
	/// 문 구멍 중심, 바닥 높이(로컬 y=0), 복도 쪽 벽(로컬 z=0)에 빈 오브젝트로 두고 붙인다.
	/// Rotation 0,0,0 / Scale 1,1,1 유지.
	/// </summary>
	public class DoorOpening : MonoBehaviour
	{
		[Tooltip("문 구멍 폭(m). 프리팹 스케일 1 기준")]
		public float width = 1.0f;

		[Tooltip("기즈모 표시용 높이(m)")]
		public float height = 2.25f;

		void OnDrawGizmos()
		{
			if (!enabled) return; // 도면 데이터로 문 위치를 정한 방은 생성기가 끔
			Gizmos.color = Color.cyan;
			Gizmos.matrix = transform.localToWorldMatrix;
			Gizmos.DrawWireCube(new Vector3(0f, height * 0.5f, 0f), new Vector3(width, height, 0.05f));
		}
	}
}