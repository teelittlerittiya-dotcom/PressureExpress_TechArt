using UnityEngine;

public class PlatformUnit : MonoBehaviour
{
    // In 3D, one-way platform logic is handled via Physics.IgnoreCollision
    // or by toggling the collider. This component stores a reference to
    // its own collider so the player controller can toggle collision.
    public Collider platformCollider;

    void Awake()
    {
        platformCollider = GetComponent<Collider>();
        PlatformController.Register(this);
    }

    /// <summary>
    /// Lets one specific body pass through this platform for <paramref name="time"/> seconds.
    /// Takes the requester's collider on purpose: disabling the platform collider outright
    /// would make it non-solid for every player in the session, not just the one dropping.
    /// </summary>
    public void AllowDropDown(Collider requester, float time)
    {
        if (requester == null || platformCollider == null) return;
        StartCoroutine(DropRoutine(requester, time));
    }

    private System.Collections.IEnumerator DropRoutine(Collider requester, float time)
    {
        Physics.IgnoreCollision(requester, platformCollider, true);
        yield return new WaitForSeconds(time);

        // Physics.IgnoreCollision is global state - restore it even if either side went away.
        if (requester != null && platformCollider != null)
            Physics.IgnoreCollision(requester, platformCollider, false);
    }
}
