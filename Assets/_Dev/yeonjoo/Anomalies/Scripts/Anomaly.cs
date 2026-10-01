namespace NightPatrol.Anomalies
{
    public abstract class Anomaly : UnityEngine.MonoBehaviour
    {
        public bool IsActive { get; protected set; }

        public abstract void Activate();
        public abstract void Deactivate();
    }
}