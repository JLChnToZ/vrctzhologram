using System;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common;
using VRC.Udon.Common.Interfaces;
using UdonSharp;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class TimeZoneSyncHandler : UdonSharpBehaviour {
    // Public properties
    [SerializeField] TextAsset mapping;
    [SerializeField] int roomCapacity = 16;
    [SerializeField] TimeZoneSyncGroup[] groups;
    [Space]
    [Header("Debug only")]
    [SerializeField] RectTransform debugContainer;
    [SerializeField] GameObject debugEntryTemplate;
    [SerializeField] int maxDebugEntries = 30;

    // Syncd properties
    [UdonSynced] string remoteMessage = "";
    string localMessage = "";

    // Local properties
    VRCPlayerApi[] players;
    string[] tzInfos;
#if UNITY_ANDROID
    TimeSpan[] tzOffsets;
#endif
    int playerCount;
    bool startSyncOver;
    [NonSerialized] public TimeSpan networkTimeOffset;

    string[] tzNames;
    string[] tzNamesAlt;
    float[] latitudes;
    float[] longitudes;

    void Start() {
        Log("Timezone Sync for VRChat by Vistanz");
        Log("===================================");
        players = new VRCPlayerApi[roomCapacity];
        tzInfos = new string[roomCapacity];
        #if UNITY_ANDROID
        tzOffsets = new TimeSpan[roomCapacity];
        #endif
        string[] splitMapping = mapping.text.Split('\n');
        tzNames = new string[splitMapping.Length];
        tzNamesAlt = new string[splitMapping.Length];
        longitudes = new float[splitMapping.Length];
        latitudes = new float[splitMapping.Length];
        for (int i = 0; i < splitMapping.Length; i++) {
            var row = splitMapping[i].Split(',');
            tzNames[i] = row[0];
            tzNamesAlt[i] = row[1];
            longitudes[i] = float.Parse(row[2]);
            latitudes[i] = float.Parse(row[3]);
        }
    #if UNITY_EDITOR
        RefreshClocks();
    #endif
    }

    public override void OnPlayerJoined(VRCPlayerApi player) {
        Log($"Player <color=#FF8000>{player.displayName}</color> (<color=#FF8000>{player.playerId}</color>) joined the room.");
        if (playerCount >= players.Length) {
            int newCapacity = playerCount + 16;
            var newPlayers = new VRCPlayerApi[newCapacity];
            Array.Copy(players, newPlayers, playerCount);
            players = newPlayers;
            var newTzInfos = new string[newCapacity];
            Array.Copy(tzInfos, newTzInfos, playerCount);
            tzInfos = newTzInfos;
        #if UNITY_ANDROID
            var newTzOffsets = new TimeSpan[newCapacity];
            Array.Copy(tzOffsets, newTzOffsets, playerCount);
            tzOffsets = newTzOffsets;
        #endif
        }
        int index = Array.IndexOf(players, player);
        if (index < 0) {
            index = playerCount;
            players[index] = player;
            playerCount++;
        }
        if (player.isLocal) {
        #if UNITY_ANDROID
            var tzOffset = TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow);
            double hourOffsets = tzOffset.TotalHours;
            tzInfos[index] = hourOffsets == 0 ? "Etc/GMT" : $"Etc/GMT{hourOffsets:+#;-#}";
            tzOffsets[index] = tzOffset;
        #else
            tzInfos[index] = TimeZoneInfo.Local.Id;
        #endif
            RefreshClocks();
        }
        startSyncOver = true;
        if (Networking.IsOwner(gameObject))
            RequestSerialization();
    }

    public override void OnPlayerLeft(VRCPlayerApi player) {
        if (player == null) return;
        Log($"Player <color=#FF8000>{player.displayName}</color> (<color=#FF8000>{player.playerId}</color>) left the room.");
        int index = Array.IndexOf(players, player);
        if (index < 0) return;
        int lastIndex = --playerCount;
        if (index < lastIndex) {
            players[index] = players[lastIndex];
            tzInfos[index] = tzInfos[lastIndex];
            #if UNITY_ANDROID
            tzOffsets[index] = tzOffsets[lastIndex];
            #endif
        }
        players[lastIndex] = null;
        tzInfos[lastIndex] = null;
        #if UNITY_ANDROID
        tzOffsets[lastIndex] = TimeSpan.Zero;
        #endif
        RefreshClocks();
    }

    public override void OnOwnershipTransferred(VRCPlayerApi player) {
        if (!player.isLocal) {
            Log($"Turn pass detected. This is <color=#FF8000>{player.displayName}</color>'s (<color=#FF8000>{player.playerId}</color>) turn now.");
            return;
        }
        Log("Turn pass detected. This is my turn now.");
        RequestSerialization();
    }

    public override void OnPreSerialization() {
        UnpackData(); // Enforce unpack before packing.
        if (!Networking.IsOwner(gameObject)) {
            Log("Ignored sync flow, not my turn.");
            return;
        }
        Log("Sync flow triggered.");
        var localTimeZone = TimeZoneInfo.Local;
        var offset = localTimeZone.GetUtcOffset(DateTime.UtcNow);
    #if UNITY_ANDROID
        double hourOffsets = offset.TotalHours;
        var tzId = hourOffsets == 0 ? "Etc/GMT" : $"Etc/GMT{hourOffsets:+#;-#}";
    #else
        var tzId = localTimeZone.Id;
    #endif
        remoteMessage = localMessage = $"{Networking.LocalPlayer.playerId}:{tzId}:{offset.Ticks}";
        Log($"Data packed: <color=#FFFF88>{remoteMessage}</color>");
    }

    public override void OnDeserialization() {
        if (!string.IsNullOrEmpty(remoteMessage)) UnpackData();
    }

    public override void OnPostSerialization(SerializationResult result) {
        if (result.success)
            SendCustomEventDelayedSeconds(nameof(DeferTransfer), 1F);
        else
            SendCustomEventDelayedSeconds(nameof(RetrySync), 1F);
    }
    
    public void RetrySync() {
        RequestSerialization();
    }

    public void Resync() { // Callback for resync button: UdonBehaviour.SendCustomEvent("Resync")
        if (startSyncOver) return;
        startSyncOver = true;
        if (Networking.IsOwner(gameObject))
            DeferTransfer();
        else
            SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(Resync));
    }

    void UnpackData() {
        if (string.Equals(remoteMessage, localMessage)) return;
        localMessage = remoteMessage;
        Log($"Data received: <color=#FFFF88>{localMessage}</color>");
        int playerId;
        string[] data = localMessage.Split(':');
        if (playerCount <= 1 || !int.TryParse(data[0], out playerId)) return;
        for (int i = 0; i < playerCount; i++) {
            var otherPlayer = players[i];
            if (Utilities.IsValid(otherPlayer) &&
                !otherPlayer.isLocal &&
                otherPlayer.playerId == playerId) {
                tzInfos[i] = data[1];
            #if UNITY_ANDROID
                tzOffsets[i] = new TimeSpan(long.Parse(data[2]));
            #endif
                Log($"Data unpacked. Player <color=#FF8000>{otherPlayer.displayName}</color> (<color=#FF8000>{otherPlayer.playerId}</color>) is in timezone <color=#00FF88>{tzInfos[i]}</color>.");
                break;
            }
        }
        RefreshClocks();
    }

    public void DeferTransfer() {
        if (!Networking.IsOwner(gameObject)) {
            Log("Ignored passing to other player, not my turn.");
            return;
        }
        int myPlayerId = Networking.LocalPlayer.playerId;
        VRCPlayerApi nextPlayer = null;
        for (int i = 0; i < playerCount; i++) {
            var player = players[i];
            if (Utilities.IsValid(player) && !player.isLocal &&
                player.playerId > myPlayerId && (
                nextPlayer == null ||
                nextPlayer.playerId > player.playerId
            )) nextPlayer = player;
        }
        if (nextPlayer != null) {
            Log($"Passing to <B>NEXT</B> player <color=#FF8000>{nextPlayer.displayName}</color> (<color=#FF8000>{nextPlayer.playerId}</color>).");
            startSyncOver = false;
            Networking.SetOwner(nextPlayer, gameObject);
            return;
        }
        if (!startSyncOver) {
            Log("Paused.");
            return;
        }
        startSyncOver = false;
        for (int i = 0; i < playerCount; i++) {
            var player = players[i];
            if (Utilities.IsValid(player) && !player.isLocal && (
                nextPlayer == null ||
                nextPlayer.playerId > player.playerId
            )) nextPlayer = player;
        }
        if (nextPlayer != null) {
            Log($"Passing to <B>FIRST</B> player <color=#FF8000>{nextPlayer.displayName}</color> (<color=#FF8000>{nextPlayer.playerId}</color>).");
            Networking.SetOwner(nextPlayer, gameObject);
            return;
        }
        Log($"Nobody to pass to.");
    }

    void RefreshClocks() {
        Log("Refresh clocks...");
    #if UNITY_EDITOR
        int maxCount = tzNames.Length;
        var nameLists = new string[maxCount];
        var tzInfoList = new string[maxCount];
    #if UNITY_ANDROID
        var tzOffsetList = new TimeSpan[maxCount];
    #endif
        for (int i = 0; i < maxCount; i++) {
            tzInfoList[i] = tzNames[tzNames.Length - i - 1];
            nameLists[i] = "AAAAAA\nBBBBBB\nCCCCCC";
        }
    #else
        var nameLists = new string[playerCount];
        var tzInfoList = new string[playerCount];
    #if UNITY_ANDROID
        var tzOffsetList = new TimeSpan[playerCount];
    #endif
        int maxCount = 0;
        for (int i = 0; i < playerCount; i++) {
            var playerTimeZone = tzInfos[i];
            if (playerTimeZone == null) continue;
            string playerName = players[i].displayName;
            if (maxCount > 0) {
                int index = Array.IndexOf(tzInfoList, playerTimeZone, 0, maxCount);
                if (index >= 0) {
                    nameLists[index] += $"\n{playerName}";
                    continue;
                }
            }
            bool inserted = false;
            for (int j = 0; j < maxCount; j++) {
                if (string.CompareOrdinal(playerTimeZone, tzInfoList[i]) >= 0)
                    continue;
                Array.Copy(tzInfoList, j, tzInfoList, j + 1, maxCount - j);
                tzInfoList[j] = playerTimeZone;
                Array.Copy(nameLists, j, nameLists, j + 1, maxCount - j);
                nameLists[j] = playerName;
            #if UNITY_ANDROID
                Array.Copy(tzOffsetList, j, tzOffsetList, j + 1, maxCount - j);
                tzOffsetList[j] = tzOffsets[i];
            #endif
                inserted = true;
                break;
            }
            if (!inserted) {
                tzInfoList[maxCount] = playerTimeZone;
                nameLists[maxCount] = playerName;
            #if UNITY_ANDROID
                tzOffsetList[maxCount] = tzOffsets[i];
            #endif
            }
            maxCount++;
        }
    #endif
        for (int n = 0; n < groups.Length; n++) {
            var targets = groups[n].targets;
            for (int i = 0; i < targets.Length; i++) {
                var target = (UdonBehaviour)targets[i].GetComponent(typeof(UdonBehaviour));
                if (i < maxCount) {
                    int index = Array.IndexOf(tzNames, tzInfoList[i]);
                    if (index >= 0) {
                        target.SetProgramVariable("tzName", tzNames[index]);
                        target.SetProgramVariable("tzNameAlt", tzNamesAlt[index]);
                        target.SetProgramVariable("latitude", latitudes[index]);
                        target.SetProgramVariable("longitude", longitudes[index]);
                        target.SetProgramVariable("playerNames", nameLists[i]);
                    #if UNITY_ANDROID
                        target.SetProgramVariable("tzOffset", tzOffsetList[i]);
                    #else
                        if (!tzNamesAlt[index].Contains("/"))
                            target.SetProgramVariable("timeZone", TimeZoneInfo.FindSystemTimeZoneById(tzNamesAlt[index]));
                        else if(!tzNames[index].Contains("/"))
                            target.SetProgramVariable("timeZone", TimeZoneInfo.FindSystemTimeZoneById(tzNames[index]));
                        else
                            target.SetProgramVariable("timeZone", null);
                    #endif
                        target.SendCustomEvent("SetActive");
                    } else
                        target.SendCustomEvent("SetInactive");
                } else
                    target.SendCustomEvent("SetInactive");
            }
        }
    }

    void Log(string message) {
        if (string.IsNullOrEmpty(message)) return;
        Debug.Log($"[<color=#0080FF>TZSync</color>] {message}");
        if (debugContainer == null) return;
        GameObject entry;
        if (debugContainer.childCount < maxDebugEntries && debugEntryTemplate != null) {
            entry = VRCInstantiate(debugEntryTemplate);
            entry.transform.SetParent(debugContainer, false);
        } else if (debugContainer.childCount > 0) {
            var entryTransform = debugContainer.GetChild(0);
            entry = entryTransform.gameObject;
            entryTransform.SetAsLastSibling();
        } else return;
        entry.GetComponent<Text>().text = $"[{DateTime.UtcNow + networkTimeOffset:MM/dd/yyyy HH:mm:ss}] {message}";
    }
}
