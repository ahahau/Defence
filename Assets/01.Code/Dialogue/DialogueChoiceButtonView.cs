using TMPro;
using UnityEngine.UI;

namespace _01.Code.Dialogue
{
    public class DialogueChoiceButtonView : DialogueChoiceButtonBase
    {
        private TMP_Text label;
        private Button button;

        protected override void Awake()
        {
            base.Awake();
            label = GetComponentInChildren<TMP_Text>(true);
            button = GetComponent<Button>();
        }

        public void Bind(DialogueChoice choice, bool canSelect)
        {
            if (label == null)
                label = GetComponentInChildren<TMP_Text>(true);

            if (button == null)
                button = GetComponent<Button>();

            if (label != null)
                label.text = choice.Text;

            if (button != null)
                button.interactable = canSelect;

            SetVisualState(canSelect);
        }
    }
}
