using Code.UI.ViewModels;
using TMPro;
using UnityEngine;

namespace Code.UI
{
    /// <summary>운영 방침 카드 한 칸(그리기만). 값은 <see cref="PolicyCardViewModel"/>, 누르면 <see cref="Choose"/> 버튼이 알린다.</summary>
    public sealed class PolicyCardSlot : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private UnityEngine.UI.Image outline;
        [SerializeField] private TMP_Text cardName;
        [SerializeField] private TMP_Text duration;
        [SerializeField] private TMP_Text description;
        [SerializeField] private TMP_Text effectLabels;
        [SerializeField] private TMP_Text effectValues;
        [SerializeField] private UnityEngine.UI.Image comboBack;
        [SerializeField] private UnityEngine.UI.Image comboOutline;
        [SerializeField] private TMP_Text comboTitle;
        [SerializeField] private TMP_Text comboBody;
        [SerializeField] private UiFrameButton choose;

        public UiFrameButton Choose => choose;

        public void Bind(PolicyCardViewModel model)
        {
            gameObject.SetActive(true);
            cardName.text = model.Name;
            duration.text = model.Duration;
            description.text = model.Description;
            effectLabels.text = model.EffectLabels;
            effectValues.text = model.EffectValues;

            comboTitle.text = model.ComboTitle;
            comboBody.text = model.ComboBody;
            comboTitle.color = model.ComboComplete ? HudPalette.Accent : HudPalette.TextDim;
            comboBack.color = model.ComboComplete ? HudPalette.AlertChip : HudPalette.Chip;
            comboOutline.color = model.ComboComplete ? HudPalette.BorderAccent : HudPalette.BorderQuiet;
            var hasCombo = !string.IsNullOrEmpty(model.ComboTitle);
            comboBack.enabled = hasCombo;
            comboOutline.enabled = hasCombo;

            // 잠긴 카드는 흐리게 두되 이유("금화 부족")는 읽히게 버튼 글자로 남긴다. 버튼이 이미 흐려지므로 카드는 조금만 흐린다
            // (0.55로 두면 겹쳐서 "금화 부족"이 읽히지 않았다).
            group.alpha = model.Locked ? 0.75f : 1f;
            outline.color = model.Locked ? HudPalette.BorderQuiet : HudPalette.Border;
            choose.SetLabel(model.ButtonLabel);
            choose.SetInteractable(!model.Locked);
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
