using System;
using System.Collections.Generic;
using NightPatrol.Run;
using UnityEngine;

namespace NightPatrol.Audio
{
    /// <summary>
    /// 사물에서 나는 소리 (3D: 붙인 위치에서 방향·거리에 따라 들림). 문·창문·모니터·엘리베이터·이상현상 어디에나 붙인다.
    /// 이름 붙인 소리 묶음을 코드에서 Play("열림")으로 낸다. 언제 낼지는 그 사물 코드가 정한다.
    /// 층이 시작되는 동안(사물 초기화·점검 항목 만들기)과 씬을 막 불러온 순간에는 내지 않는다 — 층마다 모든 문이 한꺼번에 울리지 않게.
    /// 파일이 비어 있는 묶음은 조용하다. 전체 소리 크기와 일시정지는 ESC 메뉴를 따른다 (AudioListener).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SoundEmitter : MonoBehaviour
    {
        /// <summary>코드 없이 저절로 울릴 시스템 연출 순간 (층 이동 연출, FloorTransition)</summary>
        public enum Moment
        {
            None,
            /// <summary>하행 연출 시작 (버튼·문 닫힘·구동음)</summary>
            DescendStarted,
            /// <summary>하행 성공으로 도착, "N층"을 띄우기 직전 (띵)</summary>
            Arrived,
        }

        [Serializable]
        public sealed class Cue
        {
            [Tooltip("부를 때 쓰는 이름 (예: 열림 → Play(\"열림\"))")]
            public string name = "";
            [Tooltip("소리 파일. 여러 개면 무작위로 고르고, 방금 낸 것은 바로 다시 고르지 않음")]
            public AudioClip[] clips = new AudioClip[0];
            [Tooltip("음량 (0~1). ESC 메뉴의 전체 소리 크기와 곱해진다")]
            [Range(0f, 1f)] public float volume = 1f;
            [Tooltip("높낮이를 이만큼 무작위로 (0.05 = ±5%, 반복이 덜 거슬리게). 0이면 늘 원래 높이 (띵처럼 늘 같아야 하는 소리)")]
            [Range(0f, 0.3f)] public float pitchJitter = 0.05f;
            [Tooltip("코드 없이 저절로 울릴 순간 (엘리베이터: 하행 시작·도착). None이면 Play로만")]
            public Moment playOn = Moment.None;
        }

        [Tooltip("소리 묶음 (이름·파일·음량·높낮이·저절로 울릴 순간)")]
        [SerializeField] private Cue[] cues = new Cue[0];
        [Tooltip("이 거리(m) 안에서는 원래 크기")]
        [SerializeField, Min(0.1f)] private float fullVolumeDistance = 1f;
        [Tooltip("이 거리(m)부터 안 들림. 그 사이는 멀수록 작아짐 (벽은 막지 않음)")]
        [SerializeField, Min(0.2f)] private float silentDistance = 20f;

        private readonly HashSet<string> warnedNames = new HashSet<string>();
        private AudioSource[] sources = new AudioSource[0];   // 묶음마다 하나 (높낮이가 서로 섞이지 않게)
        private int[] lastPicked = new int[0];
        private FloorTransition transition;

        /// <summary>
        /// 지금은 소리를 내지 않는 때인지: 층을 시작하는 중(PatrolRun.IsStartingFloor)이거나 씬을 막 불러온 순간.
        /// 이때 사물 상태가 한꺼번에 바뀐다 (사물 초기화, 점검 항목 만들기).
        /// </summary>
        public static bool IsQuiet
        {
            get
            {
                var run = PatrolRun.Current;
                return (run != null && run.IsStartingFloor) || Time.timeSinceLevelLoad <= 0f;
            }
        }

        // 테스트·진단용: 실제로 낸 횟수와 마지막 소리
        internal int PlayCount { get; private set; }
        internal AudioClip LastClip { get; private set; }
        internal float LastPitch { get; private set; } = 1f;

        /// <summary>이 이름의 소리 묶음이 있는지</summary>
        public bool Has(string cueName) => IndexOf(cueName) >= 0;

        /// <summary>
        /// 이름이 같은 소리 묶음을 낸다. 없는 이름이면 이름마다 경고 한 번.
        /// 층을 시작하는 중·꺼진 오브젝트·파일이 빈 묶음이면 조용히 넘어간다.
        /// </summary>
        public void Play(string cueName)
        {
            int index = IndexOf(cueName);
            if (index < 0)
            {
                if (warnedNames.Add(cueName ?? ""))
                    Debug.LogWarning($"[Sound] {name}: 소리 묶음 '{cueName}'이 없음 — SoundEmitter의 이름 확인", this);
                return;
            }
            PlayCue(index);
        }

        /// <summary>
        /// 이 이름의 소리 묶음을 멈춘다 (긴 소리를 끊을 때 — 예: 전화를 받으면 벨). 묶음마다 소리 나는 곳이 하나라 그 묶음의 소리만 멈춘다.
        /// 없는 이름이거나 아직 낸 적 없는 묶음이면 조용히 넘어간다
        /// </summary>
        public void Stop(string cueName)
        {
            int index = IndexOf(cueName);
            if (index < 0 || index >= sources.Length || sources[index] == null) return;
            sources[index].Stop();
        }

        private void OnEnable()
        {
            if (!HasMoments()) return;
            if (transition == null) transition = UnityEngine.Object.FindAnyObjectByType<FloorTransition>();
            if (transition == null) return;
            transition.DescendStarted += OnDescendStarted;
            transition.Arrived += OnArrived;
        }

        private void OnDisable()
        {
            if (transition == null) return;
            transition.DescendStarted -= OnDescendStarted;
            transition.Arrived -= OnArrived;
        }

        private void OnDescendStarted() => PlayMoment(Moment.DescendStarted);
        private void OnArrived(int floor) => PlayMoment(Moment.Arrived);

        /// <summary>이 순간에 울리게 한 묶음을 모두 낸다</summary>
        internal void PlayMoment(Moment moment)
        {
            if (moment == Moment.None || cues == null) return;
            for (int i = 0; i < cues.Length; i++)
                if (cues[i] != null && cues[i].playOn == moment) PlayCue(i);
        }

        private bool HasMoments()
        {
            if (cues == null) return false;
            foreach (var cue in cues)
                if (cue != null && cue.playOn != Moment.None) return true;
            return false;
        }

        private int IndexOf(string cueName)
        {
            if (cues == null || string.IsNullOrEmpty(cueName)) return -1;
            for (int i = 0; i < cues.Length; i++)
                if (cues[i] != null && cues[i].name == cueName) return i;
            return -1;
        }

        private void PlayCue(int index)
        {
            if (IsQuiet || !isActiveAndEnabled) return;
            var cue = cues[index];
            if (cue == null || cue.clips == null || cue.clips.Length == 0) return;

            EnsureSlots();
            int pick = PlayerSounds.PickIndex(cue.clips.Length, lastPicked[index], UnityEngine.Random.Range(0, int.MaxValue));
            lastPicked[index] = pick;
            var clip = cue.clips[pick];
            if (clip == null) return;

            var source = sources[index] != null ? sources[index] : sources[index] = CreateSource();
            float jitter = Mathf.Clamp(cue.pitchJitter, 0f, 0.3f);
            source.pitch = 1f + UnityEngine.Random.Range(-jitter, jitter);
            source.PlayOneShot(clip, Mathf.Clamp01(cue.volume));
            PlayCount++;
            LastClip = clip;
            LastPitch = source.pitch;
        }

        // Play 중에 Inspector에서 묶음 수를 바꿔도 맞춘다
        private void EnsureSlots()
        {
            int count = cues.Length;
            if (sources.Length == count) return;
            Array.Resize(ref sources, count);
            int old = lastPicked.Length;
            Array.Resize(ref lastPicked, count);
            for (int i = old; i < count; i++) lastPicked[i] = -1;
        }

        private AudioSource CreateSource()
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1f;      // 3D: 방향·거리
            source.dopplerLevel = 0f;      // 걸을 때 높낮이가 출렁이지 않게
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = fullVolumeDistance;
            source.maxDistance = Mathf.Max(silentDistance, fullVolumeDistance + 0.1f);
            return source;
        }
    }
}
