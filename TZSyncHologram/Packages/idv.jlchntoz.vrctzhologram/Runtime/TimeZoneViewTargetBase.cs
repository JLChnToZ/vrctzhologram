using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using VRC.SDK3.Data;
using UdonSharp;

namespace JLChnToZ.VRC.TimeZoneSyncHologram {
    public abstract class TimeZoneViewTargetBase : UdonSharpBehaviour {
        [SerializeField, HideInInspector] protected TimeZoneManagerV2 timeZoneManager;
        [SerializeField] protected Text[] texts;
        [SerializeField] protected TextMeshProUGUI[] tmpros;
        [SerializeField, Multiline] protected string textFormat = "{0}\n{1}\n{2:HH:mm}";
        [SerializeField, Multiline] protected string playerNameSeparator = "\n";
        protected DataDictionary tzData;
        protected string tzName = "";
        protected string[] playerNames;
        protected string jointPlayerNames = "";
        protected int playerCount;
        protected double timeOffset;
        bool slowUpdateFired, updateTextFired;

        public virtual void SetActive(bool active) {
            gameObject.SetActive(active);
            if (active) StartUpdate();
        }

        protected void StartUpdate() {
            if (!slowUpdateFired) SendCustomEventDelayedFrames(nameof(_SlowUpdate), 0);
        }

        public virtual void SetMetaInfo(int index, string tzid, double offset) {
            playerCount = 0;
            timeOffset = 0;
            tzName = "";
            jointPlayerNames = "";
            tzData = timeZoneManager.GetTimezone(tzid);
            if (tzData != null && tzData.TryGetValue("name", TokenType.String, out var token))
                tzName = token.String;
            else
                tzName = tzid;
            timeOffset = offset;
        }

        public virtual void ClearPlayerData() {
            playerCount = 0;
            jointPlayerNames = "";
            if (!updateTextFired) {
                updateTextFired = true;
                SendCustomEventDelayedFrames(nameof(_UpdateText), 0);
            }
        }

        public virtual void AddPlayerData(string playerId) {
            if (playerNames == null)
                playerNames = new string[16];
            else if (playerCount >= playerNames.Length) {
                var newPlayerNames = new string[playerNames.Length * 2];
                playerNames.CopyTo(newPlayerNames, 0);
                playerNames = newPlayerNames;
            }
            playerNames[playerCount++] = playerId;
            if (!updateTextFired) {
                updateTextFired = true;
                SendCustomEventDelayedFrames(nameof(_UpdateText), 0);
            }
        }

        public void _SlowUpdate() {
            if (!enabled || !gameObject.activeInHierarchy) {
                slowUpdateFired = false;
                return;
            }
            SendCustomEventDelayedSeconds(nameof(_SlowUpdate), Time.smoothDeltaTime + 1 - Mathf.Repeat((float)DateTime.UtcNow.TimeOfDay.TotalSeconds, 1));
            slowUpdateFired = true;
            UpdateText();
        }

        public void _UpdateText() {
            updateTextFired = false;
            jointPlayerNames = string.Join(playerNameSeparator, playerNames, 0, playerCount);
            UpdateText();
        }

        protected virtual void UpdateText() {
            SetText(string.Format(textFormat, jointPlayerNames, tzName, DateTime.UtcNow.AddMinutes(timeOffset)));
        }

        protected virtual void SetText(string text) {
            if (texts != null) foreach (var t in texts) if (t != null) t.text = text;
            if (tmpros != null) foreach (var t in tmpros) if (t != null) t.text = text;
        }
    }
}