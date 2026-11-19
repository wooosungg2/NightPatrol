
using System.Collections;
using UnityEngine;

namespace NightPatrol.Anomalies
{
    public class FriedEggAnomaly : Anomaly
    {
        [Header("References")]
        [SerializeField] private Transform player;
        [SerializeField] private GameObject friedEggRoot;

        [Tooltip("실제 계란 노른자 오브젝트")]
        [SerializeField] private Transform eggYolk;

        [SerializeField] private Transform[] drips;
        [SerializeField] private Transform puddle;

        [Header("Trigger")]
        [SerializeField, Min(0.1f)]
        private float triggerDistance = 2f;

        [Header("Flow Timing")]
        [SerializeField, Min(0.1f)]
        private float flowDuration = 2.2f;

        [SerializeField, Min(0f)]
        private float dripDelay = 0.25f;

        [Header("Drip Dimensions")]
        [SerializeField, Min(0.01f)]
        private float dripMeshLength = 1f;

        [SerializeField, Min(0.1f)]
        private float dripLength = 1.5f;

        [Header("Floor Detection")]
        [SerializeField]
        private float floorWorldY = 0f;

        [Header("Puddle")]
        [SerializeField, Min(0.1f)]
        private float puddleGrowDuration = 3f;

        [SerializeField, Min(0.01f)]
        private float puddleMaxScale = 1.5f;

        [Header("Continuous Flow")]
        [SerializeField]
        private bool continuousFlow = true;

        [SerializeField, Min(0f)]
        private float flowPulseAmplitude = 0.06f;

        [SerializeField, Min(0f)]
        private float flowPulseSpeed = 5f;

        private Vector3[] dripBaseScales;
        private Vector3[] dripBasePositions;
        private Vector3 puddleBaseScale;

        private Renderer eggYolkRenderer;
        private Coroutine flowRoutine;

        private bool initialized;
        private bool hasTriggered;

        private void Awake()
        {
            if (friedEggRoot == null || eggYolk == null)
            {
                Debug.LogWarning(
                    "[FriedEggAnomaly] FriedEggRoot or EggYolk missing.",
                    this
                );
                return;
            }

            eggYolkRenderer =
                eggYolk.GetComponentInChildren<Renderer>(true);

            if (eggYolkRenderer == null)
            {
                Debug.LogWarning(
                    "[FriedEggAnomaly] EggYolk Renderer missing.",
                    this
                );
            }

            if (drips == null)
                drips = new Transform[0];

            dripBaseScales = new Vector3[drips.Length];
            dripBasePositions = new Vector3[drips.Length];

            for (int i = 0; i < drips.Length; i++)
            {
                if (drips[i] == null)
                    continue;

                dripBaseScales[i] = drips[i].localScale;
                dripBasePositions[i] = drips[i].localPosition;
            }

            if (puddle != null)
                puddleBaseScale = puddle.localScale;

            initialized = true;

            ResetVisual();
            friedEggRoot.SetActive(false);
        }

        private void Update()
        {
            if (!IsActive || !initialized || hasTriggered)
                return;

            if (player == null || eggYolk == null)
                return;

            // 실제 노른자 메시의 중심 위치 사용
            Vector3 eggPosition =
                eggYolkRenderer != null
                    ? eggYolkRenderer.bounds.center
                    : eggYolk.position;

            Vector3 playerPosition = player.position;

            // 높이 차이를 제외한 수평 거리
            playerPosition.y = 0f;
            eggPosition.y = 0f;

            float distance = Vector3.Distance(
                playerPosition,
                eggPosition
            );

            if (distance <= triggerDistance)
            {
                hasTriggered = true;

                Debug.Log(
                    "[FriedEggAnomaly] Player approached! Flow Start!",
                    this
                );

                flowRoutine = StartCoroutine(
                    FlowAnimation()
                );
            }
        }

        public override void Activate()
        {
            if (IsActive)
                return;

            if (!initialized ||
                player == null ||
                friedEggRoot == null)
            {
                Debug.LogWarning(
                    "[FriedEggAnomaly] Not ready.",
                    this
                );
                return;
            }

            StopFlow();
            ResetVisual();

            hasTriggered = false;
            IsActive = true;

            // 계란 프라이 등장
            friedEggRoot.SetActive(true);

            RegisterAnomaly();
        }

        public override void Deactivate()
        {
            StopFlow();
            ResetVisual();

            if (friedEggRoot != null)
                friedEggRoot.SetActive(false);

            hasTriggered = false;
            IsActive = false;

            UnregisterAnomaly();
        }

        public override void ResetAfterFloorReset()
        {
            StopFlow();
            ResetVisual();

            if (friedEggRoot != null)
                friedEggRoot.SetActive(false);

            hasTriggered = false;
            IsActive = false;

            ForgetRegistrationAfterFloorReset();
        }

        private IEnumerator FlowAnimation()
        {
            float elapsed = 0f;
            float puddleElapsed = 0f;

            bool floorReached = false;

            while (true)
            {
                elapsed += Time.deltaTime;

                for (int i = 0; i < drips.Length; i++)
                {
                    Transform drip = drips[i];

                    if (drip == null)
                        continue;

                    float startTime = i * dripDelay;

                    if (elapsed < startTime)
                        continue;

                    float u = Mathf.Clamp01(
                        (elapsed - startTime) /
                        flowDuration
                    );

                    drip.gameObject.SetActive(true);

                    // 액체가 늘어나는 길이
                    float desiredLength = Mathf.Lerp(
                        0.001f,
                        dripLength,
                        u
                    );

                    // 바닥까지의 거리
                    float distanceToFloor = Mathf.Max(
                        0f,
                        drip.position.y - floorWorldY
                    );

                    float actualLength = Mathf.Min(
                        desiredLength,
                        distanceToFloor
                    );

                    float scaleY =
                        actualLength / dripMeshLength;

                    Vector3 scale = dripBaseScales[i];

                    scale.y =
                        dripBaseScales[i].y *
                        Mathf.Max(0.001f, scaleY);

                    // 바닥에 도달한 이후에도
                    // 노른자가 흐르는 듯한 효과
                    if (continuousFlow && floorReached)
                    {
                        float pulse =
                            1f +
                            Mathf.Sin(
                                elapsed * flowPulseSpeed +
                                i * 1.4f
                            ) * flowPulseAmplitude;

                        scale.x *= pulse;
                        scale.z *= pulse;
                    }

                    drip.localScale = scale;

                    // 첫 번째 줄기가 바닥에 도달하면
                    // 웅덩이 생성 시작
                    if (i == 0 &&
                        desiredLength >= distanceToFloor &&
                        distanceToFloor > 0f)
                    {
                        floorReached = true;
                    }
                }

                if (floorReached && puddle != null)
                {
                    puddle.gameObject.SetActive(true);

                    puddleElapsed += Time.deltaTime;

                    float t = Mathf.Clamp01(
                        puddleElapsed / puddleGrowDuration
                    );

                    float smoothT =
                        t * t * (3f - 2f * t);

                    float multiplier = Mathf.Lerp(
                        0.001f,
                        puddleMaxScale,
                        smoothT
                    );

                    puddle.localScale =
                        puddleBaseScale * multiplier;
                }

                if (!continuousFlow &&
                    floorReached &&
                    puddleElapsed >= puddleGrowDuration)
                {
                    break;
                }

                yield return null;
            }

            flowRoutine = null;
        }

        private void StopFlow()
        {
            if (flowRoutine != null)
            {
                StopCoroutine(flowRoutine);
                flowRoutine = null;
            }
        }

        private void ResetVisual()
        {
            if (!initialized)
                return;

            if (drips != null)
            {
                for (int i = 0; i < drips.Length; i++)
                {
                    Transform drip = drips[i];

                    if (drip == null)
                        continue;

                    drip.localPosition = dripBasePositions[i];
                    drip.localScale = dripBaseScales[i];
                    drip.gameObject.SetActive(false);
                }
            }

            if (puddle != null)
            {
                puddle.localScale = puddleBaseScale;
                puddle.gameObject.SetActive(false);
            }
        }
    }
}
