using System;
using UnityEngine;
using VRC.SDKBase;
using VRC.SDK3.Data;
using UdonSharp;
using JLChnToZ.VRC.Foundation;

namespace JLChnToZ.VRC.TimeZoneSyncHologram {
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class LocalSunlight : UdonSharpEventSender {
        const long J2000_TICKS = 630823248000000000L; // 2000-01-01 12:00:00 UTC
        const double DAYS_PER_TICK = 1.0 / TimeSpan.TicksPerDay;
        const float MINUTES_PER_DEGREE = 4F; // 1440 min / 360 deg of hour angle
        const float SUNRISE_SIN_ALTITUDE = -0.0145439F; // Altitude of the Sun's CENTRE at rise/set: -0.8333 deg (34' refraction + 16' semi-diameter)
        [SerializeField, HideInInspector, BindUdonSharpEvent(nameof(_OnTzDataReady))] TimeZoneManagerV2 timeZoneManager;
        [SerializeField, Min(0)] float peakIntensity = 1F; // use ~100000 (lux) with physical light units
        [SerializeField, Range(1000F, 8000F)] float horizonColorTemperature = 1800F;
        [SerializeField, Range(1000F, 8000F)] float zenithColorTemperature = 6500F;
        [SerializeField, Resolve(".")] Light sunLight;
        [SerializeField, HideInInspector, Resolve(nameof(sunLight))] Transform lightTransform;
        public bool calcSolarPosition;
        public bool calcNextSolarEventTime;
        [NonSerialized] public double latitude, longitude;
        [NonSerialized] public float solarElevation, solarAzimuth;
        [NonSerialized] public DateTime nextSunrise, nextSunset, nextSolarEvent;
        [NonSerialized] public bool hasSunriseAndSunset;
        [NonSerialized] public DayNightMode dayNightMode;
        DateTime now;
        double daysSinceJ2000;
        float sinLat, cosLat, sinDecl, cosDecl, rightAsc, meanLng;
        bool isSlowUpdateFired, useColorTemperature;

#if COMPILER_UDONSHARP
        public
#endif
        void _onVarChange_latitude() => SinCos((float)latitude * Mathf.Deg2Rad, out sinLat, out cosLat);

        void OnEnable() {
            if (Utilities.IsValid(sunLight)) {
                calcSolarPosition = true;
                useColorTemperature = sunLight.useColorTemperature;
                sunLight.intensity = 0F; // Disable the light first
            } else
                lightTransform = transform;
            if (!isSlowUpdateFired) SendCustomEventDelayedFrames(nameof(_SlowUpdate), 0);
        }

#if COMPILER_UDONSHARP
        public
#endif
        void _OnTzDataReady() {
            var tzData = timeZoneManager.GetLocalTimezone();
            if (Utilities.IsValid(tzData)) {
                if (tzData.TryGetValue("latitude", TokenType.Double, out var token)) {
                    latitude = token.Double;
                    _onVarChange_latitude();
                }
                if (tzData.TryGetValue("longitude", TokenType.Double, out token)) {
                    longitude = token.Double;
                }
            }
        }

#if COMPILER_UDONSHARP
        public
#endif
        void _SlowUpdate() {
            if (!isActiveAndEnabled) {
                isSlowUpdateFired = false;
                return;
            }
            SendCustomEventDelayedSeconds(nameof(_SlowUpdate), 1F);
            now = Networking.GetNetworkDateTime();
            if (calcSolarPosition) SimulateSun();
            if (calcNextSolarEventTime) DetermineNextSolarEvent();
        }

        void CalcDaysSinceJ2000(DateTime time, double offset) =>
            daysSinceJ2000 = (time.Ticks - J2000_TICKS) * DAYS_PER_TICK + offset;

        float WrapTime(double scale, double add) {
            double result = scale * daysSinceJ2000 + add;
            return (float)(result - Math.Floor(result / 360.0) * 360.0);
        }

        void ComputeSolarCoordinates() {
            meanLng = WrapTime(0.9856474, 280.46061837);
            float g = WrapTime(0.9856003, 357.528) * Mathf.Deg2Rad;
            SinCos((meanLng + 1.915F * Mathf.Sin(g) + 0.02F * Mathf.Sin(2F * g)) * Mathf.Deg2Rad, out float sinLambda, out float cosLambda);
            SinCos((float)(23.439 - 0.0000004 * daysSinceJ2000) * Mathf.Deg2Rad, out float sinEps, out float cosEps);
            sinDecl = sinEps * sinLambda;
            cosDecl = Mathf.Sqrt(1F - sinDecl * sinDecl);
            rightAsc = Mathf.Atan2(cosEps * sinLambda, cosLambda);
        }

        void SimulateSun() {
            CalcDaysSinceJ2000(now, 0);
            ComputeSolarCoordinates();
            float lst = WrapTime(360.98564736629, 280.46061837 + longitude) * Mathf.Deg2Rad;
            SinCos(lst - rightAsc, out float sinHour, out float cosHour);
            float sinElv = Mathf.Clamp(sinLat * sinDecl + cosLat * cosDecl * cosHour, -1F, 1F);
            solarElevation = Mathf.Asin(sinElv) * Mathf.Rad2Deg;
            solarAzimuth = Mathf.Repeat(Mathf.Atan2(-cosDecl * sinHour, sinDecl * cosLat - cosDecl * sinLat * cosHour) * Mathf.Rad2Deg, 360F);
            lightTransform.localRotation = Quaternion.Euler(solarElevation, solarAzimuth + 180F, 0F);
            if (!Utilities.IsValid(sunLight)) return;
            float t = Mathf.Max(0F, sinElv);
            sunLight.intensity = peakIntensity * t;
            t = Mathf.Lerp(horizonColorTemperature, zenithColorTemperature, Mathf.Sqrt(t));
            if (useColorTemperature)
                sunLight.colorTemperature = t;
            else
                sunLight.color = Mathf.CorrelatedColorTemperatureToRGB(t);
        }

        void DetermineNextSolarEvent() {
            bool hadEvents = hasSunriseAndSunset;
            DateTime prevSunrise = nextSunrise, prevSunset = nextSunset, rise = default, set = default, se;
            bool hasRise = false, hasSet = false;
            var nowDate = now.Date;
            for (int dayOffset = 0; dayOffset < 4 && (!hasRise || !hasSet); dayOffset++) {
                nextSunrise = nextSunset = DateTime.MaxValue;
                var offsetDate = nowDate.AddDays(dayOffset);
                CalcDaysSinceJ2000(offsetDate, 0.5 - longitude / 360.0);
                dayNightMode = DayNightMode.DayNight;
                for (int i = 0; i < 2; i++) {
                    ComputeSolarCoordinates();
                    var noonMinutes = 720.0 - longitude * MINUTES_PER_DEGREE - EquationOfTimeMinutes(meanLng, rightAsc * Mathf.Rad2Deg);
                    var solarNoon = offsetDate.AddMinutes(noonMinutes);
                    float cosH0 = Mathf.Abs(cosLat) < 1E-6F ?
                        (sinLat * sinDecl > SUNRISE_SIN_ALTITUDE ? -1F : 1F) :
                        (SUNRISE_SIN_ALTITUDE - sinLat * sinDecl) / (cosLat * cosDecl);
                    if (cosH0 <= -1F) {
                        dayNightMode = DayNightMode.DayOnly;
                        break;
                    }
                    if (cosH0 >= 1F) {
                        dayNightMode = DayNightMode.NightOnly;
                        break;
                    }
                    var halfDay = Mathf.Acos(cosH0) * Mathf.Rad2Deg * MINUTES_PER_DEGREE;
                    se = solarNoon.AddMinutes(-halfDay);
                    if (now < se) {
                        rise = se;
                        hasRise = true;
                    }
                    se = solarNoon.AddMinutes(halfDay);
                    if (now < se) {
                        set = se;
                        hasSet = true;
                    }
                    CalcDaysSinceJ2000(solarNoon, 0);
                }
                if (dayNightMode != DayNightMode.DayNight) continue;
            }
            hasSunriseAndSunset = hasRise && hasSet;
            if (!hasSunriseAndSunset) {
                nextSunrise = nextSunset = nextSolarEvent = DateTime.MaxValue;
                return;
            }
            if (hadEvents) {
                if (prevSunrise < DateTime.MaxValue && prevSunrise <= now) SendEvent("_OnSunrise");
                if (prevSunset < DateTime.MaxValue && prevSunset <= now) SendEvent("_OnSunset");
            }
            nextSunrise = rise;
            nextSunset = set;
            nextSolarEvent = nextSunrise < nextSunset ? nextSunrise : nextSunset;
        }

        void SinCos(float angle, out float sin, out float cos) {
            sin = Mathf.Sin(angle);
            cos = Mathf.Cos(angle);
        }

        float EquationOfTimeMinutes(float meanLongitude, float rightAscension) => (Mathf.Repeat(meanLongitude - rightAscension + 180F, 360F) - 180F) * MINUTES_PER_DEGREE;
    }

    public enum DayNightMode {
        DayNight = 0,
        DayOnly = 1,
        NightOnly = -1,
    }
}