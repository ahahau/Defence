using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Code.UI.Toolkit
{
    /// <summary>
    /// HUD 우측 "최근 기록". 같은 알림, 또는 같은 종류(<c>groupKey</c>)의 알림이 이어지면 최신 문구 한 줄로 묶어 횟수를 붙인다.
    ///
    /// 묶지 않으면 "침입자가 늘었습니다" 같은 알림이 몇 초 만에 기록 칸을 다 밀어내,
    /// 정작 낮·밤 전환이나 관리 모드 같은 한 번뿐인 알림이 사라진다. 그 알림은 "현재 N명"처럼 숫자가
    /// 매번 달라 문구만으로는 묶이지 않아 종류를 따로 받는다(실측에서 1명·2명·3명이 세 줄로 쌓였다).
    /// </summary>
    public sealed class HudActivityLog
    {
        public const int MaxEntries = 8;

        private const string LineClass = "log-line";
        private const string AlertClass = "is-alert";

        private sealed class Entry
        {
            public string Message;
            public string GroupKey;
            public int Count;
            public bool Alert;
        }

        private readonly VisualElement _host;
        private readonly List<Entry> _entries = new();

        public HudActivityLog(VisualElement host)
        {
            _host = host;
        }

        /// <param name="groupKey">같은 종류의 알림을 묶는 키. 비우면 문구가 똑같을 때만 묶는다.</param>
        public void Add(string message, bool alert, string groupKey = null)
        {
            if (string.IsNullOrEmpty(message))
                return;

            var latest = _entries.Count > 0 ? _entries[_entries.Count - 1] : null;
            if (latest != null && IsSameKind(latest, message, groupKey))
            {
                // 최신 문구로 바꾼다 — "현재 3명"이 "현재 1명"보다 지금을 말한다.
                latest.Message = message;
                latest.Count++;
                latest.Alert |= alert;
            }
            else
            {
                if (_entries.Count >= MaxEntries)
                    _entries.RemoveAt(0);
                _entries.Add(new Entry { Message = message, GroupKey = groupKey, Count = 1, Alert = alert });
            }

            Render();
        }

        /// <summary>지금 보이는 줄의 글. 위에서 아래(오래된 것부터) 순서.</summary>
        public IEnumerable<string> Lines
        {
            get
            {
                foreach (var entry in _entries)
                    yield return Format(entry);
            }
        }

        private static bool IsSameKind(Entry latest, string message, string groupKey) =>
            string.IsNullOrEmpty(groupKey)
                ? string.IsNullOrEmpty(latest.GroupKey) && latest.Message == message
                : latest.GroupKey == groupKey;

        private static string Format(Entry entry) =>
            entry.Count > 1 ? $"{entry.Message} · {entry.Count}회" : entry.Message;

        private void Render()
        {
            if (_host == null)
                return;

            _host.Clear();
            foreach (var entry in _entries)
            {
                var line = new Label(Format(entry));
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
