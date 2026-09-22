using Code.Enemies;
using Code.Events;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace Code.UI
{
    /// <summary>
    /// 적의 경계·탐욕·소비를 월드 위에 작게 보여 준다.
    /// WaveManager가 스폰 직후 월드 UI 프리팹을 붙인다.
    /// </summary>
    [DisallowMultipleComponent]
    public class EnemyMoodHud : MonoBehaviour
    {
        private const float BarWidth = 1.1f;
        private const float BarHeight = 0.075f;
        private const float MoodDisplayMaximum = 20f;

        private Enemy _enemy;
        [SerializeField] private Transform _visualRoot;
        [SerializeField] private Transform _fearFill;
        [SerializeField] private Transform _greedFill;
        [SerializeField] private TMP_Text _label;
        private Vector3 _baseScale;
        private Tween _pulseTween;

        public static EnemyMoodHud Attach(Enemy enemy)
        {
            if (enemy == null)
                return null;

            var hud = enemy.GetComponentInChildren<EnemyMoodHud>(true);
            if (hud == null)
            {
                var prefab = Resources.Load<EnemyMoodHud>("UI/EnemyMoodHud");
                if (prefab == null)
                {
                    Debug.LogError("Missing UI prefab: UI/EnemyMoodHud");
                    return null;
                }
                hud = Instantiate(prefab, enemy.transform, false);
            }

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
            _baseScale = _visualRoot != null ? _visualRoot.localScale : Vector3.one;
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
            var visitText = $"{_enemy.VisitPurposeLabel} {_enemy.RemainingBudget}G";
            _label.text = $"{traitText}{visitText}  ·  경 {_enemy.Fear}  탐 {_enemy.Greed}{retreatText}{spendingText}";
            _label.color = retreat >= 50
                ? new Color(1f, 0.42f, 0.34f, 1f)
                : new Color(1f, 0.91f, 0.7f, 1f);
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
