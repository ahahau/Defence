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
        private bool _isSubscribed;

        public void Initialize(IManagerContainer managerContainer)
        {
            ResolveReferences();
            _presenter = new UiDamageTextPresenter(damageTextPrefab, poolManager, damageTextPoolingItem);
            if (uiEventChannel != null)
            {
                uiEventChannel.AddListener<ShowDamageTextRequestedEvent>(HandleShowDamageTextRequestedEvent);
                _isSubscribed = true;
            }
        }

        private void OnDestroy()
        {
            if (_isSubscribed && uiEventChannel != null)
            {
                uiEventChannel.RemoveListener<ShowDamageTextRequestedEvent>(HandleShowDamageTextRequestedEvent);
                _isSubscribed = false;
            }
        }

        private void HandleShowDamageTextRequestedEvent(ShowDamageTextRequestedEvent evt)
        {
            _presenter?.ShowDamage(evt);
        }

        private void ResolveReferences()
        {
            if (damageTextPrefab == null && damageTextPoolingItem != null && damageTextPoolingItem.prefab != null)
            {
                damageTextPrefab = damageTextPoolingItem.prefab.GetComponent<DamageText>();
            }
        }
    }
}
