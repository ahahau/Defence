using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _01.Code.UI
{
    public sealed class BossWaveUiView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text subtitle;
        [SerializeField] private Button retryButton;

        public CanvasGroup CanvasGroup => canvasGroup;
        public TMP_Text Title => title;
        public TMP_Text Subtitle => subtitle;
        public Button RetryButton => retryButton;
    }
}
