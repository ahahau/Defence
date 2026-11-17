using System;
using TMPro;
using UnityEngine;

namespace Code.UI.Stages
{
    /// <summary>
    /// 놓은 함정을 누르면 그 둘레에 뜨는 메뉴: 위 강화·오른쪽 판매·왼쪽 회전(벽 함정, 준비 단계)과 이름·단계.
    /// 바탕이 없어 버튼 밖 클릭은 지도로 간다. 값과 판단은 StageHudView가 넘긴다.
    /// </summary>
    public sealed class StageTrapRing : MonoBehaviour
    {
        [SerializeField] private RectTransform root;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text reason;
        [SerializeField] private UiFrameButton upgrade;
        [SerializeField] private UiFrameButton sell;
        [SerializeField] private UiFrameButton rotate;

        public bool IsValid => root != null && title != null && reason != null && upgrade != null && sell != null && rotate != null;
        public bool IsOpen => root != null && root.gameObject.activeSelf;

        public event Action UpgradeClicked;
        public event Action SellClicked;
        public event Action RotateClicked;

        /// <summary>함정 가운데의 화면 좌표에 메뉴 가운데를 둔다.</summary>
        public void Place(Vector2 screenPoint)
        {
            RectTransform parent = (RectTransform)root.parent;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPoint, null, out Vector2 local))
                root.anchoredPosition = local;
        }

        public void Show(string titleText, string upgradeLabel, bool canUpgrade, string sellLabel, bool canSell, bool canRotate,
            string reasonText)
        {
            if (!root.gameObject.activeSelf)
                root.gameObject.SetActive(true);
            title.text = titleText;
            reason.text = reasonText;
            upgrade.SetLabel(upgradeLabel);
            upgrade.SetInteractable(canUpgrade);
            sell.SetLabel(sellLabel);
            sell.SetInteractable(canSell);
            rotate.SetVisible(canRotate);
        }

        public void Hide()
        {
            if (root != null && root.gameObject.activeSelf)
                root.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            if (!IsValid)
                return;
            upgrade.Button.onClick.AddListener(ClickUpgrade);
            sell.Button.onClick.AddListener(ClickSell);
            rotate.Button.onClick.AddListener(ClickRotate);
        }

        private void OnDisable()
        {
            if (!IsValid)
                return;
            upgrade.Button.onClick.RemoveListener(ClickUpgrade);
            sell.Button.onClick.RemoveListener(ClickSell);
            rotate.Button.onClick.RemoveListener(ClickRotate);
        }

        private void ClickUpgrade() => UpgradeClicked?.Invoke();
        private void ClickSell() => SellClicked?.Invoke();
        private void ClickRotate() => RotateClicked?.Invoke();
    }
}
