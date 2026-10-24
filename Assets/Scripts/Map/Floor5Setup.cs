#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NightPatrol
{
	/// <summary>
	/// 공용 씬(Floor5)에 시스템을 놓는 메뉴 (씬 담당용). 양우성 님 시스템 가이드 "1. 씬에 놓기" 순서를 그대로 함.
	/// 1) 예전에 복사해 둔 Player·Main Camera를 지우고 Player 프리팹을 같은 자리에 놓음
	/// 2) PatrolSystems 프리팹이 없으면 놓음
	/// 3) Elevator_Base 프리팹: 안쪽 버튼판(CarPanel_Main)에 Elevator + 콜라이더, 문짝 Static 해제
	/// </summary>
	public static class Floor5Setup
	{
		const string PlayerPath = "Assets/Prefabs/Player.prefab";
		const string SystemsPath = "Assets/Prefabs/PatrolSystems.prefab";
		const string ElevatorPath = "Assets/Prefabs/Elevator_Base.prefab";

		[MenuItem("Night Patrol/Floor5 배치 (씬 담당)", priority = 50)]
		static void Setup()
		{
			if (Application.isPlaying) { EditorUtility.DisplayDialog("Floor5 배치", "플레이 중에는 할 수 없습니다. 정지한 뒤 다시 눌러 주세요.", "확인"); return; }
			var log = new System.Text.StringBuilder();

			// 1) 플레이어
			var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath);
			Vector3 pos = Vector3.zero; Quaternion rot = Quaternion.identity; bool hadPlayer = false;
			foreach (var cc in Object.FindObjectsByType<CharacterController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
			{
				var go = cc.gameObject;
				if (PrefabUtility.GetCorrespondingObjectFromSource(go) == playerPrefab) { log.AppendLine("Player 프리팹이 이미 있음"); hadPlayer = true; continue; }
				pos = go.transform.position; rot = go.transform.rotation;
				Undo.DestroyObjectImmediate(go);
				log.AppendLine("예전 Player 삭제 (자리 기억)");
			}
			var mainCam = GameObject.Find("Main Camera");
			if (mainCam == null)
				foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
					if (cam.gameObject.name == "Main Camera") mainCam = cam.gameObject;
			if (mainCam != null && mainCam.transform.root.GetComponentInChildren<CharacterController>() == null)
			{
				Undo.DestroyObjectImmediate(mainCam);
				log.AppendLine("Main Camera 삭제");
			}
			if (!hadPlayer)
			{
				if (playerPrefab == null) log.AppendLine("Player 프리팹을 찾을 수 없음: " + PlayerPath);
				else
				{
					var p = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
					p.transform.SetPositionAndRotation(pos, rot);
					Undo.RegisterCreatedObjectUndo(p, "Floor5 배치");
					log.AppendLine($"Player 프리팹 놓음 ({pos.x:0.0}, {pos.y:0.0}, {pos.z:0.0})");
				}
			}

			// 2) 층 진행
			if (Object.FindAnyObjectByType<NightPatrol.Run.PatrolRun>(FindObjectsInactive.Include) != null)
				log.AppendLine("PatrolSystems(PatrolRun)가 이미 있음");
			else
			{
				var sys = AssetDatabase.LoadAssetAtPath<GameObject>(SystemsPath);
				if (sys == null) log.AppendLine("PatrolSystems 프리팹을 찾을 수 없음: " + SystemsPath);
				else
				{
					var s = (GameObject)PrefabUtility.InstantiatePrefab(sys);
					Undo.RegisterCreatedObjectUndo(s, "Floor5 배치");
					log.AppendLine("PatrolSystems 놓음");
				}
			}

			// 3) 엘리베이터 프리팹
			if (AssetDatabase.LoadAssetAtPath<GameObject>(ElevatorPath) == null) log.AppendLine("Elevator_Base 프리팹을 찾을 수 없음: " + ElevatorPath);
			else
			{
				var root = PrefabUtility.LoadPrefabContents(ElevatorPath);
				try
				{
					if (root.GetComponent<ElevatorDoors>() == null) { root.AddComponent<ElevatorDoors>(); log.AppendLine("Elevator_Base에 ElevatorDoors 추가"); }
					var panel = Find(root.transform, "CarPanel_Main");
					if (panel == null) log.AppendLine("Elevator_Base 안에 CarPanel_Main이 없음 (Elevator는 직접 붙여 주세요)");
					else
					{
						if (panel.GetComponent<NightPatrol.Run.Elevator>() == null) { panel.gameObject.AddComponent<NightPatrol.Run.Elevator>(); log.AppendLine("CarPanel_Main에 Elevator 추가"); }
						if (panel.GetComponent<Collider>() == null) { panel.gameObject.AddComponent<BoxCollider>(); log.AppendLine("CarPanel_Main에 BoxCollider 추가"); }
					}
					foreach (var n in new[] { "Door_Left", "Door_Right" })
					{
						var d = Find(root.transform, n);
						if (d != null && d.gameObject.isStatic) { d.gameObject.isStatic = false; log.AppendLine($"{n} Static 해제"); }
					}
					PrefabUtility.SaveAsPrefabAsset(root, ElevatorPath);
				}
				finally { PrefabUtility.UnloadPrefabContents(root); }
			}

			EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
			Debug.Log("[Floor5 배치]\n" + log);
			EditorUtility.DisplayDialog("Floor5 배치", log + "\nCtrl+S로 씬을 저장하고, Night Patrol → 씬 점검으로 확인하세요.", "확인");
		}

		// 엘리베이터 프리팹에 재질 넣기 (ProBuilder 격자 무늬 대신). Assets/Materials의 재질을 이름으로 찾음
		[MenuItem("Night Patrol/엘리베이터 재질 넣기 (씬 담당)", priority = 51)]
		static void ElevatorMaterials()
		{
			if (Application.isPlaying) { EditorUtility.DisplayDialog("엘리베이터 재질", "플레이 중에는 할 수 없습니다.", "확인"); return; }
			Material M(string n)
			{
				foreach (var g in AssetDatabase.FindAssets(n + " t:Material", new[] { "Assets/Materials" }))
				{
					var p = AssetDatabase.GUIDToAssetPath(g);
					if (System.IO.Path.GetFileNameWithoutExtension(p) == n) return AssetDatabase.LoadAssetAtPath<Material>(p);
				}
				return null;
			}
			var steel = M("M_Stainless"); var floor = M("M_Granite") ?? M("M_CorridorFloor"); var ceil = M("M_Wall"); var glass = M("M_Glass");
			var map = new System.Collections.Generic.Dictionary<string, Material>
			{
				{ "Wall_Back", steel }, { "Wall_Left", steel }, { "Wall_Right", steel }, { "Wall_Front", steel },
				{ "Door_Left", steel }, { "Door_Right", steel }, { "CallPanel", steel }, { "CarPanel_Main", steel }, { "CarPanel_Sub", steel },
				{ "Floor", floor }, { "Ceiling", ceil }, { "Mirror_Back", glass }, { "FloorSign", ceil },
			};
			var log = new System.Text.StringBuilder();
			var root = PrefabUtility.LoadPrefabContents(ElevatorPath);
			try
			{
				foreach (var kv in map)
				{
					var t = Find(root.transform, kv.Key);
					if (t == null) continue;
					var r = t.GetComponent<Renderer>();
					if (r == null) continue;
					if (kv.Value == null) { log.AppendLine($"{kv.Key}: 재질을 못 찾음 (Assets/Materials)"); continue; }
					var mats = r.sharedMaterials;
					for (int i = 0; i < mats.Length; i++) mats[i] = kv.Value;
					r.sharedMaterials = mats;
					log.AppendLine($"{kv.Key} → {kv.Value.name}");
				}
				PrefabUtility.SaveAsPrefabAsset(root, ElevatorPath);
			}
			finally { PrefabUtility.UnloadPrefabContents(root); }
			Debug.Log("[엘리베이터 재질]\n" + log);
			EditorUtility.DisplayDialog("엘리베이터 재질", log.ToString(), "확인");
		}

		static Transform Find(Transform t, string name)
		{
			foreach (Transform c in t)
			{
				if (c.name == name) return c;
				var r = Find(c, name);
				if (r != null) return r;
			}
			return null;
		}
	}
}
#endif
