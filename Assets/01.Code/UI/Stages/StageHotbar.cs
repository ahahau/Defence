using System;
using System.Collections.Generic;
using Code.Stages;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace Code.UI.Stages
{
    /// <summary>
    /// 함정 본편 아래 가운데 단축칸: 바닥 함정 / 벽 함정 / 도구(바리케이드·철거) 세 묶음과 왼쪽 아래 부하 묶음.
    /// 편성에 넣은 함정만 숫자키 1~8 순서로 놓고(부하는 키 없음), 금화가 모자라거나 지금 놓을 수 없으면 어둡게 한다.
    /// 판단은 StageHudView가 하고 이 뷰는 칸을 그리고 누른 것을 알린다.
    /// </summary>
    public sealed class StageHotbar : MonoBehaviour
    {
        private static readonly Key[] DigitKeys =
            { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8 };

        [SerializeField] private StageHotbarSlot[] floorSlots;
        [SerializeField] private StageHotbarSlot[] wallSlots;
        [SerializeField] private GameObject wallGroup;
        [SerializeField] private StageHotbarSlot barricadeSlot;
        [SerializeField] private StageHotbarSlot demolishSlot;
        [SerializeField] private Sprite barricadeIcon;
        [SerializeField] private StageHotbarSlot[] unitSlots;
        [SerializeField] private GameObject unitGroup;

        private enum Kind
        {
            Floor,
            Wall,
            Unit
        }

        private readonly List<StageOption> _keyed = new();
        private readonly StageOptionIcons _icons = new();
        private StageHotbarSlot[] _optionSlots;
        private StageOption[] _slotOptions;
        private UnityAction[] _optionClicks;
        private bool _trapsUsable;
        private bool _barricadeUsable;
        private bool _demolishUsable;

        public event Action<StageOption> OptionPicked;
        public event Action BarricadePicked;
        public event Action DemolishPicked;

        public bool IsValid => floorSlots != null && floorSlots.Length > 0 && wallSlots != null && wallSlots.Length > 0
            && wallGroup != null && barricadeSlot != null && demolishSlot != null
            && unitSlots != null && unitSlots.Length > 0 && unitGroup != null;

        public void Render(StageSession session, StagePlacementTool tool)
        {
            if (!IsValid || session == null || session.Catalog == null || tool == null)
                return;
            EnsureRuntimeState();
            bool preparing = session.Phase == StagePhase.Preparing;
            bool editing = session.EditsTrapsNow;
            _keyed.Clear();
            Fill(session, tool, floorSlots, 0, Kind.Floor, editing);
            bool hasWallTraps = false;
            foreach (StageOption option in session.Catalog.Options)
                hasWallTraps |= option != null && option.WallTrap != null;
            wallGroup.SetActive(hasWallTraps);
            if (hasWallTraps)
                Fill(session, tool, wallSlots, floorSlots.Length, Kind.Wall, editing);
            // 부하는 준비 단계에만 놓는다. 편성한 부하가 없으면 묶음을 숨긴다.
            bool hasUnits = false;
            foreach (StageOption option in session.Catalog.Options)
                hasUnits |= option != null && option.Unit != null && session.IsSelected(option);
            unitGroup.SetActive(hasUnits && preparing);
            if (hasUnits && preparing)
                Fill(session, tool, unitSlots, floorSlots.Length + wallSlots.Length, Kind.Unit, preparing);
            _trapsUsable = editing;
            _barricadeUsable = preparing;
            _demolishUsable = editing;
            int barricadeCost = session.Definition != null ? session.Definition.BarricadeCost : 0;
            barricadeSlot.Show(barricadeIcon, "바리", "Q", barricadeCost, _barricadeUsable, session.Gold >= barricadeCost,
                preparing && tool.IsSelected(null));
            demolishSlot.Show(null, "철거", "X", 0, _demolishUsable, true, editing && tool.Demolishing);
        }

        /// <summary>숫자키·Q·X. 창이 열려 있으면 HUD가 부르지 않는다.</summary>
        public void HandleKeys(Keyboard keyboard)
        {
            if (keyboard == null || !IsValid)
                return;
            // 칸이 어두운 때(결과 화면 등)는 숫자키도 받지 않는다
            for (int i = 0; _trapsUsable && i < _keyed.Count && i < DigitKeys.Length; i++)
            {
                if (keyboard[DigitKeys[i]].wasPressedThisFrame)
                {
                    OptionPicked?.Invoke(_keyed[i]);
                    return;
                }
            }
            if (keyboard.qKey.wasPressedThisFrame && _barricadeUsable)
                BarricadePicked?.Invoke();
            else if (keyboard.xKey.wasPressedThisFrame && _demolishUsable)
                DemolishPicked?.Invoke();
        }

        private void OnEnable()
        {
            if (!IsValid)
                return;
            EnsureRuntimeState();
            for (int i = 0; i < _optionSlots.Length; i++)
                if (_optionSlots[i] != null)
                    _optionSlots[i].Button.onClick.AddListener(_optionClicks[i]);
            barricadeSlot.Button.onClick.AddListener(PickBarricade);
            demolishSlot.Button.onClick.AddListener(PickDemolish);
        }

        private void OnDisable()
        {
            if (!IsValid || _optionSlots == null || _optionClicks == null)
                return;
            for (int i = 0; i < _optionSlots.Length; i++)
                if (_optionSlots[i] != null)
                    _optionSlots[i].Button.onClick.RemoveListener(_optionClicks[i]);
            barricadeSlot.Button.onClick.RemoveListener(PickBarricade);
            demolishSlot.Button.onClick.RemoveListener(PickDemolish);
        }

        private void PickBarricade()
        {
            if (_barricadeUsable)
                BarricadePicked?.Invoke();
        }

        private void PickDemolish()
        {
            if (_demolishUsable)
                DemolishPicked?.Invoke();
        }

        /// <summary>바닥 칸 뒤에 벽 칸을 잇는 한 줄 배열과, 칸마다 같은 인스턴스를 쓰는 클릭 콜백을 만든다.</summary>
        private void EnsureRuntimeState()
        {
            if (_optionSlots != null && _optionClicks != null && _slotOptions != null)
                return;
            _optionSlots = new StageHotbarSlot[floorSlots.Length + wallSlots.Length + unitSlots.Length];
            floorSlots.CopyTo(_optionSlots, 0);
            wallSlots.CopyTo(_optionSlots, floorSlots.Length);
            unitSlots.CopyTo(_optionSlots, floorSlots.Length + wallSlots.Length);
            _slotOptions = new StageOption[_optionSlots.Length];
            _optionClicks = new UnityAction[_optionSlots.Length];
            for (int i = 0; i < _optionSlots.Length; i++)
            {
                int index = i;
                _optionClicks[i] = () =>
                {
                    StageOption option = _slotOptions[index];
                    if (option != null)
                        OptionPicked?.Invoke(option);
                };
            }
        }

        /// <summary>그 종류의 편성 함정을 칸에 채운다. 하나도 없으면 첫 칸을 빈 자리로 남긴다.</summary>
        private void Fill(StageSession session, StagePlacementTool tool, StageHotbarSlot[] slots, int offset, Kind kind,
            bool usable)
        {
            int used = 0;
            foreach (StageOption option in session.Catalog.Options)
            {
                if (option == null || !session.IsSelected(option) || !Matches(option, kind))
                    continue;
                if (used >= slots.Length)
                    break;
                _slotOptions[offset + used] = option;
                string hotkey = string.Empty;
                if (kind != Kind.Unit)
                {
                    _keyed.Add(option);
                    hotkey = _keyed.Count.ToString();
                }
                slots[used].Show(_icons.For(option), StageOptionIcons.ShortName(option.Label), hotkey, option.Cost, usable,
                    session.Gold >= option.Cost, tool.IsSelected(option));
                used++;
            }
            for (int i = used; i < slots.Length; i++)
            {
                _slotOptions[offset + i] = null;
                if (i == 0)
                    slots[i].Show(null, string.Empty, string.Empty, 0, false, true, false);
                else
                    slots[i].Hide();
            }
        }

        private static bool Matches(StageOption option, Kind kind) => kind switch
        {
            Kind.Wall => option.WallTrap != null,
            Kind.Unit => option.Unit != null,
            _ => option.Trap != null
        };
    }
}
