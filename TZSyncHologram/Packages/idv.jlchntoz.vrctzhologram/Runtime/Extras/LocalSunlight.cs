using System;
using UnityEngine;
using VRC.SDKBase;
using VRC.SDK3.Data;
using UdonSharp;
using JLChnToZ.VRC.Foundation;

namespace JLChnToZ.VRC.TimeZoneSyncHologram {
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class LocalSunlight : UdonSharpEventSender {
        const long JD2000_TICKS = 630823248000000000L;
        const double DAY_PER_TICKS = 1.0 / TimeSpan.TicksPerDay;
        const float SUNRISE_ANGLE = 0.0145438976515827F; // Sin of 0.83 degree
        [SerializeField, HideInInspector, BindUdonSharpEvent] TimeZoneManagerV2 timeZoneManager;
        public bool calcNextSolarEventTime;
        [NonSerialized] public double latitude, longitude;
        [NonSerialized] public DateTime nextSunrise, nextSunset, nextSolarEvent;
        [NonSerialized] public bool hasSunriseAndSunset;
        Light sunLight;
        DateTime now;
        double daySinceJ2k;
        float sinLatitude, cosLatitude, sinLongitude, cosLongitude;
        float solarLongitude, rightAsc, sinDecl, cosDecl;
        bool isSlowUpdateFired;

        void Start() {
            sunLight = GetComponent<Light>();
            if (sunLight != null) {
                sunLight.useColorTemperature = true;
                sunLight.type = LightType.Directional;
                sunLight.intensity = 0F; // Disable the light first
            }
        }

        void OnEnable() {
            if (!isSlowUpdateFired) SendCustomEventDelayedFrames(nameof(_SlowUpdate), 0);
        }

        public void _OnTzDataReady() {
            var tzData = timeZoneManager.GetLocalTimezone();
            if (tzData != null) {
                if (tzData.TryGetValue("latitude", TokenType.Double, out var token)) {
                    latitude = token.Double;
                    _onVarChange_latitude();
                }
                if (tzData.TryGetValue("longitude", TokenType.Double, out token)) {
                    longitude = token.Double;
                    _onVarChange_longitude();
                }
            }
        }

        public void _SlowUpdate() {
            if (!enabled || !gameObject.activeInHierarchy) {
                isSlowUpdateFired = false;
                return;
            }
            SendCustomEventDelayedSeconds(nameof(_SlowUpdate), 1);
            CalculateSunCoordinates();
            if (sunLight != null) SimulateSun();
            if (calcNextSolarEventTime) DetermineNextSolarEvent();
        }

        void CalculateSunCoordinates() {
            now = Networking.GetNetworkDateTime();
            daySinceJ2k = (now.Ticks - JD2000_TICKS) * DAY_PER_TICKS;
            solarLongitude = ToFloatDegrees(280.460 + 0.9856474 * daySinceJ2k);
            float g = ToFloatDegrees(357.528 + 0.9856003 * daySinceJ2k) * Mathf.Deg2Rad;
            SinCos(
                (solarLongitude + 1.915F * Mathf.Sin(g) + 0.020F * Mathf.Sin(g * 2F)) * Mathf.Deg2Rad,
                out float sinEclLng, out float cosEclLng
            );
            SinCos(
                (23.439F - (float)(0.0000004 * daySinceJ2k)) * Mathf.Deg2Rad,
                out float sinEclObl, out float cosEclObl
            );
            rightAsc = Mathf.Atan2(cosEclObl * sinEclLng, cosEclLng);
            sinDecl = sinEclObl * sinEclLng;
            cosDecl = Mathf.Sqrt(1 - sinDecl * sinDecl);
        }

        void SimulateSun() {
            double utcHourAngle = (daySinceJ2k + 0.5) % 1.0 * 360.0;
            float lst = ToFloatDegrees(100.46 + 0.985647352 * daySinceJ2k + longitude + utcHourAngle) * Mathf.Deg2Rad;
            float cosHourAngle = Mathf.Cos(lst - rightAsc);
            float sinElevation = sinLatitude * sinDecl + cosLatitude * cosDecl * cosHourAngle;
            float elevation = Mathf.Asin(sinElevation);
            float cosElevation = Mathf.Cos(elevation);
            elevation *= Mathf.Rad2Deg;
            float azimuth = Mathf.Acos((sinDecl - sinLatitude * sinElevation) / cosLatitude / cosElevation) * Mathf.Rad2Deg;
            sunLight.intensity = Mathf.Max(0, sinElevation);
            sunLight.colorTemperature = 2200F + 2300F * sinElevation;
            transform.localRotation = Quaternion.Euler(elevation, azimuth, 0F);
        }

        void DetermineNextSolarEvent() {
            float solarNoon = 720F - ((float)longitude - (solarLongitude - rightAsc) * Mathf.Rad2Deg) * 4F;
            float cosHourAngle = (SUNRISE_ANGLE - sinLatitude * sinDecl) / cosLatitude / cosDecl;
            if (cosHourAngle < -1F || cosHourAngle > 1F) {
                hasSunriseAndSunset = false;
                this.nextSunrise = this.nextSunset = DateTime.MinValue;
                return;
            }
            float deltaTime = Mathf.Acos(cosHourAngle) * Mathf.Rad2Deg * 4F;
            var todaySolarNoon = now.Date.AddMinutes(solarNoon);
            var nextSunrise = todaySolarNoon.AddMinutes(-deltaTime);
            if (nextSunrise < now) nextSunrise = nextSunrise.AddDays(1);
            var nextSunset = todaySolarNoon.AddMinutes(deltaTime);
            if (nextSunset < now) nextSunset = nextSunset.AddDays(1);
            if (hasSunriseAndSunset) {
                if (this.nextSunrise < now) SendEvent("_OnSunrise");
                if (this.nextSunset < now) SendEvent("_OnSunset");
            }
            this.nextSunrise = nextSunrise;
            this.nextSunset = nextSunset;
            nextSolarEvent = nextSunrise < nextSunset ? nextSunrise : nextSunset;
            hasSunriseAndSunset = true;
        }

        float ToFloatDegrees(double degrees) => (float)(degrees % 360.0);

        void SinCos(float angle, out float sin, out float cos) {
            sin = Mathf.Sin(angle);
            cos = Mathf.Cos(angle);
        }

        public void _onVarChange_latitude() => SinCos((float)latitude * Mathf.Deg2Rad, out sinLatitude, out cosLatitude);

        public void _onVarChange_longitude() => SinCos((float)longitude * Mathf.Deg2Rad, out sinLongitude, out cosLongitude);
    }
}