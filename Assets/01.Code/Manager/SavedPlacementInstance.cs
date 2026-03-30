using System.Collections.Generic;
using _01.Code.Entities;
using UnityEngine;

namespace _01.Code.Manager
{
    public class SavedPlacementInstance : MonoBehaviour
    {
        private static readonly List<SavedPlacementInstance> Instances = new();

        [field: SerializeField] public string SaveKey { get; private set; }

        public PlaceableEntity PlaceableEntity => GetComponent<PlaceableEntity>();
        public static IReadOnlyList<SavedPlacementInstance> ActiveInstances => Instances;

        private void OnEnable()
        {
            if (!Instances.Contains(this))
            {
                Instances.Add(this);
            }
        }

        private void OnDisable()
        {
            Instances.Remove(this);
        }

        public void Bind(string saveKey)
        {
            SaveKey = saveKey;
        }
    }
}
