using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
public class NetworkTimeSyncHandler: UdonSharpBehaviour {
    [SerializeField] GameObject[] targets;
    [SerializeField] float timeCheckInterval = 300;
    [NonSerialized] public TimeSpan networkTimeOffset;

    void Start() { SyncNetworkTime(); }

    public void SyncNetworkTime() {
        networkTimeOffset = DateTime.UtcNow - Networking.GetNetworkDateTime();
        for (int i = 0, l = targets.Length; i < l; i++)
            ((UdonBehaviour)targets[i].GetComponent(typeof(UdonBehaviour)))
            .SetProgramVariable("networkTimeOffset", networkTimeOffset);
        SendCustomEventDelayedSeconds(nameof(SyncNetworkTime), timeCheckInterval);
    }
}
