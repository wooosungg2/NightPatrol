using System;
using System.Collections.Generic;
using NightPatrol.Interaction;
using NightPatrol.Journal;
using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using NightPatrol.Debugging;
#endif

namespace NightPatrol.Player
{
    /// <summary>플레이어의 조작 잠금 편의 기능. 개별 컴포넌트의 부분 잠금은 그대로 사용할 수 있다.</summary>
    [DisallowMultipleComponent]
    public sealed class PlayerControl : MonoBehaviour
    {
        private PlayerMovement movement;
        private PlayerLook look;
        private PlayerInteractor interactor;
        private JournalHolder journal;
        private readonly HashSet<object> owners = new HashSet<object>();

        public static PlayerControl Current { get; private set; }
        public bool IsLocked => owners.Count > 0;

        /// <summary>
        /// 게임 동작 키를 받아도 되는지 (ESC 메뉴·층 이동 연출·창 밖이면 false).
        /// KeyInput은 키만 읽으므로 if (key.WasPressed() &amp;&amp; PlayerControl.CanAct)처럼 같이 쓴다. 일지를 든 동안은 true.
        /// </summary>
        public static bool CanAct => Time.timeScale > 0f && Cursor.lockState == CursorLockMode.Locked &&
            Application.isFocused && (Current == null || !Current.IsLocked);

        private void Awake()
        {
            movement = GetComponent<PlayerMovement>();
            look = GetComponent<PlayerLook>();
            interactor = GetComponent<PlayerInteractor>();
            journal = GetComponentInChildren<JournalHolder>(true);
        }

        private void OnEnable()
        {
            if (Current == null) Current = this;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            else if (Current != this)
                Debug.LogWarning($"[Player] 플레이어가 둘 이상 — 진행·연출이 엉뚱한 플레이어를 잡을 수 있음: {Current.name}, {name}", this);
            DevInfo.Register(this, "조작 잠금", LockInfo, 40);
#endif
        }

        private void OnDisable()
        {
            if (Current == this) Current = null;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            DevInfo.Unregister(this);
#endif
        }

        /// <summary>같은 owner로 이동·시점·E·일지를 잠그거나 푼다. 메뉴는 includeJournal을 false로 사용한다.</summary>
        public void Lock(object owner, bool locked, bool includeJournal = true)
        {
            if (owner == null) return;
            if (locked) owners.Add(owner);
            else owners.Remove(owner);
            if (movement != null) movement.SetBlocked(owner, locked);
            if (look != null) look.SetBlocked(owner, locked);
            if (interactor != null) interactor.SetBlocked(owner, locked);
            if (includeJournal && journal != null) journal.SetBlocked(owner, locked);
        }

        /// <summary>전달된 주체만 읽어 이름 목록을 만든다. 씬 검색이나 잠금 상태 변경 없이 이름순으로 정렬한다.</summary>
        public static IReadOnlyList<string> BlockerNames(IEnumerable<object> blockers)
        {
            var names = new List<string>();
            if (blockers == null) return names;
            foreach (var owner in blockers)
            {
                if (ReferenceEquals(owner, null)) continue;
                string type = owner.GetType().Name;
                names.Add(owner is UnityEngine.Object obj
                    ? $"{type}({(obj != null ? obj.name : "파괴됨")})" : type);
            }
            names.Sort(StringComparer.Ordinal);
            return names;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private string LockInfo() =>
            "전체: " + BlockerText(owners) +
            "\n이동: " + BlockerText(movement != null ? movement.Blockers : null) +
            "\n시점: " + BlockerText(look != null ? look.Blockers : null) +
            "\nE: " + BlockerText(interactor != null ? interactor.Blockers : null) +
            "\n일지: " + BlockerText(journal != null ? journal.Blockers : null);

        private static string BlockerText(IEnumerable<object> blockers)
        {
            var names = BlockerNames(blockers);
            return names.Count == 0 ? DevInfo.Paint("없음", DevInfo.OkColor) : string.Join(", ", names);
        }
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Current = null;
    }
}
