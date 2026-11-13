using System.Collections.Generic;
using UnityEngine;

namespace NightPatrol.Photo
{
    /// <summary>
    /// 화면에 보이는 것들(렌더러·빛)의 모습을 한순간 적어 둔 것. 나중에 어떤 물체가 "그때와 달라졌는지" 본다.
    /// 이상현상이 무엇을 바꿨는지 시스템이 스스로 찾는 데 쓴다 (PhotoSubjects) — 이상현상 쪽에서 따로 알려 줄 필요가 없게.
    /// 모습 = 켜짐 + 레이어(카메라로만 보이게 바꾼 것) + 재질(색·그림) + 프로퍼티 블록 색. 자리(위치·방향·크기)는 따로 본다. 빛은 켜짐·색·밝기
    /// </summary>
    public sealed class LookBaseline
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int MainColor = Shader.PropertyToID("_Color");
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        private static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        private static readonly int MainTexture = Shader.PropertyToID("_MainTex");
        private static readonly List<Material> materials = new List<Material>();
        private static MaterialPropertyBlock block;

        private readonly Dictionary<Renderer, (int look, int place)> renderers = new Dictionary<Renderer, (int, int)>();
        private readonly Dictionary<Light, int> lights = new Dictionary<Light, int>();

        /// <summary>적어 둔 렌더러·빛 수</summary>
        public int Count => renderers.Count + lights.Count;

        /// <summary>지금 씬의 모든 렌더러·빛의 모습을 적는다 (꺼진 것 포함). 층마다 한두 번만 부를 것</summary>
        public static LookBaseline Capture()
        {
            var baseline = new LookBaseline();
            var materialLooks = new Dictionary<Material, int>();
            foreach (var renderer in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include))
                if (renderer != null) baseline.renderers[renderer] = (Look(renderer, materialLooks), Place(renderer));
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsInactive.Include))
                if (light != null) baseline.lights[light] = Look(light);
            return baseline;
        }

        /// <summary>그 렌더러의 모습이 적어 둔 때와 다른지. 그때 없던 것도 달라진 것으로 친다. countMoves면 자리가 바뀐 것도</summary>
        public bool Changed(Renderer renderer, bool countMoves)
        {
            if (renderer == null) return false;
            if (!renderers.TryGetValue(renderer, out var then)) return true;
            return then.look != Look(renderer, null) || (countMoves && then.place != Place(renderer));
        }

        /// <summary>그 빛의 모습(켜짐·색·밝기)이 적어 둔 때와 다른지. 그때 없던 것도 달라진 것으로 친다</summary>
        public bool Changed(Light light)
        {
            if (light == null) return false;
            return !lights.TryGetValue(light, out int then) || then != Look(light);
        }

        private static int Look(Renderer renderer, Dictionary<Material, int> materialLooks)
        {
            int look = renderer.enabled && renderer.gameObject.activeInHierarchy ? 1 : 0;
            look = Mix(look, renderer.gameObject.layer);
            renderer.GetSharedMaterials(materials);
            foreach (var material in materials)
            {
                if (material == null)
                {
                    look = Mix(look, 0);
                    continue;
                }
                if (materialLooks == null || !materialLooks.TryGetValue(material, out int materialLook))
                {
                    materialLook = Look(material);
                    if (materialLooks != null) materialLooks[material] = materialLook;
                }
                look = Mix(look, materialLook);
            }
            if (renderer.HasPropertyBlock())
            {
                if (block == null) block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                look = Mix(look, Tint(block, BaseColor));
                look = Mix(look, Tint(block, MainColor));
                look = Mix(look, Tint(block, EmissionColor));
                look = Mix(look, Picture(block, BaseMap));
                look = Mix(look, Picture(block, MainTexture));
            }
            return look;
        }

        // 재질: 어느 재질인지 + 색·발광색·그림 (같은 재질의 색만 바꾸는 경우도 잡게)
        private static int Look(Material material)
        {
            int look = material.GetHashCode();
            if (material.HasProperty(BaseColor)) look = Mix(look, Hash(material.GetColor(BaseColor)));
            if (material.HasProperty(MainColor)) look = Mix(look, Hash(material.GetColor(MainColor)));
            if (material.HasProperty(EmissionColor)) look = Mix(look, Hash(material.GetColor(EmissionColor)));
            if (material.HasProperty(BaseMap)) look = Mix(look, Hash(material.GetTexture(BaseMap)));
            if (material.HasProperty(MainTexture)) look = Mix(look, Hash(material.GetTexture(MainTexture)));
            return look;
        }

        private static int Look(Light light)
        {
            int look = light.enabled && light.gameObject.activeInHierarchy ? 1 : 0;
            look = Mix(look, Hash(light.color));
            return Mix(look, Step(light.intensity, 100f));
        }

        private static int Place(Renderer renderer)
        {
            var transform = renderer.transform;
            int place = Hash(transform.position);
            var rotation = transform.rotation;
            place = Mix(place, Hash(new Vector3(rotation.x, rotation.y, rotation.z)));
            place = Mix(place, Step(rotation.w, 1000f));
            return Mix(place, Hash(transform.lossyScale));
        }

        private static int Tint(MaterialPropertyBlock properties, int id) => properties.HasColor(id) ? Hash(properties.GetColor(id)) : 0;
        private static int Picture(MaterialPropertyBlock properties, int id) => properties.HasTexture(id) ? Hash(properties.GetTexture(id)) : 0;
        private static int Hash(Texture texture) => texture != null ? texture.GetHashCode() : 0;
        private static int Hash(Color color) => Mix(Mix(Mix(Step(color.r, 255f), Step(color.g, 255f)), Step(color.b, 255f)), Step(color.a, 255f));
        private static int Hash(Vector3 v) => Mix(Mix(Step(v.x, 1000f), Step(v.y, 1000f)), Step(v.z, 1000f));
        private static int Step(float value, float perUnit) => Mathf.RoundToInt(Mathf.Clamp(value, -1e6f, 1e6f) * perUnit);
        private static int Mix(int hash, int value) => unchecked(hash * 31 + value);
    }
}
