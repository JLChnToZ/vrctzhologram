using System;
using UnityEngine;
using UdonSharp;

namespace JLChnToZ.VRC.TimeZoneSyncHologram {
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class TZClock : TimeZoneViewTargetBase {
        [SerializeField] Transform hourHand, minuteHand;
        [SerializeField] float paddingX = 1, paddingY = 1;
        [SerializeField] int maxColumn = 4;

        protected override void UpdateText() {
            base.UpdateText();
            var time = DateTime.UtcNow.AddMinutes(timeOffset);
            float minuteAngle = time.Minute * 6f + time.Second / 10f;
            float hourAngle = time.Hour * 30f + minuteAngle / 12f;
            minuteHand.localRotation = Quaternion.Euler(0, 0, minuteAngle);
            hourHand.localRotation = Quaternion.Euler(0, 0, hourAngle);
        }

        public override void SetMetaInfo(int index, string tzid, double offset) {
            base.SetMetaInfo(index, tzid, offset);
            var row = index / maxColumn;
            var column = index % maxColumn;
            column = column % 2 == 0 ? column / 2 : -column / 2 - 1;
            transform.localPosition = new Vector3(column * paddingX, -row * paddingY, 0);
        }
    }
}