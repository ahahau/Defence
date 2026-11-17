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
        [SerializeField, Tooltip("칠해진 몸을 맞출 칸 크기(아이콘 칸 안쪽).")]
        private Vector2 iconBox = new(40f, 40f);
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
            UiSpriteFit.Apply(icon, iconBox);
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
