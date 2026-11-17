using System;
using System.Collections.Generic;
using Code.Run;

namespace Code.UI.Run
{
    /// <summary>
    /// 원정 파견 판의 판단(2026-11-16 사용자 A안 승인): 어떤 부하를 고를 수 있는지, 누가 골라졌는지, 카드 문구.
    /// 확정하면 고른 부하 번호(명단 순서)를 <see cref="Confirmed"/>로 넘긴다. 진입 경로와 원정 시작은 세션 컨트롤러가 한다.
    /// </summary>
    internal sealed class RunRaidSelectPresenter
    {
        internal const int MaxRaiders = 3;

        private readonly RunRaidSelectView _view;
        private readonly List<int> _picked = new(MaxRaiders);
        private readonly System.Text.StringBuilder _text = new();
        private RunDemoCampaign _demo;
        private float _weakRatio;

        internal RunRaidSelectPresenter(RunRaidSelectView view)
        {
            _view = view;
            _view.CardClicked += HandleCard;
            _view.CloseClicked += HandleClose;
            _view.ConfirmClicked += HandleConfirm;
        }

        /// <summary>고른 부하로 확정했다. 목록은 명단 순서이고, 다음에 다시 열 때까지 바뀌지 않는다.</summary>
        internal event Action<IReadOnlyList<int>> Confirmed;

        internal bool IsOpen => _view.IsOpen;

        /// <param name="previous">진입 경로 창에서 "다시 선택"으로 돌아왔을 때 앞서 고른 부하. 처음이면 비어 있다.</param>
        internal void Show(RunDemoCampaign demo, float weakRatio, IReadOnlyList<int> previous)
        {
            _demo = demo;
            _weakRatio = weakRatio;
            _picked.Clear();
            foreach (int index in previous)
                if (IsAvailable(index) && _picked.Count < MaxRaiders) _picked.Add(index);
            Refresh();
            _view.Open();
        }

        internal void Hide() => _view.Hide();

        private bool IsAvailable(int index) => index >= 0 && index < _demo.MinionCount && _demo.Carryover.MinionHealth[index] > 0;

        private void Refresh()
        {
            _view.SetHeader($"골라 둔 부하 {_picked.Count}/{MaxRaiders}", "진지 대장 처치 · 남은 부하는 본진에 남습니다");
            for (int i = 0; i < _view.CardCount; i++)
            {
                if (i >= _demo.MinionCount)
                {
                    _view.SetCard(i, string.Empty, string.Empty, null, false, false);
                    continue;
                }
                RunCombatantSO data = _demo.Minion(i);
                int health = _demo.Carryover.MinionHealth[i];
                if (health <= 0)
                {
                    _view.SetCard(i, data.DisplayName, "쓰러짐\n고를 수 없음", RunMerchantPresenter.Portrait(data), false, false);
                    continue;
                }
                // 명단의 부하 데이터는 유물·강화가 이미 더해진 값이다.
                int max = data.MaxHealth;
                bool weak = health < max * _weakRatio;
                string healthLine = $"체력 {health}/{max}";
                if (weak) healthLine = HudPalette.Paint(healthLine, HudPalette.Warn);
                string reach = data.IsRanged ? "원거리" : "근접";
                string affinity = HudPalette.Paint(RunMerchantPresenter.AffinityName(data.Affinity), RunVisualPalette.Affinity(data.Affinity));
                string skill = data.Skill != null ? data.Skill.DisplayName : "없음";
                string body = $"{healthLine}\n{reach} · {affinity}\n큰 기술 · {skill}";
                if (weak) body += "\n" + HudPalette.Paint($"체력 {UnityEngine.Mathf.RoundToInt(_weakRatio * 100f)}% 아래", HudPalette.Warn);
                _view.SetCard(i, data.DisplayName, body, RunMerchantPresenter.Portrait(data), _picked.Contains(i), true);
            }

            _text.Clear();
            if (_picked.Count == 0) _text.Append("부하를 눌러 고르세요 · 최대 ").Append(MaxRaiders).Append("명");
            else
            {
                _text.Append("파견 · ");
                for (int i = 0; i < _picked.Count; i++)
                {
                    if (i > 0) _text.Append(", ");
                    _text.Append(_demo.Minion(_picked[i]).DisplayName);
                }
            }
            _view.SetFooter(_text.ToString(), "진입 경로 고르기", _picked.Count > 0);
        }

        // 고른 부하를 다시 누르면 뺀다. 세 명이 찼으면 다른 부하는 먼저 하나를 빼야 고를 수 있다.
        private void HandleCard(int index)
        {
            if (_demo == null || !IsAvailable(index)) return;
            if (!_picked.Remove(index))
            {
                if (_picked.Count >= MaxRaiders) return;
                _picked.Add(index);
                _picked.Sort();
            }
            Refresh();
        }

        private void HandleClose() => _view.Hide();

        private void HandleConfirm()
        {
            if (_demo == null || _picked.Count == 0) return;
            _view.Hide();
            Confirmed?.Invoke(_picked);
        }
    }
}
