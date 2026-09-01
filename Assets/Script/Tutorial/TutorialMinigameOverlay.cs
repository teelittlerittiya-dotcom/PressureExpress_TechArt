using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Febucci.TextAnimatorForUnity;
using Febucci.TextAnimatorForUnity.TextMeshPro;
using PressureExpress.Framework;

namespace PressureExpress.Tutorial
{
    [Serializable]
    public struct DialogueStep
    {
        [TextArea(2, 6)] public string text;
        public string targetControlName;
    }

    [Serializable]
    public class MachineMultiPageGuide
    {
        public MachineUIType uiType;
        public string machineTitle;
        public List<DialogueStep> steps = new List<DialogueStep>();
    }

    public class TutorialMinigameOverlay : MonoBehaviour
    {
        public static TutorialMinigameOverlay Instance { get; private set; }

        [Header("UI Dialogue References")]
        [SerializeField] private GameObject dialoguePanel;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI dialogueText;
        [SerializeField] private TextMeshProUGUI pageIndicatorText;
        [SerializeField] private TypewriterComponent typewriter;
        [SerializeField] private TextAnimator_TMP textAnimator;

        [Header("Navigation Buttons")]
        [SerializeField] private Button nextButton;
        [SerializeField] private Button prevButton;
        [SerializeField] private Button dismissButton;
        [SerializeField] private Button closeButton;

        public bool IsShowing => dialoguePanel != null && dialoguePanel.activeSelf;

        [Header("UI Highlight Pointer")]
        [SerializeField] private RectTransform highlightFrame;
        [SerializeField] private Image highlightImage;
        [SerializeField] private float bounceSpeed = 6f;
        [SerializeField] private float bounceDistance = 12f;
        [SerializeField] private float pointerYOffset = 50f;

        [Header("Machine Multi-Step Guides Database")]
        [SerializeField] private List<MachineMultiPageGuide> guideConfigs = new List<MachineMultiPageGuide>();

        private MachineMultiPageGuide activeGuide;
        private int currentStepIndex = 0;
        private GameObject currentMinigameUIInstance;
        private RectTransform currentTargetRect;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) Destroy(gameObject);

            if (highlightImage != null)
            {
                highlightImage.enabled = false;
            }
            if (highlightFrame != null)
            {
                var img = highlightFrame.GetComponent<Image>();
                if (img != null) img.enabled = false;

                var txt = highlightFrame.GetComponentInChildren<TextMeshProUGUI>();
                if (txt != null)
                {
                    txt.text = "▼";
                    txt.fontSize = 44;
                    txt.color = new Color(1f, 0.9f, 0.2f, 1f);
                    txt.alignment = TextAlignmentOptions.Center;
                    txt.raycastTarget = false;
                }
            }

            InitializeDefaultGuides();

            if (nextButton != null) nextButton.onClick.AddListener(OnNextClicked);
            if (prevButton != null) prevButton.onClick.AddListener(OnPrevClicked);
            if (dismissButton != null) dismissButton.onClick.AddListener(HideDialogue);
            if (closeButton != null) closeButton.onClick.AddListener(HideDialogue);
        }

        private void OnEnable()
        {
            CanvasManager.OnMachineUIOpened += HandleMachineUIOpened;
            CanvasManager.OnMachineUIClosed += HandleMachineUIClosed;
        }

        private void OnDisable()
        {
            CanvasManager.OnMachineUIOpened -= HandleMachineUIOpened;
            CanvasManager.OnMachineUIClosed -= HandleMachineUIClosed;
            HideDialogue();
        }

        private void HandleMachineUIOpened(MachineUIType type, GameObject uiInstance)
        {
            ShowGuideForMachine(type, uiInstance);
        }

        private void HandleMachineUIClosed()
        {
            HideDialogue();
        }

        private void Update()
        {
            if (highlightFrame != null && highlightFrame.gameObject.activeSelf && currentTargetRect != null)
            {
                Vector3 basePos = currentTargetRect.position;
                float halfHeight = Mathf.Min(currentTargetRect.rect.height * 0.5f, 60f);
                float yOffset = halfHeight + pointerYOffset + Mathf.Sin(Time.time * bounceSpeed) * bounceDistance;
                highlightFrame.position = basePos + new Vector3(0f, yOffset, 0f);
            }
        }

        public void ShowGuideForMachine(MachineUIType machineType, GameObject minigameUIInstance)
        {
            if (guideConfigs == null || guideConfigs.Count == 0)
            {
                InitializeDefaultGuides();
            }

            currentMinigameUIInstance = minigameUIInstance;
            activeGuide = guideConfigs.Find(g => g.uiType == machineType);

            if (activeGuide == null || activeGuide.steps.Count == 0)
            {
                HideDialogue();
                return;
            }

            if (dialoguePanel != null) dialoguePanel.SetActive(true);

            currentStepIndex = 0;
            DisplayCurrentStep();
        }

        private void DisplayCurrentStep()
        {
            if (activeGuide == null || currentStepIndex < 0 || currentStepIndex >= activeGuide.steps.Count) return;

            var step = activeGuide.steps[currentStepIndex];

            if (titleText != null)
            {
                titleText.text = activeGuide.machineTitle;
            }

            if (typewriter != null)
            {
                typewriter.ShowText(step.text);
            }
            else if (textAnimator != null)
            {
                textAnimator.SetText(step.text);
            }
            else if (dialogueText != null)
            {
                dialogueText.text = step.text;
            }

            if (pageIndicatorText != null)
            {
                pageIndicatorText.text = $"{currentStepIndex + 1} / {activeGuide.steps.Count}";
            }

            bool isLastStep = currentStepIndex >= activeGuide.steps.Count - 1;

            if (prevButton != null)
            {
                prevButton.gameObject.SetActive(currentStepIndex > 0);
            }

            if (nextButton != null)
            {
                nextButton.gameObject.SetActive(!isLastStep);
            }

            if (dismissButton != null)
            {
                dismissButton.gameObject.SetActive(isLastStep);
            }

            // Update Highlight Pointer
            currentTargetRect = null;
            if (currentMinigameUIInstance != null && !string.IsNullOrEmpty(step.targetControlName))
            {
                Transform targetT = FindChildRecursive(currentMinigameUIInstance.transform, step.targetControlName);
                if (targetT != null && targetT is RectTransform rectT)
                {
                    currentTargetRect = rectT;
                    if (highlightFrame != null)
                    {
                        highlightFrame.gameObject.SetActive(true);
                        highlightFrame.sizeDelta = new Vector2(60f, 60f);
                        float halfHeight = Mathf.Min(rectT.rect.height * 0.5f, 60f);
                        highlightFrame.position = rectT.position + new Vector3(0f, halfHeight + pointerYOffset, 0f);
                    }
                }
            }

            if (currentTargetRect == null && highlightFrame != null)
            {
                highlightFrame.gameObject.SetActive(false);
            }
        }

        public void OnNextClicked()
        {
            if (activeGuide == null) return;
            if (currentStepIndex < activeGuide.steps.Count - 1)
            {
                currentStepIndex++;
                DisplayCurrentStep();
            }
        }

        public void OnPrevClicked()
        {
            if (activeGuide == null) return;
            if (currentStepIndex > 0)
            {
                currentStepIndex--;
                DisplayCurrentStep();
            }
        }

        public void HideDialogue()
        {
            if (dialoguePanel != null) dialoguePanel.SetActive(false);
            if (highlightFrame != null) highlightFrame.gameObject.SetActive(false);
            currentTargetRect = null;
            activeGuide = null;
        }

        private Transform FindChildRecursive(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return child;
                }

                Transform found = FindChildRecursive(child, name);
                if (found != null) return found;
            }
            return null;
        }

        public void InitializeDefaultGuides()
        {
            guideConfigs = new List<MachineMultiPageGuide>
            {
                // 1. Fuel Converter
                new MachineMultiPageGuide
                {
                    uiType = MachineUIType.FuelConverter,
                    machineTitle = "FUEL CONVERTER",
                    steps = new List<DialogueStep>
                    {
                        new DialogueStep
                        {
                            text = "Powers the submarine's engines and life-support systems by converting stored fuel stock into reactor power.",
                            targetControlName = ""
                        },
                        new DialogueStep
                        {
                            text = "Hold <color=#4ef>SPACE</color> to recharge and convert stored fuel stock into reactor power!",
                            targetControlName = "FuelSlider"
                        },
                        new DialogueStep
                        {
                            text = "Click the <color=#ff4>Mode Button</color> to toggle between <b>Normal Mode</b> (fast conversion, higher heat) and <b>Safe Mode</b> (slower conversion, low heat).",
                            targetControlName = "Mode"
                        }
                    }
                },

                // 2. Oxygen Generator
                new MachineMultiPageGuide
                {
                    uiType = MachineUIType.OxygenPump,
                    machineTitle = "OXYGEN GENERATOR",
                    steps = new List<DialogueStep>
                    {
                        new DialogueStep
                        {
                            text = "Extracts water directly from the submarine's <b>Ballast Tanks</b> and electrolyzes it into breathable oxygen for the crew.",
                            targetControlName = ""
                        },
                        new DialogueStep
                        {
                            text = "Hold <color=#4ef>SPACE</color>, <color=#4ef>W</color>, or drag the <b>Water Slider</b> down to fill the chamber. <color=#ffb>Warning</color>: Oxygen generation consumes ballast water!",
                            targetControlName = "Handle"
                        },
                        new DialogueStep
                        {
                            text = "Click <color=#4f4>GENERATE</color> (or BOOST) to produce oxygen. If ballast water gets too low, pump water back in via the Bilge Pump station!",
                            targetControlName = "GenerationButton"
                        }
                    }
                },

                // 3. Coolant Valve
                new MachineMultiPageGuide
                {
                    uiType = MachineUIType.CoolantGame,
                    machineTitle = "CORE TEMPERATURE VALVE",
                    steps = new List<DialogueStep>
                    {
                        new DialogueStep
                        {
                            text = "Controls heating and cooling for <b>ALL ROOMS</b> in the submarine, balancing internal engine heat against icy sea water.",
                            targetControlName = ""
                        },
                        new DialogueStep
                        {
                            text = "Hold <color=#4ef>A / LEFT ARROW</color> to open coolant valve and cool all rooms. Hold <color=#f44>D / RIGHT ARROW</color> to warm up the submarine.",
                            targetControlName = "Valve"
                        },
                        new DialogueStep
                        {
                            text = "Keep the temperature gauge needle steady in the safe green zone (20°C–70°C) to prevent catastrophic fires or frozen equipment!",
                            targetControlName = "CurrentTempText"
                        }
                    }
                },

                // 4. Pressure Stabilizer
                new MachineMultiPageGuide
                {
                    uiType = MachineUIType.PressureGame,
                    machineTitle = "HULL PRESSURE STABILIZER",
                    steps = new List<DialogueStep>
                    {
                        new DialogueStep
                        {
                            text = "Deep ocean depth exerts immense hydrostatic pressure. This stabilizer calibrates pressure release valves across the hull.",
                            targetControlName = ""
                        },
                        new DialogueStep
                        {
                            text = "Watch the moving needle. Press <color=#4f4>SPACE</color> or <color=#4f4>LEFT CLICK</color> when it reaches a colored zone.",
                            targetControlName = "needle"
                        },
                        new DialogueStep
                        {
                            text = "<color=#FFD700>YELLOW</color> is the <b>GOOD</b> zone: hit it to reduce hull stress by <color=#FFD700>15%</color>. <color=#39FF14>GREEN</color> is the <b>GREAT</b> zone: hit it to reduce hull stress by <color=#39FF14>25%</color>.",
                            targetControlName = ""
                        },
                        new DialogueStep
                        {
                            text = "Missing both zones adds <color=#f44>5% hull stress</color>. Great and Good hits complete the pressure task. Keep stress low to prevent a breach!",
                            targetControlName = ""
                        }
                    }
                },

                // 5. Bilge Pump
                new MachineMultiPageGuide
                {
                    uiType = MachineUIType.WaterPump,
                    machineTitle = "BILGE WATER DRAIN PUMP",
                    steps = new List<DialogueStep>
                    {
                        new DialogueStep
                        {
                            text = "Expels compartment floodwater and regulates <b>Ballast Tank levels</b> to control diving and surfacing.",
                            targetControlName = ""
                        },
                        new DialogueStep
                        {
                            text = "Press <color=#ff4>T</color> or click <color=#ff4>CHANGE MODE</color> to toggle modes. In Drain mode, mash <color=#4ef>SPACE</color> or click <color=#4ef>PUMP OUT</color> to pump room water out!",
                            targetControlName = "PumpOut"
                        },
                        new DialogueStep
                        {
                            text = "Since Oxygen consumes ballast water, keep ballast tanks balanced around 50% so the submarine remains buoyant and steerable!",
                            targetControlName = ""
                        }
                    }
                },

                // 6. Sonar & Helm
                new MachineMultiPageGuide
                {
                    uiType = MachineUIType.MapNavigation,
                    machineTitle = "SONAR & HELM NAVIGATION",
                    steps = new List<DialogueStep>
                    {
                        new DialogueStep
                        {
                            text = "The Command Helm steers the submarine through deep ocean trenches, detects terrain obstacles, and tracks navigation waypoints.",
                            targetControlName = ""
                        },
                        new DialogueStep
                        {
                            text = "Steer the submarine horizontally using <color=#4f4>A</color> (Left) and <color=#4f4>D</color> (Right). Press <color=#4ef>SPACE</color> to fire an active <b>SONAR PING</b>!",
                            targetControlName = "BT_NAV"
                        },
                        new DialogueStep
                        {
                            text = "Yellow blips reveal rock obstacles. Follow the labeled <b>EXIT BEACON</b> waypoint marker and steer until distance reaches <b>0m</b> to complete the tutorial!",
                            targetControlName = ""
                        }
                    }
                }
            };
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
