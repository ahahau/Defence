using System.Collections.Generic;
using Code.UI;
using UnityEngine;

namespace Code.Stages
{
    /// <summary>
    /// 전투 중 궁극기가 있는 부하 머리 위에 게이지 막대를 그린다. 차는 동안 파랑, 가득 차면 금색이고 그 부하를 누르면 궁극기를 쓴다.
    /// 그림은 배치 커서 스프라이트를 복제해 쓴다(경로 미리보기와 같은 방식).
    /// </summary>
    public sealed class StageUltimateBars
    {
        private const float Width = 0.8f;
        private const float Height = 0.12f;
        private const float Lift = 0.7f;

        /// <summary>캐릭터(20) 위, 뜨는 숫자(60) 아래.</summary>
        private const int SortingOrder = 45;

        private readonly SpriteRenderer _template;
        private readonly Vector3 _unitScale;
        private readonly Transform _root;
        private readonly List<SpriteRenderer> _backs = new();
        private readonly List<SpriteRenderer> _fills = new();

        public StageUltimateBars(SpriteRenderer template, Vector3 unitScale)
        {
            _template = template;
            _unitScale = unitScale;
            _root = new GameObject("Ultimate Bars").transform;
        }

        public void Show(StageSession session)
        {
            int used = 0;
            float size = session.Definition.CellSize;
            foreach (StageActor actor in session.Board.Actors)
            {
                if (actor == null || actor.IsEnemy || !actor.IsAlive || actor.Skills == null || !actor.Skills.HasUltimate)
                    continue;
                Vector3 anchor = actor.transform.position + Vector3.up * (Lift * size);
                float charge = actor.UltimateCharge;
                Place(Bar(_backs, used, SortingOrder), anchor, size, 1f, HudPalette.Track, 0.85f);
                Color fill = charge >= 1f ? HudPalette.Accent : HudPalette.Info;
                Place(Bar(_fills, used, SortingOrder + 1), anchor, size, charge, fill, charge >= 1f ? 1f : 0.6f);
                used++;
            }
            Trim(_backs, used);
            Trim(_fills, used);
        }

        public void Hide()
        {
            Trim(_backs, 0);
            Trim(_fills, 0);
        }

        private void Place(SpriteRenderer bar, Vector3 anchor, float size, float ratio, Color color, float alpha)
        {
            float width = Width * size;
            bar.gameObject.SetActive(ratio > 0f);
            // 왼쪽 끝을 고정하고 오른쪽으로 찬다
            bar.transform.position = anchor + Vector3.right * (width * (ratio - 1f) * 0.5f);
            bar.transform.localScale = new Vector3(_unitScale.x * width * ratio, _unitScale.y * Height * size, _unitScale.z);
            bar.color = new Color(color.r, color.g, color.b, alpha);
        }

        private SpriteRenderer Bar(List<SpriteRenderer> pool, int index, int order)
        {
            while (pool.Count <= index)
            {
                SpriteRenderer created = Object.Instantiate(_template, _root);
                created.name = "Ultimate Bar";
                created.sortingOrder = order;
                pool.Add(created);
            }
            return pool[index];
        }

        private static void Trim(List<SpriteRenderer> pool, int used)
        {
            for (int i = used; i < pool.Count; i++)
                pool[i].gameObject.SetActive(false);
        }
    }
}
