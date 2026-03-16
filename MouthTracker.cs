using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Dynamics;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class MouthTracker : UdonSharpBehaviour
{
    public FrisbyLauncher launcher;

    [Header("Mouth Offset (Local Space, relative to Head bone)")]
    [Tooltip("Move the mouth point relative to the head. Z = forward, Y = up/down, X = left/right.")]
    public Vector3 positionOffset = new Vector3(0f, -0.05f, 0.09f);

    [Tooltip("Rotate the frisbee attachment point relative to head rotation. Tune if frisbee sits at a wrong angle.")]
    public Vector3 rotationOffset = new Vector3(0f, 0f, 0f);

    private VRCPlayerApi localPlayer;

    void Start() => localPlayer = Networking.LocalPlayer;

    public override void PostLateUpdate()
    {
        if (localPlayer == null) return;

        var head = localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);

        // Apply position offset in head-local space so it rotates correctly with the head
        transform.position = head.position + head.rotation * positionOffset;
        transform.rotation = head.rotation * Quaternion.Euler(rotationOffset);
    }

    public override void OnContactEnter(ContactEnterInfo info)
    {
        if (!info.contactSender.isValid) return;
        if (launcher != null) launcher.OnFrisbeeCaught();
    }
}