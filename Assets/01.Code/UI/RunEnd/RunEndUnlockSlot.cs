using Code.UI.ViewModels;
using TMPro;
using UnityEngine;

namespace Code.UI
{
    /// <summary>결과 화면 "새로 열림" 한 줄(그리기만). 아이콘 칸은 그림이 없어도 자리를 지킨다.</summary>
    public sealed class RunEndUnlockSlot : MonoBehaviour
    {
        [SerializeField] private GameObject iconFrame;
        [SerializeField] private UnityEngine.UI.Image icon;
        [SerializeField] private TMP_Text unlockName;
        [SerializeField] private TMP_Text detail;

        public void Bind(RunEndUnlockViewModel model)
        {
            gameObject.SetActive(true);
            unlockName.text = model.Name;
            unlockName.color = model.IsMore ? HudPalette.TextDim : HudPalette.Text;
            detail.text = model.Detail;
            iconFrame.SetActive(!model.IsMore);
            icon.sprite = model.Icon;
            icon.enabled = model.Icon != null;
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
