// 새 사물 만드는 법: PatrolObject를 상속한다.
// IsNormal에 규정상 정상 상태를, ResetToDefault에 초기화 동작을 구현한다.
// Prompt에 안내 문구를, Interact에 E키 조작을 구현한다.
// 점검 항목으로 뽑히게 하려면 TrySetAbnormal을 구현하고 TaskTable에 Id를 넣는다.
using NightPatrol.Interaction;
using UnityEngine;

namespace NightPatrol.Patrol
{
    public class PatrolLamp : PatrolObject
    {
        [Tooltip("켜고 끌 조명. 꺼져 있어야 정상이고, 비워 두면 항상 비정상으로 판정된다")]
        [SerializeField] private Light targetLight;
        [Tooltip("층이 시작될 때 켜 둘지. 켜 두면 처음부터 비정상이 되므로 보통 끈다")]
        [SerializeField] private bool startsOn;

        public override bool IsNormal => targetLight != null && !targetLight.enabled;

        public override bool TrySetAbnormal()
        {
            if (targetLight == null) return false;
            targetLight.enabled = true;
            return true;
        }
        public override string Prompt => targetLight == null
            ? string.Empty : targetLight.enabled ? "E - 전등 끄기" : "E - 전등 켜기";

        private void Awake()
        {
            ResetToDefault();
        }

        public override void ResetToDefault()
        {
            if (targetLight != null) targetLight.enabled = startsOn;
        }

        public override void Interact(PlayerInteractor who)
        {
            if (targetLight != null) targetLight.enabled = !targetLight.enabled;
        }
    }
}
