using Code.UI;
using TMPro;
using UnityEngine;

namespace Code.Stages
{
    public sealed class StageOptionRow : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private UiFrameButton pick;
        [SerializeField] private UiFrameButton upgrade;
        [SerializeField] private UiFrameButton remove;
        private StageHudView _view;
        private StageSession _session;
        private StageOption _option;

        public void Bind(StageHudView view, StageSession session, StageOption option)
        {
            _view = view;
            _session = session;
            _option = option;
            pick.Button.onClick.AddListener(Pick);
            upgrade.Button.onClick.AddListener(Upgrade);
            remove.Button.onClick.AddListener(Remove);
        }

        public void Refresh()
        {
            if (_option == null || _session.Profile == null)
                return;
            bool unlocked = _session.Profile.IsUnlocked(_option);
            bool selected = _session.IsSelected(_option);
            int level = _session.Profile.Level(_option.Id);
            label.text = unlocked ? $"{_option.Label} · {_option.Cost}G · 강화 {level} (+{Mathf.RoundToInt(level * _session.Catalog.DamagePerUpgrade * 100f)}%)" : $"{_option.Label} · {_session.Catalog.StageLabel(_option.RequiredClear)} 클리어 필요";
            pick.SetLabel(selected ? "배치 선택" : "편성 추가");
            remove.SetVisible(selected && _session.CanChoose);
            remove.SetInteractable(selected && _session.CanChoose);
            pick.SetSelected(selected);
            pick.SetInteractable(unlocked && (_session.CanChoose || selected && _session.Phase == StagePhase.Preparing));
            int cost = _session.Catalog.UpgradeBaseCost * (level + 1);
            upgrade.SetLabel(level >= _session.Catalog.UpgradeLimit ? "강화 완료" : $"강화 {cost}");
            upgrade.SetInteractable(unlocked && _session.CanUpgrade && level < _session.Catalog.UpgradeLimit
                && _session.Profile.Currency >= cost);
        }

        private void Pick()
        {
            if (_session.CanChoose && !_session.IsSelected(_option))
                _session.ToggleOption(_option);
            if (_session.IsSelected(_option))
                _view.Select(_option);
        }

        private void Remove()
        {
            if (_session.ToggleOption(_option))
                _view.Forget(_option);
        }

        private void Upgrade() => _session.TryUpgrade(_option);

        private void OnDestroy()
        {
            pick?.Button.onClick.RemoveListener(Pick);
            upgrade?.Button.onClick.RemoveListener(Upgrade);
            remove?.Button.onClick.RemoveListener(Remove);
        }
    }
}
