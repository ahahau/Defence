using UnityEngine;

namespace Code.Combat
{
    /// <summary>
    /// 맞은 순간 캐릭터를 단색(흰색)으로 번쩍이게 한다. 원래 그림 위에 같은 그림을 실루엣 재질로 한 장 더 그리고 알파만 줄인다.
    /// 기본 스프라이트 셰이더는 색을 곱하므로 흰색으로 칠하면 아무 변화가 없고, 붉게 칠하면 붉은 피해 숫자와 섞였다.
    /// 그림이 프레임마다 바뀌므로 번쩍이는 동안만 원본 그림을 따라 바꾼다. <see cref="FeelCombatFeedbacks"/>가 붙인다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SpriteFlashOverlay : MonoBehaviour
    {
        private SpriteRenderer _source;
        private SpriteRenderer _overlay;
        private Color _color;
        private float _duration;
        private float _remaining;

        /// <summary>실루엣 재질을 받았으면 true. 없으면 부른 쪽이 예전 색 곱하기 점멸로 대신한다.</summary>
        public bool IsReady => _overlay != null || TryBuild();

        public void Bind(SpriteRenderer source) => _source = source;

        /// <summary>그 색으로 번쩍였다가 <paramref name="duration"/>초(실시간) 동안 사라진다. 일시정지·배속과 무관하게 같은 길이.</summary>
        public void Flash(Color color, float duration)
        {
            if (!IsReady)
                return;

            _color = color;
            _duration = Mathf.Max(0.01f, duration);
            _remaining = _duration;
            _overlay.enabled = true;
            SyncFromSource();
        }

        private void LateUpdate()
        {
            if (_overlay == null || !_overlay.enabled)
                return;

            _remaining -= Time.unscaledDeltaTime;
            if (_remaining <= 0f || _source == null || !_source.enabled)
            {
                _overlay.enabled = false;
                return;
            }

            SyncFromSource();
        }

        private void SyncFromSource()
        {
            if (_source == null)
                return;

            _overlay.sprite = _source.sprite;
            _overlay.flipX = _source.flipX;
            _overlay.flipY = _source.flipY;
            _overlay.sortingLayerID = _source.sortingLayerID;
            _overlay.sortingOrder = _source.sortingOrder + 1;

            // 처음엔 거의 불투명하게 덮고 빠르게 빠진다(제곱 감쇠).
            var t = Mathf.Clamp01(_remaining / _duration);
            var color = _color;
            color.a *= t * t * _source.color.a;
            _overlay.color = color;
        }

        private bool TryBuild()
        {
            var material = CombatFxHooks.Catalog != null ? CombatFxHooks.Catalog.HitFlashMaterial : null;
            if (material == null || _source == null)
                return false;

            var child = new GameObject("HitFlash");
            child.transform.SetParent(_source.transform, false);
            _overlay = child.AddComponent<SpriteRenderer>();
            _overlay.sharedMaterial = material;
            _overlay.enabled = false;
            return true;
        }
    }
}
