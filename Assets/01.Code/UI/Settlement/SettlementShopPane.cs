using System.Collections.Generic;
using Code.Audio;
using Code.Manager;
using Code.Units;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Code.UI
{
    /// <summary>
    /// 지원자 목록 칸(정산표 "유닛 상점"·고용 창이 함께 쓴다). 이번 주 지원자를 한 줄씩 보여 주고, 누르면 고용한다.
    /// 시세가 싸면 초록, 비싸면 빨강. 유지비는 정가 기준이라 시세와 따로 적는다.
    /// 고용 판단·결제는 <see cref="HiredUnitRoster"/>가 한다.
    /// </summary>
    public sealed class SettlementShopPane : MonoBehaviour
    {
        [SerializeField] private HiredUnitRoster roster;
        [SerializeField] private CostManager costManager;
        [SerializeField] private ManagementSettlementManager settlement;
        [SerializeField] private TMP_Text status;
        [SerializeField] private TMP_Text empty;
        [SerializeField] private UiFrameButton[] slots;
        [SerializeField, Tooltip("슬롯마다 둘째 줄(특성·성격·떠나는 날·정가)")] private TMP_Text[] details;
        [SerializeField, Tooltip("슬롯마다 왼쪽 유닛 아이콘(대기 첫 프레임)")] private Image[] icons;
        [SerializeField] private TMP_Text feedback;
        [SerializeField] private ScrollRect scroll;

        private readonly List<UnitDataSO> _candidates = new();
        private UnityEngine.Events.UnityAction[] _handlers;

        /// <summary>고용하면 운영 자금이 바뀌므로 대출 칸도 다시 그리게 알린다.</summary>
        public event System.Action Hired;

        public void SetFeedback(string message) => feedback.text = message ?? string.Empty;

        public void Refresh()
        {
            if (roster == null)
                roster = HiredUnitRoster.Current;
            if (costManager == null)
                costManager = CostManager.Current;
            if (settlement == null)
                settlement = ManagementSettlementManager.Current;

            _candidates.Clear();
            if (roster == null)
            {
                status.text = "고용 명단을 불러오지 못했습니다.";
                HideFrom(0);
                empty.gameObject.SetActive(false);
                return;
            }

            status.text = $"고용 중인 부하 {roster.TotalHiredCount}명 · 영업 전 대기 중에 언제든 계약할 수 있습니다.";
            foreach (var unit in roster.UnitCatalog)
            {
                if (unit != null && roster.GetCandidateCount(unit) > 0)
                    _candidates.Add(unit);
            }

            var shown = Mathf.Min(_candidates.Count, slots.Length);
            for (var i = 0; i < shown; i++)
                Bind(i, _candidates[i]);
            HideFrom(shown);
            empty.gameObject.SetActive(_candidates.Count == 0);

            if (_candidates.Count > slots.Length)
                Debug.LogWarning($"{nameof(SettlementShopPane)}: 지원자 {_candidates.Count}명 중 {slots.Length}명만 그립니다.", this);
        }

        public void RewindScroll()
        {
            if (scroll != null)
                scroll.verticalNormalizedPosition = 1f;
        }

        private void EnsureRuntimeState()
        {
            if (_handlers != null)
                return;

            _handlers = new UnityEngine.Events.UnityAction[slots != null ? slots.Length : 0];
            for (var i = 0; i < _handlers.Length; i++)
            {
                var index = i;
                _handlers[i] = () => Hire(index);
            }
        }

        private void Awake()
        {
            EnsureRuntimeState();
        }

        private void OnEnable()
        {
            EnsureRuntimeState();
            for (var i = 0; i < _handlers.Length; i++)
                slots[i]?.Button.onClick.AddListener(_handlers[i]);
        }

        private void OnDisable()
        {
            for (var i = 0; _handlers != null && i < _handlers.Length; i++)
                slots[i]?.Button.onClick.RemoveListener(_handlers[i]);
        }

        private void Bind(int index, UnitDataSO unit)
        {
            var slot = slots[index];
            slot.SetVisible(true);

            var applicant = roster.PeekApplicant(unit);
            var daysLeft = roster.GetApplicantDaysLeft(unit);
            var price = roster.GetApplicantPrice(unit);
            var tier = roster.GetApplicantPriceTier(unit);
            var upkeep = settlement != null ? settlement.EstimateUnitUpkeep(unit) : 0;

            slot.SetLabel($"{unit.Name} · 고용 {GoldText.Amount(price)} ({UnitMarketRules.TierLabel(tier)})"
                          + $" · 유지비 {GoldText.Amount(upkeep)}/일 · 마력 {unit.MagicCost}");
            slot.SetLabelColor(tier switch
            {
                UnitPriceTier.Cheap => HudPalette.Gain,
                UnitPriceTier.Expensive => HudPalette.Loss,
                _ => HudPalette.TextBody
            });

            if (icons != null && index < icons.Length && icons[index] != null)
            {
                var icon = unit.IconSprite;
                icons[index].sprite = icon;
                icons[index].enabled = icon != null;
            }

            // 옛 화면은 정가·시세를 툴팁으로 보였다. 툴팁이 없어 둘째 줄 끝에 붙인다.
            if (index < details.Length && details[index] != null)
            {
                details[index].text = $"{applicant.TraitLabel} · {applicant.PersonalityLabel}"
                                      + (daysLeft > 0 ? $" · {daysLeft}일 후 떠남" : string.Empty)
                                      + $" · 정가 {GoldText.Amount(unit.Cost)}";
            }

            slot.SetInteractable(costManager != null && costManager.CurrentGold >= price);
        }

        private void HideFrom(int start)
        {
            for (var i = start; i < slots.Length; i++)
                slots[i]?.SetVisible(false);
        }

        private void Hire(int index)
        {
            if (roster == null || index >= _candidates.Count)
                return;

            var unit = _candidates[index];
            var price = roster.GetApplicantPrice(unit);
            var hired = roster.TryRequestHire(unit);
            SetFeedback(hired
                ? $"{unit.Name}을(를) {GoldText.Amount(price)}에 고용했습니다. 배치 화면에서 사용할 수 있습니다."
                : "고용하지 못했습니다. 지원자 수와 운영 자금을 확인하세요.");
            GameSfxPlayer.Play(hired ? GameSfxCue.UiConfirm : GameSfxCue.UiFail);
            Refresh();
            Hired?.Invoke();
        }
    }
}
