using NightPatrol.Controls;
using NightPatrol.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace NightPatrol.Menu
{
    /// <summary>설정 메뉴가 없는 씬에도 저장된 소리·프레임 제한·감도·조작 키를 적용한다.</summary>
    public static class SettingsStartup
    {
        private static bool keysLoaded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => keysLoaded = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            GameSettings.Load();
            SceneManager.sceneLoaded -= Apply;
            SceneManager.sceneLoaded += Apply;
        }

        /// <summary>불러온 씬의 비활성 PlayerLook까지 감도를 적용한다. 기본 입력 에셋이 준비되면 키는 처음 한 번만 불러온다.</summary>
        public static void Apply(Scene scene, LoadSceneMode mode)
        {
            AudioListener.volume = GameSettings.Volume;
            GameSettings.ApplyFrameLimit(GameSettings.FrameLimit);
            if (scene.IsValid() && scene.isLoaded)
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var look in root.GetComponentsInChildren<PlayerLook>(true))
                        look.SensitivityScale = GameSettings.Sensitivity;

            var actions = InputSystem.actions;
            if (!keysLoaded && actions != null)
            {
                KeyBindings.Load(actions);
                keysLoaded = true;
            }
        }
    }
}
