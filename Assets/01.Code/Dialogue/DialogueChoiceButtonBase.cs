using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace _01.Code.Dialogue
{
    public class DialogueChoiceButtonBase : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Graphic background;
        [SerializeField] private Graphic labelGraphic;
        [SerializeField] private Color activeBackgroundColor = new(0.18f, 0.38f, 0.45f, 1f);
        [SerializeField] private Color activeLabelColor = new(0.96f, 0.98f, 1f, 1f);
        [SerializeField] private Color disabledBackgroundColor = new(0.08f, 0.09f, 0.1f, 0.88f);
        [SerializeField] private Color disabledLabelColor = new(0.48f, 0.5f, 0.52f, 1f);
        [SerializeField] private float hoverOffsetX = -24f;
        [SerializeField] private float hoverDuration = 0.12f;

        private RectTransform rectTransform;
        private Coroutine hoverRoutine;
        private Vector2 restPosition;
        private bool canHover;

        protected virtual void Awake()
        {
            rectTransform = transform as RectTransform;
            if (background == null)
                background = GetComponent<Graphic>();

            CaptureRestPosition();
        }

        public void CaptureRestPosition()
        {
            if (rectTransform == null)
                rectTransform = transform as RectTransform;

            if (rectTransform != null)
                restPosition = rectTransform.anchoredPosition;
        }

        public void SetRestPosition(Vector2 position)
        {
            restPosition = position;
        }

        public void SetVisualState(bool isEnabled)
        {
            canHover = isEnabled;

            if (background != null)
                background.color = isEnabled ? activeBackgroundColor : disabledBackgroundColor;

            if (labelGraphic != null)
                labelGraphic.color = isEnabled ? activeLabelColor : disabledLabelColor;

            if (!isEnabled)
                MoveTo(restPosition, true);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!canHover || rectTransform == null)
                return;

            MoveTo(restPosition + new Vector2(hoverOffsetX, 0f), false);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (rectTransform == null)
                return;

            MoveTo(restPosition, false);
        }

        private void MoveTo(Vector2 target, bool instant)
        {
            if (hoverRoutine != null)
                StopCoroutine(hoverRoutine);

            if (instant || !isActiveAndEnabled || hoverDuration <= 0f)
            {
                rectTransform.anchoredPosition = target;
                return;
            }

            hoverRoutine = StartCoroutine(MoveRoutine(target));
        }

        private IEnumerator MoveRoutine(Vector2 target)
        {
            var start = rectTransform.anchoredPosition;
            var elapsed = 0f;

            while (elapsed < hoverDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / hoverDuration);
                t = 1f - Mathf.Pow(1f - t, 3f);
                rectTransform.anchoredPosition = Vector2.LerpUnclamped(start, target, t);
                yield return null;
            }

            rectTransform.anchoredPosition = target;
            hoverRoutine = null;
        }
    }
}
