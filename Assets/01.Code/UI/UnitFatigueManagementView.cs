using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _01.Code.UI
{
    public sealed class UnitFatigueManagementView : MonoBehaviour
    {
        [SerializeField] private TMP_Text fatigueText;
        [SerializeField] private Image fatigueFill;
        [SerializeField] private Button recallButton;
        [SerializeField] private TMP_Text recallButtonLabel;

        public TMP_Text FatigueText => fatigueText;
        public Image FatigueFill => fatigueFill;
        public Button RecallButton => recallButton;
        public TMP_Text RecallButtonLabel => recallButtonLabel;
    }
}
