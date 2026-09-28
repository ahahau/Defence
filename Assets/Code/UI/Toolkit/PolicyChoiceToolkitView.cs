using System.Collections.Generic;
using Code.Audio;
using Code.Manager;
using UnityEngine;
using UnityEngine.UIElements;

namespace Code.UI.Toolkit
{
    /// <summary>정책 선택의 상태는 관리자가 소유하고, 이 문서는 선택지를 읽어 표시만 한다.</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class PolicyChoiceToolkitView : MonoBehaviour
    {
        private VisualElement _root;
        private Label _title;
        private Label _morale;
        private VisualElement _list;
        private MoralePolicyManager _manager;
        private GameSpeedController _speed;
        private bool _suspended;
        private int _lastChoiceCount = -1;

        private void OnEnable()
        {
            _root = GetComponent<UIDocument>().rootVisualElement;
            _title = _root.Q<Label>("policy-title");
            _morale = _root.Q<Label>("policy-morale");
            _list = _root.Q<VisualElement>("policy-list");
            SetVisible(false);
        }

        private void OnDisable()
        {
            ReleasePause();
        }

        private void Update()
        {
            _manager ??= MoralePolicyManager.Current;
            _speed ??= GameSpeedController.Current;
            var choices = _manager?.CurrentChoices;
            var count = choices?.Count ?? 0;
            if (count == _lastChoiceCount)
                return;

            _lastChoiceCount = count;
            if (count == 0)
            {
                SetVisible(false);
                ReleasePause();
                return;
            }

            Present(choices);
        }

        private void Present(IReadOnlyList<PolicyDataSO> choices)
        {
            if (_title != null)
                _title.text = "운영 방침 선택 · 시간 정지";
            if (_morale != null)
                _morale.text = $"민심 {_manager.CurrentMorale}";
            if (_list != null)
            {
                _list.Clear();
                foreach (var item in choices)
                {
                    if (item == null)
                        continue;
                    var policy = item;
                    var button = new Button(() => Select(policy))
                    {
                        text = $"{policy.DisplayName}\n\n{policy.Description}\n\n{BuildEffectSummary(policy)}"
                    };
                    button.AddToClassList("policy-choice");
                    _list.Add(button);
                }
            }

            SetVisible(true);
            if (!_suspended)
            {
                _speed?.Suspend(this);
                _suspended = true;
            }
            GameSfxPlayer.Play(GameSfxCue.UiOpen);
        }

        private void Select(PolicyDataSO policy)
        {
            _manager?.SelectPolicy(policy);
            GameSfxPlayer.Play(GameSfxCue.UiConfirm);
        }

        private void ReleasePause()
        {
            if (!_suspended)
                return;
            _speed?.Release(this);
            _suspended = false;
        }

        private void SetVisible(bool visible)
        {
            if (_root != null)
                _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private static string BuildEffectSummary(PolicyDataSO policy)
        {
            var effects = new List<string>();
            if (policy.MoraleDeltaOnSelect != 0) effects.Add($"민심 {FormatSigned(policy.MoraleDeltaOnSelect)}");
            if (policy.GoldDeltaOnSelect != 0) effects.Add($"금화 {FormatSigned(policy.GoldDeltaOnSelect)}");
            if (policy.DurationDays > 0 && policy.DailyMoraleDelta != 0) effects.Add($"{policy.DurationDays}일간 민심 {FormatSigned(policy.DailyMoraleDelta)}/일");
            if (!Mathf.Approximately(policy.UnitDamageMultiplier, 1f)) effects.Add($"공격 {FormatPercent(policy.UnitDamageMultiplier)}");
            if (policy.UnitDefenseBonus != 0) effects.Add($"방어 {FormatSigned(policy.UnitDefenseBonus)}");
            return effects.Count > 0 ? string.Join(" · ", effects) : "즉시 효과 없음";
        }

        private static string FormatSigned(int value) => value > 0 ? $"+{value}" : value.ToString();
        private static string FormatPercent(float value) => $"{Mathf.RoundToInt((value - 1f) * 100f):+0;-0;0}%";
    }
}
