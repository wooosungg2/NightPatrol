
using UnityEngine;

namespace NightPatrol.Anomalies
{
    /// <summary>
    /// 리깅된 캐릭터를 의자에 앉히고
    /// 책상에 엎드린 자세로 만드는 스크립트.
    ///
    /// Casual1 캐릭터 오브젝트에 추가한다.
    /// Humanoid Avatar가 설정되어 있어야 한다.
    /// </summary>
    [DisallowMultipleComponent]
    public class CryingChildPose : MonoBehaviour
    {
        [Header("캐릭터 Animator")]
        [SerializeField] private Animator animator;

        [Header("앉은 자세")]
        [Range(-180f, 180f)]
        public float leftThighX = 85f;

        [Range(-180f, 180f)]
        public float rightThighX = 85f;

        [Range(-180f, 180f)]
        public float leftKneeX = -90f;

        [Range(-180f, 180f)]
        public float rightKneeX = -90f;

        [Header("상체 숙이기")]
        [Range(-90f, 90f)]
        public float spineX = 48f;

        [Range(-90f, 90f)]
        public float chestX = 22f;

        [Range(-90f, 90f)]
        public float neckX = 12f;

        [Range(-90f, 90f)]
        public float headX = 20f;

        [Header("왼팔")]
        public Vector3 leftUpperArmRotation =
            new Vector3(35f, -20f, 55f);

        public Vector3 leftLowerArmRotation =
            new Vector3(0f, 0f, -65f);

        [Header("오른팔")]
        public Vector3 rightUpperArmRotation =
            new Vector3(35f, 20f, -55f);

        public Vector3 rightLowerArmRotation =
            new Vector3(0f, 0f, 65f);

        [Header("미세한 훌쩍임")]
        public bool enableSobbing = false;

        [Range(0f, 3f)]
        public float sobAngle = 0.8f;

        [Range(0.1f, 5f)]
        public float sobSpeed = 1.8f;

        private Transform hips;
        private Transform spine;
        private Transform chest;
        private Transform neck;
        private Transform head;

        private Transform leftUpperLeg;
        private Transform rightUpperLeg;
        private Transform leftLowerLeg;
        private Transform rightLowerLeg;

        private Transform leftUpperArm;
        private Transform rightUpperArm;
        private Transform leftLowerArm;
        private Transform rightLowerArm;

        private Quaternion hipsBase;
        private Quaternion spineBase;
        private Quaternion chestBase;
        private Quaternion neckBase;
        private Quaternion headBase;

        private Quaternion leftUpperLegBase;
        private Quaternion rightUpperLegBase;
        private Quaternion leftLowerLegBase;
        private Quaternion rightLowerLegBase;

        private Quaternion leftUpperArmBase;
        private Quaternion rightUpperArmBase;
        private Quaternion leftLowerArmBase;
        private Quaternion rightLowerArmBase;

        private bool initialized;

        private void Awake()
        {
            Initialize();
        }

        private void Initialize()
        {
            if (initialized)
                return;

            if (animator == null)
                animator = GetComponent<Animator>();

            if (animator == null)
                animator = GetComponentInChildren<Animator>();

            if (animator == null ||
                animator.avatar == null ||
                !animator.isHuman)
            {
                Debug.LogError(
                    "[CryingChildPose] Humanoid Animator가 필요합니다. " +
                    "모델의 Rig 설정을 확인하세요.",
                    this
                );
                return;
            }

            hips = Bone(HumanBodyBones.Hips);
            spine = Bone(HumanBodyBones.Spine);
            chest = Bone(HumanBodyBones.Chest);
            neck = Bone(HumanBodyBones.Neck);
            head = Bone(HumanBodyBones.Head);

            leftUpperLeg = Bone(HumanBodyBones.LeftUpperLeg);
            rightUpperLeg = Bone(HumanBodyBones.RightUpperLeg);
            leftLowerLeg = Bone(HumanBodyBones.LeftLowerLeg);
            rightLowerLeg = Bone(HumanBodyBones.RightLowerLeg);

            leftUpperArm = Bone(HumanBodyBones.LeftUpperArm);
            rightUpperArm = Bone(HumanBodyBones.RightUpperArm);
            leftLowerArm = Bone(HumanBodyBones.LeftLowerArm);
            rightLowerArm = Bone(HumanBodyBones.RightLowerArm);

            hipsBase = BaseRotation(hips);
            spineBase = BaseRotation(spine);
            chestBase = BaseRotation(chest);
            neckBase = BaseRotation(neck);
            headBase = BaseRotation(head);

            leftUpperLegBase = BaseRotation(leftUpperLeg);
            rightUpperLegBase = BaseRotation(rightUpperLeg);
            leftLowerLegBase = BaseRotation(leftLowerLeg);
            rightLowerLegBase = BaseRotation(rightLowerLeg);

            leftUpperArmBase = BaseRotation(leftUpperArm);
            rightUpperArmBase = BaseRotation(rightUpperArm);
            leftLowerArmBase = BaseRotation(leftLowerArm);
            rightLowerArmBase = BaseRotation(rightLowerArm);

            initialized = true;
        }

        private Transform Bone(HumanBodyBones bone)
        {
            return animator.GetBoneTransform(bone);
        }

        private Quaternion BaseRotation(Transform bone)
        {
            return bone != null
                ? bone.localRotation
                : Quaternion.identity;
        }

        private void LateUpdate()
        {
            if (!initialized)
            {
                Initialize();

                if (!initialized)
                    return;
            }

            ApplyPose();
        }

        private void ApplyPose()
        {
            float sob = 0f;

            if (enableSobbing)
            {
                sob = Mathf.Sin(
                    Time.time * sobSpeed * Mathf.PI * 2f
                ) * sobAngle;
            }

            // 다리: 의자에 앉은 형태
            Rotate(
                leftUpperLeg,
                leftUpperLegBase,
                new Vector3(leftThighX, 0f, 0f)
            );

            Rotate(
                rightUpperLeg,
                rightUpperLegBase,
                new Vector3(rightThighX, 0f, 0f)
            );

            Rotate(
                leftLowerLeg,
                leftLowerLegBase,
                new Vector3(leftKneeX, 0f, 0f)
            );

            Rotate(
                rightLowerLeg,
                rightLowerLegBase,
                new Vector3(rightKneeX, 0f, 0f)
            );

            // 상체: 책상 쪽으로 숙이기
            Rotate(
                spine,
                spineBase,
                new Vector3(spineX + sob, 0f, 0f)
            );

            Rotate(
                chest,
                chestBase,
                new Vector3(chestX + sob * 0.4f, 0f, 0f)
            );

            Rotate(
                neck,
                neckBase,
                new Vector3(neckX, 0f, 0f)
            );

            Rotate(
                head,
                headBase,
                new Vector3(headX + sob * 0.5f, 0f, 0f)
            );

            // 양팔: 앞으로 뻗고 접기
            Rotate(
                leftUpperArm,
                leftUpperArmBase,
                leftUpperArmRotation
            );

            Rotate(
                rightUpperArm,
                rightUpperArmBase,
                rightUpperArmRotation
            );

            Rotate(
                leftLowerArm,
                leftLowerArmBase,
                leftLowerArmRotation
            );

            Rotate(
                rightLowerArm,
                rightLowerArmBase,
                rightLowerArmRotation
            );
        }

        private void Rotate(
            Transform bone,
            Quaternion baseRotation,
            Vector3 offset)
        {
            if (bone == null)
                return;

            bone.localRotation =
                baseRotation * Quaternion.Euler(offset);
        }

        [ContextMenu("Reset Pose Values")]
        private void ResetPoseValues()
        {
            leftThighX = 85f;
            rightThighX = 85f;

            leftKneeX = -90f;
            rightKneeX = -90f;

            spineX = 48f;
            chestX = 22f;
            neckX = 12f;
            headX = 20f;

            leftUpperArmRotation =
                new Vector3(35f, -20f, 55f);

            rightUpperArmRotation =
                new Vector3(35f, 20f, -55f);

            leftLowerArmRotation =
                new Vector3(0f, 0f, -65f);

            rightLowerArmRotation =
                new Vector3(0f, 0f, 65f);
        }
    }
}
