using Code.UI.ViewModels;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Code.UI
{
    /// <summary>
    /// 대화 선택지 한 칸(uGUI). 결과 요약이 없으면 한 줄 높이로 줄고, 나타날 때 오른쪽에서 미끄러져 들어온다.
    /// 미끄러짐은 안쪽 <see cref="content"/>만 옮겨 세로 배치(Layout Group)를 흔들지 않는다.
    /// </summary>
    public sealed class DialogueChoiceButton : MonoBehaviour
    {
        [SerializeField] private UiFrameButton frame;
        [SerializeField] private TMP_Text text;
        [SerializeField] private TMP_Text summary;
        [SerializeField] private LayoutElement layout;
        [SerializeField, Tooltip("미끄러지며 들어오는 안쪽 묶음(바탕·글자)")] private RectTransform content;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private float fullHeight = 58f;
        [SerializeField] private float compactHeight = 38f;
        [SerializeField] private float slideDistance = 280f;
        [SerializeField] private float slideSeconds = 0.22f;

        private float _startTime;
        private bool _sliding;

        public UiFrameButton Frame => frame;

        /// <param name="delay">이 칸이 움직이기 시작할 때까지(실제 시간, 초).</param>
        public void Bind(DialogueChoiceViewModel choice, float delay)
        {
            gameObject.SetActive(true);
            var compact = string.IsNullOrWhiteSpace(choice.Summary);
            text.text = choice.Text;
            summary.text = choice.Summary;
            summary.gameObject.SetActive(!compact);
            text.rectTransform.anchorMin = new Vector2(0f, compact ? 0f : 0.42f);
            if (layout != null)
            {
                layout.preferredHeight = compact ? compactHeight : fullHeight;
                layout.minHeight = compactHeight;
            }

            // 잠긴 선택지(금화 부족 등)는 보이되 눌리지 않는다.
            frame.SetInteractable(choice.Selectable);
            text.color = choice.Selectable ? HudPalette.Text : HudPalette.TextMuted;

            _startTime = Time.unscaledTime + delay;
            _sliding = true;
            Apply(0f);
        }

        public void Clear()
        {
            _sliding = false;
            gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!_sliding)
                return;

            // 대화는 배속·일시정지와 상관없이 같은 속도로 움직인다.
            var t = Mathf.Clamp01((Time.unscaledTime - _startTime) / Mathf.Max(0.01f, slideSeconds));
            Apply(t);
            if (t >= 1f)
                _sliding = false;
        }

        private void Apply(float t)
        {
            var eased = 1f - Mathf.Pow(1f - t, 3f);
            if (content != null)
                content.anchoredPosition = new Vector2((1f - eased) * slideDistance, 0f);
            if (group != null)
                group.alpha = eased;
        }
    }
}
