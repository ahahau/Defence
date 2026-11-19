using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Code.UI.Run
{
    /// <summary>
    /// 사람 플레이 기록(2026-11-17 사용자 결정: 항상 기록, 시간 + 판단, 저장 폴더 아래 playlogs).
    /// 시연 런 한 번(씬 한 번)마다 사건 목록(.jsonl)을 바로바로 덧붙이고, 끝나거나 씬을 떠날 때 사람이 읽는 요약(.txt)을 쓴다.
    /// 시간은 멈춤·배속과 무관한 실제 경과 시간이다. 쓰기에 실패해도 게임은 그대로 진행한다.
    /// </summary>
    public sealed class RunPlayLog : IDisposable
    {
        private const string FolderName = "playlogs";
        // 화면 글(정산 본문 등)을 그대로 받으면 TMP 서식 태그가 섞인다. 기록에는 글만 남긴다.
        private static readonly Regex RichTag = new(@"</?(nobr|color|b|i|u|s|size|mark|sup|sub|br|alpha|font|style)\b[^>]*>",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private readonly StreamWriter _events;
        private readonly string _summaryPath;
        // Unity 시간은 에디터에서 플레이를 끝내는 순간(OnDestroy) 다른 값을 돌려줘 자체 시계를 쓴다.
        private readonly System.Diagnostics.Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();
        private double _battleStart;
        private readonly string _startedAt;
        private readonly string _kind;
        private readonly StringBuilder _line = new();
        private readonly List<string> _phaseOrder = new();
        private readonly Dictionary<string, double> _phaseSeconds = new();
        private readonly StringBuilder _battles = new();
        private string _phase;
        private double _phaseStart;
        private string _result;
        private bool _disposed;

        // 전투(원정 포함) 하나 동안 세는 값.
        private int _pauses;
        private double _pausedSince = -1d;
        private double _pausedSeconds;
        private int _orders;
        private int _manaChanges;
        private int _traps;
        private int _speedChanges;

        private RunPlayLog(StreamWriter events, string summaryPath, string kind)
        {
            _events = events;
            _summaryPath = summaryPath;
            _kind = kind;
            _startedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        }

        /// <summary>새 기록을 연다. 폴더나 파일을 만들 수 없으면 null을 돌려준다(기록 없이 진행).</summary>
        public static RunPlayLog Open(string kind)
        {
            try
            {
                string folder = Path.Combine(Application.persistentDataPath, FolderName);
                Directory.CreateDirectory(folder);
                string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
                string basePath = Path.Combine(folder, "run-" + stamp);
                var writer = new StreamWriter(basePath + ".jsonl", false, new UTF8Encoding(false)) { AutoFlush = true };
                var log = new RunPlayLog(writer, basePath + ".txt", kind);
                log.Event("시작", kind);
                return log;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"플레이 기록을 열지 못해 기록 없이 진행합니다: {exception.Message}");
                return null;
            }
        }

        private double Now => _clock.Elapsed.TotalSeconds;

        /// <summary>지금 구간 이름.</summary>
        public string Phase => _phase;

        /// <summary>지금 구간 이름. 바뀌면 앞 구간 시간을 더하고 사건으로 남긴다.</summary>
        public void SetPhase(string phase, string state = null)
        {
            if (_disposed || phase == _phase) return;
            ClosePhase();
            _phase = phase;
            _phaseStart = Now;
            if (phase != null && !_phaseSeconds.ContainsKey(phase))
            {
                _phaseOrder.Add(phase);
                _phaseSeconds[phase] = 0d;
            }
            Event("구간", state == null ? phase : phase + " · " + state);
        }

        /// <summary>사건 한 줄. 전투 중 판단(명령·마력·함정·배속)은 kind로 세어 전투 요약에 쓴다.</summary>
        public void Event(string kind, string detail, float battleSeconds = -1f)
        {
            if (_disposed) return;
            switch (kind)
            {
                case "이동 명령": _orders++; break;
                case "마력": _manaChanges++; break;
                case "함정": _traps++; break;
                case "배속": _speedChanges++; break;
            }
            _line.Clear();
            _line.Append("{\"t\":").Append(Now.ToString("0.00", CultureInfo.InvariantCulture));
            if (battleSeconds >= 0f) _line.Append(",\"battle\":").Append(battleSeconds.ToString("0.00", CultureInfo.InvariantCulture));
            _line.Append(",\"phase\":"); AppendJson(_phase ?? string.Empty);
            _line.Append(",\"kind\":"); AppendJson(kind);
            _line.Append(",\"detail\":"); AppendJson(Plain(detail) ?? string.Empty);
            _line.Append('}');
            try { _events.WriteLine(_line.ToString()); }
            catch (Exception exception) { Debug.LogWarning($"플레이 기록 쓰기 실패: {exception.Message}"); }
        }

        /// <summary>멈춤을 켜거나 껐다. 멈춘 실제 시간을 전투마다 더한다.</summary>
        public void Pause(bool paused, float battleSeconds)
        {
            if (_disposed) return;
            if (paused && _pausedSince < 0d)
            {
                _pauses++;
                _pausedSince = Now;
                Event("멈춤", "켬", battleSeconds);
            }
            else if (!paused && _pausedSince >= 0d)
            {
                _pausedSeconds += Now - _pausedSince;
                _pausedSince = -1d;
                Event("멈춤", "끔", battleSeconds);
            }
        }

        /// <summary>전투 하나가 시작됐다. 전투 동안 세는 값을 비운다.</summary>
        public void BattleStarted(string name, string setup)
        {
            _pauses = _orders = _manaChanges = _traps = _speedChanges = 0;
            _pausedSince = -1d;
            _pausedSeconds = 0d;
            _battleStart = Now;
            Event("전투 시작", name + " · " + setup);
        }

        /// <summary>전투 하나가 끝났다. 요약 줄을 쌓는다.</summary>
        public void BattleEnded(string name, string outcome, float battleSeconds, string state)
        {
            if (_pausedSince >= 0d) Pause(false, battleSeconds);
            double realSeconds = Now - _battleStart;
            Event("전투 끝", $"{name} · {outcome} · {state}", battleSeconds);
            _battles.Append("- ").Append(name).Append(" · ").Append(outcome)
                .Append($" · 실제 {Clock(realSeconds)} · 게임 {battleSeconds:0.0}초")
                .Append($" · 멈춤 {_pauses}번 {Clock(_pausedSeconds)}")
                .Append($" · 이동 명령 {_orders} · 마력 바꿈 {_manaChanges} · 함정 {_traps} · 배속 바꿈 {_speedChanges}\n")
                .Append("  ").Append(Plain(state)).Append('\n');
        }

        /// <summary>런이 끝났다(정산). 요약을 쓴다.</summary>
        public void Finish(string result)
        {
            if (_disposed || _result != null) return;
            _result = Plain(result);
            Event("런 끝", _result);
            WriteSummary();
        }

        public void Dispose()
        {
            if (_disposed) return;
            if (_result == null)
            {
                Event("중단", "씬을 떠남");
                WriteSummary();
            }
            _disposed = true;
            try { _events.Dispose(); }
            catch (Exception exception) { Debug.LogWarning($"플레이 기록 닫기 실패: {exception.Message}"); }
        }

        /// <summary>지금까지 흐른 실제 시간을 구간 하나에 더한다.</summary>
        private void ClosePhase()
        {
            if (_phase == null) return;
            _phaseSeconds[_phase] += Now - _phaseStart;
            _phaseStart = Now;
        }

        private void WriteSummary()
        {
            ClosePhase();
            var text = new StringBuilder();
            text.Append("시연 런 플레이 기록\n");
            text.Append("시작 ").Append(_startedAt).Append(" · ").Append(_kind).Append('\n');
            text.Append("결과 ").Append(_result ?? "중단(정산 전에 씬을 떠남)").Append('\n');
            text.Append("전체 실제 시간 ").Append(Clock(Now)).Append("\n\n구간별 실제 시간\n");
            foreach (string phase in _phaseOrder) text.Append("- ").Append(phase).Append(' ').Append(Clock(_phaseSeconds[phase])).Append('\n');
            text.Append("\n전투\n").Append(_battles.Length > 0 ? _battles.ToString() : "- 없음\n");
            try { File.WriteAllText(_summaryPath, text.ToString(), new UTF8Encoding(false)); }
            catch (Exception exception) { Debug.LogWarning($"플레이 기록 요약 쓰기 실패: {exception.Message}"); }
        }

        private static string Plain(string text) => string.IsNullOrEmpty(text) || text.IndexOf('<') < 0 ? text : RichTag.Replace(text, string.Empty);

        private static string Clock(double seconds)
        {
            int total = Mathf.Max(0, Mathf.RoundToInt((float)seconds));
            return $"{total / 60}분 {total % 60:00}초";
        }

        private void AppendJson(string value)
        {
            _line.Append('"');
            foreach (char c in value)
            {
                switch (c)
                {
                    case '"': _line.Append("\\\""); break;
                    case '\\': _line.Append("\\\\"); break;
                    case '\n': _line.Append("\\n"); break;
                    case '\r': break;
                    default:
                        if (c < ' ') _line.Append(' ');
                        else _line.Append(c);
                        break;
                }
            }
            _line.Append('"');
        }
    }
}
