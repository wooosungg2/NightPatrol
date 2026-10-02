using System;
using NightPatrol.Patrol;
using UnityEngine;

namespace NightPatrol.Run
{
    [DisallowMultipleComponent]
    public sealed class PatrolRun : MonoBehaviour
    {
        [SerializeField] private int startFloor = 5;
        [SerializeField] private int lastFloor = 1;
        [SerializeField] private Transform startPoint;
        [SerializeField] private CharacterController player;

        public int CurrentFloor { get; private set; }
        public int Attempt { get; private set; } = 1;
        public bool IsCleared { get; private set; }
        public event Action<int> FloorStarted;
        public event Action Failed;
        public event Action Cleared;

        private void Awake() => CurrentFloor = startFloor;
        private void Start()
        {
            Debug.Log($"[Run] {startFloor}층 시작", this);
            FloorStarted?.Invoke(startFloor);
        }

        public void TryDescend()
        {
            if (!IsCleared) Descend(PatrolInspection.IsAllClear());
        }

        public void Descend(bool allClear)
        {
            if (IsCleared) return;
            if (!allClear) { Fail(); return; }
            CurrentFloor--;
            if (CurrentFloor == lastFloor)
            {
                IsCleared = true;
                Debug.Log($"[Run] {CurrentFloor}층 도착 — 클리어", this);
                Cleared?.Invoke();
                return;
            }
            ResetAllObjects();
            Debug.Log($"[Run] {CurrentFloor}층 도착", this);
            FloorStarted?.Invoke(CurrentFloor);
        }

        public void Fail()
        {
            if (IsCleared) return;
            Attempt++;
            CurrentFloor = startFloor;
            ResetAllObjects();
            if (player != null && startPoint != null)
            {
                player.enabled = false;
                player.transform.SetPositionAndRotation(startPoint.position, startPoint.rotation);
                player.enabled = true;
            }
            Debug.Log($"[Run] 실패 → {startFloor}층 재시작 ({Attempt}회차)", this);
            Failed?.Invoke();
            FloorStarted?.Invoke(startFloor);
        }

        public void ResetAllObjects()
        {
            foreach (var obj in UnityEngine.Object.FindObjectsByType<PatrolObject>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (obj != null) obj.ResetToDefault();
        }
    }
}
