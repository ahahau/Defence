using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System;
using _01.Code.Units;

namespace _01.Code.UI
{
    public class UnitCardUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        [SerializeField] private Image frameImage;
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI titleLabel;
        [SerializeField] private TextMeshProUGUI descriptionLabel;
        [SerializeField] private TextMeshProUGUI countLabel;
        [SerializeField] private float hoverScale = 1.08f;
        [SerializeField] private float tweenDuration = 0.12f;
        [SerializeField] private float hoverYOffset = 12f;

        private RectTransform _rectTransform;
        private Vector3 _baseScale = Vector3.one;
        private Vector2 _basePosition;
        private int _baseSiblingIndex;
        private UnitDataSO _boundUnitData;
        private Action<UnitCardUI> _clickHandler;

        public UnitDataSO BoundUnitData => _boundUnitData;

        private void Awake()
        {
            _rectTransform = transform as RectTransform;
            _baseScale = transform.localScale;
            if (_rectTransform != null)
            {
                _basePosition = _rectTransform.anchoredPosition;
            }
        }

        public void SetData(UnitDataSO data)
        {
            _boundUnitData = data;

            if (data == null)
            {
                return;
            }

            if (frameImage != null)
            {
                frameImage.color = data.CardColor;
            }

            if (iconImage != null)
            {
                iconImage.sprite = data.CardIcon;
                iconImage.enabled = data.CardIcon != null;
            }

            if (titleLabel != null)
            {
                titleLabel.text = data.Name ?? string.Empty;
            }

            if (descriptionLabel != null)
            {
                descriptionLabel.text = data.Explanation ?? string.Empty;
            }

            if (countLabel != null)
            {
                countLabel.text = string.Empty;
            }
        }

        public void SetClickHandler(Action<UnitCardUI> clickHandler)
        {
            _clickHandler = clickHandler;
        }

        public void SetPosition(Vector2 anchoredPosition)
        {
            _rectTransform = transform as RectTransform;
            if (_rectTransform == null)
            {
                return;
            }

            _rectTransform.anchoredPosition = anchoredPosition;
            _basePosition = anchoredPosition;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            transform.DOKill();
            _baseSiblingIndex = transform.GetSiblingIndex();
            transform.SetAsLastSibling();
            if (_rectTransform != null)
            {
                _rectTransform.DOAnchorPos(_basePosition + new Vector2(0f, hoverYOffset), tweenDuration).SetEase(Ease.OutQuad);
            }
            transform.DOScale(_baseScale * hoverScale, tweenDuration).SetEase(Ease.OutQuad);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            transform.DOKill();
            transform.SetSiblingIndex(_baseSiblingIndex);
            if (_rectTransform != null)
            {
                _rectTransform.DOAnchorPos(_basePosition, tweenDuration).SetEase(Ease.OutQuad);
            }
            transform.DOScale(_baseScale, tweenDuration).SetEase(Ease.OutQuad);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || _boundUnitData == null)
            {
                return;
            }

            _clickHandler?.Invoke(this);
        }

        private void OnDisable()
        {
            transform.DOKill();
        }
    }
}
