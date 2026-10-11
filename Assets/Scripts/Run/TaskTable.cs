using System;
using System.Collections.Generic;
using UnityEngine;

namespace NightPatrol.Run
{
    // 점검 항목 표. 층이 시작될 때 FloorTasks가 여기서 몇 개를 뽑는다.
    // 대상은 사물 Id로 지정한다 (생성기로 사물을 다시 만들어도 Id가 같으면 그대로 연결됨)
    [CreateAssetMenu(menuName = "Night Patrol/Task Table")]
    public sealed class TaskTable : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [Tooltip("일지에 적힐 문구 (예: 524호 블라인드 내리기). 비우면 대상 Id를 적는다")]
            public string label;
            [Tooltip("대상 사물 Id. 여러 개면 그중 하나만 비정상이 된다 (예: 교수실 문들 → 교수실 구역 잠금 확인)")]
            public string[] ids;
        }

        [Tooltip("위에서부터 일지에 적히는 순서 (권장 순찰 순서대로)")]
        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        public IReadOnlyList<Entry> Entries => entries ?? Array.Empty<Entry>();
    }
}
