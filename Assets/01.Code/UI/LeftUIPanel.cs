using System;
using System.Collections.Generic;
using _01.Code.Core;
using _01.Code.Events;
using Unity.VisualScripting;
using UnityEngine;

namespace _01.Code.UI
{
    public class LeftUIPanel : MonoBehaviour
    {
        [SerializeField] private GameEventChannelSO uIEventChannel;
        [SerializeField] private List<UIBaseView> uIViews;

        private int _beforeIndex = 0;
        private void Awake()
        {
            uIEventChannel.AddListener<LeftUpperPanelChange>(HandleLeftUpperPanelChange);
            foreach (var uIView in uIViews)
            {
                uIView.gameObject.SetActive(false);
            }
            uIViews[_beforeIndex].gameObject.SetActive(true);
        }

        private void OnDestroy()
        {
            uIEventChannel.RemoveListener<LeftUpperPanelChange>(HandleLeftUpperPanelChange);
        }

        private void HandleLeftUpperPanelChange(LeftUpperPanelChange obj)
        {
            uIViews[_beforeIndex].gameObject.SetActive(false);
            uIViews[obj.PanelIndex].gameObject.SetActive(true);
            _beforeIndex = obj.PanelIndex;
        }
    }
}