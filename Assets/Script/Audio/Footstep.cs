using UnityEngine;

public class Footstep : MonoBehaviour
{
    [Header("Footstep Audio")]
    public AudioClip[] footstepClips;
    public float stepRate = 0.4f;
    [SerializeField, Range(0f, 1f)] private float stepVolume = 0.8f;

    [Header("Ground Check")]
    [SerializeField] private float raycastDistance = 1.2f;
    [SerializeField] private LayerMask groundMask = ~0;

    private float timer;
    private Rigidbody rb;
    private CharacterController cc;
    private SFXSource sfxSource;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        cc = GetComponent<CharacterController>();
        sfxSource = GetComponent<SFXSource>();
    }

    private void Update()
    {
        Vector3 velocity = Vector3.zero;
        if (cc != null)
        {
            velocity = cc.velocity;
        }
        else if (rb != null)
        {
            velocity = rb.linearVelocity;
        }

        Vector3 horizontalVelocity = new Vector3(velocity.x, 0f, velocity.z);
        if (horizontalVelocity.magnitude > 0.1f)
        {
            timer -= Time.deltaTime;

            if (timer <= 0f)
            {
                if (Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, out RaycastHit hit, raycastDistance, groundMask))
                {
                    if (footstepClips != null && footstepClips.Length > 0)
                    {
                        AudioClip clip = footstepClips[Random.Range(0, footstepClips.Length)];
                        if (clip != null)
                        {
                            if (SpatialAudioManager.Instance != null)
                            {
                                SpatialAudioManager.Instance.PlaySFXAtPosition(clip, hit.point, stepVolume);
                            }
                            else if (sfxSource != null)
                            {
                                sfxSource.SetBaseVolume(stepVolume);
                                sfxSource.PlayOneShot(clip, stepVolume);
                            }
                        }
                    }
                }
                timer = stepRate;
            }
        }
        else
        {
            timer = 0f;
        }
    }
}
