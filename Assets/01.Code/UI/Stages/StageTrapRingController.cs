using Code.Stages;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Code.UI.Stages
{
    /// <summary>
    /// 놓은 함정 둘레 메뉴의 판단: 누른 함정으로 열고, 오른쪽 클릭·다른 곳 클릭으로 닫고, 강화·판매·회전을 세션에 넘긴다.
    /// 함정이 사라지거나 함정을 다룰 수 없는 때가 되면 저절로 닫는다. StageHudView가 매 프레임 Tick을 부른다.
    /// </summary>
    public sealed class StageTrapRingController
    {
        private readonly StageSession _session;
        private readonly Camera _camera;
        private readonly StageTrapRing _ring;
        private Vector2Int? _cell;
        private Vector3 _world;
        private bool _bound;

        public bool IsValid => _ring != null && _ring.IsValid;

        public StageTrapRingController(StageSession session, Camera camera, StageTrapRing ring)
        {
            _session = session;
            _camera = camera;
            _ring = ring;
        }

        public void Bind()
        {
            if (!IsValid || _bound)
                return;
            _ring.UpgradeClicked += Upgrade;
            _ring.SellClicked += Sell;
            _ring.RotateClicked += Rotate;
            _bound = true;
        }

        public void Unbind()
        {
            if (_ring is null || !_bound)
                return;
            _ring.UpgradeClicked -= Upgrade;
            _ring.SellClicked -= Sell;
            _ring.RotateClicked -= Rotate;
            _bound = false;
        }

        /// <summary>
        /// 이 프레임 클릭을 메뉴가 썼으면 참(배치 도구·장치에 넘기지 않는다).
        /// 메뉴가 열린 채 다른 칸을 누르면 메뉴만 닫고, 함정·부하 도구를 들고 있으면 그 클릭으로 바로 놓게 넘긴다.
        /// </summary>
        public bool Tick(bool pointerFree, Vector2Int? focusedCell, StagePlacementTool tool)
        {
            if (!IsValid || Mouse.current == null)
                return false;
            if (_cell.HasValue && (!_session.EditsTrapsNow || !_session.Placement.TryDescribeTrap(_cell.Value, out _, out _)))
                Close();
            if (_cell.HasValue && Mouse.current.rightButton.wasPressedThisFrame)
            {
                Close();
                return true;
            }
            if (pointerFree && focusedCell.HasValue && Mouse.current.leftButton.wasPressedThisFrame)
            {
                Vector2Int cell = focusedCell.Value;
                if (!tool.Demolishing && _session.EditsTrapsNow && _session.Catalog.EditsTrapsInWaves
                    && _session.Placement.TryDescribeTrap(cell, out _, out _))
                {
                    Open(cell);
                    return true;
                }
                if (_cell.HasValue)
                {
                    Close();
                    return tool.Selected == null;
                }
            }
            if (_cell.HasValue)
                _ring.Place(_camera.WorldToScreenPoint(_world));
            return false;
        }

        /// <summary>금화·단계가 바뀌었을 수 있어 HUD 갱신 때마다 다시 그린다.</summary>
        public void Render()
        {
            if (!_cell.HasValue || !IsValid)
                return;
            Vector2Int cell = _cell.Value;
            if (!_session.Placement.TryDescribeTrap(cell, out int level, out int refund))
            {
                Close();
                return;
            }
            StageOption option = _session.Placement.TrapOptionAt(cell);
            StagePlacementCheck next = _session.Placement.CheckTrapLevel(cell);
            int maxLevel = _session.Catalog.TrapMaxLevel;
            bool top = level >= maxLevel;
            bool canSell = _session.Placement.CheckDemolition(cell).Allowed;
            bool canRotate = _session.Phase == StagePhase.Preparing && _session.Placement.WallTraps.At(cell) != null;
            _ring.Show($"{option?.Label} · Lv{level}/{maxLevel}", top ? "최고 단계" : $"강화 {next.Cost}G", next.Allowed,
                $"판매 +{refund}G", canSell, canRotate, next.Allowed || top ? string.Empty : next.Reason);
        }

        public void Close()
        {
            _cell = null;
            if (_ring != null)
                _ring.Hide();
        }

        private void Open(Vector2Int cell)
        {
            StagePlacementCheck area = _session.Placement.CheckTrapLevel(cell);
            _cell = cell;
            // 여러 칸 함정은 차지한 칸들의 가운데에 둔다
            _world = (_session.Board.World(area.Anchor) + _session.Board.World(area.Anchor + area.Footprint - Vector2Int.one)) * 0.5f;
            Render();
            _ring.Place(_camera.WorldToScreenPoint(_world));
        }

        private void Upgrade()
        {
            if (_cell.HasValue)
                _session.TryLevelTrap(_cell.Value);
            Render();
        }

        private void Sell()
        {
            if (_cell.HasValue)
                _session.Demolish(_cell.Value);
            Close();
        }

        private void Rotate()
        {
            if (_cell.HasValue)
                _session.TryRotateDevice(_cell.Value);
            Render();
        }
    }
}
