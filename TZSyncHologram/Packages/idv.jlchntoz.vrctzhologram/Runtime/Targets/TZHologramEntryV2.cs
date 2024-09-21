using UnityEngine;
using VRC.SDK3.Data;
using UdonSharp;

namespace JLChnToZ.VRC.TimeZoneSyncHologram {
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class TZHologramEntryV2 : TimeZoneViewTargetBase {
        [SerializeField] Animator textDisplayController;
        [SerializeField] LineRenderer lineRenderer;
        [SerializeField] Transform textTransform;
        float latitude, longitude;
        bool isActive;

        public override void SetActive(bool active) {
            base.SetActive(active);
            if (!active) isActive = false;
        }

        public override void SetMetaInfo(string tzid, double offset) {
            base.SetMetaInfo(tzid, offset);
            if (tzData != null) {
                if (tzData.TryGetValue("latitude", TokenType.Double, out var token))
                    latitude = (float)token.Double;
                if (tzData.TryGetValue("longitude", TokenType.Double, out token))
                    longitude = (float)token.Double;
                isActive = true;
            } else
                isActive = false;
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