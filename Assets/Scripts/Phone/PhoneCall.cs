using System;
using UnityEngine;

namespace NightPatrol.Phone
{
    /// <summary>통화 대사 한 줄: 화자, 대사, 자막을 보여 줄 시간</summary>
    [Serializable]
    public sealed class PhoneLine
    {
        [Tooltip("자막 앞에 붙는 화자 (관리실, 안내 등). 비우면 대사만")]
        public string speaker = "관리실";
        [Tooltip("자막에 나올 대사")]
        [TextArea] public string text = "";
        [Tooltip("이 줄을 보여 주는 시간(초). 0.5초보다 짧으면 0.5초")]
        public float seconds = 3f;
    }

    /// <summary>통화 한 건. 걸려 오는 통화와 거는 통화가 같은 모양이다 (연결음은 걸 때만)</summary>
    [Serializable]
    public sealed class PhoneCall
    {
        [Tooltip("통화 Id. F1·로그에 보인다")]
        public string id = "";
        [Tooltip("통화 화면·최근 기록·수신 안내에 보이는 이름. 비우면 관리실")]
        public string caller = "관리실";
        [Tooltip("걸 때 연결음이 울리는 시간(초). 0이면 바로 연결. 걸려 온 통화에서는 안 씀")]
        public float ringbackSeconds;
        [Tooltip("자막 글자 일부를 █로 (잡음 섞인 통화)")]
        public bool garbled;
        [Tooltip("대사. 비우면 연결음만 울리고 끊긴다 (받지 않는 전화)")]
        public PhoneLine[] lines = Array.Empty<PhoneLine>();
    }
}
