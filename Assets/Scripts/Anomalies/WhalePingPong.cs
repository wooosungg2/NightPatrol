
using UnityEngine;

public class WhalePingPong : MonoBehaviour
{
    [Header("Path")]
    [SerializeField] private Transform pointA;
    [SerializeField] private Transform pointB;
    [SerializeField] private Transform whaleModel;

    [Header("Movement")]
    [SerializeField] private float speed = 2f;
    [SerializeField] private float acceleration = 1.5f;
    [SerializeField] private float rotationSpeed = 120f;
    [SerializeField] private float modelYawOffset = 0f;

    [Header("Swimming Animation")]
    [SerializeField] private float swimFrequency = 0.8f;
    [SerializeField] private float swimHeight = 0.05f;
    [SerializeField] private float pitchAngle = 1.5f;
    [SerializeField] private float rollAngle = 1f;

    [Header("Tail Animation")]
    [SerializeField] private bool animateTail = true;
    [SerializeField, Range(0f, 35f)]
    private float tailAngle = 18f;
    [SerializeField] private float tailPhase = 0.5f;

    [Header("Fin Animation")]
    [SerializeField] private bool animateFins = true;
    [SerializeField, Range(0f, 45f)]
    private float finAngle = 22f;
    [SerializeField] private float finPhaseOffset = 0.4f;

    private Vector3 startPosition;
    private Vector3 endPosition;
    private Vector3 basePosition;

    private Quaternion baseRotation;

    private bool movingToB = true;
    private bool isTurning;

    private float currentSpeed;
    private float swimTime;

    private MeshFilter whaleMeshFilter;
    private Mesh originalMesh;
    private Mesh runtimeMesh;

    private Vector3[] originalVertices;
    private Vector3[] animatedVertices;

    // 실제 OBJ 기준 좌표
    // 머리: -X / 꼬리: +X
    // 가슴지느러미: -X 쪽, Z 양옆
    private const float TailStartX = 0.8f;
    private const float TailTipX = 3.53f;

    private const float FinRootZ = 0.72f;
    private const float FinTipZ = 2.16f;

    private void Start()
    {
        if (pointA == null ||
            pointB == null ||
            whaleModel == null)
        {
            Debug.LogError(
                "WhalePingPong: 참조를 확인해주세요.",
                this
            );

            enabled = false;
            return;
        }

        startPosition = pointA.position;
        endPosition = pointB.position;

        Vector3 direction = endPosition - startPosition;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f)
        {
            Debug.LogError(
                "WhalePingPong: 이동 경로가 너무 짧습니다.",
                this
            );

            enabled = false;
            return;
        }

        basePosition = startPosition;

        baseRotation =
            Quaternion.LookRotation(direction.normalized)
            * Quaternion.Euler(0f, modelYawOffset, 0f);

        whaleModel.position = basePosition;
        whaleModel.rotation = baseRotation;

        InitializeMesh();
    }

    private void InitializeMesh()
    {
        whaleMeshFilter =
            whaleModel.GetComponentInChildren<MeshFilter>(true);

        if (whaleMeshFilter == null ||
            whaleMeshFilter.sharedMesh == null)
        {
            Debug.LogError(
                "WhalePingPong: 고래 MeshFilter를 찾지 못했습니다.",
                this
            );
            return;
        }

        originalMesh = whaleMeshFilter.sharedMesh;

        if (!originalMesh.isReadable)
        {
            Debug.LogError(
                "WhalePingPong: OBJ의 Read/Write를 활성화해주세요.",
                this
            );
            return;
        }

        // 원본 OBJ 보호
        runtimeMesh = Instantiate(originalMesh);
        runtimeMesh.name = originalMesh.name + "_Swimming";

        runtimeMesh.MarkDynamic();

        whaleMeshFilter.sharedMesh = runtimeMesh;

        originalVertices = runtimeMesh.vertices;
        animatedVertices = new Vector3[originalVertices.Length];
    }

    private void Update()
    {
        float dt = Time.deltaTime;

        swimTime += dt;

        UpdateMovement(dt);
        UpdateSwimmingAnimation();
        UpdateMeshAnimation();
    }

    private void UpdateMovement(float dt)
    {
        Vector3 target =
            movingToB ? endPosition : startPosition;

        Vector3 direction = target - basePosition;
        direction.y = 0f;

        Quaternion targetRotation = baseRotation;

        if (direction.sqrMagnitude > 0.0001f)
        {
            targetRotation =
                Quaternion.LookRotation(direction.normalized)
                * Quaternion.Euler(0f, modelYawOffset, 0f);
        }

        if (isTurning)
        {
            baseRotation = Quaternion.RotateTowards(
                baseRotation,
                targetRotation,
                rotationSpeed * dt
            );

            if (Quaternion.Angle(
                baseRotation, targetRotation) < 0.5f)
            {
                baseRotation = targetRotation;
                isTurning = false;
            }

            currentSpeed = 0f;
            return;
        }

        float distance =
            Vector3.Distance(basePosition, target);

        float safeAcceleration =
            Mathf.Max(acceleration, 0.01f);

        float desiredSpeed = Mathf.Min(
            speed,
            Mathf.Sqrt(2f * safeAcceleration * distance)
        );

        currentSpeed = Mathf.MoveTowards(
            currentSpeed,
            desiredSpeed,
            safeAcceleration * dt
        );

        basePosition = Vector3.MoveTowards(
            basePosition,
            target,
            currentSpeed * dt
        );

        if (Vector3.Distance(basePosition, target) < 0.02f)
        {
            basePosition = target;
            movingToB = !movingToB;
            isTurning = true;
            currentSpeed = 0f;
        }
    }

    private void UpdateSwimmingAnimation()
    {
        float phase =
            swimTime * swimFrequency * Mathf.PI * 2f;

        float wave = Mathf.Sin(phase);
        float pitchWave = Mathf.Cos(phase);

        Vector3 swimOffset =
            Vector3.up * wave * swimHeight;

        whaleModel.position = basePosition + swimOffset;

        Quaternion swimRotation = Quaternion.Euler(
            pitchWave * pitchAngle,
            0f,
            wave * rollAngle
        );

        whaleModel.rotation =
            baseRotation * swimRotation;
    }

    private void UpdateMeshAnimation()
    {
        if (runtimeMesh == null ||
            originalVertices == null)
            return;

        float phase =
            swimTime * swimFrequency * Mathf.PI * 2f;

        float tailWave =
            Mathf.Sin(phase - tailPhase);

        float leftFinWave =
            Mathf.Sin(phase + finPhaseOffset);

        float rightFinWave =
            Mathf.Sin(phase - finPhaseOffset);

        for (int i = 0; i < originalVertices.Length; i++)
        {
            Vector3 v = originalVertices[i];

            if (animateTail)
                v = DeformTail(v, tailWave);

            if (animateFins)
                v = DeformFins(
                    v,
                    leftFinWave,
                    rightFinWave
                );

            animatedVertices[i] = v;
        }

        runtimeMesh.vertices = animatedVertices;

        runtimeMesh.RecalculateNormals();
        runtimeMesh.RecalculateBounds();
    }

    private Vector3 DeformTail(Vector3 v, float wave)
    {
        if (v.x <= TailStartX)
            return v;

        float t = Mathf.InverseLerp(
            TailStartX,
            TailTipX,
            v.x
        );

        // 부드러운 변형 가중치
        float weight = Mathf.SmoothStep(0f, 1f, t);

        // 꼬리 끝에서 약 18도 기울어지는 효과
        float maxDisplacement =
            Mathf.Tan(tailAngle * Mathf.Deg2Rad)
            * (TailTipX - TailStartX);

        // 꼬리 쪽으로 갈수록 크게 움직임
        v.y += wave * maxDisplacement * weight;

        return v;
    }

    private Vector3 DeformFins(
        Vector3 v,
        float leftWave,
        float rightWave)
    {
        // 가슴지느러미가 존재하는 X 범위
        if (v.x < -2.2f || v.x > -1.15f)
            return v;

        // 몸통 아래쪽에 있는 정점만
        if (v.y > -0.35f)
            return v;

        float absZ = Mathf.Abs(v.z);

        // 몸통 중심 정점은 움직이지 않음
        if (absZ <= FinRootZ)
            return v;

        float side = Mathf.Sign(v.z);

        float wave =
            side < 0f ? leftWave : rightWave;

        // 지느러미 뿌리에서 끝으로 갈수록 증가
        float t = Mathf.InverseLerp(
            FinRootZ,
            FinTipZ,
            absZ
        );

        float weight = Mathf.SmoothStep(0f, 1f, t);

        // 지느러미 끝이 위아래로 퍼덕이는 효과
        float maxDisplacement =
            Mathf.Tan(finAngle * Mathf.Deg2Rad)
            * (FinTipZ - FinRootZ);

        v.y += wave * maxDisplacement * weight;

        return v;
    }

    private void OnDestroy()
    {
        if (whaleMeshFilter != null &&
            runtimeMesh != null)
        {
            whaleMeshFilter.sharedMesh = originalMesh;
        }

        if (runtimeMesh != null)
        {
            Destroy(runtimeMesh);
            runtimeMesh = null;
        }
    }

    private void OnDrawGizmos()
    {
        if (pointA == null || pointB == null)
            return;

        Gizmos.color = Color.cyan;

        Gizmos.DrawLine(pointA.position, pointB.position);

        Gizmos.DrawWireSphere(
            pointA.position, 0.3f
        );

        Gizmos.DrawWireSphere(
            pointB.position, 0.3f
        );
    }
}
