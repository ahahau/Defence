using TMPro;
using UnityEngine;

namespace _01.Code.UI
{
    public class InventoryTabsUI : MonoBehaviour
    {
        [SerializeField] private GameObject resourceTabButton;
        [SerializeField] private GameObject unitTabButton;
        [SerializeField] private UnityEngine.UI.Image resourceTabImage;
        [SerializeField] private UnityEngine.UI.Image unitTabImage;
        [SerializeField] private TextMeshProUGUI resourceTabText;
        [SerializeField] private TextMeshProUGUI unitTabText;
        [SerializeField] private UnityEngine.CanvasGroup resourceTabCanvasGroup;
        [SerializeField] private UnityEngine.CanvasGroup unitTabCanvasGroup;
        [SerializeField] private GameObject resourcePage;
        [SerializeField] private GameObject unitPage;

        private void Awake()
        {
            if (resourceTabButton != null)
            {
                resourceTabButton.gameObject.SetActive(false);
            }

            if (resourcePage != null)
            {
                resourcePage.SetActive(false);
            }

            if (unitPage != null)
            {
                unitPage.SetActive(false);
            }

            if (unitTabButton != null)
            {
                unitTabButton.gameObject.SetActive(false);
            }

            if (resourceTabCanvasGroup != null)
            {
                resourceTabCanvasGroup.alpha = 0f;
            }

            if (unitTabCanvasGroup != null)
            {
                unitTabCanvasGroup.alpha = 0f;
            }

            if (resourceTabText != null)
            {
                resourceTabText.gameObject.SetActive(false);
            }

            if (resourceTabImage != null)
            {
                resourceTabImage.gameObject.SetActive(false);
            }

            if (unitTabText != null)
            {
                unitTabText.gameObject.SetActive(false);
            }

            if (unitTabImage != null)
            {
                unitTabImage.gameObject.SetActive(false);
            }
        }
    }
}
