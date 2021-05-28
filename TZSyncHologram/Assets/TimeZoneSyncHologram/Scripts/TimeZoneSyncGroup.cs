using UdonSharp;
using UnityEngine;

[UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
public class TimeZoneSyncGroup : UdonSharpBehaviour {
    [SerializeField] public GameObject[] targets;
}
