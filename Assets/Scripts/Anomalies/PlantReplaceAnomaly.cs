
using UnityEngine;

namespace NightPatrol.Anomalies
{
    public class PlantReplaceAnomaly : Anomaly
    {
        [Header("Original Plant Leaves")]
        [SerializeField]
        private GameObject[] originalLeaves;

        [Header("Replacement Plant")]
        [SerializeField]
        private GameObject replacementPlant;

        private bool[] originalActiveStates;

        private void Awake()
        {
            originalActiveStates =
                new bool[originalLeaves.Length];

            for (int i = 0; i < originalLeaves.Length; i++)
            {
                if (originalLeaves[i] != null)
                {
                    originalActiveStates[i] =
                        originalLeaves[i].activeSelf;
                }
            }

            if (replacementPlant != null)
                replacementPlant.SetActive(false);
        }

        public override void Activate()
        {
            if (IsActive)
                return;

            if (replacementPlant == null ||
                originalLeaves == null ||
                originalLeaves.Length == 0)
            {
                Debug.LogWarning(
                    "[PlantReplaceAnomaly] 참조를 확인하세요.",
                    this
                );
                return;
            }

            foreach (GameObject leaf in originalLeaves)
            {
                if (leaf != null)
                    leaf.SetActive(false);
            }

            replacementPlant.SetActive(true);

            IsActive = true;
            RegisterAnomaly();
        }

        public override void Deactivate()
        {
            RestorePlant();

            IsActive = false;
            UnregisterAnomaly();
        }

        public override void ResetAfterFloorReset()
        {
            RestorePlant();

            IsActive = false;
            ForgetRegistrationAfterFloorReset();
        }

        private void RestorePlant()
        {
            if (replacementPlant != null)
                replacementPlant.SetActive(false);

            if (originalLeaves == null ||
                originalActiveStates == null)
                return;

            for (int i = 0; i < originalLeaves.Length; i++)
            {
                if (originalLeaves[i] != null)
                {
                    originalLeaves[i].SetActive(
                        originalActiveStates[i]
                    );
                }
            }
        }

        private void OnDisable()
        {
            Deactivate();
        }
    }
}
