using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.SDK3.Components;
using Random = UnityEngine.Random;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FrisbyPickupHandler : UdonSharpBehaviour
{
    [Header("References")]
    public FrisbyLauncher launcher;

    [Tooltip("Assign the MouthTracker GameObject — frisbee follows its transform when caught")]
    public Transform mouthTrackerTransform;

    [Header("Audio / VFX")]
    public AudioSource audioSource;
    public AudioClip flightClip;
    public AudioClip catchClip;
    public AudioClip bounceClip;

    [Tooltip("Parent object whose children are all catch particle systems.")]
    public GameObject catchParticlesRoot;

    [Tooltip("Parent object whose children are all bounce particle systems.")]
    public GameObject bounceParticlesRoot;

    [Header("Throw & Despawn Settings")]
    public float despawnWindowDelay = 2f;
    public float postCollisionDespawnDelay = 1f;

    [HideInInspector] public string pendingZoneName = "";

    private Rigidbody rb;
    private VRCPickup pickup;
    private VRCPlayerApi localPlayer;

    private bool isAttachedToMouth = false;
    private bool isThrown = false;
    private bool canDespawnOnCollision = false;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        pickup = (VRCPickup)GetComponent(typeof(VRCPickup));
        localPlayer = Networking.LocalPlayer;

        rb.isKinematic = true;
        rb.useGravity = false;
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        if (pickup != null) pickup.pickupable = false;

        if (catchParticlesRoot != null) catchParticlesRoot.SetActive(false);
        if (bounceParticlesRoot != null) bounceParticlesRoot.SetActive(false);
    }

    // ── In-Flight Audio ──────────────────────────────────────────────────────

    public void StartFlightAudio()
    {
        if (audioSource == null || flightClip == null) return;
        audioSource.clip = flightClip;
        audioSource.loop = true;
        audioSource.Play();
    }

    public void StopFlightAudio()
    {
        if (audioSource == null) return;
        audioSource.Stop();
        audioSource.loop = false;
        audioSource.clip = null;
    }

    // ── Called by FrisbyLauncher when frisbee arrives at mouth ───────────────

    public void AttachToMouth()
    {
        isAttachedToMouth = true;
        isThrown = false;
        canDespawnOnCollision = false;

        // ULTIMATE FIX: make the frisbee a ghost object during mouth attachment
        // Layer 0 (Default) collides with everything — move to Layer 2 (TransparentFX)
        // which ignores player collision entirely
        gameObject.layer = 2;  // TransparentFX layer

        rb.isKinematic = true;
        rb.useGravity = false;
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        if (pickup != null) pickup.pickupable = true;

        if (audioSource != null && catchClip != null)
            audioSource.PlayOneShot(catchClip);

        if (catchParticlesRoot != null)
            catchParticlesRoot.SetActive(true);
    }

    // ── Called by FrisbyLauncher when frisbee hits a head zone ───────────────

    public void TriggerBounce()
    {
        string zone = pendingZoneName;
        pendingZoneName = "";

        isAttachedToMouth = false;
        isThrown = true;
        canDespawnOnCollision = false;

        // Restore collision layer for bounce physics
        gameObject.layer = 0;  // Default layer

        rb.isKinematic = false;
        rb.useGravity = true;

        if (pickup != null) pickup.pickupable = false;

        if (localPlayer != null)
        {
            var head = localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
            Vector3 bounceDir;

            if (zone == "Back") bounceDir = -(head.rotation * Vector3.forward) + Vector3.up * 0.8f;
            else if (zone == "Left") bounceDir = head.rotation * Vector3.right + Vector3.up * 0.5f;
            else if (zone == "Right") bounceDir = -(head.rotation * Vector3.right) + Vector3.up * 0.5f;
            else bounceDir = head.rotation * Vector3.forward + Vector3.up;

            rb.velocity = bounceDir.normalized * 3.5f;
            rb.angularVelocity = new Vector3(
                Random.Range(-6f, 6f), Random.Range(-6f, 6f), Random.Range(-6f, 6f));
        }

        if (audioSource != null && bounceClip != null)
            audioSource.PlayOneShot(bounceClip);

        if (bounceParticlesRoot != null)
            bounceParticlesRoot.SetActive(true);

        SendCustomEventDelayedSeconds("EnableCollisionDespawn", despawnWindowDelay);
    }

    // ── VRCPickup Callbacks ──────────────────────────────────────────────────

    public override void OnPickup()
    {
        isAttachedToMouth = false;
        canDespawnOnCollision = false;
        isThrown = false;
    }

    public override void OnDrop()
    {
        isThrown = true;
        canDespawnOnCollision = false;

        // Restore collision layer for drop physics
        gameObject.layer = 0;  // Default layer

        rb.isKinematic = false;
        rb.useGravity = true;

        if (pickup != null) pickup.pickupable = false;

        SendCustomEventDelayedSeconds("EnableCollisionDespawn", despawnWindowDelay);
    }

    // ── Despawn Logic ────────────────────────────────────────────────────────

    public void EnableCollisionDespawn()
    {
        if (isThrown) canDespawnOnCollision = true;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (!canDespawnOnCollision) return;
        canDespawnOnCollision = false;
        SendCustomEventDelayedSeconds("Despawn", postCollisionDespawnDelay);
    }

    public void Despawn()
    {
        isThrown = false;
        isAttachedToMouth = false;
        canDespawnOnCollision = false;

        rb.isKinematic = true;
        rb.useGravity = false;
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        if (pickup != null) pickup.pickupable = false;

        if (catchParticlesRoot != null) catchParticlesRoot.SetActive(false);
        if (bounceParticlesRoot != null) bounceParticlesRoot.SetActive(false);

        gameObject.SetActive(false);

        if (launcher != null)
            launcher.OnFrisbyDespawned();
    }

    // ── Mouth Attach Follow ──────────────────────────────────────────────────

    public override void PostLateUpdate()
    {
        if (!isAttachedToMouth) return;

        if (mouthTrackerTransform != null)
        {
            transform.position = mouthTrackerTransform.position;
            transform.rotation = mouthTrackerTransform.rotation;
        }
    }
}
