using UnityEngine;
using PressureExpress.Framework;

namespace PressureExpress.Tutorial
{
    public class TutorialWorldHighlight : MonoBehaviour
    {
        [Header("Machine Identity")]
        [SerializeField] private MachineUIType machineType;
        public MachineUIType MachineType => machineType;

        [Header("Floating Indicator")]
        [SerializeField] private GameObject floatingMarker;
        [SerializeField] private float bobbingSpeed = 3f;
        [SerializeField] private float bobbingHeight = 0.2f;
        [SerializeField] private float baseHeightOffset = 2.2f;

        [Header("Sprite Highlight (AllIn1Shader)")]
        [SerializeField] private SpriteRenderer targetSpriteRenderer;
        [SerializeField] private Color outlineColor = new Color(0.2f, 1f, 0.4f, 1f);
        [SerializeField] private float outlineWidth = 0.005f;
        [SerializeField] private float glowIntensity = 1.8f;
        [SerializeField] private float pulseSpeed = 3.5f;

        private static readonly int OutlineColorID = Shader.PropertyToID("_OutlineColor");
        private static readonly int OutlineAlphaID = Shader.PropertyToID("_OutlineAlpha");
        private static readonly int OutlineWidthID = Shader.PropertyToID("_OutlineWidth");
        private static readonly int GlowColorID = Shader.PropertyToID("_GlowColor");
        private static readonly int GlowID = Shader.PropertyToID("_Glow");
        private static readonly int GlowGlobalID = Shader.PropertyToID("_GlowGlobal");

        private Material instantiatedMat;
        private Vector3 initialMarkerLocalPos = Vector3.zero;
        private bool isTaskCompleted = false;

        private void Awake()
        {
            if (targetSpriteRenderer == null)
            {
                targetSpriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }

            if (targetSpriteRenderer != null)
            {
                instantiatedMat = targetSpriteRenderer.material;
            }

            if (floatingMarker != null)
            {
                initialMarkerLocalPos = floatingMarker.transform.localPosition;
            }
        }

        private void Start()
        {
            if (TutorialManager.Instance != null)
            {
                TutorialManager.Instance.OnMachineTaskCompleted += HandleMachineCompleted;
                TutorialManager.Instance.OnPhaseChanged += HandlePhaseChanged;
            }

            UpdateHighlightState();
        }

        private void Update()
        {
            if (isTaskCompleted) return;

            if (floatingMarker != null && floatingMarker.activeSelf)
            {
                float newY = baseHeightOffset + Mathf.Sin(Time.time * bobbingSpeed) * bobbingHeight;
                floatingMarker.transform.localPosition = new Vector3(initialMarkerLocalPos.x, newY, initialMarkerLocalPos.z);
            }

            if (instantiatedMat != null && IsHighlightActive())
            {
                float t = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
                float currentAlpha = Mathf.Lerp(0.4f, 1.0f, t);
                float currentGlow = Mathf.Lerp(0.8f, glowIntensity, t);
                instantiatedMat.SetFloat(OutlineAlphaID, currentAlpha);
                instantiatedMat.SetFloat(GlowID, currentGlow);
                instantiatedMat.SetFloat(GlowGlobalID, currentGlow);
            }
        }

        private bool IsHighlightActive()
        {
            if (TutorialManager.Instance == null) return false;

            if (machineType == MachineUIType.MapNavigation)
            {
                return TutorialManager.Instance.CurrentPhase == TutorialPhase.SonarStation;
            }
            else
            {
                return TutorialManager.Instance.CurrentPhase == TutorialPhase.InternalMachines &&
                       !TutorialManager.Instance.IsMachineCompleted(machineType);
            }
        }

        private void HandleMachineCompleted(MachineUIType completedType)
        {
            if (completedType == machineType)
            {
                isTaskCompleted = true;
                UpdateHighlightState();
            }
        }

        private void HandlePhaseChanged(TutorialPhase phase)
        {
            UpdateHighlightState();
        }

        public void UpdateHighlightState()
        {
            bool shouldBeActive = IsHighlightActive();

            if (floatingMarker != null)
            {
                floatingMarker.SetActive(shouldBeActive);
            }

            if (instantiatedMat != null)
            {
                if (shouldBeActive)
                {
                    ApplyOutline(1f, glowIntensity);
                }
                else
                {
                    ClearOutline();
                }
            }
        }

        private void ApplyOutline(float alpha, float glow)
        {
            instantiatedMat.EnableKeyword("OUTLINE_ON");
            instantiatedMat.EnableKeyword("GLOW_ON");
            instantiatedMat.SetColor(OutlineColorID, outlineColor);
            instantiatedMat.SetFloat(OutlineAlphaID, alpha);
            instantiatedMat.SetFloat(OutlineWidthID, outlineWidth);
            instantiatedMat.SetColor(GlowColorID, outlineColor);
            instantiatedMat.SetFloat(GlowID, glow);
            instantiatedMat.SetFloat(GlowGlobalID, glow);
        }

        private void ClearOutline()
        {
            instantiatedMat.DisableKeyword("OUTLINE_ON");
            instantiatedMat.DisableKeyword("GLOW_ON");
            instantiatedMat.SetFloat(OutlineAlphaID, 0f);
            instantiatedMat.SetFloat(GlowID, 0f);
            instantiatedMat.SetFloat(GlowGlobalID, 0f);
        }

        private void OnDestroy()
        {
            if (TutorialManager.Instance != null)
            {
                TutorialManager.Instance.OnMachineTaskCompleted -= HandleMachineCompleted;
                TutorialManager.Instance.OnPhaseChanged -= HandlePhaseChanged;
            }

            if (instantiatedMat != null)
            {
                ClearOutline();
            }
        }
    }
}
