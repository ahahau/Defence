using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Code.UI.Toolkit
{
    /// <summary>
    /// HUD 우측 "최근 기록". 같은 알림이 이어지면 한 줄로 묶어 횟수를 붙인다.
    ///
    /// 묶지 않으면 "침입자가 늘었습니다" 같은 알림이 몇 초 만에 기록 다섯 칸을 다 밀어내,
    /// 정작 낮·밤 전환이나 관리 모드 같은 한 번뿐인 알림이 사라진다.
    /// </summary>
    public sealed class HudActivityLog
    {
        public const int MaxEntries = 8;

        private const string LineClass = "log-line";
        private const string AlertClass = "is-alert";

        private sealed class Entry
        {
            public string Message;
            public int Count;
            public bool Alert;
        }

        private readonly VisualElement _host;
        private readonly List<Entry> _entries = new();

        public HudActivityLog(VisualElement host)
        {
            _host = host;
        }

        public void Add(string message, bool alert)
        {
            if (string.IsNullOrEmpty(message))
                return;

            var latest = _entries.Count > 0 ? _entries[_entries.Count - 1] : null;
            if (latest != null && latest.Message == message)
            {
                latest.Count++;
                latest.Alert |= alert;
            }
            else
            {
                if (_entries.Count >= MaxEntries)
                    _entries.RemoveAt(0);
                _entries.Add(new Entry { Message = message, Count = 1, Alert = alert });
            }

            Render();
        }

        private void Render()
        {
            if (_host == null)
                return;

            _host.Clear();
            foreach (var entry in _entries)
            {
                var line = new Label(entry.Count > 1 ? $"{entry.Message} ×{entry.Count}" : entry.Message);
                line.AddToClassList(LineClass);
                if (entry.Alert)
                    line.AddToClassList(AlertClass);
                _host.Add(line);
            }

            // 새 줄이 아래에 붙으므로 끝까지 내려 보여 준다.
            if (_host is ScrollView scroll)
                scroll.schedule.Execute(() =>
                {
                    var content = scroll.contentContainer;
                    if (content.childCount > 0)
                        scroll.ScrollTo(content[content.childCount - 1]);
                });
        }
    }
}
