using System.Collections.Generic;
using Code.UI;
using UnityEngine;

namespace Code.Stages
{
    /// <summary>
    /// 전투 중 특수 규칙이 붙은 적 머리 위에 표식을 그린다. 도약(바리케이드 넘기)은 주황, 돌진(교전 무시)은 빨강.
    /// 그림은 배치 커서 스프라이트를 45도 돌려 쓴다(경로 미리보기·궁극기 막대와 같은 방식).
    /// </summary>
    public sealed class StageEnemyMarks
    {
        private const float Size = 0.28f;
        private const float Lift = 0.75f;

        /// <summary>캐릭터(20) 위, 뜨는 숫자(60) 아래.</summary>
        private const int SortingOrder = 45;

        private readonly SpriteRenderer _template;
        private readonly Vector3 _unitScale;
        private readonly Transform _root;
        private readonly List<SpriteRenderer> _marks = new();

        public bool IsValid => _root != null;

        public StageEnemyMarks(SpriteRenderer template, Vector3 unitScale, Transform parent = null)
        {
            _template = template;
            _unitScale = unitScale;
            _root = new GameObject("Enemy Marks").transform;
            if (parent != null)
                _root.SetParent(parent, false);
        }

        public static Color ColorOf(StageEnemyRule rule) => rule == StageEnemyRule.Rush ? HudPalette.Danger : HudPalette.Warn;

        public void Show(StageSession session)
        {
            if (_root == null)
                return;
            int used = 0;
            float size = session.Definition.CellSize;
            foreach (StageActor actor in session.Board.Actors)
            {
                if (actor == null || !actor.IsEnemy || !actor.IsAlive || actor.Rule == StageEnemyRule.None)
                    continue;
                SpriteRenderer mark = Mark(used++);
                mark.transform.position = actor.transform.position + Vector3.up * (Lift * size);
                mark.transform.localScale = _unitScale * (Size * size);
                mark.color = ColorOf(actor.Rule);
                mark.gameObject.SetActive(true);
            }
            Trim(used);
        }

        public void Hide() => Trim(0);

        private SpriteRenderer Mark(int index)
        {
            while (_marks.Count <= index)
            {
                SpriteRenderer created = Object.Instantiate(_template, _root);
                created.name = "Enemy Mark";
                created.sortingOrder = SortingOrder;
                created.transform.rotation = Quaternion.Euler(0f, 0f, 45f);
                _marks.Add(created);
            }
            return _marks[index];
        }

        private void Trim(int used)
        {
            for (int i = used; i < _marks.Count; i++)
                if (_marks[i] != null)
                    _marks[i].gameObject.SetActive(false);
        }
    }
}
