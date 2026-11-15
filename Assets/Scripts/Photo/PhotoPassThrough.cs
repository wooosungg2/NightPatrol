using UnityEngine;

namespace NightPatrol.Photo
{
    /// <summary>
    /// 촬영 가림 판정만 통과시키는 표시. 투명 유리의 Collider와 같은 오브젝트에 붙인다.
    /// 기존 맵 유리는 PhotoOcclusion에서 자동 판별한다. 이 표시가 있으면 켜짐·꺼짐 설정을 우선한다.
    /// 부모·자식에는 전파하지 않는다. 문틀·손잡이·불투명 유리에 붙이지 않을 것.
    /// Collider의 레이어·활성 여부·트리거 설정은 바꾸지 않으므로 이동 충돌은 그대로다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PhotoPassThrough : MonoBehaviour
    {
        [Tooltip("이 오브젝트의 Collider를 촬영 가림 검사에서 제외. 끄면 자동 판별 유리도 차단. 이동 충돌에는 영향 없음")]
        [SerializeField] private bool allowPhotos = true;

        public bool AllowsPhotos => isActiveAndEnabled && allowPhotos;

        /// <summary>유리의 촬영 통과 여부를 바꾼다. 이 컴포넌트를 꺼도 통과하지 않는다.</summary>
        public void SetAllowed(bool allowed) => allowPhotos = allowed;
    }
}
