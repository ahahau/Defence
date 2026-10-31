using System.Collections.Generic;
using Code.UI;
using UnityEngine;

namespace Code.Stages
{
    /// <summary>
    /// 부하 명령을 발밑 표식으로 그린다. 사수는 파랑, 추격은 주황, 기본은 표식 없음. 준비 단계에 고른 부하는 금색 칸으로 감싼다.
    /// 그림은 배치 커서 스프라이트를 복제해 쓴다(적 표식·경로 미리보기와 같은 방식). 적 표식은 머리 위라 자리가 겹치지 않는다.
    /// </summary>
    public sealed class StageCommandMarks
    {
        private const float Size = 0.22f;
        private const float Drop = 0.42f;
        private const float SelectedAlpha = 0.35f;

        /// <summary>경로 미리보기(5)·장치(6) 위, 캐릭터(20) 아래.</summary>
        private const int SortingOrder = 8;

        private readonly SpriteRenderer _template;
        private readonly Vector3 _unitScale;
        private readonly Transform _root;
        private readonly List<SpriteRenderer> _marks = new();

        public bool IsValid => _root != null;

        public StageCommandMarks(SpriteRenderer template, Vector3 unitScale, Transform parent = null)
        {
            _template = template;
            _unitScale = unitScale;
            _root = new GameObject("Command Marks").transform;
            if (parent != null)
                _root.SetParent(parent, false);
        }

        public void Show(StageSession session, StageActor selected)
        {
            if (_root == null)
                return;
            int used = 0;
            float size = session.Definition.CellSize;
            if (selected != null)
            {
                SpriteRenderer frame = Mark(used++);
                frame.transform.position = session.Board.World(session.Board.Cell(selected.transform.position));
                frame.transform.localScale = _unitScale * size;
                frame.color = new Color(HudPalette.Accent.r, HudPalette.Accent.g, HudPalette.Accent.b, SelectedAlpha);
                frame.gameObject.SetActive(true);
            }
            foreach (StageActor actor in session.Board.Actors)
            {
                if (actor == null || actor.IsEnemy || !actor.IsAlive || actor.Command == StageUnitCommand.Default)
                    continue;
                SpriteRenderer mark = Mark(used++);
                mark.transform.position = actor.transform.position + Vector3.down * (Drop * size);
                mark.transform.localScale = _unitScale * (Size * size);
                mark.color = actor.Command == StageUnitCommand.Hold ? HudPalette.Magic : HudPalette.Warn;
                mark.gameObject.SetActive(true);
            }
            for (int i = used; i < _marks.Count; i++)
                if (_marks[i] != null)
                    _marks[i].gameObject.SetActive(false);
        }

        public void Hide()
        {
            foreach (SpriteRenderer mark in _marks)
                if (mark != null)
                    mark.gameObject.SetActive(false);
        }

        private SpriteRenderer Mark(int index)
        {
            while (_marks.Count <= index)
            {
                SpriteRenderer created = Object.Instantiate(_template, _root);
                created.name = "Command Mark";
                created.sortingOrder = SortingOrder;
                _marks.Add(created);
            }
            return _marks[index];
        }
    }
}
