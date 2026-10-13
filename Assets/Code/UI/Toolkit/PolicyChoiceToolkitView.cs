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
        /// <summary>지금 씬의 운영 방침 창. 튜토리얼·패배 화면이 찾는다.</summary>
        public static PolicyChoiceToolkitView Current { get; private set; }

        /// <summary>선택지가 떠 있는가.</summary>
        public bool IsOpen => _root != null && _root.style.display == DisplayStyle.Flex;

        /// <summary>
        /// 첫 선택지 버튼의 화면 사각형(왼쪽 아래 원점 픽셀). 튜토리얼이 강조할 곳이다. 아직 배치 전이면 false.
        /// </summary>
        public bool TryGetFirstChoiceScreenRect(out Rect rect)
        {
            rect = default;
            if (!IsOpen || _list == null || _list.childCount == 0 || _root.panel == null)
                return false;

            var panel = _root.panel.visualTree.worldBound;
            var bound = _list[0].worldBound;
            if (panel.width <= 1f || panel.height <= 1f || float.IsNaN(bound.width) || bound.width <= 1f)
                return false;

            // 패널 좌표는 왼쪽 위 원점이고 화면 크기에 맞춰 늘어난다. 비율로 화면 픽셀에 옮긴다(팝업과 같은 방식).
            var scaleX = Screen.width / panel.width;
            var scaleY = Screen.height / panel.height;
            rect = new Rect(bound.x * scaleX, Screen.height - bound.yMax * scaleY, bound.width * scaleX, bound.height * scaleY);
            return true;
        }

        /// <summary>판이 끝나면 선택지를 치우고 멈춘 시간을 푼다.</summary>
        public void CloseForRunEnd()
        {
            SetVisible(false);
            ReleasePause();
        }

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
            Current = this;
            // USS가 policy-root를 기본으로 숨겨 둔다. 문서 루트를 켜도 창은 보이지 않고 시간만 멈춰
            // 판이 굳었다. 실제로 켜고 끄는 것은 policy-root다.
            var tree = GetComponent<UIDocument>().rootVisualElement;
            _root = tree.Q<VisualElement>("policy-root") ?? tree;
            _title = _root.Q<Label>("policy-title");
            _morale = _root.Q<Label>("policy-morale");
            _list = _root.Q<VisualElement>("policy-list");
            SetVisible(false);
        }

        private void OnDisable()
        {
            if (Current == this)
                Current = null;
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
            // 튜토리얼이 정한 선택지만 고를 수 있다(옛 uGUI 창과 같은 규칙).
            var index = _manager != null && _manager.CurrentChoices != null ? IndexOf(_manager.CurrentChoices, policy) : -1;
            if (!Code.Tutorial.TutorialInputGate.AllowsPolicyChoice(index))
                return;

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

        /// <summary>
        /// 효과 요약. 즉시 효과(민심·금화)는 늘 보이고, 지속 효과(일별 민심·공격·방어)는 한 번 골라 본
        /// 방침만 보인다. 처음 고를 때는 "? 지속 효과"로만 알린다(기획서: 처음 보는 선택지는 결과 일부를 숨김).
        /// </summary>
        private string BuildEffectSummary(PolicyDataSO policy)
        {
            var known = _manager != null && _manager.Events.HasChosen(RunEventLedger.PolicyKey(policy));
            var effects = new List<string>();
            if (policy.MoraleDeltaOnSelect != 0) effects.Add($"민심 {FormatSigned(policy.MoraleDeltaOnSelect)}");
            if (policy.GoldDeltaOnSelect != 0) effects.Add($"금화 {FormatSigned(policy.GoldDeltaOnSelect)}");

            var hasOngoing = policy.DurationDays > 0 && policy.DailyMoraleDelta != 0 || policy.HasCombatEffect;
            if (hasOngoing && !known)
            {
                effects.Add("? 지속 효과");
            }
            else
            {
                if (policy.DurationDays > 0 && policy.DailyMoraleDelta != 0) effects.Add($"{policy.DurationDays}일간 민심 {FormatSigned(policy.DailyMoraleDelta)}/일");
                if (!Mathf.Approximately(policy.UnitDamageMultiplier, 1f)) effects.Add($"공격 {FormatPercent(policy.UnitDamageMultiplier)}");
                if (policy.UnitDefenseBonus != 0) effects.Add($"방어 {FormatSigned(policy.UnitDefenseBonus)}");
            }

            var summary = effects.Count > 0 ? string.Join(" · ", effects) : "즉시 효과 없음";
            return known ? summary + " · 확인함" : summary;
        }

        private static int IndexOf(IReadOnlyList<PolicyDataSO> choices, PolicyDataSO policy)
        {
            for (var i = 0; i < choices.Count; i++)
            {
                if (choices[i] == policy)
                    return i;
            }

            return -1;
        }

        private static string FormatSigned(int value) => value > 0 ? $"+{value}" : value.ToString();
        private static string FormatPercent(float value) => $"{Mathf.RoundToInt((value - 1f) * 100f):+0;-0;0}%";
    }
}
