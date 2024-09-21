using UdonSharp;
using UnityEngine;
using VRC.SDK3.Data;
using JLChnToZ.VRC.Foundation;

namespace JLChnToZ.VRC.TimeZoneSyncHologram {
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class TimeZoneViewGroupV2 : UdonSharpBehaviour {
        [SerializeField, HideInInspector, BindUdonSharpEvent]
        NetworkTimeSyncManagerV2 networkTimeSyncManager;
        [SerializeField] GameObject entryPrefab;
        [SerializeField] Transform rootTransform;
        DataDictionary instanceData, temp;
        DataList pool;

        void Start() {
            pool = new DataList();
            instanceData = new DataDictionary();
            temp = new DataDictionary();
        }

        public void _OnTzSyncData() {
            var data = networkTimeSyncManager.SyncData;
            var keys = instanceData.GetKeys();
            temp.Clear();
            for (int i = 0, count = keys.Count; i < count; i++)
                temp[keys[i]] = true;
            keys = data.GetKeys();
            for (int i = 0, count = keys.Count, activeCount = 0; i < count; i++) {
                var key = keys[i];
                if (!data.TryGetValue(key, TokenType.DataDictionary, out var token)) continue;
                var entryDict = token.DataDictionary;
                if (!entryDict.TryGetValue("tzid", TokenType.String, out token)) continue;
                var tzid = token.String;
                TimeZoneViewTargetBase instance;
                if (instanceData.TryGetValue(tzid, TokenType.Reference, out token)) {
                    instance = (TimeZoneViewTargetBase)token.Reference;
                    temp.Remove(tzid);
                } else {
                    int index = pool.Count - 1;
                    if (index >= 0) {
                        instance = (TimeZoneViewTargetBase)pool[index].Reference;
                        pool.RemoveAt(index);
                    } else {
                        var instanceGO = Instantiate(entryPrefab);
                        instanceGO.SetActive(true);
                        instanceGO.transform.SetParent(rootTransform, false);
                        instance = instanceGO.GetComponent<TimeZoneViewTargetBase>();
                    }
                    instanceData[tzid] = instance;
                    instance.SetMetaInfo(activeCount++, tzid, entryDict.TryGetValue("offset", TokenType.Double, out token) ? token.Double : 0);
                }
                instance.SetActive(true);
                instance.AddPlayerData(key.String);
            }
            keys = temp.GetKeys();
            for (int i = 0, count = keys.Count; i < count; i++) {
                var key = keys[i];
                if (!instanceData.TryGetValue(key, TokenType.Reference, out var token)) continue;
                var instance = (TimeZoneViewTargetBase)token.Reference;
                instance.SetActive(false);
                instanceData.Remove(key);
                pool.Add(instance);
            }
            temp.Clear();
        }
    }
}