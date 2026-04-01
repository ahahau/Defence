using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _01.Code.UI
{
    public class CostEntryViewUI : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private TextMeshProUGUI label;
        [SerializeField] private TextMeshProUGUI value;

        private void Awake()
        {
            CacheReferences();
        }

        public void RefreshBindings()
        {
            CacheReferences();
        }

        public void SetData(Sprite iconSprite, string displayName, int amount, int maxAmount, bool showMaxAmount)
        {
            CacheReferences();

            if (icon != null)
            {
                icon.sprite = iconSprite;
                icon.enabled = iconSprite != null;
            }

            if (label != null)
            {
                label.SetText(displayName ?? string.Empty);
            }

            if (value != null)
            {
                value.SetText(showMaxAmount ? $"{amount}/{maxAmount}" : amount.ToString());
            }
        }

        private void CacheReferences()
        {
            if (icon == null)
            {
                icon = FindChildComponent<Image>("Icon");
            }

            if (label == null)
            {
                label = FindChildComponent<TextMeshProUGUI>("Label");
            }

            if (value == null)
            {
                value = FindChildComponent<TextMeshProUGUI>("Value");
            }
        }

        private T FindChildComponent<T>(string childName) where T : Component
        {
            Transform[] children = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i].name != childName)
                {
                    continue;
                }

                return children[i].GetComponent<T>();
            }

            return null;
        }
    }
}
