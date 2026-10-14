using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Code.UI.Toolkit
{
    /// <summary>
    /// HUD 우측 파티 목록 그리기. 줄을 미리 만들어 두고 글만 바꾼다 — 0.25초마다 지우고 다시 만들면
    /// 누르는 도중에 줄이 바뀌어 클릭이 사라진다. 파티 줄을 누르면 리더 상태 패널을 연다.
    /// </summary>
    public sealed class PartyRailView
    {
        public const int MaxRows = 8;

        private const string RowClass = "party-row";
        private const string NameClass = "party-name";
        private const string RiskClass = "party-risk";
        private const string ClickableClass = "is-clickable";
        private const string AlertClass = "is-alert";

        private readonly VisualElement _host;
        private readonly VisualElement[] _rows = new VisualElement[MaxRows];
        private readonly Label[] _names = new Label[MaxRows];
        private readonly Label[] _risks = new Label[MaxRows];
        private readonly PartyRailRowViewModel[] _models = new PartyRailRowViewModel[MaxRows];

        public PartyRailView(VisualElement host)
        {
            _host = host;
            if (_host == null)
                return;

            _host.Clear();
            for (var i = 0; i < MaxRows; i++)
            {
                var index = i;
                var row = new VisualElement();
                row.AddToClassList(RowClass);
                var nameLabel = new Label();
                nameLabel.AddToClassList(NameClass);
                var riskLabel = new Label();
                riskLabel.AddToClassList(RiskClass);
                row.Add(nameLabel);
                row.Add(riskLabel);
                row.RegisterCallback<ClickEvent>(_ => HandleRowClicked(index));
                row.style.display = DisplayStyle.None;
                _host.Add(row);

                _rows[i] = row;
                _names[i] = nameLabel;
                _risks[i] = riskLabel;
            }

            // 목록은 지도 위에 떠 있다. 줄을 누를 때 아래 방까지 눌리지 않게 한다.
            ToolkitPointerBlockers.Register(_host);
        }

        public void Dispose()
        {
            if (_host != null)
                ToolkitPointerBlockers.Unregister(_host);
        }

        public void Render(IReadOnlyList<PartyRailRowViewModel> rows)
        {
            if (_host == null)
                return;

            var count = Mathf.Min(rows.Count, MaxRows);
            for (var i = 0; i < MaxRows; i++)
            {
                var row = _rows[i];
                if (i >= count)
                {
                    _models[i] = default;
                    row.style.display = DisplayStyle.None;
                    continue;
                }

                var model = rows[i];
                _models[i] = model;
                row.style.display = DisplayStyle.Flex;
                SetText(_names[i], model.Name);
                SetText(_risks[i], model.Risk);
                row.EnableInClassList(ClickableClass, model.Target != null);
                row.EnableInClassList(AlertClass, model.Alert);
            }
        }

        private void HandleRowClicked(int index)
        {
            var target = _models[index].Target;
            if (target == null || !target.IsAlive)
                return;

            // 상태 패널은 화면 좌표(왼쪽 아래 원점) 옆에 뜬다. 줄의 왼쪽 아래를 기준으로 넘기면
            // 오른쪽 끝 목록이라도 화면 안으로 밀려 들어온다.
            var position = ToolkitScreenRect.TryGet(_rows[index], out var rect)
                ? new Vector2(rect.xMin, rect.yMin)
                : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            EnemyStatusToolkitView.Show(target, position);
        }

        private static void SetText(Label label, string text)
        {
            if (label.text != text)
                label.text = text;
        }
    }
}
