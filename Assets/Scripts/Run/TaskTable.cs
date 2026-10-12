using System;
using System.Collections.Generic;
using UnityEngine;

namespace NightPatrol.Run
{
    // 점검 항목 표. 층이 시작될 때 FloorTasks가 여기서 몇 개를 뽑는다.
    // 대상은 사물 Id로 지정한다 (생성기로 사물을 다시 만들어도 Id가 같으면 그대로 연결됨).
    // Id 칸에 패턴(*, A~B)을 쓰면 새 사물도 표를 안 고치고 후보가 된다 (03_08, TaskIds)
    [CreateAssetMenu(menuName = "Night Patrol/Task Table")]
    public sealed class TaskTable : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [Tooltip("일지에 적힐 문구. {room} = 고른 대상의 방 이름(524호), {floor} = 지금 층. 예: {room} 블라인드 내리기. 비우면 대상 Id를 적는다")]
            public string label;
            [Tooltip("대상 사물 Id 또는 패턴. 여러 개면 그중 하나만 비정상이 된다. * = 그 칸의 아무 글자 (R21.Monitor.*), A~B = 숫자 범위 (R05~R19.Door.*)")]
            public string[] ids;
            [Tooltip("예외 지시: 대상을 비정상 상태로 두고, 이번 층에는 그 상태가 정상 (예: 521호 강사 PC는 끄지 말 것)")]
            public bool exception;
        }

        /// <summary>구역 코드 → 일지에 적을 이름 (StairW → 서쪽 계단실)</summary>
        [Serializable]
        public sealed class AreaName
        {
            [Tooltip("Id의 첫 칸 (예: StairW)")]
            public string area;
            [Tooltip("{room} 자리에 적을 이름 (예: 서쪽 계단실). {floor}를 쓰면 지금 층")]
            public string name;
        }

        [Tooltip("위에서부터 일지에 적히는 순서 (권장 순찰 순서대로)")]
        [SerializeField] private Entry[] entries = Array.Empty<Entry>();
        [Tooltip("{room}에 적을 구역 이름. 방(R+두 자리, 예: R21)은 없어도 층+번호호(521호)로 적는다. 같은 구역이 두 줄이면 첫 줄")]
        [SerializeField] private AreaName[] areaNames = Array.Empty<AreaName>();
        [Tooltip("방 이름의 층. 0 = 지금 층 (4층에서 R21 → 421호), 5 = 층과 상관없이 521호 (표지판이 층마다 안 바뀔 때)")]
        [Min(0)]
        [SerializeField] private int roomFloor = 0;

        public IReadOnlyList<Entry> Entries => entries ?? Array.Empty<Entry>();
        /// <summary>구역 이름 표 (Inspector 순서)</summary>
        public IReadOnlyList<AreaName> AreaNames => areaNames ?? Array.Empty<AreaName>();
        /// <summary>방 이름의 층 (0 = 지금 층)</summary>
        public int RoomFloor => roomFloor;

        /// <summary>{room}에 들어갈 이름 (TaskIds.RoomName에 이 표의 구역 이름·Room Floor를 넣은 것)</summary>
        public string RoomName(string area, int floor) => TaskIds.RoomName(area, floor, roomFloor, AreaNames);

        /// <summary>이 줄에서 대상 id를 골랐을 때 일지 문구 (floor = 지금 층). 실행과 미리보기가 같이 쓴다</summary>
        public string LabelFor(Entry entry, string id, int floor) =>
            TaskIds.Label(entry?.label, id, RoomName(TaskIds.Area(id), floor), floor);
    }
}
