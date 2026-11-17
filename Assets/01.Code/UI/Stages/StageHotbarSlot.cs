using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Code.UI.Stages
{
    /// <summary>단축칸 한 칸: 그림(없으면 짧은 이름)·단축키·가격. 금화가 모자라면 어둡게 덮는다.</summary>
    public sealed class StageHotbarSlot : MonoBehaviour
    {
        [SerializeField] private UiFrameButton button;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text shortName;
        [SerializeField] private TMP_Text key;
        [SerializeField] private TMP_Text cost;
        [SerializeField] private Image shade;

        public bool IsValid => button != null && icon != null && shortName != null && key != null && cost != null && shade != null;
        public Button Button => button.Button;

        /// <param name="price">0 이하이면 가격을 비운다(철거).</param>
        public void Show(Sprite sprite, string name, string hotkey, int price, bool usable, bool affordable, bool selected)
        {
            gameObject.SetActive(true);
            icon.sprite = sprite;
            icon.enabled = sprite != null;
            icon.preserveAspect = true;
            shortName.text = sprite == null ? name : string.Empty;
            key.text = hotkey;
            cost.text = price > 0 ? price.ToString() : string.Empty;
            cost.color = affordable ? HudPalette.Accent : HudPalette.Loss;
            shade.enabled = !usable || !affordable;
            button.SetInteractable(usable);
            button.SetSelected(selected);
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
