using System.Collections;
using NightPatrol.Menu;
using NightPatrol.Run;
using TMPro;
using UnityEngine;

namespace NightPatrol.Title
{
    /// <summary>
    /// 시작 화면 씬(Assets/Scenes/Title). 켜지면 같은 씬의 PauseMenu로 "야간순찰" — 근무 시작 · 설정 · 게임 종료를 연다
    /// (설정·조작 키는 ESC 메뉴와 같은 것). 근무 시작 → 화면이 검게 덮인 뒤 Game Scene을 불러온다.
    /// 게임에서 처음으로·퇴근하면 이 씬으로 돌아온다 (GameRestart)
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TitleScreen : MonoBehaviour
    {
        [Tooltip("근무 시작에 불러올 씬 (프로젝트 경로). 빌드에서는 빌드에 들어 있어야 함 (개발 빌드 메뉴가 Floor5를 같이 넣음)")]
        [SerializeField] private string gameScene = "Assets/Scenes/Floor5.unity";
        [Tooltip("메뉴. 비어 있으면 같은 오브젝트에서, 없으면 씬에서 찾는다")]
        [SerializeField] private PauseMenu menu;
        [Tooltip("근무 시작 때 화면을 덮는 검은 화면. 비어 있으면 바로 넘어감")]
        [SerializeField] private ScreenFader fader;
        [Tooltip("검게 덮이는 시간 (초)")]
        [SerializeField] private float fadeSeconds = 0.6f;
        [Tooltip("버전 글자 (\"v\" + Project Settings의 Version). 비어 있어도 됨")]
        [SerializeField] private TMP_Text versionText;

        /// <summary>근무 시작을 눌러 넘어가는 중인지</summary>
        public bool IsStarting { get; private set; }

        private void Awake()
        {
            if (menu == null) menu = GetComponent<PauseMenu>();
            if (menu == null) menu = FindAnyObjectByType<PauseMenu>();
            if (versionText != null) versionText.text = "v" + Application.version;
        }

        private void OnEnable()
        {
            if (menu != null) menu.StartRequested += StartShift;
        }

        private void OnDisable()
        {
            if (menu != null) menu.StartRequested -= StartShift;
        }

        private void Start()
        {
            if (fader != null) fader.SetAlpha(0f);
            if (menu == null)
            {
                Debug.LogError("[Title] PauseMenu가 없음 — 시작 화면 메뉴를 열 수 없음", this);
                return;
            }
            menu.OpenTitle();
        }

        /// <summary>근무 시작: 화면을 검게 덮고 게임 씬을 불러온다. 이미 넘어가는 중이거나 씬을 찾을 수 없으면 무시</summary>
        public void StartShift()
        {
            if (IsStarting) return;
            if (!GameRestart.CanLoad(gameScene))
            {
                Debug.LogError($"[Title] 근무 시작에 불러올 씬을 찾을 수 없음: '{gameScene}' — TitleScreen의 Game Scene 경로 확인 (빌드면 빌드에 들어 있는지)", this);
                return;
            }
            StartCoroutine(StartRoutine());
        }

        private IEnumerator StartRoutine()
        {
            IsStarting = true;
            // 메뉴가 열린 동안은 시간이 멈춰 있어서 실제 시간으로 덮는다
            while (fader != null && fader.Alpha < 1f)
            {
                yield return null;
                fader.SetAlpha(ScreenFader.Step(fader.Alpha, 1f, Time.unscaledDeltaTime, fadeSeconds));
            }
            if (menu != null) menu.Close();   // 시간·소리를 되돌리고 설정 저장
            GameRestart.StartGame(gameScene);
        }
    }
}
