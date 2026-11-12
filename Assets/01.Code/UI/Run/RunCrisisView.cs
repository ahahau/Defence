using TMPro;
using UnityEngine;

namespace Code.UI.Run
{
    /// <summary>위기 카드·고정 폭 알림·금색 테두리·방으로 이어지는 점선의 표시만 맡는다.</summary>
    public class RunCrisisView : MonoBehaviour
    {
        [SerializeField] private GameObject card;
        [SerializeField] private TMP_Text body;
        [SerializeField] private UnityEngine.UI.ScrollRect scroll;
        [SerializeField] private GameObject frame;
        [SerializeField] private TMP_Text footer;
        [SerializeField] private GameObject notification;
        [SerializeField] private TMP_Text notificationText;
        [SerializeField] private UnityEngine.UI.Image[] dots = System.Array.Empty<UnityEngine.UI.Image>();
        [SerializeField] private RectTransform canvas;

        [SerializeField, Tooltip("상성표와 안내 줄이 든 아래 띠. 위기 때나 상성 버튼으로 열 때만 보인다.")]
        private GameObject legendPanel;

        [SerializeField] private UiFrameButton legendToggle;

        private string _normalFooter;
        private float _noticeLeft;
        private bool _battleMode;
        private bool _legendOpen;
        private UnityEngine.Events.UnityAction _toggle;
        public bool IsShowing { get; private set; }

        private void Awake() => _normalFooter = footer != null ? footer.text : string.Empty;

        private void OnEnable()
        {
            _toggle ??= () => { _legendOpen = !_legendOpen; RefreshLegend(); };
            if (legendToggle != null) legendToggle.Button.onClick.AddListener(_toggle);
        }

        private void OnDisable()
        {
            if (legendToggle != null && _toggle != null) legendToggle.Button.onClick.RemoveListener(_toggle);
        }

        /// <summary>전투(원정 포함) 중에만 아래 띠와 상성 버튼을 쓴다. 준비 화면에서는 닫는다.</summary>
        public void SetBattleMode(bool battle, bool openLegend = false)
        {
            _battleMode = battle;
            _legendOpen = battle && openLegend;
            RefreshLegend();
        }

        // 아래 띠는 늘 떠 있지 않고 위기 때(대처 안내)나 상성 버튼을 눌렀을 때만 연다(2026-11-10 사용자 C 목업 승인).
        private void RefreshLegend()
        {
            // 위기 때도 저절로 열지 않는다. 상성 버튼으로만 연다(2026-11-12 사용자 목업 승인 "전투 중 고정 글" 줄이기).
            if (legendPanel != null) legendPanel.SetActive(_battleMode && _legendOpen);
        }

        public void ShowCrisis(string text)
        {
            IsShowing = !string.IsNullOrEmpty(text);
            card.SetActive(IsShowing);
            frame.SetActive(IsShowing);
            body.SetText(text ?? string.Empty);
            if (IsShowing && scroll != null)
            {
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
                scroll.verticalNormalizedPosition = 1f;
            }
            footer.SetText(_normalFooter);
            if (!IsShowing) for (int i = 0; i < dots.Length; i++) dots[i].gameObject.SetActive(false);
            RefreshLegend();
        }

        public void Notify(string text)
        {
            notificationText.SetText(text);
            notification.SetActive(true);
            _noticeLeft = 4f;
        }

        public void ClearNotice()
        {
            _noticeLeft = 0f;
            notification.SetActive(false);
        }

        public void AdvanceDisplay(float seconds)
        {
            if (_noticeLeft <= 0f) return;
            _noticeLeft = Mathf.Max(0f, _noticeLeft - seconds);
            if (_noticeLeft <= 0f) notification.SetActive(false);
        }

        /// <summary>카드 아래와 각 위기 방을 HUD 점선으로 잇는다. 화면 크기가 바뀔 때도 호출한다.</summary>
        public void LayoutLinks(Camera camera, RunGridView grid, bool[] rooms)
        {
            int used = 0;
            Vector3[] corners = _corners;
            ((RectTransform)card.transform).GetWorldCorners(corners);
            Vector2 fromScreen = (corners[0] + corners[3]) * 0.5f;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, fromScreen, null, out Vector2 from);
            for (int room = 0; room < rooms.Length; room++)
            {
                if (!IsShowing || !rooms[room]) continue;
                Rect rect = grid.RoomRect(room);
                Vector2 point = camera.WorldToScreenPoint(new Vector3(rect.center.x, rect.center.y, 0f));
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, point, null, out Vector2 to);
                for (int dash = 0; dash < 8 && used < dots.Length; dash++)
                {
                    UnityEngine.UI.Image image = dots[used++];
                    image.gameObject.SetActive(true);
                    RectTransform rt = image.rectTransform;
                    Vector2 a = Vector2.Lerp(from, to, dash / 8f);
                    Vector2 b = Vector2.Lerp(from, to, (dash + 0.45f) / 8f);
                    rt.anchoredPosition = (a + b) * 0.5f;
                    rt.sizeDelta = new Vector2((b - a).magnitude, 2f);
                    rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg);
                }
            }
            for (int i = used; i < dots.Length; i++) dots[i].gameObject.SetActive(false);
        }

        private readonly Vector3[] _corners = new Vector3[4];
    }
}
