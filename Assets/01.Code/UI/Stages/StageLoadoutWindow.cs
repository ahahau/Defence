using System;
using Code.Stages;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace Code.UI.Stages
{
    /// <summary>
    /// 함정 본편 시작 전 편성 창: 모든 함정·부하 격자, 고른 후보의 설명·편성 추가/해제·영구 강화, 가져갈 칸, 출격.
    /// 편성 규칙은 StageSession(ToggleOption·TryUpgrade)이 그대로 정하고 이 창은 그리기와 입력 전달만 한다.
    /// </summary>
    public sealed class StageLoadoutWindow : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text title;
        [SerializeField] private StageLoadoutCell[] cells;
        [SerializeField] private TMP_Text detailName;
        [SerializeField] private TMP_Text detailBody;
        [SerializeField] private UiFrameButton toggle;
        [SerializeField] private UiFrameButton upgrade;
        [SerializeField] private StageHotbarSlot[] carrySlots;
        [SerializeField] private TMP_Text status;
        [SerializeField] private UiFrameButton start;

        private readonly StageOptionIcons _icons = new();
        private StageSession _session;
        private StageOption _focused;
        private StageOption[] _carryOptions;
        private string[] _baselines;
        private UnityAction[] _cellClicks;
        private UnityAction[] _carryClicks;

        public bool IsOpen => root != null && root.activeSelf;
        public bool IsValid => root != null && title != null && cells != null && detailName != null && detailBody != null
            && toggle != null && upgrade != null && carrySlots != null && status != null && start != null;

        /// <summary>편성에서 뺀 후보. 그 후보를 들고 있던 배치 도구를 HUD가 내려놓는다.</summary>
        public event Action<StageOption> OptionRemoved;

        public void Bind(StageSession session)
        {
            _session = session;
            _baselines = null;
            _focused = null;
            if (IsValid && session != null && session.Catalog != null && session.Catalog.Options.Length > cells.Length)
                Debug.LogWarning($"편성 창 칸({cells.Length})보다 후보({session.Catalog.Options.Length})가 많습니다. StageHudPolishBuilder를 다시 돌리세요", this);
        }

        public void Open()
        {
            if (!IsValid || _session == null || _session.Catalog == null)
                return;
            root.SetActive(true);
            if (_focused == null)
                _focused = FirstUnlocked();
            Render();
        }

        public void Close()
        {
            if (root != null)
                root.SetActive(false);
        }

        public void Render()
        {
            if (!IsOpen || _session == null || _session.Catalog == null || _session.Profile == null || _session.Definition == null)
                return;
            EnsureRuntimeState();
            StageOption[] options = _session.Catalog.Options;
            title.text = $"{_session.Definition.Title} · 편성";
            for (int i = 0; i < cells.Length; i++)
            {
                if (i >= options.Length || options[i] == null)
                {
                    cells[i].Hide();
                    continue;
                }
                StageOption option = options[i];
                bool unlocked = _session.Profile.IsUnlocked(option);
                string badge = unlocked ? $"{option.Cost}G" : $"{_session.Catalog.StageLabel(option.RequiredClear)} 클리어";
                cells[i].Show(_icons.For(option), option.Label, badge, unlocked, _session.IsSelected(option), option == _focused);
            }
            RenderDetail(options);
            RenderCarry(options);
            status.text = _session.Message ?? string.Empty;
        }

        private void OnEnable()
        {
            if (!IsValid)
                return;
            EnsureRuntimeState();
            for (int i = 0; i < cells.Length; i++)
                if (cells[i] != null)
                    cells[i].Button.onClick.AddListener(_cellClicks[i]);
            for (int i = 0; i < carrySlots.Length; i++)
                if (carrySlots[i] != null)
                    carrySlots[i].Button.onClick.AddListener(_carryClicks[i]);
            toggle.Button.onClick.AddListener(ToggleFocused);
            upgrade.Button.onClick.AddListener(UpgradeFocused);
            start.Button.onClick.AddListener(Close);
        }

        private void OnDisable()
        {
            if (!IsValid || _cellClicks == null || _carryClicks == null)
                return;
            for (int i = 0; i < cells.Length; i++)
                if (cells[i] != null)
                    cells[i].Button.onClick.RemoveListener(_cellClicks[i]);
            for (int i = 0; i < carrySlots.Length; i++)
                if (carrySlots[i] != null)
                    carrySlots[i].Button.onClick.RemoveListener(_carryClicks[i]);
            toggle.Button.onClick.RemoveListener(ToggleFocused);
            upgrade.Button.onClick.RemoveListener(UpgradeFocused);
            start.Button.onClick.RemoveListener(Close);
        }

        private void EnsureRuntimeState()
        {
            if (_cellClicks == null || _cellClicks.Length != cells.Length)
            {
                _cellClicks = new UnityAction[cells.Length];
                for (int i = 0; i < cells.Length; i++)
                {
                    int index = i;
                    _cellClicks[i] = () => Focus(index);
                }
            }
            if (_carryClicks == null || _carryClicks.Length != carrySlots.Length)
            {
                _carryOptions = new StageOption[carrySlots.Length];
                _carryClicks = new UnityAction[carrySlots.Length];
                for (int i = 0; i < carrySlots.Length; i++)
                {
                    int index = i;
                    _carryClicks[i] = () => RemoveCarried(index);
                }
            }
            if (_session?.Catalog != null && (_baselines == null || _baselines.Length != _session.Catalog.Options.Length))
            {
                _baselines = new string[_session.Catalog.Options.Length];
                for (int i = 0; i < _baselines.Length; i++)
                    _baselines[i] = StageOptionDetailsPresenter.BaseMessageFor(_session.Catalog.Options[i], _session.Catalog.UsesTrapControls);
            }
        }

        private void Focus(int index)
        {
            if (_session?.Catalog == null || index >= _session.Catalog.Options.Length)
                return;
            _focused = _session.Catalog.Options[index];
            Render();
        }

        private void ToggleFocused()
        {
            if (_session == null || _focused == null)
                return;
            bool wasSelected = _session.IsSelected(_focused);
            if (_session.ToggleOption(_focused) && wasSelected)
                OptionRemoved?.Invoke(_focused);
            Render();
        }

        private void UpgradeFocused()
        {
            if (_session != null && _focused != null)
                _session.TryUpgrade(_focused);
            Render();
        }

        private void RemoveCarried(int index)
        {
            StageOption option = _carryOptions != null && index < _carryOptions.Length ? _carryOptions[index] : null;
            if (_session == null || option == null)
                return;
            if (_session.ToggleOption(option))
                OptionRemoved?.Invoke(option);
            Render();
        }

        private void RenderDetail(StageOption[] options)
        {
            if (_focused == null)
            {
                detailName.text = string.Empty;
                detailBody.text = string.Empty;
                toggle.SetVisible(false);
                upgrade.SetVisible(false);
                return;
            }
            int index = Array.IndexOf(options, _focused);
            bool unlocked = _session.Profile.IsUnlocked(_focused);
            bool selected = _session.IsSelected(_focused);
            int level = _session.Profile.Level(_focused.Id);
            int bonus = Mathf.RoundToInt(level * _session.Catalog.DamagePerUpgrade * 100f);
            detailName.text = $"{_focused.Label}  {HudPalette.Paint($"{_focused.Cost}G", HudPalette.Accent)}";
            detailBody.text = (index >= 0 && _baselines != null ? _baselines[index] : string.Empty)
                + $"\n\n영구 강화 {level} · 피해 +{bonus}%"
                + (unlocked ? string.Empty : "\n" + HudPalette.Paint($"{_session.Catalog.StageLabel(_focused.RequiredClear)} 클리어 시 해금", HudPalette.Warn));
            toggle.SetVisible(true);
            toggle.SetLabel(selected ? "편성에서 빼기" : "편성에 넣기");
            toggle.SetInteractable(unlocked && _session.CanChoose);
            int cost = _session.Catalog.UpgradeBaseCost * (level + 1);
            upgrade.SetVisible(true);
            upgrade.SetLabel(level >= _session.Catalog.UpgradeLimit ? "강화 완료" : $"영구 강화 · 재화 {cost}");
            upgrade.SetInteractable(unlocked && _session.CanUpgrade && level < _session.Catalog.UpgradeLimit
                && _session.Profile.Currency >= cost);
        }

        /// <summary>가져갈 칸: 편성 슬롯 수만큼 보이고, 든 후보를 누르면 뺀다.</summary>
        private void RenderCarry(StageOption[] options)
        {
            int slots = Mathf.Min(_session.LoadoutSlots, carrySlots.Length);
            int used = 0;
            foreach (StageOption option in options)
            {
                if (option == null || !_session.IsSelected(option) || used >= slots)
                    continue;
                _carryOptions[used] = option;
                carrySlots[used].Show(_icons.For(option), StageOptionIcons.ShortName(option.Label), string.Empty, option.Cost, _session.CanChoose, true, false);
                used++;
            }
            for (int i = used; i < carrySlots.Length; i++)
            {
                _carryOptions[i] = null;
                if (i < slots)
                    carrySlots[i].Show(null, string.Empty, string.Empty, 0, false, true, false);
                else
                    carrySlots[i].Hide();
            }
        }

        private StageOption FirstUnlocked()
        {
            foreach (StageOption option in _session.Catalog.Options)
                if (option != null && _session.Profile.IsUnlocked(option))
                    return option;
            return null;
        }
    }
}
