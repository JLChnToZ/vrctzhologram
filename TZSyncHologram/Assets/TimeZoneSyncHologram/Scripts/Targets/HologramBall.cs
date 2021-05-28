using System;
using UdonSharp;
using UnityEngine;

[UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
public class HologramBall : UdonSharpBehaviour {
    [SerializeField] Vector3 axis = Vector3.up;
    [NonSerialized] public TimeSpan networkTimeOffset;

    void Update() {
        var timeOfDay = (DateTime.UtcNow + networkTimeOffset).TimeOfDay;
        transform.localRotation = Quaternion.AngleAxis((float)timeOfDay.TotalSeconds * 6, axis);
    }
}
