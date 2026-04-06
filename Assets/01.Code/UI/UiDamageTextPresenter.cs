using _01.Code.Combat;
using _01.Code.Events;
using GondrLib.ObjectPool.Runtime;
using UnityEngine;

namespace _01.Code.UI
{
    public class UiDamageTextPresenter
    {
        private readonly DamageText _damageTextPrefab;
        private readonly PoolManagerMono _poolManager;
        private readonly PoolingItemSO _damageTextPoolingItem;

        public UiDamageTextPresenter(
            DamageText damageTextPrefab,
            PoolManagerMono poolManager,
            PoolingItemSO damageTextPoolingItem)
        {
            _damageTextPrefab = damageTextPrefab;
            _poolManager = poolManager;
            _damageTextPoolingItem = damageTextPoolingItem;
        }

        public void ShowDamage(ShowDamageTextRequestedEvent evt)
        {
            if (evt == null)
            {
                return;
            }

            DamageText damageText = null;
            if (_poolManager != null && _damageTextPoolingItem != null)
            {
                damageText = _poolManager.Pop<DamageText>(_damageTextPoolingItem);
                if (damageText != null)
                {
                    damageText.transform.position = evt.WorldPosition;
                }
            }

            if (damageText == null && _damageTextPrefab != null)
            {
                damageText = Object.Instantiate(_damageTextPrefab, evt.WorldPosition, Quaternion.identity);
            }

            if (damageText == null)
            {
                GameObject textObject = new GameObject("DamageText");
                textObject.transform.position = evt.WorldPosition;
                damageText = textObject.AddComponent<DamageText>();
            }

            damageText.Initialize(evt.Damage, evt.FollowTarget);
        }
    }
}
