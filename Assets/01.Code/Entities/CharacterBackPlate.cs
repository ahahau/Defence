using UnityEngine;

namespace _01.Code.Entities
{
    /// <summary>
    /// 캐릭터를 두르는 네모 테두리.
    ///
    /// 유닛과 침입자가 바닥 그림 위에 그냥 얹혀 있어서 어디까지가 한 명인지 읽히지 않았다.
    /// 네모로 한 번 둘러 주면 각자가 하나의 말로 보인다.
    ///
    /// 처음에는 발밑에 타원 받침을 깔았는데 그건 요청과 다른 물건이었다. 받침은 걷어냈고,
    /// 지금 남은 것은 테두리 하나뿐이다.
    ///
    /// 9슬라이스로 늘린다. 그냥 늘리면 모서리 장식이 캐릭터 비율만큼 찌그러진다. 다만 경계
    /// 합보다 작게 그리면 가운데가 없어져 모서리끼리 겹치므로, 최소 크기를 경계 합 위로 잡는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CharacterBackPlate : MonoBehaviour
    {
        private const string SkinResourcePath = "UI/CharacterPlateSkin";
        private const string FrameObjectName = "CharacterFrame";

        private static CharacterPlateSkinSO _skin;
        private static bool _skinLoaded;

        private SpriteRenderer _source;
        private SpriteRenderer _frame;

        /// <summary>테두리 색. 등급이 정해지면 침입자 쪽에서 넣어 준다.</summary>
        public void SetGlowColor(Color color)
        {
            if (_frame != null)
                _frame.color = color;
        }

        /// <summary>테두리를 세운다. 캐릭터 그림이 정해진 뒤에 불러야 크기를 잴 수 있다.</summary>
        public void Initialize(SpriteRenderer source)
        {
            _source = source;
            var skin = ResolveSkin();

            if (_source == null || skin == null || skin.Frame == null)
            {
                enabled = false;
                return;
            }

            _frame = EnsureFrame();
            _frame.sprite = skin.Frame;
            _frame.color = skin.FrameColor;
            _frame.drawMode = SpriteDrawMode.Sliced;

            Layout(skin);
        }

        /// <summary>캐릭터 그림이 바뀌면 테두리 크기도 다시 잡는다.</summary>
        public void Refresh()
        {
            var skin = ResolveSkin();
            if (_source != null && skin != null)
                Layout(skin);
        }

        private void Layout(CharacterPlateSkinSO skin)
        {
            if (_source.sprite == null)
            {
                if (_frame != null)
                    _frame.enabled = false;
                return;
            }

            _frame.enabled = true;

            var bounds = _source.sprite.bounds;
            var size = new Vector2(bounds.size.x, bounds.size.y) * skin.FramePadding;

            // 9슬라이스는 경계 합보다 작게 그릴 수 없다. 작게 주면 가운데가 사라지고 모서리가
            // 서로 겹쳐 뭉개진다. 스프라이트가 정한 최소치 아래로는 내려가지 않게 잡는다.
            var border = skin.Frame.border;
            var ppu = skin.Frame.pixelsPerUnit;
            var minWidth = (border.x + border.z) / ppu;
            var minHeight = (border.y + border.w) / ppu;
            size.x = Mathf.Max(size.x, minWidth * 1.05f);
            size.y = Mathf.Max(size.y, minHeight * 1.05f);

            _frame.size = size;
            _frame.transform.localPosition = bounds.center;
            _frame.transform.localScale = Vector3.one;

            // 캐릭터 바로 뒤에 둔다. 앞에 두면 테두리 안쪽 여백이 얼굴을 덮는다.
            _frame.sortingLayerID = _source.sortingLayerID;
            _frame.sortingOrder = _source.sortingOrder - 1;
        }

        private SpriteRenderer EnsureFrame()
        {
            if (_frame != null)
                return _frame;

            var existing = _source.transform.Find(FrameObjectName);
            if (existing != null && existing.TryGetComponent<SpriteRenderer>(out var found))
                return found;

            var created = new GameObject(FrameObjectName);
            created.transform.SetParent(_source.transform, false);
            return created.AddComponent<SpriteRenderer>();
        }

        private static CharacterPlateSkinSO ResolveSkin()
        {
            if (_skinLoaded)
                return _skin;

            _skinLoaded = true;
            _skin = Resources.Load<CharacterPlateSkinSO>(SkinResourcePath);
            if (_skin == null)
                Debug.LogWarning($"{SkinResourcePath} 을 못 찾아 캐릭터 테두리를 건너뜁니다.");

            return _skin;
        }
    }
}
