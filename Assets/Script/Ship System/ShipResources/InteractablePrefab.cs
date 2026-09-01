using System.Collections;
using Unity.Mathematics;
using UnityEngine;

public class InteractablePrefab : MonoBehaviour
{
    [SerializeField] protected interactableUI interactableUI;
    [SerializeField] protected SpriteRenderer objectSprite;

    [SerializeField] protected float UI_YOffset = 1f;
    [SerializeField] protected float UI_Scale = 1f;
    
    [Space] 
    protected Rigidbody rb;
    protected BoxCollider boxCollider;

    protected Coroutine toggleUIOffCoroutie;
    
    protected virtual void OnEnable()
    {
        rb = GetComponent<Rigidbody>();
        boxCollider = GetComponent<BoxCollider>();
        if (interactableUI != null) interactableUI.UpdateUI();

        ToggleInfoUI(false);
    }
    
    protected virtual void OnDisable()
    {
        if (toggleUIOffCoroutie != null)
        {
            StopCoroutine(toggleUIOffCoroutie);
            toggleUIOffCoroutie = null;
        }
        if (interactableUI != null) interactableUI.gameObject.SetActive(false);
    }
    public virtual void ToggleInfoUI(bool setActive)
    {
        if (interactableUI != null) interactableUI.gameObject.SetActive(setActive);

        if (setActive)
        {
            if (toggleUIOffCoroutie != null)
            {
                StopCoroutine(toggleUIOffCoroutie);
                toggleUIOffCoroutie = null;
            }
        }
        else
        {
            if (toggleUIOffCoroutie != null)
                StopCoroutine(toggleUIOffCoroutie);

            toggleUIOffCoroutie = StartCoroutine(ToggleUIOff());
        }
    }

    private IEnumerator ToggleUIOff()
    {
        yield return new WaitForSeconds(0.2f);
        if (interactableUI != null) interactableUI.gameObject.SetActive(false);
    }

    public void UpdateUI()
    {
        if (interactableUI != null) interactableUI.UpdateUI();
    }
}
