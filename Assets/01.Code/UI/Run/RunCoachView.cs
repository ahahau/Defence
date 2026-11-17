using TMPro;
using UnityEngine;

namespace Code.UI.Run
{
    /// <summary>
    /// 첫 판 안내: 누를 곳을 깜빡이는 테두리로 감싸고 그 아래(자리가 없으면 위)에 노란 한 줄을 띄운다
    /// (2026-11-12 사용자 목업 승인). 무엇을 띄울지는 디렉터가 정하고, 여기서는 그리기만 한다.
    /// 클릭을 막지 않도록 모든 그림의 Raycast Target은 끈다(생성기 <c>RunCoachBuilder</c>).
    /// </summary>
    public class RunCoachView : MonoBehaviour
    {
        [SerializeField] private RectTransform frame;
        [SerializeField] private CanvasGroup frameGroup;
        [SerializeField] private RectTransform tip;
        [SerializeField] private TMP_Text tipText;
        [SerializeField, Min(0f), Tooltip("테두리가 누를 곳보다 바깥으로 나오는 폭(px).")] private float framePadding = 6f;
        [SerializeField, Min(0f), Tooltip("테두리와 노란 줄 사이(px).")] private float tipGap = 8f;
        [SerializeField, Min(0f), Tooltip("노란 줄의 글 좌우 여백(px). 테두리 그림 두께를 넘어야 글이 안 잘린다.")] private float tipPadding = 22f;
        [SerializeField, Min(0.1f), Tooltip("깜빡임 한 번(켜짐+꺼짐)의 길이(초, 멈춤 중에도 흐른다).")] private float blinkSeconds = 1f;

        // 화면 왼쪽 이 몫 안의 대상은 노란 줄을 옆에 단다.
        private const float SideZone = 0.2f;
        private readonly Vector3[] _corners = new Vector3[4];
        private string _shownText;
        private bool _visible = true;

        /// <summary>UI 요소를 가리킨다. target이 없거나 꺼져 있으면 숨긴다.</summary>
        public void Show(string text, RectTransform target)
        {
            if (target == null || !target.gameObject.activeInHierarchy) { Hide(); return; }
            // 오버레이 캔버스라 월드 모서리가 곧 화면 픽셀이다.
            target.GetWorldCorners(_corners);
            Show(text, Rect.MinMaxRect(_corners[0].x, _corners[0].y, _corners[2].x, _corners[2].y));
        }

        /// <summary>화면 픽셀 사각형을 가리킨다(평면도의 방처럼 UI가 아닌 곳).</summary>
        public void Show(string text, Rect screenRect)
        {
            if (string.IsNullOrEmpty(text) || frame == null || tip == null || tipText == null) { Hide(); return; }
            SetVisible(true);
            RectTransform canvas = (RectTransform)frame.parent;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, screenRect.min, null, out Vector2 min);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, screenRect.max, null, out Vector2 max);
            min -= Vector2.one * framePadding;
            max += Vector2.one * framePadding;
            frame.anchorMin = frame.anchorMax = new Vector2(0.5f, 0.5f);
            frame.pivot = new Vector2(0.5f, 0.5f);
            frame.sizeDelta = max - min;
            frame.anchoredPosition = (min + max) * 0.5f;

            if (_shownText != text)
            {
                _shownText = text;
                tipText.text = text;
                float width = tipText.GetPreferredValues(text).x + tipPadding * 2f;
                tip.sizeDelta = new Vector2(Mathf.Ceil(width), tip.sizeDelta.y);
            }
            Rect area = canvas.rect;
            tip.anchorMin = tip.anchorMax = new Vector2(0.5f, 0.5f);
            // 왼쪽 부하 목록처럼 화면 가장자리에 줄지어 선 것은 아래에 달면 다음 칸을 가리므로 오른쪽 옆에 단다.
            if ((min.x + max.x) * 0.5f < area.xMin + area.width * SideZone)
            {
                tip.pivot = new Vector2(0f, 0.5f);
                tip.anchoredPosition = new Vector2(max.x + tipGap, (min.y + max.y) * 0.5f);
                return;
            }
            float height = tip.sizeDelta.y;
            bool above = min.y - tipGap - height < area.yMin;
            tip.pivot = new Vector2(0.5f, above ? 0f : 1f);
            float half = tip.sizeDelta.x * 0.5f;
            float x = Mathf.Clamp((min.x + max.x) * 0.5f, area.xMin + half, area.xMax - half);
            tip.anchoredPosition = new Vector2(x, above ? max.y + tipGap : min.y - tipGap);
        }

        public void Hide() => SetVisible(false);

        private void Awake() => SetVisible(false);

        private void Update()
        {
            if (!_visible || frameGroup == null) return;
            frameGroup.alpha = Mathf.PingPong(Time.unscaledTime * 2f / blinkSeconds, 1f) > 0.5f ? 1f : 0.25f;
        }

        private void SetVisible(bool visible)
        {
            if (_visible == visible) return;
            _visible = visible;
            if (frame != null) frame.gameObject.SetActive(visible);
            if (tip != null) tip.gameObject.SetActive(visible);
        }
    }
}
