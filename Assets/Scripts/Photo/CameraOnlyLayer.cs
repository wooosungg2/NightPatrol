using UnityEngine;

namespace NightPatrol.Photo
{
    /// <summary>
    /// 휴대폰 카메라로만 보이는 것의 레이어 "CameraOnly" (Project Settings → Tags and Layers 7번).
    /// 눈(Player 프리팹 PlayerCamera)은 이 레이어를 그리지 않고, 카메라 앱 뷰파인더만 그린다.
    /// 카메라로만 보이는 이상현상은 보이는 물체(와 자식)의 Layer를 CameraOnly로 두면 된다 — 찍을 곳 찾기·찍힘 판정은 다른 이상현상과 같다
    /// </summary>
    public static class CameraOnlyLayer
    {
        public const string Name = "CameraOnly";

        /// <summary>레이어 번호. 프로젝트에 없으면 -1</summary>
        public static int Index => LayerMask.NameToLayer(Name);

        /// <summary>그 컬링 마스크에 CameraOnly를 더한 것 (레이어가 없으면 그대로)</summary>
        public static int With(int mask)
        {
            int index = Index;
            return index < 0 ? mask : mask | (1 << index);
        }

        /// <summary>그 컬링 마스크가 CameraOnly를 그리는지</summary>
        public static bool In(int mask)
        {
            int index = Index;
            return index >= 0 && (mask & (1 << index)) != 0;
        }
    }
}
