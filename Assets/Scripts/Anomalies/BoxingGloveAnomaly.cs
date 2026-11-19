
using System.Collections;
using UnityEngine;

namespace NightPatrol.Anomalies
{
    public class BoxingGloveAnomaly : Anomaly
    {
        [Header("References")]
        [SerializeField] private Transform player;
        [SerializeField] private Transform boxingGlovePivot;
        [SerializeField] private Transform spring;
        [SerializeField] private Transform glove;

        [Header("Trigger")]
        [SerializeField, Min(0.1f)]
        private float triggerDistance = 2f;

        [SerializeField, Min(0.1f)]
        private float resetDistance = 3f;

        [Header("Punch Direction (Local Axis)")]
        [SerializeField]
        private Vector3 punchAxis = Vector3.forward;

        [Header("Spring Length Axis")]
        [SerializeField]
        private Vector3 springLengthAxis = Vector3.forward;

        [Header("Punch Animation")]
        [SerializeField, Min(0.1f)]
        private float punchDistance = 1.2f;

        [SerializeField, Min(0.01f)]
        private float windupDuration = 0.12f;

        [SerializeField, Min(0.01f)]
        private float punchDuration = 0.2f;

        [SerializeField, Min(0.01f)]
        private float bounceDuration = 0.65f;

        [SerializeField, Min(0.01f)]
        private float returnDuration = 0.35f;

        [Header("Spring Bounce")]
        [SerializeField, Min(0f)]
        private float bounceAmplitude = 0.22f;

        [SerializeField, Min(1)]
        private int bounceCount = 3;

        [SerializeField, Min(0f)]
        private float bounceDamping = 3.5f;

        [Header("Spring Model")]
        [SerializeField, Min(0.01f)]
        private float springRestLength = 0.5f;

        [Header("Visual")]
        [SerializeField]
        private bool hideWhenIdle = true;

        private Vector3 gloveInitialPosition;
        private Vector3 springInitialPosition;
        private Vector3 springInitialScale;

        private Coroutine punchRoutine;

        private bool armed;
        private bool initialized;

        private void Awake()
        {
            if (boxingGlovePivot == null ||
                spring == null ||
                glove == null)
            {
                Debug.LogWarning(
                    "[BoxingGloveAnomaly] Missing references.",
                    this
                );
                return;
            }

            gloveInitialPosition = glove.localPosition;
            springInitialPosition = spring.localPosition;
            springInitialScale = spring.localScale;

            initialized = true;

            RestoreVisual();
        }

        private void Update()
        {
            if (!IsActive || !initialized)
                return;

            if (player == null)
                return;

            float distance = Vector3.Distance(
                player.position,
                boxingGlovePivot.position
            );

            // 플레이어가 충분히 멀어지면 재발동 준비
            if (!armed)
            {
                if (distance >= resetDistance &&
                    punchRoutine == null)
                {
                    armed = true;
                }

                return;
            }

            // 플레이어가 접근하면 발사
            if (distance <= triggerDistance &&
                punchRoutine == null)
            {
                armed = false;

                punchRoutine = StartCoroutine(
                    PunchAnimation()
                );
            }
        }

        public override void Activate()
        {
            if (IsActive)
                return;

            if (!initialized)
            {
                Debug.LogWarning(
                    "[BoxingGloveAnomaly] Not initialized.",
                    this
                );
                return;
            }

            if (player == null)
            {
                Debug.LogWarning(
                    "[BoxingGloveAnomaly] Player reference missing.",
                    this
                );
                return;
            }

            StopPunch();
            RestoreVisual();

            armed = true;

            IsActive = true;
            RegisterAnomaly();
        }

        public override void Deactivate()
        {
            StopPunch();
            RestoreVisual();

            armed = false;
            IsActive = false;

            UnregisterAnomaly();
        }

        public override void ResetAfterFloorReset()
        {
            StopPunch();
            RestoreVisual();

            armed = false;
            IsActive = false;

            ForgetRegistrationAfterFloorReset();
        }

        private IEnumerator PunchAnimation()
        {
            SetVisualActive(true);

            // 1. 발사 직전 살짝 움츠리기
            yield return AnimateDistance(
                0f,
                -0.08f,
                windupDuration
            );

            // 2. 빠르게 앞으로 발사
            yield return AnimateDistance(
                -0.08f,
                punchDistance,
                punchDuration
            );

            // 3. 끝에서 띠용! 감쇠 진동
            float elapsed = 0f;

            while (elapsed < bounceDuration)
            {
                elapsed += Time.deltaTime;

                float t = Mathf.Clamp01(
                    elapsed / bounceDuration
                );

                float oscillation = Mathf.Cos(
                    t * Mathf.PI * 2f * bounceCount
                );

                float damping = Mathf.Exp(
                    -bounceDamping * t
                );

                float distance =
                    punchDistance +
                    oscillation *
                    bounceAmplitude *
                    damping;

                ApplyDistance(distance);

                yield return null;
            }

            ApplyDistance(punchDistance);

            // 4. 쓰레기통 안으로 복귀
            yield return AnimateDistance(
                punchDistance,
                0f,
                returnDuration
            );

            // 5. 원래 상태 복구 및 숨기기
            RestoreVisual();

            punchRoutine = null;
        }

        private IEnumerator AnimateDistance(
            float from,
            float to,
            float duration
        )
        {
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;

                float t = Mathf.Clamp01(
                    elapsed / duration
                );

                float smoothT =
                    t * t * (3f - 2f * t);

                float distance = Mathf.Lerp(
                    from,
                    to,
                    smoothT
                );

                ApplyDistance(distance);

                yield return null;
            }

            ApplyDistance(to);
        }

        private void ApplyDistance(float distance)
        {
            if (spring == null || glove == null)
                return;

            Vector3 direction =
                punchAxis.sqrMagnitude > 0.0001f
                    ? punchAxis.normalized
                    : Vector3.forward;

            glove.localPosition =
                gloveInitialPosition +
                direction * distance;

            float lengthMultiplier = Mathf.Max(
                0.05f,
                (springRestLength + distance) /
                springRestLength
            );

            Vector3 scale = springInitialScale;
            Vector3 axis = springLengthAxis;

            if (Mathf.Abs(axis.x) >= Mathf.Abs(axis.y) &&
                Mathf.Abs(axis.x) >= Mathf.Abs(axis.z))
            {
                scale.x *= lengthMultiplier;
            }
            else if (Mathf.Abs(axis.y) >= Mathf.Abs(axis.z))
            {
                scale.y *= lengthMultiplier;
            }
            else
            {
                scale.z *= lengthMultiplier;
            }

            spring.localScale = scale;

            spring.localPosition =
                springInitialPosition +
                direction * (distance * 0.5f);
        }

        private void StopPunch()
        {
            if (punchRoutine != null)
            {
                StopCoroutine(punchRoutine);
                punchRoutine = null;
            }
        }

        private void RestoreVisual()
        {
            if (!initialized)
                return;

            if (spring != null)
            {
                spring.localPosition = springInitialPosition;
                spring.localScale = springInitialScale;
            }

            if (glove != null)
            {
                glove.localPosition = gloveInitialPosition;
            }

            SetVisualActive(!hideWhenIdle);
        }

        private void SetVisualActive(bool active)
        {
            if (spring != null)
                spring.gameObject.SetActive(active);

            if (glove != null)
                glove.gameObject.SetActive(active);
        }
    }
}
