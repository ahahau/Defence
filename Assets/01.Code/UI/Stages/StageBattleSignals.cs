using System.Collections.Generic;
using Code.Stages;
using UnityEngine;

namespace Code.UI.Stages
{
    /// <summary>전투 결과를 바꾸지 않는 코어/파괴 위치 강조와 복구 대기 표시.</summary>
    public sealed class StageBattleSignals
    {
        private const float CorePulseSeconds = 0.45f;
        private const float CoreAlertSeconds = 1.2f;
        private const float BreakSeconds = 0.65f;
        private const int BreakSlots = 8;
        private readonly StageSession _session;
        private readonly SpriteRenderer _template;
        private readonly Vector3 _unitScale;
        private readonly Transform _root;
        private readonly SpriteRenderer _core;
        private readonly SpriteRenderer[] _breaks = new SpriteRenderer[BreakSlots * 2];
        private readonly float[] _breakUntil = new float[BreakSlots];
        private readonly Vector3[] _breakPositions = new Vector3[BreakSlots];
        private readonly List<SpriteRenderer> _waiting = new();
        private StageBoard _board;
        private float _corePulseUntil;
        private float _coreAlertUntil;
        private float _breakAlertUntil;
        private int _coreDamage;
        private int _nextBreak;
        private string _coreMessage;
        public int WaitingCount { get; private set; }
        public bool IsValid => _root != null && _core != null;
        public string Alert => Time.time < _coreAlertUntil ? _coreMessage
            : Time.time < _breakAlertUntil ? "바리케이드 파괴 · 해당 칸의 길이 열렸습니다" : string.Empty;

        public StageBattleSignals(StageSession session, SpriteRenderer template, Vector3 unitScale, Transform parent = null)
        {
            _session = session;
            _template = template;
            _unitScale = unitScale;
            _root = new GameObject("Battle Signals").transform;
            _root.SetParent(parent != null ? parent : session.transform, false);
            _core = Create("Core Hit", 51);
            for (int i = 0; i < _breaks.Length; i++)
                _breaks[i] = Create("Barricade Broken", 49);
        }

        public string CoreLabel()
        {
            int current = _session.CoreHealth;
            int maximum = _session.Definition.CoreHealth;
            string text = $"코어 {current}/{maximum}";
            bool low = current <= maximum * 0.3f;
            if (current <= 0)
                text += " [파괴]";
            else if (low)
                text += " [위험]";
            return HudPalette.Paint(text, low ? HudPalette.Danger : HudPalette.TextBody);
        }

        public void CoreDamaged(int amount)
        {
            SyncBoard();
            if (_board == null || amount <= 0)
                return;
            if (Time.time >= _coreAlertUntil)
                _coreDamage = 0;
            _coreDamage += amount;
            _coreMessage = $"코어 피격 -{_coreDamage}";
            _coreAlertUntil = Time.time + CoreAlertSeconds;
            _corePulseUntil = Time.time + CorePulseSeconds;
        }

        public void BarricadeDestroyed(Vector2Int cell)
        {
            SyncBoard();
            if (_board == null || (_session.Phase != StagePhase.Preparing && _session.Phase != StagePhase.Fighting))
                return;
            _breakPositions[_nextBreak] = _board.World(cell);
            _breakUntil[_nextBreak] = Time.time + BreakSeconds;
            _nextBreak = (_nextBreak + 1) % BreakSlots;
            _breakAlertUntil = Time.time + 0.9f;
        }

        public void Tick()
        {
            if (_root == null)
                return;
            SyncBoard();
            if (_board == null || _session.Phase == StagePhase.Unavailable)
            {
                Hide();
                return;
            }
            float size = _session.Definition.CellSize;
            float pulse = Mathf.Clamp01((_corePulseUntil - Time.time) / CorePulseSeconds);
            Place(_core, _board.World(_session.Definition.Core), size, 1.3f + (1f - pulse) * 0.25f,
                1.3f + (1f - pulse) * 0.25f, 0, HudPalette.Danger, pulse * 0.45f);
            for (int i = 0; i < BreakSlots; i++)
            {
                float alpha = Mathf.Clamp01((_breakUntil[i] - Time.time) / BreakSeconds);
                Place(_breaks[i * 2], _breakPositions[i], size, 0.9f, 0.12f, 45, HudPalette.Danger, alpha);
                Place(_breaks[i * 2 + 1], _breakPositions[i], size, 0.9f, 0.12f, -45, HudPalette.Danger, alpha);
            }
            int used = 0;
            WaitingCount = 0;
            if (_session.Phase == StagePhase.Preparing || _session.Phase == StagePhase.Fighting)
                foreach (StageActor actor in _board.Actors)
                {
                    if (actor == null || actor.IsEnemy || actor.IsAlive)
                        continue;
                    Vector3 point = actor.transform.position + Vector3.up * (0.8f * size);
                    Place(Waiting(used++), point + Vector3.up * (0.08f * size), size, 0.4f, 0.08f, 0, HudPalette.FrameDisabled, 0.95f);
                    Place(Waiting(used++), point - Vector3.up * (0.08f * size), size, 0.4f, 0.08f, 0, HudPalette.FrameDisabled, 0.95f);
                    WaitingCount++;
                }
            for (int i = used; i < _waiting.Count; i++)
                _waiting[i].gameObject.SetActive(false);
        }

        public void Hide()
        {
            if (_core != null)
                _core.gameObject.SetActive(false);
            foreach (SpriteRenderer mark in _breaks)
                if (mark != null)
                    mark.gameObject.SetActive(false);
            foreach (SpriteRenderer mark in _waiting)
                if (mark != null)
                    mark.gameObject.SetActive(false);
            System.Array.Clear(_breakUntil, 0, _breakUntil.Length);
            _corePulseUntil = 0;
            _coreAlertUntil = 0;
            _breakAlertUntil = 0;
            WaitingCount = 0;
        }

        public void Dispose()
        {
            if (_root != null)
                Object.Destroy(_root.gameObject);
        }

        private void SyncBoard()
        {
            if (_board == _session.Board)
                return;
            _board = _session.Board;
            Hide();
        }

        private SpriteRenderer Waiting(int index)
        {
            while (_waiting.Count <= index)
                _waiting.Add(Create("Recovery Waiting", 46));
            return _waiting[index];
        }

        private SpriteRenderer Create(string label, int order)
        {
            SpriteRenderer sprite = Object.Instantiate(_template, _root);
            sprite.name = label;
            sprite.sortingOrder = order;
            sprite.gameObject.SetActive(false);
            return sprite;
        }

        private void Place(SpriteRenderer sprite, Vector3 point, float size, float width, float height, float angle, Color color, float alpha)
        {
            if (sprite == null)
                return;
            sprite.gameObject.SetActive(alpha > 0);
            if (alpha <= 0)
                return;
            sprite.transform.position = point;
            sprite.transform.localScale = new Vector3(_unitScale.x * width * size, _unitScale.y * height * size, _unitScale.z);
            sprite.transform.localRotation = Quaternion.Euler(0, 0, angle);
            color.a = alpha;
            sprite.color = color;
        }
    }
}
