
namespace NightPatrol.Anomalies
{
    public abstract class Anomaly : UnityEngine.MonoBehaviour
    {
        public bool IsActive { get; protected set; }

        public abstract void Activate();
        public abstract void Deactivate();

        // PatrolRun이 이상현상 카운터를 초기화한 후 사용
        public virtual void ResetAfterFloorReset()
        {
            Deactivate();
        }
    }
}
