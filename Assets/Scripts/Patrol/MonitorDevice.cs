using NightPatrol.Interaction;
using NightPatrol.Patrol;
using UnityEngine;

namespace NightPatrol
{
	/// <summary>
	/// 모니터·TV (점검 대상, 예: R21.Monitor.03, R24.TV.01). 생성기가 강의실 모니터와 천장 TV마다 붙이고 Id도 채운다.
	/// 정상 = 꺼짐 (근무일지 규정: 점검 대상 전자기기가 꺼져 있을 것). E로 켜고 끔.
	/// 켜지면 화면이 스스로 빛나서 (Unlit) 소등한 방에서 위치가 드러남.
	///
	/// 이상현상 쪽: 화면 재질을 직접 바꾸지 말고 OverrideScreen(재질) / ClearScreenOverride()를 쓸 것
	/// (켜짐/꺼짐 표시와 섞이지 않게). 판정은 PatrolObject.AddAnomaly / RemoveAnomaly.
	/// </summary>
	public class MonitorDevice : PatrolObject
	{
		[Tooltip("방 호수 (예: 521)")] public string roomLabel;
		[Tooltip("Id 종류 이름 (Monitor / TV)")] public string kind = "Monitor";
		[Tooltip("화면 렌더러 (자식 'Screen')")] [SerializeField] private Renderer screen;

		[Header("처음 상태 (ResetToDefault)")]
		public bool defaultOn = false;
		[SerializeField] private bool isOn;

		[Header("화면")]
		[Tooltip("켜졌을 때 화면 색 (스스로 빛남)")] public Color onColor = new Color(0.35f, 0.55f, 0.95f);
		[Tooltip("켜졌을 때 쓸 재질. 비워두면 URP Unlit으로 만듦")] public Material onMaterial;

		public event System.Action<MonitorDevice> StateChanged;

		public bool IsOn => isOn;
		public Renderer ScreenRenderer => screen;

		Material offMaterial;          // 꺼졌을 때 = 원래 화면 재질
		MaterialPropertyBlock offBlock; // 원래 화면 색 (생성기가 칠한 값)
		Material overrideMaterial;     // 이상현상이 덮어쓴 재질
		static Material sharedUnlit;
		MaterialPropertyBlock mpb;

		// ── PatrolObject ──
		public override bool IsNormal => !isOn;
		public override string Prompt => isOn ? (kind == "TV" ? "E - TV 끄기" : "E - 모니터 끄기")
											   : (kind == "TV" ? "E - TV 켜기" : "E - 모니터 켜기");
		public override void Interact(PlayerInteractor who) => SetOn(!isOn);
		public override void ResetToDefault() => SetStateImmediate(defaultOn);

		/// <summary>점검 항목으로 뽑혔을 때: 켜 둔 채로</summary>
		public override bool TrySetAbnormal()
		{
			if (screen == null) return false;
			SetStateImmediate(true);
			return true;
		}

		/// <summary>생성기용</summary>
		public void Setup(string label, string kindName, Renderer screenRenderer)
		{
			roomLabel = label;
			kind = kindName;
			screen = screenRenderer;
			if (screen != null) screen.gameObject.isStatic = false;   // 켜고 끌 때 재질이 바뀌므로 정적 배칭에서 뺌
		}

		void Awake()
		{
			if (screen == null)
			{
				var t = FindChild(transform, "Screen");
				if (t != null) screen = t.GetComponent<Renderer>();
			}
			if (screen != null)
			{
				offMaterial = screen.sharedMaterial;
				offBlock = new MaterialPropertyBlock();
				screen.GetPropertyBlock(offBlock);
			}
			ResetToDefault();
		}

		public void SetOn(bool on)
		{
			if (isOn == on) return;
			isOn = on;
			Apply();
			StateChanged?.Invoke(this);
		}

		public void SetStateImmediate(bool on)
		{
			isOn = on;
			Apply();
			StateChanged?.Invoke(this);
		}

		/// <summary>이상현상: 화면을 이 재질로 덮어씀 (켜짐/꺼짐과 상관없이 보임)</summary>
		public void OverrideScreen(Material m)
		{
			overrideMaterial = m;
			Apply();
		}

		/// <summary>이상현상 해제: 켜짐/꺼짐 표시로 돌아감</summary>
		public void ClearScreenOverride()
		{
			overrideMaterial = null;
			Apply();
		}

		void Apply()
		{
			if (screen == null || !Application.isPlaying) return;
			if (mpb == null) mpb = new MaterialPropertyBlock();
			if (overrideMaterial != null)
			{
				screen.sharedMaterial = overrideMaterial;
				screen.SetPropertyBlock(null);
				return;
			}
			if (isOn)
			{
				screen.sharedMaterial = onMaterial != null ? onMaterial : UnlitMaterial();
				mpb.Clear();
				mpb.SetColor("_BaseColor", onColor);
				mpb.SetColor("_Color", onColor);
				screen.SetPropertyBlock(mpb);
			}
			else
			{
				if (offMaterial != null) screen.sharedMaterial = offMaterial;
				screen.SetPropertyBlock(offBlock);
			}
		}

		static Material UnlitMaterial()
		{
			if (sharedUnlit == null)
			{
				var sh = Shader.Find("Universal Render Pipeline/Unlit");
				if (sh == null) sh = Shader.Find("Unlit/Color");
				sharedUnlit = new Material(sh) { name = "MonitorOn_Unlit (Runtime)" };
			}
			return sharedUnlit;
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
