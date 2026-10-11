using System.Collections.Generic;
using UnityEngine;

namespace Code.Enemies
{
    /// <summary>
    /// 쓰러진 파티원의 관. 살아 있는 파티원 뒤를 따라다니며 싸우지도 맞지도 않는다.
    ///
    /// 죽은 적이 새 적으로 돌아오는 대신 기존 파티원의 관이 된다(기획서). 파티가 교회에 가면
    /// 공동 자금을 내고 이 관을 되살린다. 파티가 전멸하면 관도 사라지고, 살아 나가면 함께 나간다.
    /// 웨이브의 남은 적 수에는 세지 않는다.
    /// </summary>
    public sealed class PartyCoffin : MonoBehaviour
    {
        private const float FollowSpeed = 4f;
        private const float TrailSpacing = 0.45f;
        private static readonly Color CoffinTint = new(0.35f, 0.33f, 0.4f, 0.9f);

        private static readonly List<PartyCoffin> ActiveCoffins = new();

        private EnemyMover _leader;
        private Enemy _leaderEnemy;
        private SpriteRenderer _renderer;

        public int PartyId { get; private set; }
        public EnemyDataSO Data { get; private set; }
        public int Level { get; private set; }
        public AdventurerVisitPurpose Purpose { get; private set; }

        public static IReadOnlyList<PartyCoffin> All => ActiveCoffins;

        /// <summary>쓰러진 자리에서 관을 만든다. 그림은 그 적의 쓰러진 모습을 어둡게 칠해 쓴다.</summary>
        public static PartyCoffin Spawn(
            int partyId,
            EnemyDataSO data,
            int level,
            AdventurerVisitPurpose purpose,
            SpriteRenderer source,
            Vector3 position)
        {
            if (partyId <= 0 || data == null)
                return null;

            var go = new GameObject($"Coffin_{data.name}");
            go.transform.position = position;
            var coffin = go.AddComponent<PartyCoffin>();
            coffin.PartyId = partyId;
            coffin.Data = data;
            coffin.Level = Mathf.Max(1, level);
            coffin.Purpose = purpose;

            coffin._renderer = go.AddComponent<SpriteRenderer>();
            coffin._renderer.sprite = data.DefeatedSprite != null
                ? data.DefeatedSprite
                : source != null ? source.sprite : null;
            coffin._renderer.color = CoffinTint;
            if (source != null)
            {
                coffin._renderer.sortingLayerID = source.sortingLayerID;
                coffin._renderer.sortingOrder = source.sortingOrder - 1;
                go.transform.localScale = source.transform.lossyScale * 0.8f;
            }

            ActiveCoffins.Add(coffin);
            return coffin;
        }

        /// <summary>그 파티가 끌고 다니는 관 수.</summary>
        public static int CountFor(int partyId)
        {
            var count = 0;
            foreach (var coffin in ActiveCoffins)
            {
                if (coffin != null && coffin.PartyId == partyId)
                    count++;
            }

            return count;
        }

        /// <summary>그 파티의 관을 모은다. 매번 새 목록을 만들지 않도록 부르는 쪽 목록을 채운다.</summary>
        public static void CollectFor(int partyId, List<PartyCoffin> target)
        {
            target.Clear();
            foreach (var coffin in ActiveCoffins)
            {
                if (coffin != null && coffin.PartyId == partyId)
                    target.Add(coffin);
            }
        }

        /// <summary>파티가 전멸했거나 모두 나갔다. 그 파티의 관을 치운다.</summary>
        public static void ClearParty(int partyId)
        {
            for (var i = ActiveCoffins.Count - 1; i >= 0; i--)
            {
                var coffin = ActiveCoffins[i];
                if (coffin == null)
                    ActiveCoffins.RemoveAt(i);
                else if (coffin.PartyId == partyId)
                    coffin.Consume();
            }
        }

        /// <summary>영업이 끝나거나 새로 시작할 때 남은 관을 모두 치운다.</summary>
        public static void ClearAll()
        {
            for (var i = ActiveCoffins.Count - 1; i >= 0; i--)
            {
                var coffin = ActiveCoffins[i];
                if (coffin != null)
                    coffin.Consume();
            }

            ActiveCoffins.Clear();
        }

        /// <summary>되살아났거나 치워졌다.</summary>
        public void Consume()
        {
            ActiveCoffins.Remove(this);
            if (this == null)
                return;

            // EditMode 테스트도 같은 경로를 탄다. 런타임 Destroy는 에디트 모드에서 경고를 남긴다.
            if (Application.isPlaying)
                Destroy(gameObject);
            else
                DestroyImmediate(gameObject);
        }

        private void OnDestroy() => ActiveCoffins.Remove(this);

        private void Update()
        {
            if (_leader == null || _leaderEnemy == null || _leaderEnemy.IsDead)
                FindLeader();
            if (_leader == null)
                return;

            // 관끼리 겹치지 않게 순서대로 뒤에 선다.
            var index = Mathf.Max(0, IndexInParty());
            var target = _leader.transform.position + new Vector3(-TrailSpacing * (index + 1), -0.15f, 0f);
            transform.position = Vector3.Lerp(transform.position, target, 1f - Mathf.Exp(-FollowSpeed * Time.deltaTime));
        }

        /// <summary>따라갈 살아 있는 파티원을 고른다. 앞장선 사람이 쓰러졌을 때만 다시 찾는다.</summary>
        private void FindLeader()
        {
            _leader = null;
            _leaderEnemy = null;
            foreach (var mover in EnemyMover.ActiveEnemies)
            {
                if (mover == null || mover.PartyId != PartyId)
                    continue;

                var enemy = mover.GetComponent<Enemy>();
                if (enemy == null || enemy.IsDead)
                    continue;

                _leader = mover;
                _leaderEnemy = enemy;
                return;
            }
        }

        private int IndexInParty()
        {
            var index = 0;
            foreach (var coffin in ActiveCoffins)
            {
                if (coffin == this)
                    return index;
                if (coffin != null && coffin.PartyId == PartyId)
                    index++;
            }

            return index;
        }
    }
}
