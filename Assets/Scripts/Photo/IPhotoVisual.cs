using UnityEngine;

namespace NightPatrol.Photo
{
    /// <summary>
    /// 켜진 동안 보이는 물체가 이상현상 오브젝트 밖에 있는 이상현상이 구현한다 (맵의 모니터·천장 등·벨, 실행 중 만들거나 옮긴 물체).
    /// 카메라는 이 물체의 켜진 렌더러를 찍을 곳으로 쓴다. 보이는 게 이상현상 오브젝트 아래에 있으면 구현하지 않아도 된다.
    /// 예: public GameObject PhotoVisual => IsActive &amp;&amp; monitor != null ? monitor.gameObject : null;
    /// </summary>
    public interface IPhotoVisual
    {
        /// <summary>지금 보이는 물체. 꺼졌거나 모르면 null</summary>
        GameObject PhotoVisual { get; }
    }
}
