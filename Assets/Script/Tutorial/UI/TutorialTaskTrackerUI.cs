using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MoreMountains.Feedbacks;
using PressureExpress.Framework;

namespace PressureExpress.Tutorial.UI
{
    [Serializable]
    public class TaskRowItem
    {
        public MachineUIType machineType;
        public GameObject rowContainer;
        public Image checkmarkImage;
        public TextMeshProUGUI taskText;
        public string defaultText;
        public string completedText;
    }

    public class TutorialTaskTrackerUI : MonoBehaviour
    {
        public static TutorialTaskTrackerUI Instance { get; private set; }

        [Header("Panel Container")]
        [SerializeField] private GameObject trackerPanel;

        [Header("Header")]
        [SerializeField] private TextMeshProUGUI headerTitleText;
        [SerializeField] private TextMeshProUGUI progressCounterText;

        [Header("Task Rows")]
        [SerializeField] private List<TaskRowItem> taskRows = new List<TaskRowItem>();

        [Header("Styles")]
        [SerializeField] private Color pendingColor = Color.white;
        [SerializeField] private Color completedColor = new Color(0.4f, 1f, 0.4f, 0.9f);
        [SerializeField] private Sprite checkedSprite;
        [SerializeField] private Sprite uncheckedSprite;

        [Header("Audio")]
        [SerializeField] private AudioClip taskCompleteClip;
        private AudioSource audioSource;

        [Header("Feel Feedbacks")]
        [SerializeField] private MMF_Player taskCompletedFeedback;
        [SerializeField] private MMF_Player phaseAdvanceFeedback;
        [SerializeField] private MMF_Player allCompletedFeedback;

        private static readonly Vector2 CheckmarkSize = new Vector2(22f, 22f);

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            audioSource.volume = 0.85f;

            if (trackerPanel == null && transform.childCount > 0)
            {
                trackerPanel = transform.GetChild(0).gameObject;
            }
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
        }

        private void HandleMachineUIOpened(MachineUIType type, GameObject uiInstance)
        {
            SetTrackerVisible(false);
        }

        private void HandleMachineUIClosed()
        {
            SetTrackerVisible(true);
            RefreshAll();
        }

        public void SetTrackerVisible(bool visible)
        {
            if (trackerPanel != null)
            {
                trackerPanel.SetActive(visible);
            }
        }

        private void Start()
        {
            if (TutorialManager.Instance != null)
            {
                TutorialManager.Instance.OnMachineTaskCompleted += HandleTaskCompleted;
                TutorialManager.Instance.OnPhaseChanged += HandlePhaseChanged;
            }

            RefreshAll();
        }

        public void HandleTaskCompleted(MachineUIType machineType)
        {
            TaskRowItem targetRow = taskRows.Find(r => r.machineType == machineType);
            if (targetRow != null)
            {
                SetRowCompleted(targetRow);
            }

            UpdateCounter();

            if (taskCompleteClip != null && audioSource != null)
            {
                audioSource.PlayOneShot(taskCompleteClip);
            }

            if (taskCompletedFeedback != null)
            {
                taskCompletedFeedback.PlayFeedbacks();
            }
        }

        public void HandlePhaseChanged(TutorialPhase phase)
        {
            RefreshAll();

            switch (phase)
            {
                case TutorialPhase.SonarStation:
                case TutorialPhase.SteerToExit:
                    if (phaseAdvanceFeedback != null) phaseAdvanceFeedback.PlayFeedbacks();
                    break;
                case TutorialPhase.Complete:
                    if (allCompletedFeedback != null) allCompletedFeedback.PlayFeedbacks();
                    break;
            }
        }

        private void SetRowCompleted(TaskRowItem row)
        {
            if (row.checkmarkImage != null)
            {
                if (checkedSprite != null) row.checkmarkImage.sprite = checkedSprite;
                row.checkmarkImage.color = completedColor;
                row.checkmarkImage.rectTransform.sizeDelta = CheckmarkSize;
            }

            if (row.taskText != null)
            {
                row.taskText.color = completedColor;
                if (!string.IsNullOrEmpty(row.completedText))
                {
                    row.taskText.text = $"<s>{row.completedText}</s>";
                }
            }
        }

        private void SetRowPending(TaskRowItem row)
        {
            if (row.checkmarkImage != null)
            {
                if (uncheckedSprite != null) row.checkmarkImage.sprite = uncheckedSprite;
                row.checkmarkImage.color = pendingColor;
                row.checkmarkImage.rectTransform.sizeDelta = CheckmarkSize;
            }

            if (row.taskText != null)
            {
                row.taskText.color = pendingColor;
                if (!string.IsNullOrEmpty(row.defaultText)) row.taskText.text = row.defaultText;
            }
        }

        private void SetRowPendingWithText(TaskRowItem row, string text)
        {
            SetRowPending(row);
            if (row.taskText != null) row.taskText.text = text;
        }

        private void UpdateCounter()
        {
            if (TutorialManager.Instance == null || progressCounterText == null) return;

            switch (TutorialManager.Instance.CurrentPhase)
            {
                case TutorialPhase.InternalMachines:
                    int total = TutorialManager.Instance.pendingMachines.Count;
                    int done = 0;
                    foreach (var m in TutorialManager.Instance.pendingMachines)
                    {
                        if (TutorialManager.Instance.IsMachineCompleted(m)) done++;
                    }
                    progressCounterText.text = $"Stations Done: {done}/{total}";
                    break;
                case TutorialPhase.SonarStation:
                    progressCounterText.text = "<color=#4ef>Objective: Bridge Deck (Deck 2)</color>";
                    break;
                case TutorialPhase.SteerToExit:
                    progressCounterText.text = "<color=#4ef>Follow Yellow Radar Beacon</color>";
                    break;
                case TutorialPhase.Complete:
                    progressCounterText.text = "<color=#4f4>Submarine Ready for Departure</color>";
                    break;
            }
        }

        public void RefreshAll()
        {
            if (TutorialManager.Instance == null) return;

            TutorialPhase currentPhase = TutorialManager.Instance.CurrentPhase;

            if (trackerPanel != null && trackerPanel.transform is RectTransform panelRT)
            {
                panelRT.sizeDelta = currentPhase == TutorialPhase.InternalMachines
                    ? new Vector2(400f, 310f)
                    : new Vector2(400f, 150f);
            }

            if (currentPhase == TutorialPhase.InternalMachines)
            {
                if (headerTitleText != null) headerTitleText.text = "TUTORIAL: SHIP STATIONS";

                foreach (var row in taskRows)
                {
                    if (row.machineType == MachineUIType.MapNavigation)
                    {
                        if (row.rowContainer != null) row.rowContainer.SetActive(false);
                        continue;
                    }

                    if (row.rowContainer != null) row.rowContainer.SetActive(true);

                    if (TutorialManager.Instance.IsMachineCompleted(row.machineType))
                        SetRowCompleted(row);
                    else
                        SetRowPending(row);
                }
            }
            else
            {
                foreach (var row in taskRows)
                {
                    if (row.machineType != MachineUIType.MapNavigation)
                    {
                        if (row.rowContainer != null) row.rowContainer.SetActive(false);
                    }
                }

                TaskRowItem sonarRow = taskRows.Find(r => r.machineType == MachineUIType.MapNavigation);
                if (sonarRow != null && sonarRow.rowContainer != null)
                {
                    sonarRow.rowContainer.SetActive(true);

                    switch (currentPhase)
                    {
                        case TutorialPhase.SonarStation:
                            if (headerTitleText != null) headerTitleText.text = "<color=#ffd700>NEW QUEST: COMMAND HELM</color>";
                            SetRowPendingWithText(sonarRow, "1. Bridge: Fire Sonar Ping [SPACE]");
                            break;
                        case TutorialPhase.SteerToExit:
                            if (headerTitleText != null) headerTitleText.text = "<color=#4f4>QUEST: REACH EXIT</color>";
                            SetRowPendingWithText(sonarRow, "2. Steer towards EXIT BEACON (0m)");
                            break;
                        case TutorialPhase.Complete:
                            if (headerTitleText != null) headerTitleText.text = "<color=#4f4>TUTORIAL COMPLETE!</color>";
                            SetRowCompleted(sonarRow);
                            break;
                    }
                }
            }

            UpdateCounter();
        }

        private void OnDestroy()
        {
            if (TutorialManager.Instance != null)
            {
                TutorialManager.Instance.OnMachineTaskCompleted -= HandleTaskCompleted;
                TutorialManager.Instance.OnPhaseChanged -= HandlePhaseChanged;
            }

            if (Instance == this) Instance = null;
        }
    }
}
