using _01.Code.Enemies;
using _01.Code.Events;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace _01.Code.UI
{
    /// <summary>
    /// 적의 경계·탐욕·소비를 월드 위에 작게 보여 준다.
    /// 프리팹 배선을 요구하지 않도록 WaveManager가 스폰 직후 붙인다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyMoodHud : MonoBehaviour
    {
        private const float BarWidth = 1.1f;
        private const float BarHeight = 0.075f;
        private const float MoodDisplayMaximum = 20f;

        private static Sprite _solidSprite;

        private Enemy _enemy;
        private Transform _visualRoot;
        private Transform _fearFill;
        private Transform _greedFill;
        private TMP_Text _label;
        private Vector3 _baseScale;
        private Tween _pulseTween;

        public static EnemyMoodHud Attach(Enemy enemy)
        {
            if (enemy == null)
                return null;

            var hud = enemy.GetComponent<EnemyMoodHud>();
            if (hud == null)
                hud = enemy.gameObject.AddComponent<EnemyMoodHud>();

            hud.Bind(enemy);
            return hud;
        }

        private void Bind(Enemy enemy)
        {
            if (_enemy == enemy && _visualRoot != null)
            {
                Refresh();
                return;
            }

            Unbind();
            _enemy = enemy;
            EnsureVisuals();
            _enemy.MoodChanged += HandleMoodChanged;
            _enemy.FacilityGoldSpent += HandleFacilityGoldSpent;
            Refresh();
        }

        private void OnDestroy()
        {
            Unbind();
            _pulseTween?.Kill();
        }

        private void Unbind()
        {
            if (_enemy == null)
                return;

            _enemy.MoodChanged -= HandleMoodChanged;
            _enemy.FacilityGoldSpent -= HandleFacilityGoldSpent;
            _enemy = null;
        }

        private void HandleMoodChanged(Enemy enemy)
        {
            if (enemy == _enemy)
                Refresh();
        }

        private void HandleFacilityGoldSpent(Enemy enemy, int amount, int baseAmount, GoldChangeSource source)
        {
            if (enemy != _enemy)
                return;

            Refresh();
            if (_visualRoot == null)
                return;

            _pulseTween?.Kill(true);
            _visualRoot.localScale = _baseScale;
            _pulseTween = _visualRoot.DOPunchScale(_baseScale * 0.16f, 0.28f, 5, 0.45f)
                .SetUpdate(true)
                .SetLink(gameObject);
        }

        private void Refresh()
        {
            if (_enemy == null || _visualRoot == null)
                return;

            SetFill(_fearFill, Mathf.Clamp01(_enemy.Fear / MoodDisplayMaximum), 0f);
            SetFill(_greedFill, Mathf.Clamp01(_enemy.Greed / MoodDisplayMaximum), -0.12f);

            if (_label == null)
                return;

            var retreat = Mathf.RoundToInt(_enemy.RetreatChance * 100f);
            var retreatText = retreat > 0 ? $"  철수 {retreat}%" : string.Empty;
            var spendingText = _enemy.TotalFacilityGold > 0 ? $"  <color=#FFD05A>{_enemy.TotalFacilityGold}G</color>" : string.Empty;
            var traitText = string.IsNullOrWhiteSpace(_enemy.TraitLabel)
                ? string.Empty
                : $"[{_enemy.TraitLabel}] ";
            _label.text = $"{traitText}경 {_enemy.Fear}  탐 {_enemy.Greed}{retreatText}{spendingText}";
            _label.color = retreat >= 50
                ? new Color(1f, 0.42f, 0.34f, 1f)
                : new Color(1f, 0.91f, 0.7f, 1f);
        }

        private void EnsureVisuals()
        {
            if (_visualRoot != null)
                return;

            var root = new GameObject("MoodHud");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = new Vector3(0f, 0.92f, 0f);
            _visualRoot = root.transform;
            _baseScale = Vector3.one;

            CreateBar("FearBack", new Color(0.08f, 0.04f, 0.035f, 0.82f), 0f, 80);
            _fearFill = CreateBar("FearFill", new Color(0.92f, 0.22f, 0.16f, 1f), 0f, 81);
            CreateBar("GreedBack", new Color(0.08f, 0.04f, 0.035f, 0.82f), -0.12f, 80);
            _greedFill = CreateBar("GreedFill", new Color(1f, 0.68f, 0.12f, 1f), -0.12f, 81);

            var labelObject = new GameObject("MoodLabel");
            labelObject.transform.SetParent(_visualRoot, false);
            labelObject.transform.localPosition = new Vector3(0f, 0.17f, 0f);
            _label = labelObject.AddComponent<TextMeshPro>();
            _label.alignment = TextAlignmentOptions.Center;
            _label.fontSize = 1.35f;
            _label.fontStyle = FontStyles.Bold;
            _label.textWrappingMode = TextWrappingModes.NoWrap;
            _label.rectTransform.sizeDelta = new Vector2(4f, 0.45f);
            var labelRenderer = _label.GetComponent<Renderer>();
            if (labelRenderer != null)
                labelRenderer.sortingOrder = 82;
        }

        private Transform CreateBar(string objectName, Color color, float y, int sortingOrder)
        {
            var bar = new GameObject(objectName);
            bar.transform.SetParent(_visualRoot, false);
            bar.transform.localPosition = new Vector3(0f, y, 0f);
            bar.transform.localScale = new Vector3(BarWidth, BarHeight, 1f);

            var renderer = bar.AddComponent<SpriteRenderer>();
            renderer.sprite = SolidSprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            return bar.transform;
        }

        private static Sprite SolidSprite
        {
            get
            {
                if (_solidSprite == null)
                {
                    _solidSprite = Sprite.Create(
                        Texture2D.whiteTexture,
                        new Rect(0f, 0f, 1f, 1f),
                        new Vector2(0.5f, 0.5f),
                        1f);
                    _solidSprite.name = "EnemyMoodHudSolid";
                }

                return _solidSprite;
            }
        }

        private static void SetFill(Transform fill, float ratio, float y)
        {
            if (fill == null)
                return;

            var width = BarWidth * Mathf.Clamp01(ratio);
            fill.localScale = new Vector3(width, BarHeight, 1f);
            fill.localPosition = new Vector3(-BarWidth * 0.5f + width * 0.5f, y, 0f);
        }
    }
}
