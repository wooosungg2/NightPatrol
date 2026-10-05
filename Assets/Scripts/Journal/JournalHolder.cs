using System;
using NightPatrol.Interaction;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NightPatrol.Journal
{
    [DisallowMultipleComponent]
    public sealed class JournalHolder : MonoBehaviour
    {
        [SerializeField] private PlayerInteractor interactor;
        [Tooltip("일지를 든 동안 E 조작을 막는다. 끄면 들고도 조작 가능")]
        [SerializeField] private bool blockInteractionWhileRaised = true;
        [SerializeField] private Vector3 loweredPosition = new Vector3(-0.16f, -0.30f, 0.30f);
        [SerializeField] private Vector3 loweredEuler = new Vector3(55f, 12f, 0f);
        [SerializeField] private Vector3 raisedPosition = new Vector3(0f, -0.06f, 0.28f);
        [SerializeField] private Vector3 raisedEuler = new Vector3(8f, 0f, 0f);
        [SerializeField] private float transitionSeconds = 0.25f;
        private float progress;

        public bool IsRaised { get; private set; }
        public event Action<bool> RaisedChanged;

        private void Awake()
        {
            if (interactor == null) interactor = GetComponentInParent<PlayerInteractor>();
        }
        private void OnEnable() => SetRaised(IsRaised);
        private void OnDisable()
        {
            if (interactor != null) interactor.InteractionBlocked = false;
        }
        private void Start()
        {
            SetRaised(false);
            progress = 0f;
            ApplyPose();
        }
        private void Update()
        {
            HandleInput();
            progress = Step(progress, IsRaised, Time.deltaTime, transitionSeconds);
            ApplyPose();
        }
        private void HandleInput()
        {
            if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame) Toggle();
        }
        public void Toggle() => SetRaised(!IsRaised);
        public void SetRaised(bool raised)
        {
            if (interactor != null) interactor.InteractionBlocked = raised && blockInteractionWhileRaised;
            if (IsRaised == raised) return;
            IsRaised = raised;
            RaisedChanged?.Invoke(raised);
        }
        private void ApplyPose()
        {
            float eased = Mathf.SmoothStep(0f, 1f, progress);
            transform.localPosition = Vector3.Lerp(loweredPosition, raisedPosition, eased);
            transform.localRotation = Quaternion.Slerp(
                Quaternion.Euler(loweredEuler), Quaternion.Euler(raisedEuler), eased);
        }
        public static float Step(float t, bool raised, float deltaTime, float seconds)
        {
            float target = raised ? 1f : 0f;
            if (seconds <= 0f) return target;
            return Mathf.MoveTowards(Mathf.Clamp01(t), target, Mathf.Max(0f, deltaTime) / seconds);
        }
    }
}
