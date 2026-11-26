using System.Collections;
using System.Collections.Generic;
using NightPatrol.Anomalies;
using NightPatrol.Player;
using UnityEngine;

namespace NightPatrol
{
	/// <summary>
	/// 평소엔 잠겨 있고 불 꺼진 강의실(503·504·527·528)에 이상현상이 생기면
	///  - 문을 열 수 있게 되고 (E - 문 열기)
	///  - 방 불이 깜빡이며 켜져서, 복도에서도 문 위 유리창(Transom)과 문 밑 틈 불빛으로 알아챌 수 있다.
	/// 이상현상이 사라지면(촬영·층 넘어감) 다시 '열쇠 없음'으로 돌아가고 불이 꺼진다. 플레이어가 방 안에 있으면 나갈 때까지 불은 켜 둠.
	///
	/// 사용법: FloorGenerator_5F에 붙이고 Play. 방 번호는 Rooms에서 바꿀 수 있다.
	/// 이상현상은 각 방 오브젝트(예: FloorGenerator_5F/503) 아래에서 자동으로 찾고, 다른 곳에 있으면 Extra Anomalies에 끌어다 놓는다.
	/// 문의 잠김/열림 상태는 건드리지 않고 '열 수 있는지'만 바꾸므로 점검 판정(문 잠그기 항목)과 충돌하지 않는다.
	/// 조명·불빛은 Play 때 만들어지고 씬에 저장되지 않는다 (방 오브젝트 아래에 붙어서 1층처럼 방이 숨겨지는 층에서는 같이 숨겨짐).
	/// </summary>
	[DisallowMultipleComponent]
	public sealed class RoomAnomalyAccess : MonoBehaviour
	{
		[System.Serializable]
		public sealed class Room
		{
			[Tooltip("이 오브젝트 바로 아래의 방 이름 (예: 503)")]
			public string name;
			[Tooltip("방 아래에 없는 이상현상이 있으면 여기에")]
			public Anomaly[] extraAnomalies = new Anomaly[0];

			[System.NonSerialized] public Transform root;
			[System.NonSerialized] public readonly List<Anomaly> anomalies = new List<Anomaly>();
			[System.NonSerialized] public readonly List<Door> doors = new List<Door>();
			[System.NonSerialized] public readonly List<bool> doorCanUnlock = new List<bool>();
			[System.NonSerialized] public readonly List<Light> lights = new List<Light>();
			[System.NonSerialized] public readonly List<Renderer> transoms = new List<Renderer>();
			[System.NonSerialized] public readonly List<Material> transomOriginal = new List<Material>();
			[System.NonSerialized] public Bounds bounds;
			[System.NonSerialized] public bool lit, active;
			[System.NonSerialized] public Coroutine routine;
		}

		[SerializeField] private List<Room> rooms = new List<Room>
		{
			new Room { name = "503" }, new Room { name = "504" }, new Room { name = "527" }, new Room { name = "528" },
		};

		[Header("방 불")]
		[SerializeField] private Color lightColor = new Color(1f, 0.97f, 0.9f);
		[SerializeField, Min(0f)] private float lightIntensity = 1.6f;
		[Tooltip("천장 아래 몇 m")]
		[SerializeField, Min(0f)] private float lightDrop = 0.35f;
		[Tooltip("켜질 때 형광등처럼 깜빡이는 시간 (초)")]
		[SerializeField, Min(0f)] private float flickerSeconds = 0.6f;
		[Tooltip("벽 너머로 빛이 새지 않게 그림자를 켬")]
		[SerializeField] private bool shadows = true;

		[Header("복도에서 보이는 단서")]
		[Tooltip("문 위 유리창이 방 불빛에 밝아짐")]
		[SerializeField] private bool glowTransom = true;
		[SerializeField, Min(0f)] private float transomGlow = 1.2f;
		[Tooltip("문 밑 틈으로 새는 불빛")]
		[SerializeField] private bool underDoorLight = true;
		[SerializeField, Min(0f)] private float underDoorIntensity = 2.5f;

		static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
		readonly List<Material> owned = new List<Material>();
		Transform player;

		void Start()
		{
			foreach (var r in rooms) Setup(r);
		}

		void OnDestroy()
		{
			foreach (var r in rooms) RestoreTransoms(r);
			foreach (var m in owned) if (m != null) Destroy(m);
		}

		void Update()
		{
			if (player == null)
			{
				var pc = PlayerControl.Current;
				if (pc != null) player = pc.transform;
			}
			foreach (var r in rooms)
			{
				if (r.root == null || r.anomalies.Count == 0) continue;
				bool active = false;
				foreach (var a in r.anomalies)
					if (a != null && a.IsActive) { active = true; break; }

				if (active != r.active)
				{
					r.active = active;
					SetAccess(r, active);
				}
				// 불: 이상현상이 있으면 켜고, 없어지면 플레이어가 방 밖에 있을 때 끔
				bool wantLit = active || (r.lit && PlayerInside(r));
				if (wantLit != r.lit) SetLit(r, wantLit);
			}
		}

		// ── 준비 ──

		void Setup(Room r)
		{
			r.root = transform.Find(r.name);
			if (r.root == null)
			{
				Debug.LogWarning($"[RoomAnomalyAccess] '{r.name}' 방을 {name} 아래에서 찾지 못함", this);
				return;
			}
			foreach (var a in r.root.GetComponentsInChildren<Anomaly>(true)) if (!r.anomalies.Contains(a)) r.anomalies.Add(a);
			foreach (var a in r.extraAnomalies) if (a != null && !r.anomalies.Contains(a)) r.anomalies.Add(a);
			if (r.anomalies.Count == 0)
				Debug.Log($"[RoomAnomalyAccess] {r.name}호에 이상현상이 없음 — 강의실 이상현상이 들어오면 자동으로 연결됨", this);

			foreach (var d in r.root.GetComponentsInChildren<Door>(true))
			{
				r.doors.Add(d);
				r.doorCanUnlock.Add(d.playerCanUnlock);
				if (glowTransom)
				{
					var t = d.transform.Find("Transom");
					var tr = t != null ? t.GetComponent<Renderer>() : null;
					if (tr != null) { r.transoms.Add(tr); r.transomOriginal.Add(tr.sharedMaterial); }
				}
			}

			r.bounds = RoomBounds(r.root);
			BuildLights(r);
		}

		static Bounds RoomBounds(Transform root)
		{
			var floor = root.Find("Floor");
			var fr = floor != null ? floor.GetComponent<Renderer>() : null;
			Bounds b = fr != null ? fr.bounds : new Bounds(root.position, new Vector3(6f, 0.1f, 6f));
			var ceil = root.Find("Ceiling");
			var cr = ceil != null ? ceil.GetComponent<Renderer>() : null;
			float top = cr != null ? cr.bounds.min.y : b.max.y + 2.8f;
			b.SetMinMax(new Vector3(b.min.x, b.max.y, b.min.z), new Vector3(b.max.x, top, b.max.z));
			return b;
		}

		void BuildLights(Room r)
		{
			var b = r.bounds;
			// 넓이에 맞춰 1~4개 (긴 쪽으로 나란히)
			float lx = b.size.x, lz = b.size.z;
			int n = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(lx, lz) / 4f), 1, 4);
			float range = Mathf.Max(lx, lz) / n + Mathf.Min(lx, lz) * 0.6f;
			for (int i = 0; i < n; i++)
			{
				float k = (i + 0.5f) / n;
				Vector3 p = new Vector3(
					lx >= lz ? Mathf.Lerp(b.min.x, b.max.x, k) : b.center.x,
					b.max.y - lightDrop,
					lx >= lz ? b.center.z : Mathf.Lerp(b.min.z, b.max.z, k));
				var go = new GameObject($"RoomLight_{i} (runtime)");
				go.transform.SetParent(r.root, true);
				go.transform.position = p;
				var l = go.AddComponent<Light>();
				l.type = LightType.Point;
				l.color = lightColor;
				l.range = range;
				l.intensity = 0f;
				l.shadows = shadows ? LightShadows.Hard : LightShadows.None;
				l.enabled = false;
				r.lights.Add(l);
			}

			if (!underDoorLight) return;
			foreach (var d in r.doors)
			{
				// 방 안쪽 문 바로 앞 바닥에서 복도 쪽으로 낮게 비추는 빛 → 문 밑 틈으로 새는 것처럼 복도 바닥이 밝아짐
				Vector3 door = d.transform.position;
				Vector3 toCorridor = door - b.center; toCorridor.y = 0f;
				if (toCorridor.sqrMagnitude < 1e-4f) continue;
				toCorridor.Normalize();
				var go = new GameObject("UnderDoorLight (runtime)");
				go.transform.SetParent(r.root, true);
				go.transform.position = new Vector3(door.x, b.min.y + 0.04f, door.z) - toCorridor * 0.25f;
				go.transform.rotation = Quaternion.LookRotation(toCorridor + Vector3.down * 0.15f, Vector3.up);
				var l = go.AddComponent<Light>();
				l.type = LightType.Spot;
				l.color = lightColor;
				l.spotAngle = 150f;
				l.innerSpotAngle = 60f;
				l.range = 1.6f;
				l.intensity = 0f;
				l.shadows = LightShadows.None;
				l.enabled = false;
				l.name = "UnderDoor";
				r.lights.Add(l);
			}
		}

		// ── 바꾸기 ──

		void SetAccess(Room r, bool active)
		{
			for (int i = 0; i < r.doors.Count; i++)
			{
				if (r.doors[i] == null) continue;
				// 열 수 있는지만 바꿈 (잠김/열림 상태는 그대로)
				r.doors[i].playerCanUnlock = active || r.doorCanUnlock[i];
			}
		}

		void SetLit(Room r, bool on)
		{
			r.lit = on;
			if (r.routine != null) StopCoroutine(r.routine);
			r.routine = StartCoroutine(on ? TurnOn(r) : TurnOff(r));
		}

		IEnumerator TurnOn(Room r)
		{
			foreach (var l in r.lights) if (l != null) l.enabled = true;
			// 형광등이 켜질 때처럼 몇 번 깜빡
			float t = 0f;
			while (t < flickerSeconds)
			{
				bool blink = Random.value < 0.55f;
				Apply(r, blink ? Random.Range(0.2f, 0.9f) : 0f);
				float wait = Random.Range(0.04f, 0.12f);
				t += wait;
				yield return new WaitForSeconds(wait);
			}
			Apply(r, 1f);
			r.routine = null;
		}

		IEnumerator TurnOff(Room r)
		{
			Apply(r, 0f);
			foreach (var l in r.lights) if (l != null) l.enabled = false;
			RestoreTransoms(r);
			r.routine = null;
			yield break;
		}

		void Apply(Room r, float k)
		{
			foreach (var l in r.lights)
			{
				if (l == null) continue;
				l.intensity = (l.name == "UnderDoor" ? underDoorIntensity : lightIntensity) * k;
			}
			if (!glowTransom) return;
			for (int i = 0; i < r.transoms.Count; i++)
			{
				var tr = r.transoms[i];
				if (tr == null) continue;
				if (k <= 0f) { tr.sharedMaterial = r.transomOriginal[i]; continue; }
				var m = GlowMaterial(r.transomOriginal[i]);
				if (m == null) continue;
				m.SetColor(EmissionColor, lightColor * (transomGlow * k));
				tr.sharedMaterial = m;
			}
		}

		readonly Dictionary<Material, Material> glowCache = new Dictionary<Material, Material>();

		Material GlowMaterial(Material original)
		{
			if (original == null) return null;
			if (glowCache.TryGetValue(original, out var m) && m != null) return m;
			m = new Material(original) { name = original.name + " (lit room)" };
			m.EnableKeyword("_EMISSION");
			m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
			glowCache[original] = m;
			owned.Add(m);
			return m;
		}

		void RestoreTransoms(Room r)
		{
			for (int i = 0; i < r.transoms.Count; i++)
				if (r.transoms[i] != null) r.transoms[i].sharedMaterial = r.transomOriginal[i];
		}

		bool PlayerInside(Room r)
		{
			if (player == null) return false;
			var b = r.bounds;
			b.Expand(new Vector3(0.2f, 2f, 0.2f));
			return b.Contains(player.position);
		}
	}
}
