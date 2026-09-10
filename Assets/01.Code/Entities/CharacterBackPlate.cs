using UnityEngine;

namespace _01.Code.Entities
{
    /// <summary>
    /// 캐릭터 발밑에 까는 받침과, 그 바깥을 두르는 발광 링.
    ///
    /// 유닛과 적이 바닥 그림 위에 그냥 얹혀 있어서 어디에 서 있는 것인지 읽히지 않았다.
    /// 발밑에 받침을 하나 깔면 바닥에 붙어 선 것으로 보인다.
    ///
    /// 그리는 순서가 이 컴포넌트의 전부다:
    ///
    ///   발광 링 (등급 색, 받침보다 크게)  ← 받침 뒤. 크기 차이만큼 바깥으로 삐져나온다
    ///   받침
    ///   캐릭터
    ///
    /// 발광을 받침보다 앞에 두면 받침을 덮어 버리고, 같은 크기로 두면 받침에 완전히 가려
    /// 없는 것과 같다. 뒤에 두되 더 크게 만드는 것이 빛이 테두리로만 보이는 유일한 배치다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CharacterBackPlate : MonoBehaviour
    {
        private const string SkinResourcePath = "UI/CharacterPlateSkin";
        private const string PlateObjectName = "BackPlate";
        private const string GlowObjectName = "PlateGlow";

        private static CharacterPlateSkinSO _skin;
        private static bool _skinLoaded;

        private SpriteRenderer _source;
        private SpriteRenderer _plate;
        private SpriteRenderer _glow;

        /// <summary>발광 링의 색. 등급이 정해지면 적 쪽에서 넣어 준다.</summary>
        public void SetGlowColor(Color color)
        {
            if (_glow != null)
                _glow.color = color;
        }

        /// <summary>받침을 세운다. 캐릭터 그림이 정해진 뒤에 불러야 크기를 잴 수 있다.</summary>
        public void Initialize(SpriteRenderer source)
        {
            _source = source;
            var skin = ResolveSkin();

            if (_source == null || skin == null || skin.Plate == null)
            {
                enabled = false;
                return;
            }

            _glow = EnsureChild(GlowObjectName, ref _glow);
            _plate = EnsureChild(PlateObjectName, ref _plate);

            _glow.sprite = skin.Plate;
            _plate.sprite = skin.Plate;
            _plate.color = skin.PlateColor;

            Layout(skin);
        }

        /// <summary>캐릭터 그림이 바뀌면 받침 크기도 다시 잡는다.</summary>
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
                SetVisible(false);
                return;
            }

            SetVisible(true);

            // 받침은 캐릭터 그림의 밑변에 맞춘다. 가운데에 두면 공중에 뜬 고리가 된다.
            var bounds = _source.sprite.bounds;
            var width = bounds.size.x * skin.PlateWidthFactor;
            var plateSprite = _plate.sprite.bounds.size;
            var scaleX = plateSprite.x > 0f ? width / plateSprite.x : 1f;
            var scale = new Vector3(scaleX, scaleX * skin.PlateFlatten, 1f);
            var footY = bounds.min.y;

            _plate.transform.localScale = scale;
            _plate.transform.localPosition = new Vector3(0f, footY, 0f);

            _glow.transform.localScale = scale * skin.GlowScale;
            _glow.transform.localPosition = _plate.transform.localPosition;

            // 발광 → 받침 → 캐릭터 순으로 뒤에서부터 쌓는다.
            _glow.sortingLayerID = _source.sortingLayerID;
            _plate.sortingLayerID = _source.sortingLayerID;
            _glow.sortingOrder = _source.sortingOrder - 3;
            _plate.sortingOrder = _source.sortingOrder - 2;
        }

        private void SetVisible(bool visible)
        {
            if (_plate != null) _plate.enabled = visible;
            if (_glow != null) _glow.enabled = visible;
        }

        private SpriteRenderer EnsureChild(string childName, ref SpriteRenderer cached)
        {
            if (cached != null)
                return cached;

            var existing = _source.transform.Find(childName);
            if (existing != null && existing.TryGetComponent<SpriteRenderer>(out var found))
                return found;

            var created = new GameObject(childName);
            created.transform.SetParent(_source.transform, false);
            var renderer = created.AddComponent<SpriteRenderer>();
            renderer.color = Color.white;
            return renderer;
        }

        private static CharacterPlateSkinSO ResolveSkin()
        {
            if (_skinLoaded)
                return _skin;

            _skinLoaded = true;
            _skin = Resources.Load<CharacterPlateSkinSO>(SkinResourcePath);
            if (_skin == null)
                Debug.LogWarning($"{SkinResourcePath} 을 못 찾아 캐릭터 받침을 건너뜁니다.");

            return _skin;
        }
    }
}
