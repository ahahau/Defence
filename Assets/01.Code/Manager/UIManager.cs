using _01.Code.UI;
using System;
using System.Collections.Generic;
using _01.Code.Buildings;
using _01.Code.Combat;
using _01.Code.Core;
using _01.Code.Events;
using _01.Code.Unit;
using GondrLib.ObjectPool.Runtime;
using UnityEngine;

namespace _01.Code.Manager
{
    public class UIManager : MonoBehaviour
    {
        [SerializeField] private GameEventChannelSO uiEventChannel;
        [SerializeField] private GameEventChannelSO buildEventChannel;
        [SerializeField] private GameEventChannelSO costEventChannel;
        [SerializeField] private GameObject buildingPenalPrefab;
        [SerializeField] private UIHeader uiHeader;
        [SerializeField] private DamageText damageTextPrefab;
        [SerializeField] private PoolManagerMono poolManager;
        [SerializeField] private PoolingItemSO damageTextPoolingItem;
        [SerializeField] private List<UnitDataSO> availableBuildings = new();
        private int _currentGold;

        public UnitDataSO SelectedUnit { get; private set; }
        public Vector3 CurrentBuildPosition { get; private set; }

        public event Action<UnitDataSO> OnBuildingSelected;
        public event Action<UnitDataSO, Vector3> OnBuildRequested;

        public void Initialize()
        {
          
            uiHeader?.Initialize();

            uiHeader?.RefreshAvailability();
            uiEventChannel.AddListener<ShowDamageTextRequestedEvent>(HandleShowDamageTextRequestedEvent);
        }

        private bool CanAfford(UnitDataSO unitData)
        {
            return _currentGold >= unitData.Cost;
        }

        private void HandleBuildingSelected(UnitDataSO unitData)
        {
            SelectedUnit = unitData;
            OnBuildingSelected?.Invoke(unitData);
        }

        private void HandlePanelCancelled()
        {
            SelectedUnit = null;
        }

        private void HandleShowDamageTextRequestedEvent(ShowDamageTextRequestedEvent evt)
        {
            if (evt == null)
            {
                return;
            }

            DamageText damageText = null;
            if (poolManager != null && damageTextPoolingItem != null)
            {
                damageText = poolManager.Pop<DamageText>(damageTextPoolingItem);
                if (damageText != null)
                {
                    damageText.transform.position = evt.WorldPosition;
                }
            }

            if (damageText == null && damageTextPrefab != null)
            {
                damageText = Instantiate(damageTextPrefab, evt.WorldPosition, Quaternion.identity);
            }

            if (damageText == null)
            {
                GameObject damageTextObject = new GameObject("DamageText");
                damageTextObject.transform.position = evt.WorldPosition;
                damageText = damageTextObject.AddComponent<DamageText>();
            }

            damageText.Initialize(evt.Damage, evt.FollowTarget);
        }

    }
}
