using UnityEngine;
using Cysharp.Threading.Tasks;
using MoreMountains.Feedbacks;
using PressureExpress.Network;
using UnityEngine.SceneManagement;

namespace PressureExpress.Tutorial
{
    public class TutorialExitBeacon : MonoBehaviour
    {
        [Header("Feedback")]
        [SerializeField] private MMF_Player victoryFeedback;
        [SerializeField] private GameObject celebrationEffects;
        [SerializeField] private float exitDelay = 3f;
        [SerializeField] private string fallbackMenuScene = "MainMenu";

        private bool hasTriggered = false;

        private void OnTriggerEnter(Collider other)
        {
            if (hasTriggered) return;

            bool isPlayerOrSub = other.CompareTag("Player") ||
                                 other.transform.root.CompareTag("Player") ||
                                 other.GetComponentInParent<SubmarineCollision>() != null ||
                                 other.GetComponentInParent<SubmarineManager>() != null;

            if (isPlayerOrSub)
            {
                hasTriggered = true;
                ExecuteVictorySequence().Forget();
            }
        }

        private async UniTaskVoid ExecuteVictorySequence()
        {
            Debug.Log("[TutorialExitBeacon] Submarine reached Exit Beacon! Triggering Victory...");

            if (TutorialManager.Instance != null)
            {
                TutorialManager.Instance.FinishTutorial();
            }

            if (victoryFeedback != null)
            {
                victoryFeedback.PlayFeedbacks();
            }

            if (celebrationEffects != null)
            {
                celebrationEffects.SetActive(true);
            }

            await UniTask.Delay((int)(exitDelay * 1000f));

            ReturnToMainMenu();
        }

        private void ReturnToMainMenu()
        {
            SessionService session = SessionService.Instance;
            if (session != null)
            {
                session.LeaveSessionAsync().Forget();
            }
            else
            {
                SceneManager.LoadScene(fallbackMenuScene);
            }
        }
    }
}
