using System;
using System.Collections.Generic;
using NightPatrol.Anomalies;
using UnityEngine;

namespace NightPatrol.Photo
{
    /// <summary>찍어야 할 것 하나 = 켜진 이상현상 하나. 모양 중 하나라도 찍히면 찍힌 것</summary>
    public sealed class PhotoSubject
    {
        public readonly Anomaly Source;
        public readonly IReadOnlyList<PhotoShape> Shapes;

        public PhotoSubject(Anomaly source, IReadOnlyList<PhotoShape> shapes)
        {
            Source = source;
            Shapes = shapes;
        }

        public string Name => Source != null ? Source.name : "?";
    }

    /// <summary>
    /// 지금 찍어야 할 것 목록. 판정·촬영 장수·플레이 기록이 모두 이 목록 하나를 쓴다.
    /// 이상현상은 공통 부모 Anomaly를 상속하기만 하면 된다 — 켜져 있으면(IsActive) 목록에 들어간다.
    /// 찍을 곳 (앞에서부터 하나만 씀):
    /// 1. 그 이상현상을 맡은 PhotoTarget들 (보이는 물체가 없는 자리 — 예외)
    /// 2. 이상현상이 IPhotoVisual로 알려 준 바깥 물체 (맵의 모니터·등·벨, 실행 중 만들거나 옮긴 물체)
    /// 3. 이상현상 오브젝트와 자식 중 켜진 렌더러 (이미지·여학생처럼 보이는 게 자기 아래에 있는 것)
    /// </summary>
    public static class PhotoSubjects
    {
        private static readonly HashSet<Anomaly> warned = new HashSet<Anomaly>();

        /// <summary>
        /// 켜진 이상현상 중 찍을 곳이 있는 것 (이름 순). 찍을 곳이 없는 이상현상은 요구하지 않고 missing에 넣는다
        /// (예: 바깥 물체를 알려 주지 않았고 자기 아래 렌더러도 없음). 씬 전체를 찾으므로 매 프레임 부르지 말 것
        /// </summary>
        public static List<PhotoSubject> Active(List<Anomaly> missing = null)
        {
            var targets = new Dictionary<Anomaly, List<PhotoTarget>>();
            foreach (var target in UnityEngine.Object.FindObjectsByType<PhotoTarget>(FindObjectsInactive.Include))
            {
                var owner = target != null ? target.Anomaly : null;
                if (owner == null) continue;
                if (!targets.TryGetValue(owner, out var list)) targets.Add(owner, list = new List<PhotoTarget>());
                list.Add(target);
            }

            var result = new List<PhotoSubject>();
            foreach (var anomaly in UnityEngine.Object.FindObjectsByType<Anomaly>(FindObjectsInactive.Exclude))
            {
                if (anomaly == null || !anomaly.IsActive) continue;
                var shapes = new List<PhotoShape>();
                if (targets.TryGetValue(anomaly, out var list))
                {
                    foreach (var target in list) shapes.Add(target.Shape());
                }
                else if (OutsideVisual(anomaly) is GameObject visual)
                {
                    if (PhotoShape.TryFromRenderers(visual, out var shape)) shapes.Add(shape);
                }
                else if (PhotoShape.TryFromRenderers(anomaly.gameObject, out var own))
                    shapes.Add(own);

                if (shapes.Count > 0) result.Add(new PhotoSubject(anomaly, shapes));
                else
                {
                    missing?.Add(anomaly);
                    WarnOnce(anomaly);
                }
            }
            result.Sort((a, b) => StringComparer.Ordinal.Compare(a.Name, b.Name));
            return result;
        }

        // IPhotoVisual이 알려 준 바깥 물체. 구현 안 했거나 null·파괴됨이면 null (자기 아래 렌더러로 넘어감)
        private static GameObject OutsideVisual(Anomaly anomaly)
        {
            if (!(anomaly is IPhotoVisual source)) return null;
            GameObject visual;
            try { visual = source.PhotoVisual; }
            catch (Exception e)
            {
                Debug.LogException(e, anomaly);
                return null;
            }
            return visual != null ? visual : null;   // 파괴된 오브젝트도 null로
        }

        // 찍을 곳이 없는 이상현상은 플레이어가 통과할 수 없게 만들지 않도록 요구에서 빼고, 만든 사람에게 한 번 알린다 (F1 "사진"에도 보임)
        private static void WarnOnce(Anomaly anomaly)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (warned.Add(anomaly))
                Debug.LogWarning($"[Photo] {anomaly.name}: 찍을 곳이 없어 판정에서 뺌 — 자기 아래에 켜진 렌더러가 없음. " +
                                 "보이는 게 바깥 물체면 IPhotoVisual로 알려 주고, 보이는 물체가 없는 자리면 PhotoTarget", anomaly);
#endif
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => warned.Clear();
    }
}
