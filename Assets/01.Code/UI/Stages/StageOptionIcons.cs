using System.Collections.Generic;
using Code.Stages;
using UnityEngine;

namespace Code.UI.Stages
{
    /// <summary>편성 후보의 그림: 부하는 초상, 바닥 함정은 프리팹 그림. 벽 함정은 그림이 없어 null. 화면마다 하나씩 들고 캐시한다.</summary>
    public sealed class StageOptionIcons
    {
        private readonly Dictionary<StageOption, Sprite> _icons = new();

        public Sprite For(StageOption option)
        {
            if (option == null)
                return null;
            if (_icons.TryGetValue(option, out Sprite sprite))
                return sprite;
            if (option.Unit != null)
                sprite = option.Unit.IconSprite;
            else if (option.Trap != null && option.Trap.Prefab != null)
            {
                SpriteRenderer renderer = option.Trap.Prefab.GetComponentInChildren<SpriteRenderer>(true);
                sprite = renderer != null ? renderer.sprite : null;
            }
            _icons[option] = sprite;
            return sprite;
        }

        /// <summary>그림이 없는 칸에 넣을 두 글자 이름.</summary>
        public static string ShortName(string label) =>
            string.IsNullOrEmpty(label) ? string.Empty : label.Length <= 2 ? label : label.Substring(0, 2);
    }
}
