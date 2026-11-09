using System;
using TMPro;
using UnityEngine;

namespace Code.UI.Run
{
    /// <summary>왼쪽 부하 목록의 칸 하나(로보토미식): 얼굴·이름·있는 곳·체력. 누르면 밖으로 알린다.</summary>
    public class RunRosterCard : MonoBehaviour
    {
        [SerializeField] private UnityEngine.UI.Image face;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text placeText;
        [SerializeField] private UnityEngine.UI.Image healthFill;
        [SerializeField] private UnityEngine.UI.Image outline;
        [SerializeField] private UnityEngine.UI.Button button;
        [SerializeField] private UnityEngine.UI.Image affinityDot;

        private UnityEngine.Events.UnityAction _clickAction;

        public event Action Clicked;

        private void OnEnable()
        {
            _clickAction ??= () => Clicked?.Invoke();
            if (button != null) button.onClick.AddListener(_clickAction);
        }

        private void OnDisable()
        {
            if (button != null && _clickAction != null) button.onClick.RemoveListener(_clickAction);
        }

        public void Show(Sprite faceSprite, string displayName, string place, float health, bool selected, bool down, Color affinityColor)
        {
            if (face != null)
            {
                face.sprite = faceSprite;
                face.enabled = faceSprite != null;
                face.preserveAspect = true;
                face.color = down ? HudPalette.FrameDisabled : HudPalette.Frame;
            }
            if (affinityDot != null) affinityDot.color = affinityColor;
            if (nameText != null) nameText.text = displayName;
            if (placeText != null)
            {
                placeText.text = place;
                placeText.color = down ? HudPalette.Danger : HudPalette.TextDim;
            }
            if (healthFill != null)
            {
                healthFill.fillAmount = Mathf.Clamp01(health);
                healthFill.color = health <= 0.3f ? HudPalette.Danger : HudPalette.Gain;
            }
            if (outline != null) outline.color = selected ? HudPalette.BorderAccent : HudPalette.BorderQuiet;
        }
    }
}
