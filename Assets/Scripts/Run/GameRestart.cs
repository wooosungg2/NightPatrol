using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NightPatrol.Run
{
    /// <summary>
    /// 씬 넘기기. 처음(5층 1회차)부터 다시 = 시작 화면 씬에서 근무를 시작했으면 시작 화면으로, 아니면 지금 씬을 다시 불러온다
    /// (사물·이상현상·일지·기록이 전부 처음 상태로). ESC 메뉴의 "처음으로"와 1층 퇴근(FloorTransition.EndShift)이 같이 쓴다.
    /// 시작 화면 씬의 근무 시작은 StartGame
    /// </summary>
    public static class GameRestart
    {
        // 테스트용: 씬을 불러오는 대신 이것을 부른다 (불러올 씬 경로를 받음, 테스트 씬이 지워지지 않게). null이면 실제로 불러옴
        internal static Action<string> Override;

        /// <summary>근무를 시작한 시작 화면 씬의 경로 (처음으로·퇴근이 여기로 돌아옴). 시작 화면 없이 Play했으면 null</summary>
        public static string TitleScenePath { get; internal set; }

        /// <summary>시작 화면 씬에서 근무 시작: 지금 씬을 돌아올 시작 화면으로 기억하고 게임 씬(프로젝트 경로)을 불러온다</summary>
        public static void StartGame(string gameScene)
        {
            TitleScenePath = SceneManager.GetActiveScene().path;
            Load(gameScene);
        }

        /// <summary>처음부터 다시: 시작 화면에서 들어왔으면 시작 화면으로, 아니면 지금 씬을 다시 (테스트 씬에서 바로 Play한 경우)</summary>
        public static void Restart() =>
            Load(string.IsNullOrEmpty(TitleScenePath) ? SceneManager.GetActiveScene().path : TitleScenePath);

        /// <summary>그 경로의 씬을 불러올 수 있는지 (에디터는 파일이 있으면, 빌드는 빌드에 들어 있으면)</summary>
        public static bool CanLoad(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
#if UNITY_EDITOR
            return System.IO.File.Exists(path);
#else
            return Application.CanStreamedLevelBeLoaded(path);
#endif
        }

        private static void Load(string path)
        {
            if (Override != null)
            {
                Override(path);
                return;
            }
#if UNITY_EDITOR
            // 에디터에서는 Build Settings에 없는 씬(테스트 씬 등)도 불러야 하므로 경로로
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Single));
#else
            SceneManager.LoadScene(path);
#endif
        }

        // 에디터에서 도메인 리로드를 꺼도 지난 Play의 값이 남지 않게
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Override = null;
            TitleScenePath = null;
        }
    }
}
