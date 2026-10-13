using UnityEngine;
using UnityEngine.UIElements;

namespace Code.UI.Toolkit
{
    /// <summary>누른 자리 옆에 카드를 놓는 공통 배치. 화면 밖으로 나가면 안으로 밀어 넣는다.</summary>
    public static class ToolkitPlacement
    {
        /// <summary>
        /// 화면 좌표(왼쪽 아래 원점)의 오른쪽 위에 카드를 놓는다. 카드 크기가 아직 정해지지 않았으면 false.
        /// </summary>
        public static bool PlaceNear(VisualElement card, Vector2 screenPosition, Vector2 screenOffset)
        {
            if (card == null || card.panel == null)
                return false;

            var panelSize = card.panel.visualTree.layout.size;
            var cardSize = card.layout.size;
            if (float.IsNaN(cardSize.x) || float.IsNaN(panelSize.x) || panelSize.x <= 0f)
                return false;

            var origin = RuntimePanelUtils.ScreenToPanel(card.panel,
                new Vector2(screenPosition.x + screenOffset.x, Screen.height - screenPosition.y - screenOffset.y));
            card.style.left = Mathf.Clamp(origin.x, 0f, Mathf.Max(0f, panelSize.x - cardSize.x));
            card.style.top = Mathf.Clamp(origin.y - cardSize.y, 0f, Mathf.Max(0f, panelSize.y - cardSize.y));
            return true;
        }
    }
}
