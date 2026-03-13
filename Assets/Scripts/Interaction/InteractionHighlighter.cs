#nullable enable
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace InteractionSystem
{
    public class InteractionHighlighter : MonoBehaviour
    {
        [Header("Highlight")]
        [SerializeField] private Shader? highlightShader;
        [SerializeField] private Color highlightColor = new(1f, 0.8f, 0.2f, 0.6f);
        [SerializeField] private float fresnelPower = 2f;
        [SerializeField] private float fresnelIntensity = 1.5f;

        [Header("Hint Text")]
        [SerializeField] private float hintVerticalOffset = 1.2f;
        [SerializeField] private float hintFontSize = 3f;
        [SerializeField] private Color hintTextColor = Color.white;
        [SerializeField] private Color hintBackgroundColor = new(0f, 0f, 0f, 0.5f);

        private InteractionDetector? detector;
        private Camera? playerCamera;
        private Material? highlightMaterial;

        private IInteractable? previousTarget;
        private Renderer[]? previousRenderers;

        private Canvas? hintCanvas;
        private TextMeshProUGUI? hintText;
        private RectTransform? hintRectTransform;
        private UnityEngine.UI.Image? hintBackground;

        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int FresnelPowerId = Shader.PropertyToID("_FresnelPower");
        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");

        private void Awake()
        {
            detector = GetComponent<InteractionDetector>();
            playerCamera = GetComponentInChildren<Camera>(true);
        }

        private void Start()
        {
            CreateHighlightMaterial();
            CreateHintUI();
        }

        private void OnDestroy()
        {
            if (highlightMaterial != null)
                Destroy(highlightMaterial);

            if (hintCanvas != null)
                Destroy(hintCanvas.gameObject);
        }

        private void LateUpdate()
        {
            if (detector == null || playerCamera == null) return;

            var currentTarget = detector.CurrentTarget;

            if (currentTarget != previousTarget)
            {
                RemoveHighlight();
                previousTarget = currentTarget;

                if (currentTarget != null)
                    ApplyHighlight(currentTarget);
            }

            UpdateHintPosition();
        }

        private void CreateHighlightMaterial()
        {
            if (highlightShader == null)
                highlightShader = Shader.Find("Custom/HighlightFresnel");

            if (highlightShader == null) return;

            highlightMaterial = new Material(highlightShader)
            {
                renderQueue = (int)RenderQueue.Transparent + 10
            };
            highlightMaterial.SetColor(ColorId, highlightColor);
            highlightMaterial.SetFloat(FresnelPowerId, fresnelPower);
            highlightMaterial.SetFloat(IntensityId, fresnelIntensity);
        }

        private void CreateHintUI()
        {
            var canvasGo = new GameObject("InteractionHintCanvas");
            hintCanvas = canvasGo.AddComponent<Canvas>();
            hintCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            hintCanvas.sortingOrder = 100;

            var scaler = canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            var bgGo = new GameObject("HintBackground");
            bgGo.transform.SetParent(canvasGo.transform, false);
            hintBackground = bgGo.AddComponent<UnityEngine.UI.Image>();
            hintBackground.color = hintBackgroundColor;

            hintRectTransform = bgGo.GetComponent<RectTransform>();
            hintRectTransform.pivot = new Vector2(0.5f, 0f);

            var textGo = new GameObject("HintText");
            textGo.transform.SetParent(bgGo.transform, false);
            hintText = textGo.AddComponent<TextMeshProUGUI>();
            hintText.fontSize = hintFontSize;
            hintText.color = hintTextColor;
            hintText.alignment = TextAlignmentOptions.Center;
            hintText.enableWordWrapping = false;

            var textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(12f, 4f);
            textRect.offsetMax = new Vector2(-12f, -4f);

            var fitter = bgGo.AddComponent<UnityEngine.UI.ContentSizeFitter>();
            fitter.horizontalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;

            var layoutGroup = bgGo.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            layoutGroup.padding = new RectOffset(12, 12, 4, 4);
            layoutGroup.childAlignment = TextAnchor.MiddleCenter;

            hintCanvas.gameObject.SetActive(false);
        }

        private void ApplyHighlight(IInteractable target)
        {
            if (highlightMaterial == null) return;

            var targetMono = target as MonoBehaviour;
            if (targetMono == null) return;

            previousRenderers = targetMono.GetComponentsInChildren<Renderer>();

            foreach (var renderer in previousRenderers)
            {
                if (renderer is ParticleSystemRenderer) continue;

                var materials = renderer.sharedMaterials;
                var newMaterials = new Material[materials.Length + 1];
                materials.CopyTo(newMaterials, 0);
                newMaterials[materials.Length] = highlightMaterial;
                renderer.materials = newMaterials;
            }

            if (hintCanvas != null && hintText != null)
            {
                hintText.text = $"[E] {target.InteractionPrompt}";
                hintCanvas.gameObject.SetActive(true);
            }
        }

        private void RemoveHighlight()
        {
            if (previousRenderers != null)
            {
                foreach (var renderer in previousRenderers)
                {
                    if (renderer == null) continue;
                    if (renderer is ParticleSystemRenderer) continue;

                    var materials = renderer.sharedMaterials;
                    if (materials.Length <= 1) continue;

                    var originalMaterials = new Material[materials.Length - 1];
                    System.Array.Copy(materials, originalMaterials, originalMaterials.Length);
                    renderer.materials = originalMaterials;
                }

                previousRenderers = null;
            }

            if (hintCanvas != null)
                hintCanvas.gameObject.SetActive(false);
        }

        private void UpdateHintPosition()
        {
            if (hintCanvas == null || hintRectTransform == null || playerCamera == null) return;
            if (previousTarget == null)
            {
                hintCanvas.gameObject.SetActive(false);
                return;
            }

            var targetMono = previousTarget as MonoBehaviour;
            if (targetMono == null) return;

            var targetCollider = targetMono.GetComponentInChildren<Collider>();
            Vector3 worldPos;

            if (targetCollider != null)
            {
                var bounds = targetCollider.bounds;
                worldPos = bounds.center + Vector3.up * (bounds.extents.y + hintVerticalOffset);
            }
            else
            {
                worldPos = targetMono.transform.position + Vector3.up * hintVerticalOffset;
            }

            Vector3 screenPos = playerCamera.WorldToScreenPoint(worldPos);

            if (screenPos.z <= 0f)
            {
                hintCanvas.gameObject.SetActive(false);
                return;
            }

            hintCanvas.gameObject.SetActive(true);
            hintRectTransform.position = screenPos;
        }
    }
}
