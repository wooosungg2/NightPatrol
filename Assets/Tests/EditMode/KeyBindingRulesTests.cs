using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NightPatrol.Controls;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.InputSystem;

namespace NightPatrol.Tests
{
    // 01_06c: 실제 입력 에셋·조작 키 목록 검사. 팀원이 키 동작을 추가하고 목록에 안 넣으면 여기서 실패한다.
    // 키 배정 원칙 (10/11): 기본 키끼리 겹치지 않고, 개발자 키(ESC·F1~F7·PgUp/PgDn)를 쓰지 않는다.
    public class KeyBindingRulesTests
    {
        private KeyBindingList list;
        private InputActionMap player;

        [SetUp]
        public void SetUp()
        {
            var guids = AssetDatabase.FindAssets("t:KeyBindingList");
            Assert.AreEqual(1, guids.Length, "조작 키 목록(KeyBindingList) 에셋은 프로젝트에 하나여야 함");
            list = AssetDatabase.LoadAssetAtPath<KeyBindingList>(AssetDatabase.GUIDToAssetPath(guids[0]));
            Assert.IsNotNull(list.Asset, "조작 키 목록의 동작이 비어 있음");
            player = list.Asset.FindActionMap("Player", throwIfNotFound: true);
        }

        private static bool IsKey(string path) =>
            path.StartsWith("<Keyboard>/") || (path.StartsWith("<Mouse>/") && path.EndsWith("Button"));

        private static bool InKeyboardMouse(InputBinding b) =>
            (b.groups ?? string.Empty).Split(';').Contains(KeyBindings.KeyboardMouse);

        // (동작, 합성 파트) 별 기본 키. Move는 첫 합성(WASD)만 — 방향키 보조 바인딩은 메뉴에서 안 바꿈
        private IEnumerable<(string action, string part, string path)> DefaultKeys()
        {
            foreach (var action in player.actions)
            {
                var seenParts = new HashSet<string>();
                bool seenSingle = false;
                foreach (var b in action.bindings)
                {
                    if (b.isComposite || !IsKey(b.path) || !InKeyboardMouse(b)) continue;
                    if (b.isPartOfComposite)
                    {
                        if (seenParts.Add(b.name.ToLowerInvariant())) yield return (action.name, b.name.ToLowerInvariant(), b.path);
                    }
                    else if (!seenSingle)
                    {
                        seenSingle = true;
                        yield return (action.name, string.Empty, b.path);
                    }
                }
            }
        }

        [Test]
        public void EveryKeyAction_IsInTheKeyList()
        {
            var listed = new HashSet<(string, string)>(list.Entries
                .Where(e => e?.action != null && e.action.action != null)
                .Select(e => (e.action.action.name, (e.compositePart ?? string.Empty).ToLowerInvariant())));
            var missing = DefaultKeys().Where(k => !listed.Contains((k.action, k.part)))
                .Select(k => k.part.Length > 0 ? $"{k.action}/{k.part}" : k.action).ToList();
            Assert.IsEmpty(missing, "설정 → 조작 키에 안 나오는 키 동작: " + string.Join(", ", missing) +
                " — KeyBindingList에 한 줄 추가 (가이드 \"조작 키\")");
        }

        [Test]
        public void DefaultKeys_DoNotOverlap()
        {
            var overlaps = DefaultKeys().GroupBy(k => k.path.ToLowerInvariant()).Where(g => g.Count() > 1)
                .Select(g => g.Key + " ← " + string.Join(", ", g.Select(k => k.part.Length > 0 ? $"{k.action}/{k.part}" : k.action)))
                .ToList();
            Assert.IsEmpty(overlaps, "기본 키가 겹침 (먼저 쓴 쪽 유지, 새 동작이 다른 키로): " + string.Join(" / ", overlaps));
        }

        [Test]
        public void DefaultKeys_AvoidReservedDevKeys()
        {
            var reserved = (string[])typeof(KeyBindings).GetField("Reserved", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            var bad = DefaultKeys().Where(k => reserved.Contains(k.path, System.StringComparer.OrdinalIgnoreCase))
                .Select(k => $"{k.action} ({k.path})").ToList();
            Assert.IsEmpty(bad, "개발자·메뉴 키는 기본 키로 못 씀: " + string.Join(", ", bad));
        }

        // 10/24 (00_15 검사): 목록에 없는 고정 키(이동의 방향키)를 다른 줄에 지정하면 겹침 맞바꿈에 안 잡혀
        // 두 동작이 같이 눌림 (뒤로를 ↑로 → 전진·후진이 겹침). 그래서 다시 지정할 때 받지 않는다
        [Test]
        public void FixedKeys_NotInTheList_CannotBeChosenWhenRebinding()
        {
            var reserved = (string[])typeof(KeyBindings).GetField("Reserved", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            var listed = new HashSet<string>(DefaultKeys().Select(k => k.path), System.StringComparer.OrdinalIgnoreCase);
            var open = new List<string>();
            foreach (var action in player.actions)
                foreach (var b in action.bindings)
                {
                    if (b.isComposite || !IsKey(b.path) || !InKeyboardMouse(b) || listed.Contains(b.path)) continue;
                    if (!reserved.Contains(b.path, System.StringComparer.OrdinalIgnoreCase)) open.Add($"{action.name} ({b.path})");
                }
            Assert.IsEmpty(open, "목록에 없는 고정 키는 다시 지정할 때 막아야 함 (KeyBindings.Reserved): " + string.Join(", ", open));
            Assert.IsTrue(reserved.Contains("<Keyboard>/upArrow"), "방향키는 이동의 고정 보조 키");
        }

        // 목록 에셋을 탐색기에서 옮기거나 복사하면 GUID가 바뀌어 메뉴 연결이 끊긴다 (10/11 실제로 있었음)
        [Test]
        public void EveryPauseMenuPrefab_UsesTheKeyList()
        {
            var broken = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(path);
                if (prefab == null) continue;
                foreach (var menu in prefab.GetComponentsInChildren<NightPatrol.Menu.PauseMenu>(true))
                {
                    var assigned = new SerializedObject(menu).FindProperty("keyBindings").objectReferenceValue;
                    if (assigned != list) broken.Add(path);
                }
            }
            Assert.IsEmpty(broken, "PauseMenu의 Key Bindings 칸이 조작 키 목록을 가리키지 않음 (Inspector에서 다시 연결): " + string.Join(", ", broken));
        }

        [Test]
        public void KeyList_HasTheCurrentGameKeys()
        {
            var labels = list.Entries.Select(e => e.label).ToList();
            CollectionAssert.IsSubsetOf(new[] { "앞으로", "뒤로", "왼쪽", "오른쪽", "상호작용", "근무일지", "손전등", "전화" }, labels);
        }
    }
}
