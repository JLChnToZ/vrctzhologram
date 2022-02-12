using System;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;

namespace JLChnToZ.VRC.TimeZoneSyncHologram {
[UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
public class TZHologramEntry : UdonSharpBehaviour {
    [SerializeField] Animator textDisplayController;
    [SerializeField] LineRenderer lineRenderer;
    [SerializeField] Transform textTransform;
    [SerializeField] Text textDisplay, textDisplay2;
    bool started = false;
    bool isActive;
    [NonSerialized] public string playerNames = "";
    [NonSerialized] public string tzName;
    [NonSerialized] public string tzAltName;
    [NonSerialized] public float latitude;
    [NonSerialized] public float longitude;
#if UNITY_ANDROID
    [NonSerialized] public TimeSpan tzOffset;
#else
    [NonSerialized] public TimeZoneInfo timeZone;
#endif
    [NonSerialized] public TimeSpan networkTimeOffset;

    void Start() {
        started = true;
        UpdateState();
    }

    void Update() {
        if (!isActive) return;
    #if UNITY_ANDROID
        var now = DateTime.UtcNow + networkTimeOffset + tzOffset;
        textDisplay2.text = textDisplay.text = $"{playerNames}\n{tzName}\n{now:HH:mm:ss}";
    #else
        if (timeZone != null) {
            var now = TimeZoneInfo.ConvertTime(DateTime.UtcNow + networkTimeOffset, timeZone);
            textDisplay2.text = textDisplay.text = $"{playerNames}\n{tzName}\n{now:HH:mm:ss}";
        } else
            textDisplay2.text = textDisplay.text = $"{playerNames}\n{tzName}";
    #endif
    }

    public void SetActive() {
        isActive = true;
        if (started) UpdateState();
    }

    public void SetInactive() {
        isActive = false;
    #if !UNITY_ANDROID
        timeZone = null;
    #endif
        if (started) UpdateState();
    }

    void UpdateState() {
        float radLat = (latitude - 90) * Mathf.Deg2Rad;
        float radLon = longitude * Mathf.Deg2Rad;
        float sinLat = Mathf.Sin(radLat);
        var pos = new Vector3(sinLat * Mathf.Cos(radLon), Mathf.Cos(radLat), sinLat * Mathf.Sin(radLon));
        transform.localPosition = pos * transform.localPosition.magnitude;
        var pos2 = pos * textTransform.localPosition.magnitude;
        lineRenderer.SetPosition(1, pos2);
        textTransform.localPosition = pos2;
        textTransform.localRotation = Quaternion.AngleAxis(Quaternion.LookRotation(pos2).eulerAngles.y + 90, Vector3.up);
        if (textDisplayController != null)
            textDisplayController.SetBool("isActive", isActive);
    }
}
}
