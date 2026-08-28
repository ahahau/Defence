using UnityEngine;

namespace _01.Code.Core.Sensing
{
    /// <summary>
    /// 물리 기반 주변 감지.
    ///
    /// 여태 Defence의 표적 찾기는 NodeBattlefield가 들고 있는 명단을 훑는 방식이었다.
    /// 명단은 "이 구역에 누가 있나"는 알지만 "지금 내 앞 2m에 누가 있나"는 모른다.
    /// 사거리·시야·엄폐가 필요한 순간(원거리 유닛, 관통 스킬, 설치물 뒤 표적)에는
    /// 명단만으로 답이 안 나와서, 그 판정을 여기로 모은다.
    ///
    /// 참조 프로젝트의 AgentSensor에서 플랫포머 전용 판정(바닥 박스캐스트)은 빼고,
    /// 탑다운 아레나에 필요한 사거리·시야·범위 수집만 남겼다.
    /// </summary>
    public sealed class EntitySensor : MonoBehaviour
    {
        [SerializeField, Tooltip("시야를 가리는 것들. 벽·설치물 레이어.")]
        private LayerMask obstacleLayer;

        [SerializeField, Tooltip("표적으로 삼을 것들. 적이면 아군 레이어, 아군이면 적 레이어.")]
        private LayerMask targetLayer;

        [SerializeField, Tooltip("감지 원점의 오프셋(월드 단위). 발밑이 아니라 몸통에서 재고 싶을 때.")]
        private Vector2 offset;

        [SerializeField, Min(0f), Tooltip("기즈모로 그릴 기본 사거리. 판정에는 쓰지 않는다.")]
        private float gizmoRange = 3f;

        /// <summary>한 번에 훑을 수 있는 표적 수. 넘치면 가까운 순이 아니라 물리 엔진 순서로 잘린다.</summary>
        private const int MaxTargets = 32;

        private static readonly Collider2D[] Buffer = new Collider2D[MaxTargets];

        public Vector2 Origin => (Vector2)transform.position + offset;

        /// <summary>사거리 안에 표적이 하나라도 있는가.</summary>
        public bool IsTargetInRange(float range, out Collider2D target)
        {
            target = Physics2D.OverlapCircle(Origin, range, targetLayer);
            return target != null;
        }

        /// <summary>
        /// 사거리 안의 표적을 버퍼에 담아 개수를 돌려준다.
        /// 매 프레임 도는 판정이라 할당하지 않으려고 공용 버퍼를 쓴다 —
        /// 돌려받은 배열은 다음 호출 전까지만 유효하다.
        /// </summary>
        public int CollectTargetsInRange(float range, out Collider2D[] targets)
        {
            targets = Buffer;
            var filter = new ContactFilter2D { useTriggers = true };
            filter.SetLayerMask(targetLayer);
            return Physics2D.OverlapCircle(Origin, range, filter, Buffer);
        }

        /// <summary>사이에 가리는 것이 없는가. 벽 뒤의 적을 쏘지 않게 하는 판정.</summary>
        public bool HasLineOfSight(Vector2 targetPosition)
        {
            var toTarget = targetPosition - Origin;
            if (toTarget.sqrMagnitude <= Mathf.Epsilon)
                return true;

            var blocker = Physics2D.Raycast(Origin, toTarget.normalized, toTarget.magnitude, obstacleLayer);
            return blocker.collider == null;
        }

        public bool HasLineOfSight(Collider2D target) =>
            target != null && HasLineOfSight(target.transform.position);

        /// <summary>사거리 안이면서 시야도 트인 가장 가까운 표적.</summary>
        public Collider2D FindNearestVisibleTarget(float range)
        {
            var count = CollectTargetsInRange(range, out var targets);
            Collider2D best = null;
            var bestDistance = float.MaxValue;

            for (var i = 0; i < count; i++)
            {
                var candidate = targets[i];
                if (candidate == null || !HasLineOfSight(candidate))
                    continue;

                var distance = ((Vector2)candidate.transform.position - Origin).sqrMagnitude;
                if (distance >= bestDistance)
                    continue;

                bestDistance = distance;
                best = candidate;
            }

            return best;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.45f, 0.2f, 0.9f);
            Gizmos.DrawWireSphere((Vector3)Origin, gizmoRange);
        }
    }
}
