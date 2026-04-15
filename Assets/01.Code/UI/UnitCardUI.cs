using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System;
using _01.Code.Units;

namespace _01.Code.UI
{
    public class UnitCardUI : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private Image frameImage;
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI titleLabel;
        [SerializeField] private TextMeshProUGUI descriptionLabel;
        [SerializeField] private TextMeshProUGUI countLabel;
        [SerializeField] private float selectedFrameBrightness = 1.25f;

        private Color _baseFrameColor = Color.white;
        public UnitDataSO BoundUnitData { get; private set; }
        private Action<UnitCardUI> _clickHandler;

        private void Awake()
        {
            if (frameImage != null)
            {
                _baseFrameColor = frameImage.color;
            }
        }

        public void SetData(UnitDataSO data)
        {
            BoundUnitData = data;

            if (data == null)
            {
                return;
            }

            if (frameImage != null)
            {
                _baseFrameColor = data.CardColor;
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
            RectTransform rectTransform = transform as RectTransform;
            if (rectTransform == null)
            {
                return;
            }

            rectTransform.anchoredPosition = anchoredPosition;
        }

        public void SetSelected(bool selected)
        {
            if (frameImage == null)
            {
                return;
            }

            if (!selected)
            {
                frameImage.color = _baseFrameColor;
                return;
            }

            frameImage.color = new Color(
                Mathf.Clamp01(_baseFrameColor.r * selectedFrameBrightness),
                Mathf.Clamp01(_baseFrameColor.g * selectedFrameBrightness),
                Mathf.Clamp01(_baseFrameColor.b * selectedFrameBrightness),
                _baseFrameColor.a);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || BoundUnitData == null)
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
