
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NightPatrol.Lighting
{
    /// <summary>
    /// 같이 켜지고 꺼지는 조명 묶음.
    /// 복도 구간 및 강의실 조명을 관리한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LightZone : MonoBehaviour
    {
        [Tooltip("조명 구간 ID")]
        public string Id;

        [Tooltip("층 시작 시 켜짐 여부")]
        public bool startsOn = true;

        [SerializeField]
        private List<Light> lights = new List<Light>();

        [SerializeField]
        private List<Renderer> panels = new List<Renderer>();

        public Color panelOn = new Color(1f, 1f, 0.97f);
        public Color panelOff = new Color(0.35f, 0.36f, 0.37f);
        public Color emissionOn =
            new Color(1f, 1f, 0.95f) * 1.5f;

        public bool IsOn { get; private set; } = true;
        public bool HasAnomalyColor => tint.HasValue;
        public bool IsFlickering =>
            flicker != null || singleFlicker != null;

        public event Action<LightZone, bool> Changed;

        private static readonly List<LightZone> all =
            new List<LightZone>();

        public static IReadOnlyList<LightZone> All => all;

        public static LightZone FindById(string id)
        {
            foreach (var z in all)
            {
                if (z != null && z.Id == id)
                    return z;
            }

            return null;
        }

        private MaterialPropertyBlock mpb;
        private Coroutine flicker;
        private Color? tint;

        private readonly List<Color> baseColors =
            new List<Color>();

        private NightPatrol.Run.PatrolRun run;

        // 개별 조명 색상 이상현상
        private int singleLightIndex = -1;
        private Color? singleLightColor;

        // 개별 조명 깜빡임 이상현상
        private Coroutine singleFlicker;
        private int singleFlickerIndex = -1;
        private bool singleFlickerVisible = true;

        /// <summary>
        /// 자식의 Light와 Tube 렌더러 수집
        /// </summary>
        public void Collect()
        {
            lights.Clear();
            panels.Clear();

            lights.AddRange(
                GetComponentsInChildren<Light>(true)
            );

            foreach (var r in
                GetComponentsInChildren<Renderer>(true))
            {
                if (r.gameObject.name == "Tube")
                    panels.Add(r);
            }
        }

        private void Awake()
        {
            if (lights.Count == 0 && panels.Count == 0)
                Collect();

            baseColors.Clear();

            foreach (var l in lights)
            {
                baseColors.Add(
                    l != null ? l.color : Color.white
                );
            }

            Apply(startsOn);
        }

        private void OnEnable()
        {
            if (!all.Contains(this))
                all.Add(this);

            run = FindAnyObjectByType<
                NightPatrol.Run.PatrolRun>();

            if (run != null)
                run.FloorReset += OnFloorReset;
        }

        private void OnDisable()
        {
            all.Remove(this);

            if (run != null)
                run.FloorReset -= OnFloorReset;

            StopSingleLightFlicker();
        }

        private void OnFloorReset(int floor)
        {
            StopFlicker();
            StopSingleLightFlicker();

            tint = null;

            singleLightIndex = -1;
            singleLightColor = null;

            SetOn(startsOn);
            Apply(IsOn);
        }

        // =====================================
        // 기존 전체 조명 색상 이상현상
        // =====================================

        public void SetAnomalyColor(Color c)
        {
            tint = c;
            Apply(IsOn);
        }

        public void ClearAnomalyColor()
        {
            tint = null;
            Apply(IsOn);
        }

        // =====================================
        // 기존 전체 조명 깜빡임
        // =====================================

        public void StartFlicker()
        {
            if (flicker != null)
                StopCoroutine(flicker);

            flicker = StartCoroutine(
                FlickerRoutine(float.PositiveInfinity)
            );
        }

        public void StopFlicker()
        {
            if (flicker == null)
                return;

            StopCoroutine(flicker);
            flicker = null;

            Apply(IsOn);
        }

        // =====================================
        // 개별 조명 색상 이상현상
        // =====================================

        /// <summary>
        /// 특정 조명 하나만 색상 변경.
        /// index 0 = 첫 번째 조명
        /// </summary>
        public void SetSingleLightAnomalyColor(
            int index,
            Color color)
        {
            if (!IsValidLightIndex(index))
            {
                Debug.LogWarning(
                    $"[LightZone] Invalid light index: {index}",
                    this
                );
                return;
            }

            singleLightIndex = index;
            singleLightColor = color;

            Apply(IsOn);
        }

        /// <summary>
        /// 개별 조명 색상 이상현상 해제
        /// </summary>
        public void ClearSingleLightAnomalyColor()
        {
            singleLightIndex = -1;
            singleLightColor = null;

            Apply(IsOn);
        }

        // =====================================
        // 개별 조명 깜빡임 이상현상
        // =====================================

        /// <summary>
        /// 특정 조명 하나만 불규칙하게 깜빡임.
        /// index 0 = 첫 번째 조명
        /// </summary>
        public void StartSingleLightFlicker(int index)
        {
            if (!IsValidLightIndex(index))
            {
                Debug.LogWarning(
                    $"[LightZone] Invalid flicker index: {index}",
                    this
                );
                return;
            }

            StopSingleLightFlicker();

            singleFlickerIndex = index;
            singleFlickerVisible = true;

            singleFlicker = StartCoroutine(
                SingleLightFlickerRoutine()
            );
        }

        /// <summary>
        /// 개별 조명 깜빡임 중지 및 원상복구
        /// </summary>
        public void StopSingleLightFlicker()
        {
            if (singleFlicker != null)
            {
                StopCoroutine(singleFlicker);
                singleFlicker = null;
            }

            singleFlickerIndex = -1;
            singleFlickerVisible = true;

            Apply(IsOn);
        }

        private IEnumerator SingleLightFlickerRoutine()
        {
            while (true)
            {
                singleFlickerVisible =
                    UnityEngine.Random.value >= 0.4f;

                // 전체 조명 ON/OFF 상태는 유지하면서
                // 선택한 조명 하나의 표시 상태만 갱신
                Apply(IsOn);

                yield return new WaitForSeconds(
                    UnityEngine.Random.Range(
                        0.05f,
                        0.35f
                    )
                );
            }
        }

        // =====================================
        // 조명 ON / OFF
        // =====================================

        public void Toggle()
        {
            SetOn(!IsOn);
        }

        public void SetOn(bool on)
        {
            bool changed = on != IsOn;

            Apply(on);

            if (changed)
                Changed?.Invoke(this, on);
        }

        // =====================================
        // 실제 조명 상태 적용
        // =====================================

        private void Apply(bool on)
        {
            IsOn = on;

            // Light 컴포넌트
            for (int i = 0; i < lights.Count; i++)
            {
                var l = lights[i];

                if (l == null)
                    continue;

                bool lightOn = on;

                // 개별 깜빡임 대상만 ON/OFF 변경
                if (i == singleFlickerIndex)
                {
                    lightOn =
                        on && singleFlickerVisible;
                }

                l.enabled = lightOn;

                Color baseColor =
                    i < baseColors.Count
                        ? baseColors[i]
                        : l.color;

                if (i == singleLightIndex &&
                    singleLightColor.HasValue)
                {
                    l.color = singleLightColor.Value;
                }
                else
                {
                    l.color = tint ?? baseColor;
                }
            }

            // Tube 렌더러
            if (mpb == null)
                mpb = new MaterialPropertyBlock();

            foreach (var r in panels)
            {
                if (r == null)
                    continue;

                bool isSingleColorTarget =
                    IsPanelOfLight(r, singleLightIndex);

                bool isSingleFlickerTarget =
                    IsPanelOfLight(r, singleFlickerIndex);

                bool panelVisible = on;

                if (isSingleFlickerTarget)
                {
                    panelVisible =
                        on && singleFlickerVisible;
                }

                Color c;
                Color emission;

                if (!panelVisible)
                {
                    c = panelOff;
                    emission = Color.black;
                }
                else if (isSingleColorTarget &&
                         singleLightColor.HasValue)
                {
                    c = singleLightColor.Value;
                    emission =
                        singleLightColor.Value * 1.5f;
                }
                else
                {
                    c = tint ?? panelOn;

                    emission = tint.HasValue
                        ? tint.Value * 1.5f
                        : emissionOn;
                }

                r.GetPropertyBlock(mpb);

                mpb.SetColor("_BaseColor", c);
                mpb.SetColor("_Color", c);
                mpb.SetColor("_EmissionColor", emission);

                r.SetPropertyBlock(mpb);
            }
        }

        // =====================================
        // 보조 함수
        // =====================================

        /// <summary>
        /// index번 조명 기구 (형광등 면·몸체가 들어 있는 오브젝트). 없으면 null.
        /// 촬영 판정(IPhotoVisual)·이상현상이 등 하나를 가리킬 때 쓴다
        /// </summary>
        public GameObject GetFixture(int index)
        {
            if (!IsValidLightIndex(index))
                return null;

            Transform fixture = lights[index].transform.parent;
            return fixture != null ? fixture.gameObject : lights[index].gameObject;
        }

        /// <summary>이 구역의 조명 개수</summary>
        public int LightCount => lights.Count;

        private bool IsValidLightIndex(int index)
        {
            return index >= 0 &&
                   index < lights.Count &&
                   lights[index] != null;
        }

        /// <summary>
        /// Tube가 지정한 Light와 같은 조명 기구
        /// 아래에 있는지 확인
        /// </summary>
        private bool IsPanelOfLight(
            Renderer panel,
            int lightIndex)
        {
            if (panel == null ||
                !IsValidLightIndex(lightIndex))
            {
                return false;
            }

            Transform fixture =
                lights[lightIndex].transform.parent;

            return fixture != null &&
                   panel.transform.IsChildOf(fixture);
        }

        // =====================================
        // 기존 깜빡임 기능
        // =====================================

        public void Flicker(float seconds)
        {
            if (flicker != null)
                StopCoroutine(flicker);

            flicker = StartCoroutine(
                FlickerRoutine(seconds)
            );
        }

        private IEnumerator FlickerRoutine(float seconds)
        {
            bool keep = IsOn;
            float end = Time.time + seconds;

            while (Time.time < end)
            {
                Apply(
                    UnityEngine.Random.value < 0.6f
                        ? keep
                        : !keep
                );

                yield return new WaitForSeconds(
                    UnityEngine.Random.Range(
                        0.03f,
                        0.18f
                    )
                );
            }

            Apply(keep);
            flicker = null;
        }
    }
}
