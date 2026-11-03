using Code.UI;
using Code.UI.Stages;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Code.Stages
{
    public sealed class StageOptionRow : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private UiFrameButton pick;
        [SerializeField] private UiFrameButton upgrade;
        [SerializeField] private UiFrameButton remove;
        private StageHudView _view;
        private StageSession _session;
        private StageOption _option;
        private PointerEventData _pointer;
        private string _baseline;

        internal string InspectionMessage => CanInteract && _pointer?.pointerEnter != null
            && _pointer.pointerEnter.transform.IsChildOf(transform)
            ? StageOptionDetailsPresenter.MessageFor(_session, _option, _baseline) : string.Empty;

        public void Bind(StageHudView view, StageSession session, StageOption option)
        {
            Unbind();
            _view = view;
            _session = session;
            _option = option;
            _baseline = StageOptionDetailsPresenter.BaseMessageFor(option, session.Catalog.UsesTrapControls);
            // 잠긴 후보도 이름 위에서 설명을 읽을 수 있어야 한다.
            if (label != null)
                label.raycastTarget = true;
            if (CanInteract)
                BindCallbacks();
        }

        /// <summary>이 행이 연결한 런타임 콜백만 해제한다. 재활성화용 대상은 유지한다.</summary>
        public void Unbind()
        {
            _pointer = null;
            if (pick != null && pick.Button != null)
                pick.Button.onClick.RemoveListener(Pick);
            if (upgrade != null && upgrade.Button != null)
                upgrade.Button.onClick.RemoveListener(Upgrade);
            if (remove != null && remove.Button != null)
                remove.Button.onClick.RemoveListener(Remove);
        }

        private void BindCallbacks()
        {
            Unbind();
            if (pick != null && pick.Button != null)
                pick.Button.onClick.AddListener(Pick);
            if (upgrade != null && upgrade.Button != null)
                upgrade.Button.onClick.AddListener(Upgrade);
            if (remove != null && remove.Button != null)
                remove.Button.onClick.AddListener(Remove);
        }

        private void OnEnable()
        {
            if (CanInteract)
                BindCallbacks();
        }

        private void OnDisable() => Unbind();
        private void OnDestroy() => Unbind();

        public void OnPointerEnter(PointerEventData eventData) => _pointer = eventData;
        public void OnPointerExit(PointerEventData eventData) => _pointer = null;

        private bool CanInteract => isActiveAndEnabled && _view != null && _view.isActiveAndEnabled
            && _session != null && _session.Profile != null && _session.Board != null && _option != null;

        public void Refresh()
        {
            if (_view == null || _session == null || _option == null || _session.Profile == null)
                return;
            bool unlocked = _session.Profile.IsUnlocked(_option);
            bool selected = _session.IsSelected(_option);
            bool activeTool = _view.IsPlacementSelected(_option);
            int level = _session.Profile.Level(_option.Id);
            label.text = unlocked ? $"{_option.Label} · {_option.Cost}G · 강화 {level} (+{Mathf.RoundToInt(level * _session.Catalog.DamagePerUpgrade * 100f)}%)" : $"{_option.Label} · {_session.Catalog.StageLabel(_option.RequiredClear)} 클리어 필요";
            pick.SetLabel(activeTool ? "배치 중" : selected ? "배치 선택" : "편성 추가");
            remove.SetVisible(selected && _session.CanChoose);
            remove.SetInteractable(selected && _session.CanChoose);
            pick.SetSelected(activeTool);
            // 함정 본편은 전투 중에도 편성한 함정을 골라 설치한다
            pick.SetInteractable(unlocked && (_session.CanChoose || selected && (_session.Phase == StagePhase.Preparing
                || _option.IsAnyTrap && _session.EditsTrapsNow)));
            int cost = _session.Catalog.UpgradeBaseCost * (level + 1);
            upgrade.SetLabel(level >= _session.Catalog.UpgradeLimit ? "강화 완료" : $"강화 {cost}");
            upgrade.SetInteractable(unlocked && _session.CanUpgrade && level < _session.Catalog.UpgradeLimit
                && _session.Profile.Currency >= cost);
        }

        private void Pick()
        {
            if (!CanInteract)
                return;
            if (_session.CanChoose && !_session.IsSelected(_option))
                _session.ToggleOption(_option);
            if (_session.IsSelected(_option))
                _view.Select(_option);
        }

        private void Remove()
        {
            if (!CanInteract)
                return;
            if (_session.ToggleOption(_option))
                _view.Forget(_option);
        }

        private void Upgrade()
        {
            if (CanInteract)
                _session.TryUpgrade(_option);
        }
    }
}
