using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Dynamics;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class AvatarToggleTracker : UdonSharpBehaviour
{
    public FrisbyLauncher launcher;

    private VRCPlayerApi localPlayer;

    void Start() => localPlayer = Networking.LocalPlayer;

    public override void PostLateUpdate()
    {
        if (localPlayer == null) return;

        // Follow player chest — keeps receiver overlapping avatar's toggle sender
        Vector3 chestPos = localPlayer.GetBonePosition(HumanBodyBones.Chest);
        if (chestPos != Vector3.zero)
        {
            transform.position = chestPos;
        }
        else
        {
            var origin = localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Origin);
            transform.position = origin.position + Vector3.up * 1.2f;
        }
    }

    public override void OnContactEnter(ContactEnterInfo info)
    {
        if (!info.contactSender.isValid) return;
        if (launcher != null) launcher.ActivateSystem();
    }

    public override void OnContactExit(ContactExitInfo info)
    {
        if (!info.contactSender.isValid) return;
        if (launcher != null) launcher.DeactivateSystem();
    }
}