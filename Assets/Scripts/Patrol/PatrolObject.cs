using NightPatrol.Interaction;
using UnityEngine;

namespace NightPatrol.Patrol
{
    public abstract class PatrolObject : MonoBehaviour, IInteractable
    {
        [Tooltip("구역.종류.번호 형식 (예: R21.Monitor.03, Corridor.Lamp.01)")]
        public string Id;

        private int anomalyCount;
        public bool HasAnomaly => anomalyCount > 0;
        public bool IsClear => IsNormal && !HasAnomaly;
        public void AddAnomaly() => anomalyCount++;
        public void RemoveAnomaly()
        {
            if (anomalyCount > 0) anomalyCount--;
        }

        public abstract bool IsNormal { get; }
        // 점검 항목 후보가 되려면 TrySetAbnormal을 override
        public virtual bool TrySetAbnormal() => false;
        public virtual string TaskLabel => Id;
        public abstract void ResetToDefault();
        public abstract string Prompt { get; }
        public abstract void Interact(PlayerInteractor who);
    }
}
