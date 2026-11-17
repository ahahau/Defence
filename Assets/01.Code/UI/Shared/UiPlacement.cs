using UnityEngine;

namespace Code.UI
{
    /// <summary>누른 자리 옆에 카드를 놓는 공통 배치(오버레이 캔버스). 화면 밖으로 나가면 안으로 밀어 넣는다.</summary>
    public static class UiPlacement
    {
        /// <summary>
        /// 화면 좌표(왼쪽 아래 원점)의 오른쪽 위에 카드를 놓는다. 카드는 부모 왼쪽 아래 기준(앵커·피벗 0,0)이어야 한다.
        /// </summary>
        public static void PlaceNear(RectTransform card, Vector2 screenPosition, Vector2 screenOffset)
        {
            var parent = card.parent as RectTransform;
            if (parent == null)
                return;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPosition + screenOffset, null, out var local);
            var bounds = parent.rect;
            var size = card.rect.size;
            var x = Mathf.Clamp(local.x - bounds.xMin, 0f, Mathf.Max(0f, bounds.width - size.x));
            var y = Mathf.Clamp(local.y - bounds.yMin, 0f, Mathf.Max(0f, bounds.height - size.y));
            card.anchoredPosition = new Vector2(x, y);
        }
    }
}
