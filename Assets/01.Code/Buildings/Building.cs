using System.Collections;
using _01.Code.Combat;
using MoreMountains.Feedbacks;
using DG.Tweening;
using UnityEngine;

namespace _01.Code.Buildings
{
    public class Building : MonoBehaviour
    {
        public BuildingDataSO Data { get; private set; }
        [field: SerializeField, Min(0)] public int DangerRating { get; private set; }

        [Header("Durability")]
        [SerializeField] private bool destructible;
        [SerializeField, Min(1)] private int maxDurability = 20;
        [SerializeField] private Transform damageAnimationTarget;
        [SerializeField, Min(0f)] private float damageShakeDistance = 0.08f;
        [SerializeField, Min(0.01f)] private float damageShakeDuration = 0.16f;
        [SerializeField, Min(0f)] private float destroyDelay = 0.05f;

        private int currentDurability;
        private bool isDestroyed;
        private Vector3 damageAnimationBaseLocalPosition;
        private Tween damageTween;

        [Header("Closure")]
        [SerializeField, Min(0), Tooltip("닫아 둔 시설을 다시 여는 데 드는 금화. 닫는 것은 공짜다.")]
        private int reopenCost = 25;
        [SerializeField, Min(1), Tooltip("다시 열기로 한 뒤 실제로 문을 열기까지 걸리는 날.")]
        private int reopenDays = 2;

        private bool isClosed;

        /// <summary>다시 열기로 한 뒤, 실제로 열리는 날. 0이면 재개를 예약하지 않았다.</summary>
        private int reopenOnDay;

        public bool IsDestructible => destructible;
        public bool IsDestroyed => isDestroyed;
        public int CurrentDurability => currentDurability;
        public int MaxDurability => maxDurability;

        /// <summary>
        /// 문을 닫아 둔 시설인가. 닫힌 시설은 벌지도, 운영비를 먹지도, 소문을 내지도 않는다.
        ///
        /// 명성이 오르기만 하는 구조라 감당이 안 될 때 물러설 곳이 필요했다. 다만 공짜로
        /// 물러설 수 있으면 힘들 때마다 전부 닫아 두는 것이 정답이 되므로, 여는 데 값을 치른다.
        /// </summary>
        public bool IsClosed => isClosed;

        /// <summary>다시 열기를 예약해 두고 날짜를 기다리는 중인가.</summary>
        public bool IsReopening => isClosed && reopenOnDay > 0;

        public int ReopenCost => Mathf.Max(0, reopenCost);
        public int ReopenDays => Mathf.Max(1, reopenDays);

        /// <summary>다시 열리기까지 남은 날. 예약하지 않았으면 0.</summary>
        public int ReopenDaysRemaining(int currentDay) =>
            IsReopening ? Mathf.Max(0, reopenOnDay - currentDay) : 0;

        /// <summary>운영 중인 시설인가. 부서졌거나 닫혀 있으면 아니다.</summary>
        public bool IsOperating => !isDestroyed && !isClosed;

        /// <summary>모험가가 여기 머무는 시간(초). 데이터가 정한다.</summary>
        public float DwellSeconds => Data != null ? Mathf.Max(0f, Data.DwellSeconds) : 0f;

        /// <summary>머무를 수 있는 시설인가.</summary>
        public bool AcceptsDwell => IsOperating && DwellSeconds > 0f;

        /// <summary>
        /// 한 번 머무는 동안 모험가가 여기서 쓰는 총액. 시설이 정하고, 머무는 쪽이 시간에 나눠 낸다.
        /// 0이면 돈을 받지 않는 시설이다.
        /// </summary>
        public virtual int DwellGoldTotal => 0;

        /// <summary>이 시설의 수입이 장부에 적히는 줄.</summary>
        public virtual Events.GoldChangeSource DwellGoldSource => Events.GoldChangeSource.Store;

        /// <summary>머물던 모험가가 낸 금화를 장부에 올린다. 채널이 시설마다 따로라 시설이 직접 쏜다.</summary>
        public virtual void ReportDwellIncome(int gold) { }

        protected virtual void Awake()
        {
            if (damageAnimationTarget == null)
                damageAnimationTarget = transform;

            damageAnimationBaseLocalPosition = damageAnimationTarget.localPosition;
            currentDurability = Mathf.Max(1, maxDurability);
        }

        public virtual void Initialize(BuildingDataSO data)
        {
            Data = data;
            DangerRating = data.BaseDanger;
            currentDurability = Mathf.Max(1, maxDurability);
            isDestroyed = false;
            ApplyBoardSprite(data);
        }

        /// <summary>
        /// 데이터가 들고 있는 그림을 실제로 보드에 올린다.
        ///
        /// 종류 열하나가 프리팹 여덟을 나눠 쓴다 — 주점과 큰 주점이 Inn 하나를, 광산과 깊은 광산이
        /// Mine 하나를, 상점과 무기 상점이 Store 하나를 쓴다. 프리팹에만 그림을 두면 승급해도
        /// 화면이 그대로라 무엇이 지어졌는지 알 수 없다. 침입자 쪽이 이미 같은 방식으로 돌고 있다.
        /// </summary>
        private void ApplyBoardSprite(BuildingDataSO data)
        {
            if (data == null || data.BoardSprite == null)
                return;

            // 프리팹에는 SpriteRenderer가 둘이다 — 몸통 Visual 과 테두리 Border.
            // 이름으로 몸통을 집는다. 아무거나 집으면 테두리에 건물 그림이 들어간다.
            SpriteRenderer body = null;
            foreach (var renderer in GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (renderer.gameObject.name != "Visual")
                    continue;

                body = renderer;
                break;
            }

            // 이름이 다른 프리팹이 섞여 있어도 아무것도 안 나오는 것보다는 낫다.
            if (body == null)
                body = GetComponentInChildren<SpriteRenderer>(true);

            if (body != null)
                body.sprite = data.BoardSprite;
        }

        public void RestoreDurability(int durability)
        {
            currentDurability = Mathf.Clamp(durability, 1, Mathf.Max(1, maxDurability));
            isDestroyed = false;
        }

        /// <summary>문을 닫는다. 값은 들지 않지만 그날부터 벌이도 소문도 멈춘다.</summary>
        public void Close()
        {
            if (isDestroyed || isClosed)
                return;

            isClosed = true;
            reopenOnDay = 0;
            ApplyClosedLook();
        }

        /// <summary>
        /// 다시 열기로 하고 날짜를 잡는다. 금화는 부르는 쪽이 이미 치렀다.
        /// 예약만 해두고 그날이 와야 실제로 열린다 — 청산 직전에 급히 열어 막을 수는 없다.
        /// </summary>
        public void BeginReopen(int currentDay)
        {
            if (isDestroyed || !isClosed || IsReopening)
                return;

            // 오늘 날짜에 그대로 더한다. 준비 단계(0일차)에 바닥을 1로 올리면
            // 첫날 닫은 시설만 하루 더 기다리게 된다.
            reopenOnDay = Mathf.Max(0, currentDay) + ReopenDays;
        }

        /// <summary>예약한 날이 됐으면 실제로 연다. 열렸으면 true.</summary>
        public bool TryCompleteReopen(int currentDay)
        {
            if (!IsReopening || currentDay < reopenOnDay)
                return false;

            isClosed = false;
            reopenOnDay = 0;
            ApplyClosedLook();
            return true;
        }

        /// <summary>저장에서 되돌릴 때. 날짜 계산 없이 상태를 그대로 세운다.</summary>
        public void RestoreClosure(bool closed, int reopenDay)
        {
            isClosed = closed;
            reopenOnDay = closed ? Mathf.Max(0, reopenDay) : 0;
            ApplyClosedLook();
        }

        /// <summary>저장용 재개 예정일. 예약하지 않았으면 0.</summary>
        public int ReopenOnDay => IsReopening ? reopenOnDay : 0;

        /// <summary>닫힌 시설은 흐릿하게 둔다. 지도만 보고도 무엇이 쉬고 있는지 알아야 한다.</summary>
        private void ApplyClosedLook()
        {
            foreach (var renderer in GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (renderer == null)
                    continue;

                var color = renderer.color;
                color.a = isClosed ? 0.4f : 1f;
                renderer.color = color;
            }
        }

        public bool TakeBuildingDamage(int damage)
        {
            if (!destructible || isDestroyed || damage <= 0)
                return false;

            currentDurability = Mathf.Max(0, currentDurability - damage);
            PlayHitAnimation();

            if (currentDurability > 0)
                return false;

            BreakBuilding();
            return true;
        }

        protected virtual void BreakBuilding()
        {
            if (isDestroyed)
                return;

            isDestroyed = true;
            damageTween?.Kill();
            if (destroyDelay <= 0f)
                Destroy(gameObject);
            else
                Destroy(gameObject, destroyDelay);
        }

        protected void PlayPassEffectFeedback(
            Combatant target,
            Color flashColor,
            float duration,
            MMF_Player feelFeedback = null)
        {
            if (target == null)
                return;

            if (feelFeedback != null)
                feelFeedback.PlayFeedbacks(target.transform.position);
            StartCoroutine(FlashTargetColor(target, flashColor, duration));
        }

        private IEnumerator FlashTargetColor(Combatant target, Color flashColor, float duration)
        {
            if (target == null)
                yield break;

            var renderers = target.GetComponentsInChildren<SpriteRenderer>();
            if (renderers == null || renderers.Length == 0)
                yield break;

            var originalColors = new Color[renderers.Length];
            for (var i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null)
                    continue;

                originalColors[i] = renderers[i].color;
                renderers[i].color = flashColor;
            }

            yield return new WaitForSeconds(Mathf.Max(0.01f, duration));

            for (var i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                    renderers[i].color = originalColors[i];
            }
        }

        private void PlayHitAnimation()
        {
            if (damageAnimationTarget == null || damageShakeDistance <= 0f)
                return;

            damageTween?.Kill();
            damageAnimationTarget.localPosition = damageAnimationBaseLocalPosition;
            damageTween = damageAnimationTarget
                .DOLocalMoveX(damageAnimationBaseLocalPosition.x + damageShakeDistance, damageShakeDuration * 0.5f)
                .SetEase(Ease.InOutSine)
                .SetLoops(2, LoopType.Yoyo)
                .OnComplete(() => damageAnimationTarget.localPosition = damageAnimationBaseLocalPosition)
                .SetLink(gameObject);
        }

        private void OnDisable()
        {
            damageTween?.Kill();
            damageTween = null;

            if (damageAnimationTarget != null)
                damageAnimationTarget.localPosition = damageAnimationBaseLocalPosition;
        }
    }
}
