using Code.Stages;
using UnityEngine;

namespace Code.UI.Stages
{
    /// <summary>
    /// 전투 중 왼쪽 아래에 배치한 부하의 초상 줄. 매 프레임 체력·궁극기 비율만 갱신하고 글은 상태가 바뀔 때만 쓴다.
    /// 준비 단계에는 같은 자리에 단축칸의 부하 묶음(배치용)이 나온다.
    /// </summary>
    public sealed class StageUnitPortraits : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private StageUnitPortrait[] portraits;
        private readonly StageOptionIcons _icons = new();

        public bool IsValid => root != null && portraits != null && portraits.Length > 0;

        public void Render(StageSession session)
        {
            if (!IsValid || session?.Board == null)
                return;
            int used = 0;
            foreach (StageActor actor in session.Board.Actors)
            {
                if (actor == null || actor.IsEnemy || used >= portraits.Length)
                    continue;
                bool alive = actor.IsAlive;
                float health = alive && actor.Combat != null && actor.Combat.Health != null ? actor.Combat.Health.CurrentRatio : 0f;
                float charge = actor.Skills != null && actor.Skills.HasUltimate ? actor.UltimateCharge : -1f;
                portraits[used].Show(_icons.For(actor.Option), health, charge, !alive);
                used++;
            }
            for (int i = used; i < portraits.Length; i++)
                portraits[i].Hide();
            if (root.activeSelf != used > 0)
                root.SetActive(used > 0);
        }

        public void Hide()
        {
            if (root != null && root.activeSelf)
                root.SetActive(false);
        }
    }
}
