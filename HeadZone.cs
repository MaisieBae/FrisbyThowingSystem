using UdonSharp;
using UnityEngine;
using VRC.Dynamics;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class HeadZone : UdonSharpBehaviour
{
    public HeadZoneTracker tracker;

    // Set in Inspector: "Front", "Back", "Left", or "Right"
    public string zoneName;

    public override void OnContactEnter(ContactEnterInfo info)
    {
        if (!info.contactSender.isValid) return;
        if (tracker == null) return;

        // SetProgramVariable + SendCustomEvent — no parameter passed directly
        tracker.SetProgramVariable("pendingZoneName", zoneName);
        tracker.SendCustomEvent("OnZoneHit");
    }
}