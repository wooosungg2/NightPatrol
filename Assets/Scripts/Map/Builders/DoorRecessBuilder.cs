using System.Collections.Generic;
using UnityEngine;

namespace NightPatrol
{
	/// <summary>
	/// recess만큼 뒤로 물린 방 앞의 띠(복도 벽선 ~ 방 앞벽)를 두꺼운 벽으로 채우고,
	/// 방 프리팹의 DoorOpening 위치만 비워서 오목한 문 자리를 만든다.
	/// 방 프리팹 자체는 손대지 않는다.
	/// </summary>
	public static class DoorRecessBuilder
	{
		const float MinSize = 0.01f;

		/// <param name="room">배치·스케일까지 끝난 방 인스턴스</param>
		/// <param name="bandWidth">슬롯 폭(m)</param>
		/// <param name="recess">오목한 칸 깊이(m)</param>
		/// <param name="parent">층 루트 (Clear Floor 때 같이 지워지도록)</param>
		/// <param name="wallHeight">바닥~천장 높이(m)</param>
		public static void Build(
			GameObject room, float bandWidth, float recess, Transform parent,
			float wallHeight, Material wallMat, Material floorMat, Material ceilingMat,
			string label, float slabThickness = 0.1f, IList<Vector2> openingsOverride = null)
		{
			if (room == null || recess <= MinSize) return;

			// 방과 같은 위치·회전, 스케일 1인 기준 프레임.
			// 이 프레임에서 방은 +Z로 뻗고, 복도 벽선은 z = -recess.
			var frame = new GameObject($"Recess_{label}").transform;
			frame.SetParent(parent, false);
			frame.SetPositionAndRotation(room.transform.position, room.transform.rotation);
			frame.localScale = Vector3.one;
			frame.gameObject.isStatic = true;

			float half = bandWidth * 0.5f;

			// 문 구멍 구간 (프레임 기준 x 범위). 방이 늘어나거나 mirror돼도 월드 위치로 계산.
			var openings = new List<Vector2>();
			if (openingsOverride != null) // 방 로컬 X 구간 (frame과 같은 기준)
			{
				foreach (var o in openingsOverride)
				{
					float a = Mathf.Max(o.x, -half), b = Mathf.Min(o.y, half);
					if (b - a > MinSize) openings.Add(new Vector2(a, b));
				}
			}
			else foreach (var d in room.GetComponentsInChildren<DoorOpening>(true))
			{
				float cx = frame.InverseTransformPoint(d.transform.position).x;
				float w = d.width * Mathf.Abs(d.transform.lossyScale.x);
				float a = Mathf.Max(cx - w * 0.5f, -half);
				float b = Mathf.Min(cx + w * 0.5f, half);
				if (b - a > MinSize) openings.Add(new Vector2(a, b));
			}
			openings.Sort((p, q) => p.x.CompareTo(q.x));

			if (openings.Count == 0)
				Debug.LogWarning($"[DoorRecess] {label}: DoorOpening이 없어 띠 전체를 벽으로 막습니다.", room);

			float zCenter = -recess * 0.5f;
			float cursor = -half;

			foreach (var o in openings)
			{
				// 문 앞까지 막힌 벽
				AddWall(frame, cursor, o.x, zCenter, recess, wallHeight, wallMat);
				// 오목한 칸: 바닥 + 천장 (옆벽은 양쪽 벽 덩어리 끝면이 대신함)
				float w = o.y - Mathf.Max(o.x, cursor);
				float cx = (Mathf.Max(o.x, cursor) + o.y) * 0.5f;
				if (w > MinSize)
				{
					AddBox(frame, "Recess_Floor",
						new Vector3(cx, -slabThickness * 0.5f, zCenter),
						new Vector3(w, slabThickness, recess), floorMat);
					AddBox(frame, "Recess_Ceiling",
						new Vector3(cx, wallHeight + slabThickness * 0.5f, zCenter),
						new Vector3(w, slabThickness, recess), ceilingMat);
				}
				cursor = Mathf.Max(cursor, o.y);
			}
			AddWall(frame, cursor, half, zCenter, recess, wallHeight, wallMat);
		}

		static void AddWall(Transform frame, float a, float b, float zCenter, float depth, float h, Material mat)
		{
			if (b - a <= MinSize) return;
			AddBox(frame, "Recess_Wall",
				new Vector3((a + b) * 0.5f, h * 0.5f, zCenter),
				new Vector3(b - a, h, depth), mat);
		}

		static void AddBox(Transform frame, string name, Vector3 localCenter, Vector3 size, Material mat)
		{
			var go = GameObject.CreatePrimitive(PrimitiveType.Cube); // BoxCollider 포함
			go.name = name;
			go.isStatic = true;
			go.transform.SetParent(frame, false);
			go.transform.localPosition = localCenter;
			go.transform.localRotation = Quaternion.identity;
			go.transform.localScale = size;
			if (mat != null) go.GetComponent<MeshRenderer>().sharedMaterial = mat;
		}
	}
}