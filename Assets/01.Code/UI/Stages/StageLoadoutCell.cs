using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Code.UI.Stages
{
    /// <summary>편성 창 격자 한 칸: 그림·이름·가격(잠기면 해금 조건). 편성에 든 칸은 금색 테두리, 고른 칸은 파란 테두리.</summary>
    public sealed class StageLoadoutCell : MonoBehaviour
    {
        [SerializeField] private UiFrameButton button;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text shortName;
        [SerializeField] private TMP_Text label;
        [SerializeField] private TMP_Text badge;
        [SerializeField] private Image shade;
        [SerializeField] private Image focus;

        public bool IsValid => button != null && icon != null && shortName != null && label != null && badge != null
            && shade != null && focus != null;
        public Button Button => button.Button;

        public void Show(Sprite sprite, string name, string badgeText, bool unlocked, bool inLoadout, bool focused)
        {
            gameObject.SetActive(true);
            icon.sprite = sprite;
            icon.enabled = sprite != null;
            icon.preserveAspect = true;
            shortName.text = sprite == null ? StageOptionIcons.ShortName(name) : string.Empty;
            label.text = name;
            badge.text = badgeText;
            badge.color = unlocked ? HudPalette.Accent : HudPalette.TextMuted;
            shade.enabled = !unlocked;
            focus.enabled = focused;
            button.SetSelected(inLoadout);
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
