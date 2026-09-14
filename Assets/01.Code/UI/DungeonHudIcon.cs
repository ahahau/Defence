using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _01.Code.UI
{
    /// <summary>
    /// 오른쪽 위 상태 카드 왼쪽에 표식을 끼운다.
    ///
    /// 카드는 <c>DungeonHudStyle.ApplyTopRightCard</c>가 만들지만 거기에는 그림을 놓을 자리가 없다.
    /// 그 코드는 다른 작업이 쓰는 파일 안에 있어서 건드리지 않고, 카드가 다 만들어진 다음에
    /// 여기서 한 겹 더 얹는다. 그래서 <b>반드시 ApplyTopRightCard 뒤에</b> 불러야 한다 —
    /// 먼저 부르면 그쪽이 글자 여백을 도로 덮어쓴다.
    ///
    /// 그림은 <see cref="UiSkinSO"/>를 통해 가져온다. 그림 폴더가 Resources 밖이라
    /// 실행 중에 직접 못 집기 때문이다.
    /// </summary>
    public static class DungeonHudIcon
    {
        private const string ChildName = "Hud Icon";

        // 카드가 350x60 이다. 왼쪽에 34짜리 정사각을 놓고 글자를 그만큼 민다.
        private const float IconSize = 34f;
        private const float LeftPad = 13f;
        private const float TextGap = 9f;

        /// <summary>카드에 표식을 붙이고 글자를 그만큼 오른쪽으로 민다. 그림이 없으면 아무것도 하지 않는다.</summary>
        public static void Attach(GameObject card, TMP_Text primaryText, Sprite icon)
        {
            if (card == null || icon == null)
                return;

            var image = FindOrCreate(card.transform);
            if (image == null)
                return;
            image.sprite = icon;
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.color = Color.white;

            var rect = image.rectTransform;
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.sizeDelta = new Vector2(IconSize, IconSize);
            rect.anchoredPosition = new Vector2(LeftPad, 0f);
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;

            if (primaryText == null || primaryText.transform is not RectTransform textRect)
                return;

            // 글자가 표식 위로 올라타지 않게 왼쪽 여백만 넓힌다. 위아래와 오른쪽은 그대로 둔다.
            var offset = textRect.offsetMin;
            textRect.offsetMin = new Vector2(LeftPad + IconSize + TextGap, offset.y);
        }

        private static Image FindOrCreate(Transform card)
        {
            var existing = card.Find(ChildName);
            if (existing != null && existing.TryGetComponent<Image>(out var found))
                return found;

            var prefab = Resources.Load<Image>("UI/HudIcon");
            if (prefab == null)
            {
                Debug.LogError("UI/HudIcon prefab is missing.");
                return null;
            }

            var image = Object.Instantiate(prefab, card, false);
            image.gameObject.name = ChildName;
            return image;
        }

        /// <summary>독립된 제목·날짜 표시에는 텍스트 영역 안에 아이콘 여백을 확보한다.</summary>
        public static void AttachToLabel(TMP_Text label, Sprite icon)
        {
            if (label == null || icon == null)
                return;

            Attach(label.gameObject, null, icon);
            var margin = label.margin;
            margin.x = Mathf.Max(margin.x, LeftPad + IconSize + TextGap);
            label.margin = margin;
        }

        /// <summary>Resources 의 UI 표를 한 번만 읽어 둔다. 카드마다 다시 읽을 이유가 없다.</summary>
        public static UiSkinSO Skin
        {
            get
            {
                if (_skin == null)
                    _skin = Resources.Load<UiSkinSO>("UI/UiSkin");

                return _skin;
            }
        }

        private static UiSkinSO _skin;
    }
}
