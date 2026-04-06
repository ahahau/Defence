using _01.Code.Combat;
using _01.Code.Core;
using _01.Code.Events;
using _01.Code.UI;
using GondrLib.ObjectPool.Runtime;
using UnityEngine;

namespace _01.Code.Manager
{
    public class DamageTextManager : MonoBehaviour, IManageable
    {
        [SerializeField] private GameEventChannelSO uiEventChannel;
        [SerializeField] private DamageText damageTextPrefab;
        [SerializeField] private PoolManagerMono poolManager;
        [SerializeField] private PoolingItemSO damageTextPoolingItem;

        private UiDamageTextPresenter _presenter;

        public void Initialize(IManagerContainer managerContainer)
        {
            _presenter = new UiDamageTextPresenter(damageTextPrefab, poolManager, damageTextPoolingItem);
            uiEventChannel.AddListener<ShowDamageTextRequestedEvent>(HandleShowDamageTextRequestedEvent);
        }

        private void OnDestroy()
        {
            uiEventChannel.RemoveListener<ShowDamageTextRequestedEvent>(HandleShowDamageTextRequestedEvent);
        }

        private void HandleShowDamageTextRequestedEvent(ShowDamageTextRequestedEvent evt)
        {
            _presenter?.ShowDamage(evt);
        }
    }
}
