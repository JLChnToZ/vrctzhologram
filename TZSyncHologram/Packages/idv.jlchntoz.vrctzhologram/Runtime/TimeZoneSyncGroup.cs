using UdonSharp;
using UnityEngine;

namespace JLChnToZ.VRC.TimeZoneSyncHologram {
[UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
public class TimeZoneSyncGroup : UdonSharpBehaviour {
    [SerializeField] public GameObject[] targets;
}
}
