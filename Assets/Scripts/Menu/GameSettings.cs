using System;
using System.Globalization;
using UnityEngine;

namespace NightPatrol.Menu
{
    /// <summary>
    /// 플레이어 설정: 마우스 감도 배율, 소리 크기, 프레임 제한. PlayerPrefs에 저장해 다음 실행에도 유지한다.
    /// (해상도·화면 모드는 Unity가 따로 기억한다 — ScreenOptions)
    /// 한 칸 올리기·범위·표시 글은 순수 함수 (Unity 없이 테스트)
    /// </summary>
    public static class GameSettings
    {
        public const string SensitivityKey = "NightPatrol.MouseSensitivity";
        public const string VolumeKey = "NightPatrol.Volume";
        public const string FrameLimitKey = "NightPatrol.FrameLimit";
        public const float MinSensitivity = 0.2f;
        public const float MaxSensitivity = 3f;
        public const float SensitivityStep = 0.1f;
        public const float VolumeStep = 0.1f;
        /// <summary>프레임 제한 선택지 (이 순서로 넘김): 0 = 수직 동기(모니터 주사율), 숫자 = 초당 프레임 상한, −1 = 무제한</summary>
        public static readonly int[] FrameLimits = { 0, 30, 60, 120, 144, -1 };

        private static float sensitivity = 1f;
        private static float volume = 1f;
        private static int frameLimit;

        /// <summary>마우스 감도 배율 (1 = PlayerSettings 그대로, 0.2~3)</summary>
        public static float Sensitivity
        {
            get => sensitivity;
            set => sensitivity = ClampSensitivity(value);
        }

        /// <summary>전체 소리 크기 (0~1, AudioListener.volume)</summary>
        public static float Volume
        {
            get => volume;
            set => volume = ClampVolume(value);
        }

        /// <summary>프레임 제한 (FrameLimits 중 하나, 기본 수직 동기)</summary>
        public static int FrameLimit
        {
            get => frameLimit;
            set => frameLimit = ValidFrameLimit(value);
        }

        /// <summary>저장된 값 읽기 (없으면 기본값: 감도 1, 소리 1, 수직 동기)</summary>
        public static void Load()
        {
            Sensitivity = PlayerPrefs.GetFloat(SensitivityKey, 1f);
            Volume = PlayerPrefs.GetFloat(VolumeKey, 1f);
            FrameLimit = PlayerPrefs.GetInt(FrameLimitKey, 0);
        }

        public static void Save()
        {
            PlayerPrefs.SetFloat(SensitivityKey, Sensitivity);
            PlayerPrefs.SetFloat(VolumeKey, Volume);
            PlayerPrefs.SetInt(FrameLimitKey, FrameLimit);
            PlayerPrefs.Save();
        }

        /// <summary>선택지에 없는 값이면 수직 동기(0)</summary>
        public static int ValidFrameLimit(int value) => Array.IndexOf(FrameLimits, value) >= 0 ? value : 0;

        /// <summary>프레임 제한을 다음(+)·이전(−) 선택지로. 끝에서는 그대로</summary>
        public static int StepFrameLimit(int value, int direction)
        {
            int index = Array.IndexOf(FrameLimits, ValidFrameLimit(value));
            index = Mathf.Clamp(index + Math.Sign(direction), 0, FrameLimits.Length - 1);
            return FrameLimits[index];
        }

        /// <summary>메뉴 표시: "수직 동기" / "60 FPS" / "무제한"</summary>
        public static string FrameLimitLabel(int value)
        {
            value = ValidFrameLimit(value);
            return value == 0 ? "수직 동기" : value < 0 ? "무제한" : $"{value} FPS";
        }

        /// <summary>
        /// 프레임 제한 적용. 수직 동기는 빌드에서만 켠다 —
        /// 에디터에서 QualitySettings를 바꾸면 프로젝트 품질 설정 파일에 그대로 저장돼 버리므로
        /// </summary>
        public static void ApplyFrameLimit(int value)
        {
            value = ValidFrameLimit(value);
#if !UNITY_EDITOR
            QualitySettings.vSyncCount = value == 0 ? 1 : 0;
#endif
            Application.targetFrameRate = value > 0 ? value : -1;
        }

        /// <summary>감도를 한 칸(0.1) 올림(+) 또는 내림(−). 범위 끝에서는 그대로</summary>
        public static float StepSensitivity(float value, int direction) =>
            direction == 0 ? ClampSensitivity(value)
                : ClampSensitivity(Snap(value + (direction > 0 ? SensitivityStep : -SensitivityStep), SensitivityStep));

        /// <summary>소리 크기를 한 칸(10%) 올림(+) 또는 내림(−). 범위 끝에서는 그대로</summary>
        public static float StepVolume(float value, int direction) =>
            direction == 0 ? ClampVolume(value)
                : ClampVolume(Snap(value + (direction > 0 ? VolumeStep : -VolumeStep), VolumeStep));

        public static float ClampSensitivity(float value) =>
            float.IsNaN(value) ? 1f : Mathf.Clamp(value, MinSensitivity, MaxSensitivity);

        public static float ClampVolume(float value) => float.IsNaN(value) ? 1f : Mathf.Clamp01(value);

        /// <summary>메뉴 표시: "1.0"</summary>
        public static string SensitivityLabel(float value) =>
            ClampSensitivity(value).ToString("0.0", CultureInfo.InvariantCulture);

        /// <summary>메뉴 표시: "80%"</summary>
        public static string VolumeLabel(float value) => $"{Mathf.RoundToInt(ClampVolume(value) * 100f)}%";

        // 0.1씩 더하다 생기는 0.30000001 같은 오차를 칸에 맞춘다
        private static float Snap(float value, float step) => Mathf.Round(value / step) * step;
    }
}
