using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.SDK3.Data;
using VRC.Udon.Common;
using JLChnToZ.VRC.Foundation;

namespace JLChnToZ.VRC.TimeZoneSyncHologram {
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public partial class NetworkTimeSyncManagerV2 : UdonSharpEventSender {
        [SerializeField, HideInInspector, BindUdonSharpEvent]
        TimeZoneManagerV2 timeZoneManager;
        [UdonSynced] string data;
        VRCPlayerApi localPlayer;
        DataDictionary playerDataDict;
        bool myDataFilled;
        bool tzDataReady;
        bool pendingUpdate;
        bool delaySerializationRequested;

        public DataDictionary SyncData => playerDataDict;

        void Start() {
            localPlayer = Networking.LocalPlayer;
            SendCustomEventDelayedSeconds(nameof(_CheckAndFillData), 1);
        }

        public override void OnPlayerLeft(VRCPlayerApi player) {
            if (playerDataDict != null && playerDataDict.Remove(player.displayName)) {
                TzSyncData();
                if (!delaySerializationRequested) {
                    delaySerializationRequested = true;
                    SendCustomEventDelayedSeconds(nameof(_DelayRequestSerialization), 0.5F);
                }
            }
        }

        public override void OnPreSerialization() {
            if (VRCJson.TrySerializeToJson(playerDataDict, JsonExportType.Minify, out var json))
                data = json.String;
            else
                Debug.LogError($"Failed to serialize JSON data: {json}");
        }

        public override void OnPostSerialization(SerializationResult result) {
            if (result.success && playerDataDict != null &&
                playerDataDict.ContainsKey(localPlayer.displayName))
                myDataFilled = true;
        }

        public override void OnDeserialization() {
            if (VRCJson.TryDeserializeFromJson(data, out var token))
                playerDataDict = token.DataDictionary;
            else {
                Debug.LogError($"Failed to parse JSON data: {token}");
                if (playerDataDict == null) playerDataDict = new DataDictionary();
            }
            myDataFilled = playerDataDict.ContainsKey(localPlayer.displayName);
            DelayCheckAndFillData();
            TzSyncData();
        }

        public override void OnOwnershipTransferred(VRCPlayerApi player) => DelayCheckAndFillData();
        
        void DelayCheckAndFillData() {
            if (myDataFilled) return;
            SendCustomEventDelayedSeconds(
                nameof(_CheckAndFillData),
                localPlayer.playerId % VRCPlayerApi.GetPlayerCount() * 0.1f
            );
        }

        public void _CheckAndFillData() {
            if (myDataFilled) return;
            if (Networking.IsOwner(gameObject)) {
                var data = timeZoneManager.GetLocalTimezone();
                var offset = timeZoneManager.LocalOffset;
                if (playerDataDict == null) playerDataDict = new DataDictionary();
                string myName = localPlayer.displayName;
                DataDictionary myDict;
                if (playerDataDict.TryGetValue(myName, TokenType.DataDictionary, out var token))
                    myDict = token.DataDictionary;
                else
                    playerDataDict[myName] = myDict = new DataDictionary();
                if (data.TryGetValue("id", out token)) myDict["tzid"] = token;
                myDict["offset"] = offset;
                RequestSerialization();
                TzSyncData();
                return;
            }
            var owner = Networking.GetOwner(gameObject);
            if (playerDataDict != null && playerDataDict.ContainsKey(owner.displayName))
                Networking.SetOwner(localPlayer, gameObject);
        }

        public void _DelayRequestSerialization() {
            delaySerializationRequested = false;
            if (Networking.IsOwner(gameObject))
                RequestSerialization();
        }

        public void _OnTzDataReady() {
            tzDataReady = true;
            if (pendingUpdate) {
                SendEvent("_OnTzSyncData");
                pendingUpdate = false;
            }
        }

        void TzSyncData() {
            if (tzDataReady)
                SendEvent("_OnTzSyncData");
            else
                pendingUpdate = true;
        }
    }

#if UNITY_EDITOR && !COMPILER_UDONSHARP
    public partial class NetworkTimeSyncManagerV2 : ISingleton<NetworkTimeSyncManagerV2> {
        public void Merge(NetworkTimeSyncManagerV2[] others) {
            MergeTargets(others);
        }
    }
#endif
}