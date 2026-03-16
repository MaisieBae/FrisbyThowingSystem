using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using Random = UnityEngine.Random;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FrisbyLauncher : UdonSharpBehaviour
{
    [Header("Scene References")]
    public GameObject frisbee;
    public Transform[] spawnPoints;
    public AudioSource[] spawnAudioSources;
    public UdonSharpBehaviour pickupHandler;

    [Tooltip("Assign the MouthTracker GameObject here — flight targeting reads its position")]
    public Transform mouthPoint;

    [Header("Pre-Launch Audio")]
    public AudioClip preLaunchClip;
    public AudioClip launchClip;
    public float minWarnDelay = 1f;
    public float maxWarnDelay = 3f;

    [Header("Flight Settings")]
    public float flySpeed = 5f;
    public float snapSpeed = 20f;
    public float snapRange = 0.3f;
    public float arrivalThreshold = 0.05f;

    [Header("Spin Settings")]
    public float spinSpeedMax = 600f;
    public float spinRampTime = 0.4f;
    public float wobbleAmount = 8f;
    public float wobbleFrequency = 3f;

    private bool isFlying = false;
    private bool isSystemActive = false;
    private bool launchPending = false;
    private int chosenSpawnIndex = -1;
    private float flightTime = 0f;
    private VRCPlayerApi localPlayer;

    void Start()
    {
        localPlayer = Networking.LocalPlayer;
        if (frisbee != null) frisbee.SetActive(false);
    }

    // ── Avatar Toggle ────────────────────────────────────────────────────────

    public void ActivateSystem()
    {
        isSystemActive = true;
        LaunchFrisbee();
    }

    public void DeactivateSystem()
    {
        isSystemActive = false;
        // Do NOT cancel launchPending — a committed launch must complete
        if (isFlying)
        {
            isFlying = false;
            if (frisbee != null) frisbee.SetActive(false);
            if (pickupHandler != null) pickupHandler.SendCustomEvent("StopFlightAudio");
        }
    }

    // ── Launch Sequence ──────────────────────────────────────────────────────

    public void LaunchFrisbee()
    {
        if (!isSystemActive || isFlying || launchPending) return;
        if (spawnPoints == null || spawnPoints.Length == 0) return;

        chosenSpawnIndex = Random.Range(0, spawnPoints.Length);
        launchPending = true;

        PlaySpawnAudio(chosenSpawnIndex, preLaunchClip);

        float delay = Random.Range(minWarnDelay, maxWarnDelay);
        SendCustomEventDelayedSeconds("ExecuteLaunch", delay);
    }

    public void ExecuteLaunch()
    {
        if (!launchPending) return;

        launchPending = false;

        PlaySpawnAudio(chosenSpawnIndex, launchClip);

        frisbee.transform.position = spawnPoints[chosenSpawnIndex].position;
        frisbee.transform.rotation = spawnPoints[chosenSpawnIndex].rotation;
        frisbee.SetActive(true);

        // CRITICAL FIX: force flight-safe Rigidbody state IMMEDIATELY after spawn
        // because something is setting physics ON before StartFlightAudio
        var rb = frisbee.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        if (pickupHandler != null) pickupHandler.SendCustomEvent("StartFlightAudio");

        flightTime = 0f;
        isFlying = true;
    }

    private void PlaySpawnAudio(int index, AudioClip clip)
    {
        if (spawnAudioSources == null || index >= spawnAudioSources.Length) return;
        if (spawnAudioSources[index] == null || clip == null) return;
        spawnAudioSources[index].PlayOneShot(clip);
    }

    // ── Catch / Hit Callbacks ────────────────────────────────────────────────

    public void OnFrisbeeCaught()
    {
        if (!isFlying) return;
        isFlying = false;
        flightTime = 0f;

        if (pickupHandler != null)
        {
            pickupHandler.SendCustomEvent("StopFlightAudio");
            pickupHandler.SendCustomEvent("AttachToMouth");
        }
    }

    public void OnHeadHit(string zoneName)
    {
        if (!isFlying) return;

        isFlying = false;
        flightTime = 0f;

        if (pickupHandler != null)
        {
            pickupHandler.SendCustomEvent("StopFlightAudio");
            pickupHandler.SetProgramVariable("pendingZoneName", zoneName);
            pickupHandler.SendCustomEvent("TriggerBounce");
        }
    }

    public void OnFrisbyDespawned()
    {
        isFlying = false;
        launchPending = false;
    }

    // ── Flight Update ────────────────────────────────────────────────────────

    void Update()
    {
        if (!isFlying || localPlayer == null) return;

        flightTime += Time.deltaTime;

        Vector3 mouthPos = GetMouthPosition();
        float dist = Vector3.Distance(frisbee.transform.position, mouthPos);

        if (dist > snapRange)
        {
            frisbee.transform.position = Vector3.MoveTowards(
                frisbee.transform.position, mouthPos, flySpeed * Time.deltaTime);

            Vector3 dir = (mouthPos - frisbee.transform.position).normalized;
            if (dir.sqrMagnitude > 0.001f)
                frisbee.transform.rotation =
                    Quaternion.LookRotation(dir) * Quaternion.Euler(90f, 0f, 0f);
        }
        else
        {
            frisbee.transform.position = Vector3.Lerp(
                frisbee.transform.position, mouthPos, Time.deltaTime * snapSpeed);
        }

        UpdateFrisbeeSpin(dist);

        if (dist < arrivalThreshold)
            OnFrisbeeCaught();
    }

    private void UpdateFrisbeeSpin(float distToMouth)
    {
        float rampFactor = Mathf.Clamp01(flightTime / spinRampTime);
        float slowFactor = Mathf.Clamp01(distToMouth / snapRange);
        float spinThisFrame = spinSpeedMax * rampFactor * slowFactor * Time.deltaTime;

        frisbee.transform.Rotate(Vector3.up, spinThisFrame, Space.Self);

        float wobbleTilt = Mathf.Sin(flightTime * wobbleFrequency) * wobbleAmount * rampFactor;
        frisbee.transform.Rotate(Vector3.forward, wobbleTilt, Space.Self);
    }

    // ── Mouth Position ───────────────────────────────────────────────────────

    private Vector3 GetMouthPosition()
    {
        if (mouthPoint != null) return mouthPoint.position;

        // Fallback if mouthPoint isn't assigned
        var head = localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
        return head.position
            + head.rotation * new Vector3(0f, -0.05f, 0.09f);
    }
}
