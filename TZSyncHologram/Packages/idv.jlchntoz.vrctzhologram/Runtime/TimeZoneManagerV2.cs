using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.SDK3.Data;
using VRC.SDK3.StringLoading;
using VRC.Udon.Common.Interfaces;
using JLChnToZ.VRC.Foundation;

namespace JLChnToZ.VRC.TimeZoneSyncHologram {
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public partial class TimeZoneManagerV2 : UdonSharpEventSender {
        [SerializeField] TextAsset localDefinition;
        [SerializeField] VRCUrl definitionURL;
        DataDictionary tzData, abbrData;
        DataDictionary localData;
        TimeZoneInfo localTz;
        bool ready;

        public bool Ready => ready;

        public double LocalOffset => (double)localTz.BaseUtcOffset.Ticks / TimeSpan.TicksPerMinute;

        void Start() {
            TimeZoneInfo.ClearCachedData();
            localTz = TimeZoneInfo.Local;
            if (localDefinition != null)
                ParseData(localDefinition.text);
            if (!VRCUrl.IsNullOrEmpty(definitionURL))
                VRCStringDownloader.LoadUrl(definitionURL, (IUdonEventReceiver)this);
        }

        public override void OnStringLoadError(IVRCStringDownload result) =>
            Debug.LogError($"Failed to load data asset: {result.ErrorCode}");

        public override void OnStringLoadSuccess(IVRCStringDownload result) =>
            ParseData(result.Result);

        void ParseData(string text) {
            localData = null;
            if (!VRCJson.TryDeserializeFromJson(text, out var token)) {
                Debug.LogError($"Failed to parse JSON data: {token}");
                return;
            }
            var mainDict = token.DataDictionary;
            if (!mainDict.TryGetValue("data", TokenType.DataDictionary, out token)) {
                Debug.LogError($"Failed to find data dictionary: {token}");
                return;
            }
            tzData = token.DataDictionary;
            var keys = tzData.GetKeys();
            for (int i = 0; i < keys.Count; i++) {
                var key = keys[i];
                tzData[key].DataDictionary.Add("id", key);
            }
            if (!mainDict.TryGetValue("abbr", TokenType.DataDictionary, out token)) {
                Debug.LogError($"Failed to find abbreviation dictionary: {token}");
                return;
            }
            abbrData = token.DataDictionary;
            ready = true;
            SendEvent("_OnTzDataReady");
        }

        public DataDictionary GetLocalTimezone() {
            if (localData != null) return localData;
            localData = GetTimezone(localTz.Id);
            if (localData != null) return localData;
            // +XXX_ABBR, where XXX is offset in minutes, ABBR is abbreviation
            localData = GetTimezone($"{localTz.BaseUtcOffset.TotalMinutes:+000;-000}_{localTz.Id}");
            return localData;
        }

        public DataDictionary GetTimezone(string tzName) => ready && (
            tzData.TryGetValue(tzName, TokenType.DataDictionary, out var token) || (
            abbrData.TryGetValue(tzName, TokenType.String, out token) &&
            tzData.TryGetValue(token.String, TokenType.DataDictionary, out token)
        )) ? token.DataDictionary : null;
    }

#if UNITY_EDITOR && !COMPILER_UDONSHARP
    public partial class TimeZoneManagerV2 : ISingleton<TimeZoneManagerV2> {
        public void Merge(TimeZoneManagerV2[] others) {
            MergeTargets(others);
        }
    }
#endif
}