using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace PressureExpress.Framework
{
    /// <summary>
    /// Abstract base component for UI-based minigames in ship machines.
    /// Provides standardized state tracking, completion callbacks, progress scaling helpers, 
    /// and event-driven close button binding across all UI prefabs.
    /// </summary>
    public abstract class MinigameBaseUI : MonoBehaviour
    {
        [Header("Minigame Base Settings")]
        [SerializeField] protected bool autoResetOnEnable = true;
        public UnityEvent OnMinigameCompleted;

        [Header("Event-Driven UI Close Controls")]
        [SerializeField] protected Button closeButton;
        public UnityEvent OnCloseClicked;

        [Header("Tutorial Binding")]
        [SerializeField] protected GameObject tutorialPanel;
        [SerializeField] protected Button tutorialButton;

        public bool IsTutorialOpen => 
            (tutorialPanel != null && tutorialPanel.activeInHierarchy) ||
            (PressureExpress.Tutorial.TutorialMinigameOverlay.Instance != null && PressureExpress.Tutorial.TutorialMinigameOverlay.Instance.IsShowing);

        /// <summary>
        /// C# Instance event fired when the player requests to close the minigame UI.
        /// </summary>
        public event Action OnCloseRequested;

        /// <summary>
        /// Global static C# event fired whenever any Minigame UI requests to close.
        /// </summary>
        public static event Action<MinigameBaseUI> OnAnyMinigameCloseRequested;

        public bool IsCompleted { get; protected set; }
        public float Progress { get; protected set; }

        protected virtual void Awake()
        {
            ValidateCloseButton();
            ValidateTutorial();
        }

        protected virtual void OnEnable()
        {
            if (closeButton == null) ValidateCloseButton();
            if (tutorialPanel == null || tutorialButton == null) ValidateTutorial();

            if (autoResetOnEnable)
            {
                ResetMinigame();
            }
        }

        private void ValidateCloseButton()
        {
            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(RequestClose);
                closeButton.onClick.AddListener(RequestClose);
            }
            else
            {
                Debug.LogWarning($"[{GetType().Name}] 'closeButton' is not bound on '{gameObject.name}'. Please assign it in the Inspector.", this);
            }
        }

        protected virtual void ValidateTutorial()
        {
            if (tutorialPanel == null)
            {
                foreach (Transform child in GetComponentsInChildren<Transform>(true))
                {
                    string n = child.name.ToLower();
                    if (n.Contains("tutorial") && (n.Contains("panel") || n.Contains("parent") || n.Contains("image") || n == "tutorial"))
                    {
                        tutorialPanel = child.gameObject;
                        break;
                    }
                }
            }

            if (tutorialButton == null)
            {
                foreach (Button btn in GetComponentsInChildren<Button>(true))
                {
                    string n = btn.gameObject.name.ToLower();
                    if (n.Contains("tutorial") || n.Contains("help") || n.Contains("info") || n.Contains("guide"))
                    {
                        tutorialButton = btn;
                        break;
                    }
                }
            }

            if (tutorialButton != null && tutorialPanel != null)
            {
                tutorialButton.onClick.RemoveListener(ToggleTutorial);
                tutorialButton.onClick.AddListener(ToggleTutorial);
            }
        }

        public virtual void ToggleTutorial()
        {
            if (tutorialPanel != null)
            {
                tutorialPanel.SetActive(!tutorialPanel.activeSelf);
            }
        }

        /// <summary>
        /// Public method called by the Close Button event or UnityEvent in Inspector to close the UI.
        /// </summary>
        public void RequestClose()
        {
            OnCloseClicked?.Invoke();
            OnCloseRequested?.Invoke();
            OnAnyMinigameCloseRequested?.Invoke(this);

            var canvas = CanvasManager.Instance;
            if (canvas != null)
            {
                canvas.CloseCurrentUI();
            }
        }

        public virtual void ResetMinigame()
        {
            IsCompleted = false;
            Progress = 0f;
        }

        protected virtual void Update()
        {
            if (IsCompleted || IsTutorialOpen) return;
            OnMinigameUpdate();
        }

        protected abstract void OnMinigameUpdate();

        protected virtual void SetProgress(float value)
        {
            Progress = Mathf.Clamp01(value);
            if (Progress >= 1f && !IsCompleted)
            {
                CompleteMinigame();
            }
        }

        protected virtual void CompleteMinigame()
        {
            IsCompleted = true;
            OnMinigameCompleted?.Invoke();
        }

        protected void UpdateBarScaleY(Transform barTransform, float normalizedValue)
        {
            if (barTransform == null) return;
            Vector3 scale = barTransform.localScale;
            scale.y = Mathf.Clamp01(normalizedValue);
            barTransform.localScale = scale;
        }

        protected void UpdateBarScaleX(Transform barTransform, float normalizedValue)
        {
            if (barTransform == null) return;
            Vector3 scale = barTransform.localScale;
            scale.x = Mathf.Clamp01(normalizedValue);
            barTransform.localScale = scale;
        }
    }
}
