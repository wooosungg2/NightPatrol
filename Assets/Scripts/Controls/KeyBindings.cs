using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NightPatrol.Controls
{
    /// <summary>
    /// 키 설정: 바꾼 키를 PlayerPrefs에 저장·불러오기, 키 하나 다시 지정, 기본값으로, 키 이름.
    /// 게임 코드는 Input Actions 에셋의 액션을 그대로 쓰므로(복제하지 않음) 바꾼 키가 바로 먹힌다.
    /// 다시 지정할 수 있는 키: 키보드 키와 마우스 오른쪽·가운데·옆 버튼. ESC(메뉴·취소)와 개발자 키(F1~F5, PgUp/PgDn)는 받지 않는다
    /// </summary>
    public static class KeyBindings
    {
        public const string PrefsKey = "NightPatrol.KeyBindings";
        public const string KeyboardMouse = "Keyboard&Mouse";

        // 다시 지정할 때 받지 않는 키: 메뉴·취소, 개발자 키, 실제 키가 아닌 것,
        // 이동의 고정 보조 키(방향키 — 목록에 없어서 다른 줄에 주면 겹침 맞바꿈에 안 잡히고 이동과 같이 눌림)
        private static readonly string[] Reserved =
        {
            "<Keyboard>/escape", "<Keyboard>/anyKey",
            "<Keyboard>/f1", "<Keyboard>/f2", "<Keyboard>/f3", "<Keyboard>/f4", "<Keyboard>/f5",
            "<Keyboard>/pageUp", "<Keyboard>/pageDown",
            "<Keyboard>/upArrow", "<Keyboard>/downArrow", "<Keyboard>/leftArrow", "<Keyboard>/rightArrow",
        };

        // 마우스는 이 버튼만 (왼쪽 버튼은 메뉴를 누르는 버튼이라 받으면 누르자마자 지정돼 버림)
        private static readonly string[] MouseButtons =
            { "<Mouse>/rightButton", "<Mouse>/middleButton", "<Mouse>/forwardButton", "<Mouse>/backButton" };

        private static InputActionRebindingExtensions.RebindingOperation operation;

        /// <summary>키 설정이 바뀌었을 때 (불러오기·다시 지정·기본값). 화면 문구의 키 이름 갱신 등</summary>
        public static event Action Changed;

        /// <summary>지금 키 입력을 기다리는 중인지</summary>
        public static bool IsRebinding => operation != null;

        /// <summary>
        /// 바꿀 바인딩 번호: compositePart가 있으면 그 이름의 합성 파트, 없으면 합성이 아닌 바인딩 중
        /// group에 속한 첫 번째 (못 찾으면 −1)
        /// </summary>
        public static int FindBindingIndex(InputAction action, string compositePart, string group)
        {
            if (action == null) return -1;
            var bindings = action.bindings;
            bool wantPart = !string.IsNullOrEmpty(compositePart);
            for (int i = 0; i < bindings.Count; i++)
            {
                var binding = bindings[i];
                if (binding.isComposite || binding.isPartOfComposite != wantPart) continue;
                if (wantPart && !string.Equals(binding.name, compositePart, StringComparison.OrdinalIgnoreCase)) continue;
                if (!string.IsNullOrEmpty(group) && !InGroup(binding.groups, group)) continue;
                return i;
            }
            return -1;
        }

        /// <summary>그 바인딩의 지금 키 이름 (예: "W", "Tab", "마우스 오른쪽")</summary>
        public static string KeyName(InputAction action, int bindingIndex) =>
            action == null || bindingIndex < 0 || bindingIndex >= action.bindings.Count
                ? "-" : KeyName(action.bindings[bindingIndex].effectivePath);

        /// <summary>컨트롤 경로 → 키 이름 (키보드는 키캡 글자, 마우스는 한국어)</summary>
        public static string KeyName(string path)
        {
            if (string.IsNullOrEmpty(path)) return "-";
            switch (path)
            {
                case "<Mouse>/leftButton": return "마우스 왼쪽";
                case "<Mouse>/rightButton": return "마우스 오른쪽";
                case "<Mouse>/middleButton": return "마우스 가운데";
                case "<Mouse>/forwardButton": return "마우스 앞";
                case "<Mouse>/backButton": return "마우스 뒤";
            }
            string name = InputControlPath.ToHumanReadableString(path, InputControlPath.HumanReadableStringOptions.OmitDevice);
            return string.IsNullOrEmpty(name) ? path : name;
        }

        /// <summary>저장된 키 설정을 에셋에 적용 (먼저 기본값으로 되돌린 뒤)</summary>
        public static void Load(InputActionAsset asset, string prefsKey = PrefsKey)
        {
            if (asset == null) return;
            asset.RemoveAllBindingOverrides();
            string json = PlayerPrefs.GetString(prefsKey, string.Empty);
            if (!string.IsNullOrEmpty(json))
            {
                try { asset.LoadBindingOverridesFromJson(json); }
                catch (Exception e) { Debug.LogWarning($"[Keys] 저장된 키 설정을 읽지 못해 기본값을 씁니다: {e.Message}"); }
            }
            SafeEvent.Raise(Changed, null);
        }

        public static void Save(InputActionAsset asset, string prefsKey = PrefsKey)
        {
            if (asset == null) return;
            PlayerPrefs.SetString(prefsKey, asset.SaveBindingOverridesAsJson());
            PlayerPrefs.Save();
        }

        /// <summary>모든 키를 기본값으로 (저장까지)</summary>
        public static void ResetAll(InputActionAsset asset, string prefsKey = PrefsKey)
        {
            if (asset == null) return;
            asset.RemoveAllBindingOverrides();
            Save(asset, prefsKey);
            SafeEvent.Raise(Changed, null);
        }

        /// <summary>
        /// 키 하나를 다시 지정: 다음에 누르는 키(키보드, 마우스 오른쪽·가운데·옆)로. ESC = 취소.
        /// 지정하는 동안 그 동작은 잠시 끄고 끝나면 원래대로. done(true) = 바뀜, done(false) = 취소
        /// </summary>
        public static void StartRebind(InputAction action, int bindingIndex, Action<bool> done)
        {
            Cancel();
            if (action == null || bindingIndex < 0 || bindingIndex >= action.bindings.Count)
            {
                done?.Invoke(false);
                return;
            }
            bool wasEnabled = action.enabled;
            if (wasEnabled) action.Disable();   // 켜진 액션은 다시 지정할 수 없음
            var rebind = action.PerformInteractiveRebinding(bindingIndex)
                .WithControlsHavingToMatchPath("<Keyboard>")
                .WithCancelingThrough("<Keyboard>/escape")
                .OnMatchWaitForAnother(0.05f)
                .OnComplete(_ => Finish(action, wasEnabled, true, done))
                .OnCancel(_ => Finish(action, wasEnabled, false, done));
            foreach (var button in MouseButtons) rebind.WithControlsHavingToMatchPath(button);
            foreach (var path in Reserved) rebind.WithControlsExcluding(path);
            operation = rebind;
            try
            {
                rebind.Start();
            }
            catch
            {
                operation = null;   // 시작을 못 하면 기다리는 상태로 남지 않게 되돌린다
                rebind.Dispose();
                if (wasEnabled) action.Enable();
                throw;
            }
        }

        /// <summary>키 입력 기다리기를 그만둔다 (done(false)가 불림)</summary>
        public static void Cancel()
        {
            if (operation != null) operation.Cancel();
        }

        /// <summary>
        /// 겹침 맞바꿈: rows[changed]가 새 키를 받았는데 다른 줄이 같은 키를 쓰고 있으면, 그 줄에 changed의 이전 키(previousPath)를 준다.
        /// 맞바꾼 줄 번호, 겹침이 없으면 −1
        /// </summary>
        public static int SwapConflict(IReadOnlyList<(InputAction action, int index)> rows, int changed, string previousPath)
        {
            if (rows == null || changed < 0 || changed >= rows.Count) return -1;
            var (action, index) = rows[changed];
            if (action == null || index < 0) return -1;
            string path = action.bindings[index].effectivePath;
            for (int i = 0; i < rows.Count; i++)
            {
                if (i == changed) continue;
                var (other, otherIndex) = rows[i];
                if (other == null || otherIndex < 0) continue;
                if (other == action && otherIndex == index) continue;
                if (!string.Equals(other.bindings[otherIndex].effectivePath, path, StringComparison.OrdinalIgnoreCase)) continue;
                if (string.Equals(other.bindings[otherIndex].path, previousPath, StringComparison.OrdinalIgnoreCase))
                    other.RemoveBindingOverride(otherIndex);   // 이전 키가 그 줄의 기본값이면 덮어쓰기 대신 기본으로
                else
                    other.ApplyBindingOverride(otherIndex, previousPath);
                return i;
            }
            return -1;
        }

        private static void Finish(InputAction action, bool wasEnabled, bool changed, Action<bool> done)
        {
            var finished = operation;
            operation = null;
            finished?.Dispose();
            if (wasEnabled) action.Enable();
            done?.Invoke(changed);
            if (changed) SafeEvent.Raise(Changed, null);
        }

        private static bool InGroup(string groups, string group)
        {
            if (string.IsNullOrEmpty(groups)) return false;
            foreach (var part in groups.Split(';'))
                if (string.Equals(part, group, StringComparison.Ordinal)) return true;
            return false;
        }

        // 에디터에서 도메인 리로드를 꺼도 지난 Play의 구독자·기다리던 입력이 남지 않게
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            operation = null;
            Changed = null;
        }
    }
}
