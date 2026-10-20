using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Code.UI.RoomManagement
{
    /// <summary>
    /// 설치 창 카드 한 장(그림·이름·값·한 줄 설명). 막힌 카드는 흐리게, 고른 카드는 금색 테두리.
    /// 빈 자리도 카드 폭을 지켜 다른 카드가 움직이지 않게 한다.
    /// </summary>
    public sealed class InstallCardSlot : MonoBehaviour
    {
        [SerializeField] private UiFrameButton frame;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text cardName;
        [SerializeField] private TMP_Text price;
        [SerializeField] private TMP_Text note;
        [SerializeField, Range(0f, 1f)] private float blockedAlpha = 0.55f;

        public UiFrameButton Frame => frame;

        public void Bind(in InstallCardViewModel card)
        {
            group.alpha = card.Blocked ? blockedAlpha : 1f;
            group.blocksRaycasts = true;
            icon.sprite = card.Icon;
            icon.enabled = card.Icon != null;
            cardName.text = card.Name;
            price.text = card.Price;
            note.text = card.Note;
            note.color = card.Blocked ? HudPalette.Loss : HudPalette.TextDim;
            frame.SetSelected(card.Selected);
        }

        public void Hide()
        {
            group.alpha = 0f;
            group.blocksRaycasts = false;
            frame.SetSelected(false);
        }
    }
}
