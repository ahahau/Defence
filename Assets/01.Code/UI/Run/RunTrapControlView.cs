using System;
using System.Globalization;
using Code.Run;
using TMPro;
using UnityEngine;

namespace Code.UI.Run
{
    /// <summary>수동 함정의 준비 상태와 발동 버튼. 충전·피해 판정은 전투가 맡는다.</summary>
    public class RunTrapControlView : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text status;
        [SerializeField] private TMP_Text seconds;
        [SerializeField] private TMP_Text damage;
        [SerializeField] private UnityEngine.UI.Image charge;
        [SerializeField] private UiFrameButton activateButton;
        private UnityEngine.Events.UnityAction _activate;
        private RunBattle _battle;
        private int _room = -1;
        private string[] _tenths;
        private int _shownTenth = -1;
        private int _shownDamage = -1;
        private int _shownState = -1;
        public event Action Activated;

        private void OnEnable()
        {
            _activate ??= () => Activated?.Invoke();
            if (activateButton != null) activateButton.Button.onClick.AddListener(_activate);
        }

        private void OnDisable()
        {
            if (activateButton != null && _activate != null) activateButton.Button.onClick.RemoveListener(_activate);
        }

        public void Bind(RunBattle battle, int room)
        {
            _battle = battle;
            _room = room;
            int count = Mathf.CeilToInt(battle.Settings.TrapCooldownSeconds * 10f) + 1;
            _tenths = new string[count];
            for (int i = 0; i < count; i++) _tenths[i] = (i * 0.1f).ToString("0.0", CultureInfo.InvariantCulture) + "초";
            _shownState = _shownDamage = _shownTenth = -1;
        }

        public void Render(bool visible, bool queued, bool cancelled)
        {
            visible &= _battle != null && _room >= 0;
            if (root.activeSelf != visible) root.SetActive(visible);
            if (!visible) return;
            RunRoom room = _battle.Rooms[_room];
            bool ready = _battle.CanActivateTrap(_room);
            int state = queued ? 0 : cancelled ? 1 : room.IsBroken ? 2 : !_battle.IsConnected(_room) ? 3
                : room.Mana == 0 ? 4 : room.TrapCooldown > 0.0005f ? 5 : ready ? 6 : 7;
            if (_shownState != state)
            {
                _shownState = state;
                status.SetText(state switch
                {
                    0 => "재개하면 발동 조건을 확인합니다",
                    1 => "예약 취소 · 전력과 방 안 적을 확인하세요",
                    2 => "고장 · 수리할 부하를 보내세요",
                    3 => "마력 연결 끊김 · 충전 보존",
                    4 => "마력 없음 · 충전 보존",
                    5 => "전력으로 충전 중",
                    6 => "준비 완료 · 발동 시점을 고르세요",
                    _ => "준비 완료 · 방 안 적 없음",
                });
                activateButton.SetLabel(queued ? "발동 예약됨" : "함정 발동");
            }
            activateButton.Button.interactable = ready && !queued;
            int tenth = Mathf.Clamp(Mathf.CeilToInt(room.TrapCooldown * 10f), 0, _tenths.Length - 1);
            if (tenth != _shownTenth) { _shownTenth = tenth; seconds.SetText(_tenths[tenth]); }
            int value = _battle.TrapDamage(_room);
            int stage = _battle.TrapStage(_room);
            // 다 찬 뒤에도 모으면 단계가 올라 피해가 커진다(2026-11-10 사용자 결정). 단계도 함께 보인다.
            int shown = value * 10 + stage;
            if (shown != _shownDamage)
            {
                _shownDamage = shown;
                damage.SetText("피해 {0} · 단계 {1}/{2} · 방 안 적 전원", value, stage, _battle.Settings.TrapMaxStages);
            }
            float duration = _battle.Settings.TrapCooldownSeconds;
            charge.fillAmount = duration > 0f ? Mathf.Clamp01(1f - room.TrapCooldown / duration) : 1f;
        }
    }
}
