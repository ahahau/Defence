using _01.Code.Core;
using _01.Code.Events;
using _01.Code.Units;
using UnityEngine;

namespace _01.Code.UI
{
    [RequireComponent(typeof(ClockPanelUI))]
    public class ClockPanelStatusController : MonoBehaviour
    {
        [SerializeField] private float transientMessageDuration = 2.25f;
        [SerializeField] private _01.Code.Manager.BuildManager buildManager;

        private ClockPanelUI _clockPanelUI;
        private GameEventChannelSO _uiEventChannel;
        private bool _isDay = true;
        private string _transientMessage;
        private float _transientMessageUntil;
        private bool _buildManagerHooked;

        private void Awake()
        {
            _clockPanelUI = GetComponent<ClockPanelUI>();
            _uiEventChannel = _clockPanelUI.UiEventChannel;
        }

        private void OnEnable()
        {
            _uiEventChannel.AddListener<UiClockStateChangedEvent>(HandleClockStateChanged);

            TryHookBuildManager();
            RefreshStatusMessage();
        }

        private void OnDisable()
        {
            _uiEventChannel.RemoveListener<UiClockStateChangedEvent>(HandleClockStateChanged);

            UnhookBuildManager();
        }

        private void Update()
        {
            if (!_buildManagerHooked)
            {
                TryHookBuildManager();
            }

            if (!string.IsNullOrEmpty(_transientMessage) && Time.unscaledTime >= _transientMessageUntil)
            {
                _transientMessage = null;
                RefreshStatusMessage();
            }
        }

        private void HandleClockStateChanged(UiClockStateChangedEvent evt)
        {
            _isDay = evt.IsDay;
            RefreshStatusMessage();
        }

        private void HandleBuildingInstalled(UnitDataSO unitData, _01.Code.Entities.PlaceableEntity _)
        {
            ShowTransientMessage(GetBuildCompletedMessage(unitData));
        }

        private void HandleBuildFailed(UnitDataSO unitData, Vector2Int buildPosition)
        {
            ShowTransientMessage(GetBuildFailedMessage(unitData, buildPosition));
        }

        private void HandleBuildingMoved()
        {
            ShowTransientMessage("유닛 위치 이동 완료");
        }

        private void HandleBuildingMoveFailed()
        {
            ShowTransientMessage("이동 실패 · 빈 칸을 선택하세요");
        }

        private void TryHookBuildManager()
        {
            if (_buildManagerHooked || buildManager == null)
            {
                return;
            }

            buildManager.OnBuildingInstalled += HandleBuildingInstalled;
            buildManager.OnBuildFailed += HandleBuildFailed;
            buildManager.OnBuildingMoved += HandleBuildingMoved;
            buildManager.OnBuildingMoveFailed += HandleBuildingMoveFailed;
            _buildManagerHooked = true;
        }

        private void UnhookBuildManager()
        {
            if (!_buildManagerHooked || buildManager == null)
            {
                return;
            }

            buildManager.OnBuildingInstalled -= HandleBuildingInstalled;
            buildManager.OnBuildFailed -= HandleBuildFailed;
            buildManager.OnBuildingMoved -= HandleBuildingMoved;
            buildManager.OnBuildingMoveFailed -= HandleBuildingMoveFailed;
            _buildManagerHooked = false;
        }

        private void ShowTransientMessage(string message)
        {
            _transientMessage = message;
            _transientMessageUntil = Time.unscaledTime + transientMessageDuration;
            RefreshStatusMessage();
        }

        private void RefreshStatusMessage()
        {
            string message = !string.IsNullOrEmpty(_transientMessage) && Time.unscaledTime < _transientMessageUntil
                ? _transientMessage
                : GetClockStatusMessage();
        }

        private string GetClockStatusMessage()
        {
            if (!_isDay)
            {
                return "밤 · 배치 비활성";
            }

            return "낮 · 진행 중";
        }

        private string GetBuildCompletedMessage(UnitDataSO unitData)
        {
            return unitData == null ? "배치 완료" : $"{unitData.Name} 배치 완료";
        }

        private string GetBuildFailedMessage(UnitDataSO unitData, Vector2Int buildPosition)
        {
            string unitName = unitData == null ? "유닛" : unitData.Name;
            return $"{unitName} 배치 실패 · {buildPosition.x}, {buildPosition.y}";
        }
    }
}
