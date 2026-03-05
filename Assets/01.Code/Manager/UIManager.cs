using System;
using UnityEngine;

namespace _01.Code.Manager
{
    public class UIManager : MonoBehaviour, IManageable
    {
        [SerializeField] private GameObject buildingPenalPrefab;
        
        public void Initialize()
        {
                
        }
        public void ShowBuildingPanel()
        {
            buildingPenalPrefab.SetActive(true);
            
        }
        
    }
}