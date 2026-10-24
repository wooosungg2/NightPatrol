using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Page = NightPatrol.Menu.PauseMenu.Page;
using Row = NightPatrol.Menu.PauseMenu.Row;
using Values = NightPatrol.Menu.PauseMenu.Values;

namespace NightPatrol.Menu
{
    /// <summary>
    /// 일시정지 메뉴의 문구·줄 계산 (순수 함수, Unity 없이 테스트). PauseMenu가 그리고 입력을 처리하고, 여기는 글만 만든다.
    /// 설정 줄을 하나 더하려면: PauseMenu.Row에 이름 → SettingsRows 순서 → Label·Value(값이 있으면) → PauseMenu.Adjust(값을 바꾸는 법)
    /// </summary>
    public static class PauseMenuText
    {
        /// <summary>클립보드 한 장에 들어가는 줄 수 (조작 키 장은 목록 + 기본값으로 + 뒤로)</summary>
        public const int MaxRows = 12;
        // 값 뒤 안내 글 (에디터의 "빌드에서 적용"): 검은 본문보다 흐리고 바닥글처럼 작게
        private const string NoteColor = "#6E6E6E";

        internal static readonly Row[] MainRows = { Row.Continue, Row.Settings, Row.Restart, Row.Quit };
        internal static readonly Row[] SettingsRows =
            { Row.Sensitivity, Row.Volume, Row.ScreenMode, Row.Resolution, Row.FrameLimit, Row.Keys, Row.Back };
        internal static readonly Row[] TitleRows = { Row.Start, Row.Settings, Row.Quit };

        /// <summary>그 장의 줄 순서 (조작 키 장은 키 줄 keyCount개 + 기본값으로 + 뒤로)</summary>
        public static Row[] RowsOf(Page page, int keyCount = 0)
        {
            if (page == Page.Main) return MainRows;
            if (page == Page.Settings) return SettingsRows;
            if (page == Page.Title) return TitleRows;
            keyCount = Mathf.Clamp(keyCount, 0, MaxRows - 2);
            var keys = new Row[keyCount + 2];
            for (int i = 0; i < keyCount; i++) keys[i] = Row.KeyBinding;
            keys[keyCount] = Row.ResetKeys;
            keys[keyCount + 1] = Row.Back;
            return keys;
        }

        /// <summary>
        /// y가 몇 번째 줄인지. 줄 사이 틈은 가운데에서 나눠 빈틈 없이. 어느 줄도 아니면 −1.
        /// tops·bottoms는 위에서부터 각 줄의 위·아래 y (아래로 갈수록 작아짐)
        /// </summary>
        public static int RowAt(float y, IReadOnlyList<float> tops, IReadOnlyList<float> bottoms, int count)
        {
            count = Mathf.Min(count, Mathf.Min(tops?.Count ?? 0, bottoms?.Count ?? 0));
            for (int i = 0; i < count; i++)
            {
                float top = i == 0 ? tops[0] : (bottoms[i - 1] + tops[i]) * 0.5f;
                float bottom = i == count - 1 ? bottoms[i] : (bottoms[i] + tops[i + 1]) * 0.5f;
                if (y <= top && y >= bottom) return i;
            }
            return -1;
        }

        /// <summary>장 제목</summary>
        public static string TitleText(Page page)
        {
            switch (page)
            {
                case Page.Settings: return "설정";
                case Page.Keys: return "조작 키";
                case Page.Title: return "야간순찰";
                default: return "일시정지";
            }
        }

        /// <summary>메뉴 장 본문. 고른 줄 앞에 ▶, 값은 오른쪽에 ◀ 값 ▶ (마우스 판정용 link 태그 포함)</summary>
        public static string BodyText(Page page, int selected, Values values)
        {
            int keyCount = values.KeyLabels?.Count ?? 0;
            var pageRows = RowsOf(page, keyCount);
            var text = new StringBuilder();
            for (int i = 0; i < pageRows.Length; i++)
            {
                var row = pageRows[i];
                if (i == selected) text.Append("<pos=0%>▶");
                text.Append("<pos=8%><link=\"row").Append(i).Append("\">").Append(Label(row, i, values)).Append("</link>");
                if (row == Row.KeyBinding)
                {
                    // 키는 ◀ ▶로 넘기는 값이 아니라 눌러서 새로 받는다
                    text.Append("<pos=50%>").Append(i == values.WaitingKey
                        ? "<color=#8A0000>키를 누르세요</color>"
                        : At(values.KeyNames, i, "-"));
                }
                else
                {
                    string value = Value(row, values);
                    if (value.Length > 0)
                        text.Append("<pos=50%><link=\"dec").Append(i).Append("\"> ◀ </link>").Append(value)
                            .Append("<link=\"inc").Append(i).Append("\"> ▶ </link>");
                }
                text.Append('\n');
            }
            string footer = Footer(page, values.WaitingKey.HasValue);
            if (footer.Length > 0) text.Append("\n<size=70%>").Append(footer).Append("</size>");
            return text.ToString();
        }

        /// <summary>index를 0~count−1 안으로 (끝을 넘으면 반대쪽 끝)</summary>
        public static int Wrap(int index, int count) => count <= 0 ? 0 : ((index % count) + count) % count;

        private static string Footer(Page page, bool waiting)
        {
            if (waiting) return "새 키를 누르세요 · ESC 취소";
            switch (page)
            {
                case Page.Settings: return "클릭 · W/S 선택 · A/D 조절 · ESC 뒤로";
                case Page.Keys: return "클릭·E 키 바꾸기 · 겹치면 서로 바뀜 · ESC 뒤로";
                case Page.Title: return string.Empty;   // 시작 화면은 일반 게임의 첫 화면처럼 안내 글 없이
                default: return "클릭 · W/S 선택 · E 확인 · ESC 닫기";
            }
        }

        private static string Label(Row row, int index, Values values)
        {
            switch (row)
            {
                case Row.Continue: return "계속하기";
                case Row.Settings: return "설정";
                case Row.Restart: return values.RestartArmed ? "정말 처음부터? 한 번 더" : "처음으로";
                case Row.Quit: return values.QuitArmed ? "정말 종료? 한 번 더" : "게임 종료";
                case Row.Sensitivity: return "마우스 감도";
                case Row.Volume: return "소리 크기";
                case Row.ScreenMode: return "화면 모드";
                case Row.Resolution: return "해상도";
                case Row.FrameLimit: return "프레임 제한";
                case Row.Keys: return "조작 키";
                case Row.KeyBinding: return At(values.KeyLabels, index, "?");
                case Row.ResetKeys: return "기본값으로";
                case Row.Start: return "근무 시작";
                default: return "뒤로";
            }
        }

        private static string Value(Row row, Values values)
        {
            switch (row)
            {
                case Row.Sensitivity: return GameSettings.SensitivityLabel(values.Sensitivity);
                case Row.Volume: return GameSettings.VolumeLabel(values.Volume);
                case Row.ScreenMode: return DimNote(ScreenOptions.ScreenModeLabel(values.FullScreen, values.InEditor));
                case Row.Resolution: return DimNote(ScreenOptions.ResolutionLabel(values.Resolution, values.InEditor));
                case Row.FrameLimit: return GameSettings.FrameLimitLabel(values.FrameLimit);
                default: return string.Empty;
            }
        }

        // 에디터 안내(" (빌드에서 적용)")만 흐리고 작게. 안내가 없으면 그대로
        private static string DimNote(string label) =>
            label.EndsWith(ScreenOptions.EditorNote, StringComparison.Ordinal)
                ? label.Substring(0, label.Length - ScreenOptions.EditorNote.Length) +
                  $"<size=70%><color={NoteColor}>{ScreenOptions.EditorNote}</color></size>"
                : label;

        private static string At(IReadOnlyList<string> list, int index, string fallback) =>
            list != null && index >= 0 && index < list.Count && !string.IsNullOrEmpty(list[index]) ? list[index] : fallback;
    }
}
