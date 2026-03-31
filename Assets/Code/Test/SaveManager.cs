using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Code.Test
{
    [Serializable]
    public struct SaveData
    {
        public int id;
        public string data;
    }
    [Serializable]
    public struct DataCollection
    {
        public List<SaveData> dataCollection;
    }
    public class SaveManager : MonoBehaviour
    {
        [SerializeField] private string saveKey = "saveData";
        //[SerializeField] private BoolEventChannel saveChannel;
        //[SerializeField] private BoolEventChannel loadChannel;
        
        private List<SaveData> _unUsedData = new List<SaveData>();

        private void OnEnable()
        {
            //saveChannel.OnValueEvent += HandleSaveEvent;
            //saveChannel.OnValueEvent += HandleLoadEvent;
        }

        private void OnDestroy()
        {
            //saveChannel.OnValueEvent -= HandleSaveEvent;
            //saveChannel.OnValueEvent -= HandleLoadEvent;
        }

        private void HandleSaveEvent(bool loadFromFile)
        {
            if(loadFromFile)
                return;
            string loadData = PlayerPrefs.GetString(saveKey, string.Empty);
            RestoreDataFromJson(loadData);
        }

        private void RestoreDataFromJson(string loadData)
        {
            IEnumerable<ISaveable> saveableObjects
                = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).OfType<ISaveable>();
            DataCollection loadCollection = string.IsNullOrEmpty(loadData)
                ? new DataCollection()
                : JsonUtility.FromJson<DataCollection>(loadData);
            
            _unUsedData.Clear();
            if (loadCollection.dataCollection != null && loadCollection.dataCollection.Count > 0)
            {
                foreach (SaveData data in loadCollection.dataCollection)
                {
                    ISaveable saveable = saveableObjects.FirstOrDefault(s =>s.SaveId.saveID == data.id);
                    if(saveable != null)
                        saveable.RestoreData(data.data);
                    else
                    {
                        _unUsedData.Add(data);
                    }
                }
            }
        }

        private void HandleLoadEvent(bool saveToFile)
        {
            if(saveToFile)
                return;
            string dataJson = GetDataToSave();
            PlayerPrefs.SetString(saveKey, dataJson);
            Debug.Log(dataJson);
        }

        private string GetDataToSave()
        {
            IEnumerable<ISaveable> saveableObjects
                = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).OfType<ISaveable>();
            
            List<SaveData> saveDataList =  new List<SaveData>();
            foreach (ISaveable saveable in saveableObjects)
            {
                SaveData saveData = new SaveData()
                {
                    id = saveable.SaveId.saveID,
                    data = saveable.GetSaveData()
                };
                saveDataList.Add(saveData);
            }
            saveDataList.AddRange(_unUsedData);
            DataCollection dataCollection = new DataCollection{dataCollection = saveDataList};
            return JsonUtility.ToJson(dataCollection);
        }
    }
}