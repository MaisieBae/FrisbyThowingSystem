using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class HeadZoneTracker : UdonSharpBehaviour
{
    [Header("References")]
    public FrisbyLauncher launcher;

    [Header("Head Zone Transforms")]
    public Transform headFront;
    public Transform headBack;
    public Transform headLeft;
    public Transform headRight;

    // Written by HeadZone before calling OnZoneHit
    [HideInInspector] public string pendingZoneName = "";

    private VRCPlayerApi localPlayer;
    private const float HeadRadius = 0.13f;

    void Start() => localPlayer = Networking.LocalPlayer;

    public override void PostLateUpdate()
    {
        if (localPlayer == null) return;

        var head = localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
        Vector3 pos = head.position;
        Quaternion rot = head.rotation;

        if (headFront != null) headFront.position = pos + rot * new Vector3(0, 0, HeadRadius);
        if (headBack != null) headBack.position = pos + rot * new Vector3(0, 0, -HeadRadius);
        if (headLeft != null) headLeft.position = pos + rot * new Vector3(-HeadRadius, 0, 0);
        if (headRight != null) headRight.position = pos + rot * new Vector3(HeadRadius, 0, 0);
    }

    // Called by HeadZone via SendCustomEvent — reads pendingZoneName set beforehand
    public void OnZoneHit()
    {
        string zoneName = pendingZoneName;
        pendingZoneName = ""; // Clear immediately to prevent stale data on rapid hits

        if (launcher != null)
            launcher.OnHeadHit(zoneName);
    }
}