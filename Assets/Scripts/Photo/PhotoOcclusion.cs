using System;
using UnityEngine;

namespace NightPatrol.Photo
{
    /// <summary>
    /// 촬영 가림에서 제외할 충돌체. 명시적 표시가 우선이며, 표시가 없으면 기존 맵의 유리판만 자동 판별한다.
    /// 맵 생성이나 씬 수정 없이 호출할 수 있다. 생성 계층이 달라지면 여기의 판별 규칙도 확인해야 한다.
    /// </summary>
    public static class PhotoOcclusion
    {
        private static readonly int Surface = Shader.PropertyToID("_Surface");
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static MaterialPropertyBlock rendererProperties;
        private static MaterialPropertyBlock materialProperties;

        /// <summary>이 Collider를 촬영 가림에서 제외할지. 물리 충돌이나 오브젝트 설정은 바꾸지 않는다.</summary>
        public static bool AllowsPhotos(Collider collider)
        {
            if (collider == null) return false;

            // 꺼진 표시도 명시적 차단 설정이다. 자동 유리 판별로 다시 통과시키지 않는다.
            if (collider.TryGetComponent<PhotoPassThrough>(out var passThrough))
                return passThrough.AllowsPhotos;

            if (!IsGeneratedGlassPane(collider.transform, out bool canBeInvisible)) return false;
            if (!collider.TryGetComponent<Renderer>(out var renderer)) return false;

            // 생성기가 재질 없는 창문·투명 유리문을 Renderer만 끈 막으로 만드는 경우.
            // 일반 물체나 불투명 유리문에는 이 예외를 적용하지 않는다.
            if (!renderer.enabled) return canBeInvisible;
            return HasTransparentMaterials(renderer);
        }

        private static bool IsGeneratedGlassPane(Transform pane, out bool canBeInvisible)
        {
            canBeInvisible = false;
            string name = pane.name;
            if (name != "Glass" && name != "Transom" && name != "Glass_Upper_L" && name != "Glass_Upper_R")
                return false;

            var parent = pane.parent;
            if (parent == null) return false;
            var generator = pane.GetComponentInParent<FloorGenerator>();
            if (generator == null) return false;
            var grandparent = parent.parent;

            // DoorBuilder의 채광창. WindowBuilder의 동명 가로대는 Door가 없어 제외된다.
            if (name == "Transom")
            {
                if (!parent.TryGetComponent<Door>(out var door)) return false;
                canBeInvisible = door.style == DoorStyle.GlassClear;
                return door.style != DoorStyle.None && door.style != DoorStyle.Auto;
            }

            // WindowBuilder의 위쪽 고정창 두 장.
            if (name == "Glass_Upper_L" || name == "Glass_Upper_R")
            {
                canBeInvisible = parent.TryGetComponent<WindowUnit>(out _);
                return canBeInvisible;
            }

            // DoorBuilder의 문짝 유리. GlassDouble은 불투명 유리이므로 자동 통과시키지 않는다.
            if (grandparent != null && grandparent.TryGetComponent<Door>(out var leafDoor))
            {
                if (leafDoor.style != DoorStyle.GlassClear || leafDoor.leaves == null) return false;
                foreach (var leaf in leafDoor.leaves)
                {
                    if (leaf != parent) continue;
                    canBeInvisible = true;
                    return true;
                }
                return false;
            }

            // WindowBuilder의 아래쪽 고정창·미서기창. 창틀과 블라인드는 판 이름·계층이 다르다.
            if (grandparent != null && grandparent.TryGetComponent<WindowUnit>(out var window)
                && (parent.name == "Sash_Fixed" || parent == window.sash))
            {
                canBeInvisible = true;
                return true;
            }

            // FloorGenerator의 복도·휴게공간 외창.
            if (grandparent == generator.transform && parent.name.StartsWith("Window_", StringComparison.Ordinal))
            {
                canBeInvisible = true;
                return true;
            }

            // StairwellBuilder의 계단실 창: 층/방/Stairwell/Window/Glass.
            if (parent.name == "Window" && grandparent != null && grandparent.name == "Stairwell"
                && grandparent.parent != null && grandparent.parent.parent == generator.transform)
            {
                canBeInvisible = true;
                return true;
            }

            return false;
        }

        private static bool HasTransparentMaterials(Renderer renderer)
        {
            var materials = renderer.sharedMaterials;
            if (materials.Length == 0) return false;
            if (rendererProperties == null) rendererProperties = new MaterialPropertyBlock();
            if (materialProperties == null) materialProperties = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(rendererProperties);

            for (int i = 0; i < materials.Length; i++)
            {
                var material = materials[i];
                // 현재 맵의 URP Lit 유리만 자동 지원한다. 알 수 없는 셰이더는 명시적 표시로 설정한다.
                if (material == null || material.shader == null || material.shader.name != "Universal Render Pipeline/Lit"
                    || !material.HasProperty(Surface) || material.GetFloat(Surface) != 1f
                    || !material.HasProperty(BaseColor))
                    return false;

                // 재질 슬롯별 블록이 있으면 Renderer 전체 블록보다 우선한다.
                renderer.GetPropertyBlock(materialProperties, i);
                var properties = materialProperties.isEmpty ? rendererProperties : materialProperties;
                if (properties.HasFloat(Surface) && properties.GetFloat(Surface) != 1f) return false;
                float alpha = properties.HasColor(BaseColor)
                    ? properties.GetColor(BaseColor).a
                    : material.GetColor(BaseColor).a;
                if (!(alpha < 1f)) return false;
            }
            return true;
        }
    }
}
