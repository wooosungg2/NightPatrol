using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Linq;
using NightPatrol.Interaction;
using NightPatrol.Patrol;
using NightPatrol.Run;
using UnityEngine.InputSystem;
#endif

namespace NightPatrol.Debugging
{
    [DisallowMultipleComponent]
    public sealed class InteractionDebugView : MonoBehaviour
    {
        [SerializeField] private bool visible = false;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private PlayerInteractor interactor;
        private string targetText = "없음";
        private string inspectionText = "";
        private double nextInspectionTime;
        private PatrolRun run;
        private FloorTasks floorTasks;

        private void Awake()
        {
            run = UnityEngine.Object.FindFirstObjectByType<PatrolRun>();
            floorTasks = UnityEngine.Object.FindFirstObjectByType<FloorTasks>();
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
                visible = !visible;
        }

        private void LateUpdate()
        {
            if (!visible) return;
            targetText = GetTargetText();
            if (Time.unscaledTimeAsDouble < nextInspectionTime) return;
            nextInspectionTime = Time.unscaledTimeAsDouble + 0.5;
            var abnormal = PatrolInspection.FindAbnormal();
            inspectionText = $"비정상 {abnormal.Count}개: {string.Join(", ", abnormal.Take(5).Select(obj => obj.Id))}";
            if (abnormal.Count > 5) inspectionText += $" 외 {abnormal.Count - 5}개";
        }

        public string GetTargetText()
        {
            if (interactor == null) interactor = GetComponent<PlayerInteractor>();
            if (interactor == null) return "PlayerInteractor 없음";
            var playerCamera = interactor.PlayerCamera;
            var settings = interactor.Settings;
            if (playerCamera == null || settings == null) return "카메라 / PlayerSettings 미지정";
            if (!playerCamera.isActiveAndEnabled) return "없음";
            Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            ray.origin = playerCamera.transform.position;
            // Look beyond interaction range so out-of-range targets remain identifiable.
            if (!Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity,
                interactor.RaycastMask, QueryTriggerInteraction.Ignore)) return "없음";
            string text = $"{hit.collider.gameObject.name} — {hit.distance:F2} m";
            var target = hit.collider.GetComponentInParent<IInteractable>();
            if (target == null || (target is Object obj && obj == null)) return text;
            string state = hit.distance > settings.InteractionDistance ? "거리 밖"
                : target is Behaviour behaviour && !behaviour.isActiveAndEnabled
                    ? "상호작용 비활성" : "상호작용 가능";
            return $"{text}\n{target.Prompt}\n{state}";
        }

        private void OnGUI()
        {
            if (!visible) return;
            GUI.DrawTexture(new Rect(Screen.width * 0.5f - 2f, Screen.height * 0.5f - 2f, 4f, 4f), Texture2D.whiteTexture);
            GUILayout.BeginArea(new Rect(12f, 12f, Mathf.Max(100f, Screen.width - 24f), 200f));
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label(targetText);
            if (interactor != null)
                GUILayout.Label($"CurrentTarget: {(interactor.CurrentTarget == null ? "없음 (null)" : "있음 (non-null)")}");
            GUILayout.Label(inspectionText);
            if (run != null) GUILayout.Label($"층 {run.CurrentFloor} / 회차 {run.Attempt}");
            if (floorTasks != null)
                GUILayout.Label($"점검 항목: {string.Join(", ", floorTasks.CurrentTasks.Where(o => o != null).Select(o => o.TaskLabel))}");
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }
#endif
    }
}
