#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using NightPatrol.Anomalies;
using NightPatrol.Phone;
using UnityEngine;
using UnityEngine.Rendering;

namespace NightPatrol.Photo
{
    /// <summary>F8 외곽선·F10 레이 점 UI. 원본 렌더러와 촬영용 RenderTexture에는 쓰지 않는다.</summary>
    [DefaultExecutionOrder(10000)]
    public sealed class PhotoDebugView : MonoBehaviour
    {
        [SerializeField, Min(64)] private int maskMaxSize = 640;
        [SerializeField, Min(1f)] private float rayDotSize = 5f;
        [SerializeField, Min(1f)] private float lightMarkerPixels = 8f;
        [SerializeField] private Color outlineColor = new Color(0.2f, 1f, 0.3f, 1f);

        private static PhotoDebugView instance;
        public static bool IsVisible => instance != null && instance.visible;
        public static bool RaysVisible => instance != null && instance.raysVisible;
        private bool visible, raysVisible;
        private float nextRefresh, rayLeft;
        private bool raysDuringReview;
        private CameraApp app, capturedApp;
        private PhotoCaptureResult capture;
        private UnityEngine.UI.RawImage lensImage;
        private RectTransform phoneOverlay;
        private RenderTexture lensMask;
        private Material maskMaterial, outlineMaterial;
        private CommandBuffer commands;
        private Mesh diamond;
        private Texture2D dotTexture;
        private Sprite dotSprite;
        private readonly List<Renderer> renderers = new List<Renderer>();
        private readonly List<Vector3> markers = new List<Vector3>();
        private readonly List<UnityEngine.UI.Image> dots = new List<UnityEngine.UI.Image>();
        private static readonly int ViewProjection = Shader.PropertyToID("_PhotoDebugVP");

        private bool PhoneShown => app != null && app.isActiveAndEnabled && app.IsOpen && app.Phone != null
            && app.Phone.IsInHand && app.Screen != null && !app.Screen.IsCallShown;
        private bool ViewfinderShown => PhoneShown && !app.IsReviewing && app.Lens != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession() => instance = null;

        /// <summary>실행 중에만 유지. 호출하는 DevMenu에서 F1 표시 여부를 검사한다.</summary>
        public static void SetVisible(bool value)
        {
            if (value) EnsureInstance();
            if (instance == null) return;
            instance.visible = value;
            instance.nextRefresh = 0f;
            if (!value && instance.lensImage != null) instance.lensImage.gameObject.SetActive(false);
            if (!value && !instance.raysVisible) instance.Hide();
        }

        /// <summary>레이 점만 켜고 끈다. F8 외곽선 상태와 독립적이며 실행 중에만 유지한다.</summary>
        public static void SetRaysVisible(bool value)
        {
            if (value) EnsureInstance();
            if (instance == null) return;
            instance.raysVisible = value;
            instance.nextRefresh = 0f;
            if (!value)
            {
                instance.ClearRays();
                if (!instance.visible) instance.Hide();
            }
        }

        private static void EnsureInstance()
        {
            if (instance != null) return;
            var go = new GameObject("PhotoDebugView");
            instance = go.AddComponent<PhotoDebugView>();
            DontDestroyOnLoad(go);
        }

        /// <summary>셔터 결과만 보관한다. 사진 텍스처나 이상현상에는 손대지 않는다.</summary>
        public static void ShowCapture(CameraApp cameraApp, PhotoCaptureResult result, float reviewSeconds)
        {
            if (!RaysVisible || cameraApp == null || !cameraApp.IsOpen) return;
            instance.capturedApp = cameraApp;
            instance.capture = result;
            instance.raysDuringReview = reviewSeconds > 0f;
            instance.rayLeft = reviewSeconds > 0f ? reviewSeconds : 2f;
        }

        public static Color RayColor(PhotoRayResult ray) => ray.UsedForCapture
            ? new Color(1f, 0f, 1f, 1f)
            : !ReferenceEquals(ray.Seen, null) ? new Color(1f, 0.5f, 0f, 1f) : new Color(1f, 1f, 1f, 0.45f);

        private bool EnsureResources()
        {
            if (maskMaterial != null) return true;
            var maskShader = Resources.Load<Shader>("PhotoDebugMask");
            var outlineShader = Resources.Load<Shader>("PhotoDebugOutline");
            if (maskShader == null || outlineShader == null || !maskShader.isSupported || !outlineShader.isSupported)
            {
                Debug.LogWarning("[Photo] 찍힘 표시 셰이더를 불러올 수 없음", this);
                SetVisible(false);
                return false;
            }
            maskMaterial = new Material(maskShader) { hideFlags = HideFlags.HideAndDontSave };
            outlineMaterial = new Material(outlineShader) { hideFlags = HideFlags.HideAndDontSave };
            commands = new CommandBuffer { name = "PhotoDebugMask" };
            diamond = new Mesh { name = "PhotoDebugDiamond", hideFlags = HideFlags.HideAndDontSave };
            diamond.vertices = new[] { Vector3.up, Vector3.right, Vector3.down, Vector3.left };
            diamond.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            diamond.RecalculateBounds();
            return true;
        }

        private UnityEngine.UI.RawImage NewImage(RectTransform parent, string name)
        {
            var image = PhoneUI.Fill(parent, name).gameObject.AddComponent<UnityEngine.UI.RawImage>();
            image.raycastTarget = false;
            image.material = outlineMaterial;
            return image;
        }

        private void LateUpdate()
        {
            if (!visible && !raysVisible) return;
            bool outlines = visible && EnsureResources();
            if (Time.unscaledTime >= nextRefresh)
            {
                app = FindAnyObjectByType<CameraApp>(FindObjectsInactive.Include);
                RefreshTargets();
                nextRefresh = Time.unscaledTime + PhotoSubjects.RefreshSeconds;
            }
            if (outlines) outlineMaterial.SetColor("_OutlineColor", outlineColor);
            bool phoneShown = PhoneShown;
            bool live = outlines && ViewfinderShown;
            if (!live)
            {
                // 닫았다 다시 열거나 사진 보기를 끝냈을 때 이전 조준 대상을 남기지 않는다.
                renderers.Clear();
                markers.Clear();
            }

            BindPhone();
            if (phoneOverlay != null) phoneOverlay.gameObject.SetActive(phoneShown);
            if (lensImage != null)
            {
                lensImage.gameObject.SetActive(live);
                if (live)
                {
                    lensImage.material = outlineMaterial;
                    RenderMask(app.Lens, lensImage, ref lensMask, CameraApp.Width, CameraApp.Height);
                }
            }
            UpdateRays(phoneShown);
        }

        /// <summary>0.1초마다 현재 조준으로 찍힐 이상현상의 부품만 고른다. 촬영 기록은 바꾸지 않는다.</summary>
        public void RefreshTargets()
        {
            renderers.Clear();
            markers.Clear();
            if (!visible || !ViewfinderShown) return;
            var subjects = PhotoSubjects.Active();
            var aimed = new HashSet<Anomaly>(PhotoJudge.Capture(app.Lens, app.Rules, subjects).Captured);
            if (aimed.Count == 0) return;
            var byOwner = new Dictionary<Transform, Renderer[]>();
            var added = new HashSet<Renderer>();
            foreach (var subject in subjects)
            {
                if (!aimed.Contains(subject.Source)) continue;
                foreach (var shape in subject.Shapes)
                {
                    bool found = false;
                    if (shape.Owner != null)
                    {
                        if (!byOwner.TryGetValue(shape.Owner, out var candidates))
                            byOwner[shape.Owner] = candidates = shape.Owner.GetComponentsInChildren<Renderer>();
                        // 자동 부품의 Owner는 가림 제외용 루트다. 자식 전체를 칠하지 않고 바운드로 부품을 고른다.
                        var target = shape.Owner.GetComponent<PhotoTarget>();
                        bool explicitTarget = target != null && target.isActiveAndEnabled && target.Anomaly == subject.Source
                            && SameBounds(target.Shape().Bounds, shape.Bounds);
                        foreach (var renderer in candidates)
                        {
                            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                            if (!explicitTarget && !SameBounds(renderer.bounds, shape.Bounds) &&
                                !SameBounds(PhotoSubjects.TrimRoomSized(renderer.bounds), shape.Bounds)) continue;   // 방을 채우는 꾸밈은 줄인 상자
                            found = true;
                            if (added.Add(renderer)) renderers.Add(renderer);
                        }
                    }
                    if (!found) markers.Add(shape.Center);
                }
            }
        }

        private static bool SameBounds(Bounds a, Bounds b) =>
            (a.center - b.center).sqrMagnitude < 0.00000001f && (a.size - b.size).sqrMagnitude < 0.00000001f;

        private void BindPhone()
        {
            var parent = app != null ? app.DebugOverlay : null;
            if (phoneOverlay == parent && lensImage != null) return;
            if (lensImage != null) Destroy(lensImage.gameObject);
            foreach (var dot in dots) if (dot != null) Destroy(dot.gameObject);
            dots.Clear();
            if (phoneOverlay != null) phoneOverlay.gameObject.SetActive(false);
            phoneOverlay = parent;
            if (parent != null) lensImage = NewImage(parent, "LensOutline");
        }

        private void RenderMask(Camera camera, UnityEngine.UI.RawImage image, ref RenderTexture mask, int width, int height)
        {
            float scale = Mathf.Min(1f, Mathf.Max(64, maskMaxSize) / (float)Mathf.Max(1, Mathf.Max(width, height)));
            int w = Mathf.Max(1, Mathf.RoundToInt(width * scale)), h = Mathf.Max(1, Mathf.RoundToInt(height * scale));
            if (mask == null || mask.width != w || mask.height != h)
            {
                ReleaseMask(ref mask);
                mask = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
                {
                    name = "PhotoDebugMask", hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp,
                };
                mask.Create();
            }
            image.texture = mask;   // 앱 UI를 다시 만든 경우에도 기존 마스크를 연결한다.
            // URP 카메라 전역 행렬을 바꾸지 않고 이 재질에만 전달한다.
            maskMaterial.SetMatrix(ViewProjection, GL.GetGPUProjectionMatrix(camera.projectionMatrix, true) * camera.worldToCameraMatrix);
            commands.Clear();
            commands.SetRenderTarget(mask);
            commands.SetViewport(new Rect(0f, 0f, w, h));
            commands.ClearRenderTarget(false, true, Color.clear);
            foreach (var renderer in renderers)
            {
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                int count = renderer.sharedMaterials.Length;
                if (renderer is SkinnedMeshRenderer skin && skin.sharedMesh != null) count = skin.sharedMesh.subMeshCount;
                else if (renderer.TryGetComponent<MeshFilter>(out var filter) && filter.sharedMesh != null) count = filter.sharedMesh.subMeshCount;
                for (int submesh = 0; submesh < count; submesh++) commands.DrawRenderer(renderer, maskMaterial, submesh, 0);
            }
            foreach (var center in markers)
            {
                float depth = camera.WorldToViewportPoint(center).z;
                if (depth <= camera.nearClipPlane || depth >= camera.farClipPlane) continue;
                float worldHeight = 2f * (camera.orthographic ? 1f : depth) / Mathf.Abs(camera.projectionMatrix.m11);
                float radius = lightMarkerPixels * 0.5f * worldHeight / h;
                commands.DrawMesh(diamond, Matrix4x4.TRS(center, camera.transform.rotation, Vector3.one * radius), maskMaterial, 0, 0);
            }
            var previous = RenderTexture.active;
            try { Graphics.ExecuteCommandBuffer(commands); }
            finally { RenderTexture.active = previous; }
        }

        private void UpdateRays(bool phoneShown)
        {
            bool valid = capture != null && capturedApp != null && capturedApp == app && app.IsOpen
                && ReferenceEquals(app.LastCapture, capture) && (!raysDuringReview || app.IsReviewing);
            if (!valid) { capture = null; rayLeft = 0f; }
            if (rayLeft > 0f) rayLeft -= Time.deltaTime;
            int count = raysVisible && phoneShown && phoneOverlay != null && valid && rayLeft > 0f ? capture.Rays.Count : 0;
            if (count > 0) EnsureDotSprite();
            for (int i = dots.Count; i < count; i++)
            {
                var rect = PhoneUI.Rect(phoneOverlay, "Ray" + i);
                rect.pivot = new Vector2(0.5f, 0.5f);
                var dot = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
                dot.raycastTarget = false;
                dot.sprite = dotSprite;
                dots.Add(dot);
            }
            for (int i = 0; i < dots.Count; i++)
            {
                dots[i].gameObject.SetActive(i < count);
                if (i >= count) continue;
                var ray = capture.Rays[i];
                var rect = dots[i].rectTransform;
                rect.anchorMin = rect.anchorMax = ray.Viewport;
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = Vector2.one * rayDotSize;
                dots[i].color = RayColor(ray);
            }
        }

        private void EnsureDotSprite()
        {
            if (dotSprite != null) return;
            const int size = 16;
            dotTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                { name = "PhotoDebugDot", hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float distance = new Vector2(x + 0.5f - size / 2f, y + 0.5f - size / 2f).magnitude;
                    pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(size / 2f - distance));
                }
            dotTexture.SetPixels(pixels);
            dotTexture.Apply(false, true);
            dotSprite = Sprite.Create(dotTexture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            dotSprite.hideFlags = HideFlags.HideAndDontSave;
        }

        private void ClearRays()
        {
            capture = null;
            capturedApp = null;
            rayLeft = 0f;
            foreach (var dot in dots) if (dot != null) dot.gameObject.SetActive(false);
        }

        private void Hide()
        {
            ClearRays();
            if (phoneOverlay != null) phoneOverlay.gameObject.SetActive(false);
        }

        private void OnDisable() => Hide();

        private static void ReleaseMask(ref RenderTexture mask)
        {
            if (mask != null) { mask.Release(); Destroy(mask); }
            mask = null;
        }

        private void OnDestroy()
        {
            Hide();
            if (instance == this) instance = null;
            if (lensImage != null) Destroy(lensImage.gameObject);
            foreach (var dot in dots) if (dot != null) Destroy(dot.gameObject);
            ReleaseMask(ref lensMask);
            commands?.Release();
            if (maskMaterial != null) Destroy(maskMaterial);
            if (outlineMaterial != null) Destroy(outlineMaterial);
            if (diamond != null) Destroy(diamond);
            if (dotSprite != null) Destroy(dotSprite);
            if (dotTexture != null) Destroy(dotTexture);
        }
    }
}
#endif
