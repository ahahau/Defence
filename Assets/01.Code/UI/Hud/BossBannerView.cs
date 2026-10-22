using Code.Core;
using Code.Manager;
using Code.UI.ViewModels;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace Code.UI
{
    /// <summary>
    /// 보스 배너(uGUI, A안, 화면 위 가운데 띠). <see cref="BossWavePresenter.BannerRequested"/>를 받아 습격·증원·격노를 알린다.
    /// 위에서 내려오며 나타나 머문 뒤 사라진다. 시간을 멈추지 않고 클릭도 막지 않는다(지도 가운데·아래 가운데는 비움).
    /// 떠 있는 동안 새 요청이 오면 내용만 바꾸고 머무는 시간을 다시 센다. 움직임은 실제 시간 기준이라 배속과 상관없다.
    ///
    /// 예전에는 보스 배너를 요청해도 그리는 화면이 없어, 보스 등장·증원·격노가 아무 알림 없이 지나갔다.
    /// </summary>
    public sealed class BossBannerView : MonoBehaviour
    {
        [SerializeField] private BossWavePresenter bossWave;
        [SerializeField] private RectTransform banner;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private UnityEngine.UI.Image fill;
        [SerializeField] private UnityEngine.UI.Image outline;
        [SerializeField] private TMP_Text caption;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text subtitle;
        [SerializeField, Min(0f)] private float enterSeconds = 0.2f;
        [SerializeField, Min(0f)] private float exitSeconds = 0.3f;
        [SerializeField, Tooltip("나타날 때 위에서 내려오는 거리.")]
        private float dropDistance = 24f;
        [SerializeField, Min(0f)] private float shakeSeconds = 0.3f;
        [SerializeField, Min(0f)] private float shakeStrength = 8f;

        private Vector2 _restPosition;
        private Sequence _sequence;

        public bool IsShowing => banner != null && banner.gameObject.activeSelf;

        /// <summary>요청 한 장을 띄운다. 실측·테스트도 이 경로로 띄운다.</summary>
        public void Show(BossBannerRequest request)
        {
            var model = BossBannerPresenter.Build(request);
            var wasShowing = IsShowing;

            caption.text = model.Caption;
            title.text = model.Title;
            subtitle.text = model.Subtitle;
            var border = BossBannerPresenter.BorderColor(model.Kind);
            caption.color = border;
            outline.color = border;
            fill.color = BossBannerPresenter.FillColor(model.Kind);

            _sequence?.Kill();
            banner.DOKill();
            banner.gameObject.SetActive(true);

            _sequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
            if (wasShowing)
            {
                group.alpha = 1f;
                banner.anchoredPosition = _restPosition;
            }
            else
            {
                group.alpha = 0f;
                banner.anchoredPosition = _restPosition + new Vector2(0f, dropDistance);
                _sequence.Append(group.DOFade(1f, enterSeconds));
                _sequence.Join(banner.DOAnchorPos(_restPosition, enterSeconds).SetEase(Ease.OutQuad));
            }

            if (model.Shake)
                _sequence.Append(banner.DOShakeAnchorPos(shakeSeconds, new Vector2(shakeStrength, 0f), 20, 0f, false, true));
            _sequence.AppendInterval(model.HoldSeconds);
            _sequence.Append(group.DOFade(0f, exitSeconds));
            _sequence.OnComplete(Hide);
        }

        private void Awake()
        {
            if (bossWave == null)
                bossWave = SceneReferenceFallback.Find<BossWavePresenter>(this, nameof(bossWave));
            if (banner != null)
            {
                _restPosition = banner.anchoredPosition;
                banner.gameObject.SetActive(false);
            }
        }

        private void OnEnable()
        {
            if (bossWave != null)
                bossWave.BannerRequested += Show;
        }

        private void OnDisable()
        {
            if (bossWave != null)
                bossWave.BannerRequested -= Show;
            _sequence?.Kill();
            Hide();
        }

        private void Hide()
        {
            _sequence = null;
            if (banner == null)
                return;

            banner.anchoredPosition = _restPosition;
            banner.gameObject.SetActive(false);
        }
    }
}
