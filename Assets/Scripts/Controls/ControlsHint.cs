using System;
using System.Collections.Generic;
using System.Linq;

namespace NightPatrol.Controls
{
    /// <summary>
    /// 첫 시작 조작 힌트 문구 (InteractionPromptView의 기본 꺼짐 옵션). 지금 설정된 키 이름으로 만든다.
    /// 한 동작의 합성 파트(이동의 앞·왼쪽·뒤·오른쪽)는 "W/A/S/D 이동" 한 묶음, 나머지는 "E 상호작용"처럼, 끝에 "ESC 메뉴"
    /// </summary>
    public static class ControlsHint
    {
        /// <summary>힌트 한 칸: 동작 이름(같으면 한 묶음), 합성 파트(단일 키면 빈 값), 표시 이름, 지금 키 이름</summary>
        public readonly struct Key
        {
            public Key(string action, string part, string label, string name)
            {
                Action = action ?? string.Empty;
                Part = part ?? string.Empty;
                Label = label ?? string.Empty;
                Name = name ?? string.Empty;
            }

            public string Action { get; }
            public string Part { get; }
            public string Label { get; }
            public string Name { get; }
        }

        public const string Separator = " · ";
        /// <summary>ESC는 다시 지정할 수 없는 메뉴 키라 늘 끝에 붙인다</summary>
        public const string MenuHint = "ESC 메뉴";
        // 합성 파트를 보여 주는 순서 (W/A/S/D)
        private static readonly string[] PartOrder = { "up", "left", "down", "right" };

        /// <summary>힌트 문구. 키 이름이 비었거나 "-"(못 찾음)인 칸은 뺀다</summary>
        public static string Text(IReadOnlyList<Key> keys)
        {
            var parts = new List<string>();
            var grouped = new HashSet<string>(StringComparer.Ordinal);
            if (keys != null)
                for (int i = 0; i < keys.Count; i++)
                {
                    var key = keys[i];
                    if (key.Part.Length == 0)
                    {
                        if (Usable(key.Name)) parts.Add($"{key.Name} {key.Label}");
                        continue;
                    }
                    if (!grouped.Add(key.Action)) continue;   // 같은 동작의 파트들은 처음 나온 자리에 한 번
                    var names = keys.Skip(i)
                        .Where(k => k.Action == key.Action && k.Part.Length > 0 && Usable(k.Name))
                        .OrderBy(k => Order(k.Part))
                        .Select(k => k.Name)
                        .ToList();
                    if (names.Count > 0) parts.Add(string.Join("/", names) + " 이동");   // 합성 파트는 이동(Move)만 씀
                }
            parts.Add(MenuHint);
            return string.Join(Separator, parts);
        }

        /// <summary>조작 키 목록의 줄마다 지금 키 이름 (설정에서 바꾼 키 포함)</summary>
        public static List<Key> From(KeyBindingList list)
        {
            var keys = new List<Key>();
            if (list == null) return keys;
            foreach (var entry in list.Entries)
            {
                var action = entry?.action != null ? entry.action.action : null;
                if (action == null) continue;
                keys.Add(new Key(action.name, entry.compositePart, entry.label, KeyBindings.KeyName(action, entry.BindingIndex)));
            }
            return keys;
        }

        private static int Order(string part)
        {
            int index = Array.IndexOf(PartOrder, part.ToLowerInvariant());
            return index < 0 ? PartOrder.Length : index;
        }

        private static bool Usable(string name) => name.Length > 0 && name != "-";
    }
}
