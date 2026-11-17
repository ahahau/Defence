using Code.Stages;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Code.UI.Stages
{
    /// <summary>
    /// 전투 중 부하 초상(왼쪽 아래)·수동 장치 버튼(오른쪽 아래)의 판단. F로 고른 장치를 발동하고,
    /// 장치 버튼이나 맵의 장치를 누르면 그 장치를 고른다. 준비 단계에는 둘 다 숨긴다.
    /// </summary>
    public sealed class StageBattlePanels
    {
        private readonly StageSession _session;
        private readonly StageUnitPortraits _portraits;
        private readonly StageDeviceButtons _devices;
        private Vector2Int? _selected;
        private bool _bound;

        public StageBattlePanels(StageSession session, StageUnitPortraits portraits, StageDeviceButtons devices)
        {
            _session = session;
            _portraits = portraits;
            _devices = devices;
        }

        /// <summary>장치 버튼이 있으면 전투 중 오른쪽 아래(웨이브 시작 버튼 자리)를 쓴다.</summary>
        public bool HasDeviceButtons => _devices != null && _devices.IsValid;

        public void Bind()
        {
            if (!HasDeviceButtons || _bound)
                return;
            _devices.Picked += Trigger;
            _bound = true;
        }

        public void Unbind()
        {
            if (_devices is null || !_bound)
                return;
            _devices.Picked -= Trigger;
            _bound = false;
        }

        /// <param name="acceptKeys">창이 열려 있으면 거짓(F를 받지 않는다).</param>
        public void Tick(bool fighting, bool acceptKeys)
        {
            if (_portraits != null && _portraits.IsValid)
            {
                if (fighting)
                    _portraits.Render(_session);
                else
                    _portraits.Hide();
            }
            if (!HasDeviceButtons)
                return;
            if (!fighting || _session.Devices == null)
            {
                _devices.Hide();
                return;
            }
            if (!_selected.HasValue || _session.Devices.At(_selected.Value)?.Device.IsManual != true)
                _selected = FirstManual();
            _devices.Render(_session.Devices.States, _selected);
            if (_selected.HasValue && acceptKeys && Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
                _session.TryTriggerDevice(_selected.Value);
        }

        /// <summary>맵에서 누른 칸이 수동 장치면 F로 다시 쓸 장치로 고른다.</summary>
        public void SelectAt(Vector2Int cell)
        {
            if (_session.Devices?.At(cell)?.Device.IsManual == true)
                _selected = _session.Devices.At(cell).Device.Cell;
        }

        private Vector2Int? FirstManual()
        {
            foreach (StageDevices.State state in _session.Devices.States)
                if (state != null && state.Device.IsManual)
                    return state.Device.Cell;
            return null;
        }

        /// <summary>장치 버튼: 그 장치를 고르고 바로 발동한다.</summary>
        private void Trigger(Vector2Int cell)
        {
            _selected = cell;
            _session.TryTriggerDevice(cell);
        }
    }
}
