using System.Collections.Generic;
using _01.Code.Manager;
using _01.Code.Skills;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _01.Code.UI
{
    /// <summary>
    /// 습격 중에만 뜨는 권능 조작판.
    /// 권능을 고르면 겨냥 상태가 되고, 던전 구역을 클릭하면 그 자리에 쏟아진다.
    /// </summary>
    public sealed class DungeonPowerHudView : MonoBehaviour
    {
        [SerializeField] private DungeonPowerSystem powerSystem;
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Image powerFill;
        [SerializeField] private TMP_Text powerText;
        [SerializeField] private TMP_Text hintText;

        [SerializeField, Tooltip("권능 하나를 그릴 버튼 원본. 권능 수만큼 복제된다.")]
        private Button powerButtonTemplate;

        [SerializeField] private Transform powerButtonRoot;

        [SerializeField] private Color armedColor = new(0.85f, 0.65f, 0.25f, 1f);
        [SerializeField] private Color readyColor = new(0.20f, 0.13f, 0.07f, 1f);
        [SerializeField] private Color blockedColor = new(0.12f, 0.10f, 0.10f, 1f);

        private readonly List<Button> _buttons = new();
        private readonly List<DungeonPowerSO> _boundPowers = new();
        private float _hintClearTime;

        private void Awake()
        {
            powerSystem ??= DungeonPowerSystem.Current;
            BuildButtons();
            SetPanelVisible(false);
        }

        private void OnEnable()
        {
            powerSystem ??= DungeonPowerSystem.Current;
            if (powerSystem == null)
                return;

            powerSystem.DungeonPowerChanged += HandlePowerChanged;
            powerSystem.ArmedPowerChanged += HandleArmedChanged;
            powerSystem.CastAttempted += HandleCastAttempted;
        }

        private void OnDisable()
        {
            if (powerSystem == null)
                return;

            powerSystem.DungeonPowerChanged -= HandlePowerChanged;
            powerSystem.ArmedPowerChanged -= HandleArmedChanged;
            powerSystem.CastAttempted -= HandleCastAttempted;
        }

        private void BuildButtons()
        {
            if (powerSystem == null || powerButtonTemplate == null || powerButtonRoot == null)
                return;

            powerButtonTemplate.gameObject.SetActive(false);
            foreach (var power in powerSystem.Powers)
            {
                if (power == null)
                    continue;

                var button = Instantiate(powerButtonTemplate, powerButtonRoot);
                button.name = $"Power_{power.name}";
                button.gameObject.SetActive(true);
                var captured = power;
                button.onClick.AddListener(() => HandlePowerClicked(captured));
                _buttons.Add(button);
                _boundPowers.Add(power);
            }
        }

        private void HandlePowerClicked(DungeonPowerSO power)
        {
            if (powerSystem == null)
                return;

            if (!powerSystem.IsWaveRunning)
            {
                ShowHint("습격 중에만 쓸 수 있습니다");
                return;
            }

            if (!powerSystem.CanCast(power, null, out var reason) && !string.IsNullOrEmpty(reason)
                && reason != "겨냥할 구역을 고르세요")
            {
                ShowHint(reason);
                return;
            }

            powerSystem.Arm(power);
        }

        private void HandlePowerChanged(int current, int max)
        {
            RefreshBar(current, max);
            RefreshButtons();
        }

        private void HandleArmedChanged(DungeonPowerSO armed)
        {
            RefreshButtons();
            ShowHint(armed != null
                ? $"{armed.DisplayName} · 쏟아부을 구역을 클릭하세요"
                : string.Empty);
        }

        private void HandleCastAttempted(DungeonPowerSO power, bool success, string reason)
        {
            ShowHint(reason);
        }

        private void Update()
        {
            if (powerSystem == null)
                return;

            var day = DayManager.Current != null ? DayManager.Current.CurrentDay : 0;
            var running = powerSystem.IsWaveRunning && CoreLoopFeatureUnlocks.IsDungeonPowerUnlocked(day);
            SetPanelVisible(running);
            if (!running)
                return;

            // 쿨다운 숫자가 흐르므로 매 프레임 갱신한다. 버튼 수가 서너 개라 부담이 없다.
            RefreshBar(powerSystem.CurrentPower, powerSystem.MaxPower);
            RefreshButtons();

            if (_hintClearTime > 0f && Time.unscaledTime >= _hintClearTime)
            {
                _hintClearTime = 0f;
                if (hintText != null && powerSystem.ArmedPower == null)
                    hintText.text = string.Empty;
            }
        }

        private void RefreshBar(int current, int max)
        {
            if (powerFill != null)
                powerFill.fillAmount = max > 0 ? Mathf.Clamp01(current / (float)max) : 0f;
            if (powerText != null)
                powerText.text = $"권능 {current} / {max}";
        }

        private void RefreshButtons()
        {
            for (var i = 0; i < _buttons.Count && i < _boundPowers.Count; i++)
            {
                var button = _buttons[i];
                var power = _boundPowers[i];
                if (button == null || power == null)
                    continue;

                var cooldown = powerSystem.CooldownRemaining(power);
                var affordable = powerSystem.CurrentPower >= power.Cost;
                var usable = cooldown <= 0f && affordable;
                var armed = powerSystem.ArmedPower == power;

                var image = button.GetComponent<Image>();
                if (image != null)
                    image.color = armed ? armedColor : usable ? readyColor : blockedColor;

                var label = button.GetComponentInChildren<TMP_Text>(true);
                if (label == null)
                    continue;

                label.text = cooldown > 0f
                    ? $"{power.DisplayName}\n<size=75%>{cooldown:F1}초</size>"
                    : $"{power.DisplayName}\n<size=75%>{power.Cost}</size>";
            }
        }

        private void ShowHint(string message)
        {
            if (hintText == null)
                return;

            hintText.text = message ?? string.Empty;
            _hintClearTime = string.IsNullOrEmpty(message) ? 0f : Time.unscaledTime + 2.5f;
        }

        private void SetPanelVisible(bool visible)
        {
            if (panelRoot != null && panelRoot.activeSelf != visible)
                panelRoot.SetActive(visible);
        }
    }
}
