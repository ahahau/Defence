using System;
using System.Collections;
using System.Collections.Generic;
using Code.Combat;
using Code.Enemies;
using Code.Manager;
using MoreMountains.Feedbacks;
using DG.Tweening;
using UnityEngine;

namespace Code.Buildings
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
        public int MaxDurability => FacilityDurabilityRules.MaxDurability(
            maxDurability, Data != null ? Data.FacilityTier : 1);

        /// <summary>
        /// 문을 닫아 둔 시설인가. 닫힌 시설은 벌지도, 운영비를 먹지도, 소문을 내지도 않는다.
        ///
        /// 등급이 오르기만 하는 구조라 감당이 안 될 때 물러설 곳이 필요했다. 다만 공짜로
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

        /// <summary>머무를 수 있는 시설인가. 다 닳았거나 수리 중이면 손님을 받지 않는다.</summary>
        public bool AcceptsDwell => IsOperating && DwellSeconds > 0f && !IsWornOut && !IsRepairing;

        [Header("Visitor Queue")]
        [SerializeField, Min(1), Tooltip("한 번에 시설을 이용할 수 있는 모험가 수. 초과 인원은 도착 순서대로 기다린다.")]
        private int maxConcurrentVisitors = 1;

        private readonly Queue<Enemy> waitingVisitors = new();
        private readonly HashSet<Enemy> visitingEnemies = new();

        public int MaxConcurrentVisitors => Mathf.Max(1, maxConcurrentVisitors);
        public int CurrentVisitorCount
        {
            get
            {
                RemoveMissingVisitors();
                return visitingEnemies.Count;
            }
        }

        public int WaitingVisitorCount
        {
            get
            {
                RemoveMissingVisitors();
                return waitingVisitors.Count;
            }
        }

        /// <summary>현재 대기열에서 가장 오래 기다린 모험가의 게임 시간.</summary>
        public float LongestVisitorWaitSeconds
        {
            get
            {
                float longest = 0f;
                foreach (Enemy visitor in waitingVisitors)
                {
                    if (visitor != null && !visitor.IsDead && visitor.WaitingFacility == this)
                        longest = Mathf.Max(longest, visitor.FacilityWaitSeconds);
                }
                return longest;
            }
        }

        /// <summary>
        /// 시설 이용을 시작할 수 있으면 자리를 잡는다. 자리가 없으면 도착 순서대로 줄에 세운다.
        ///
        /// 시설 노드는 잠그지 않는다. 같은 시설을 쓰려는 손님은 방 안에서 기다리고,
        /// 다음 손님은 먼저 온 손님을 건너뛰지 않는다.
        /// </summary>
        public bool TryStartVisit(Enemy visitor)
        {
            if (visitor == null || !AcceptsDwell)
                return false;

            RemoveMissingVisitors();
            if (visitingEnemies.Contains(visitor))
                return true;

            if (!waitingVisitors.Contains(visitor))
                waitingVisitors.Enqueue(visitor);

            if (visitingEnemies.Count >= MaxConcurrentVisitors || waitingVisitors.Peek() != visitor)
                return false;

            waitingVisitors.Dequeue();
            visitingEnemies.Add(visitor);
            return true;
        }

        /// <summary>대기 또는 이용을 끝낸 손님을 제거해 다음 손님에게 자리를 넘긴다.</summary>
        public void CancelVisit(Enemy visitor)
        {
            if (visitor == null)
                return;

            visitingEnemies.Remove(visitor);
            if (!waitingVisitors.Contains(visitor))
                return;

            var retained = new Queue<Enemy>();
            while (waitingVisitors.Count > 0)
            {
                var queued = waitingVisitors.Dequeue();
                if (queued != null && queued != visitor)
                    retained.Enqueue(queued);
            }

            while (retained.Count > 0)
                waitingVisitors.Enqueue(retained.Dequeue());
        }

        private void RemoveMissingVisitors()
        {
            visitingEnemies.RemoveWhere(visitor => visitor == null || visitor.IsDead);
            if (waitingVisitors.Count == 0)
                return;

            var retained = new Queue<Enemy>();
            while (waitingVisitors.Count > 0)
            {
                var queued = waitingVisitors.Dequeue();
                if (queued != null && !queued.IsDead)
                    retained.Enqueue(queued);
            }

            while (retained.Count > 0)
                waitingVisitors.Enqueue(retained.Dequeue());
        }

        /// <summary>
        /// 한 번 머무는 동안 모험가가 여기서 쓰는 총액. 시설이 정하고, 머무는 쪽이 시간에 나눠 낸다.
        /// 0이면 돈을 받지 않는 시설이다.
        /// </summary>
        public virtual int DwellGoldTotal => 0;

        /// <summary>이 시설의 수입이 장부에 적히는 줄.</summary>
        public virtual Events.GoldChangeSource DwellGoldSource => Events.GoldChangeSource.Store;

        /// <summary>머물던 모험가가 낸 금화를 장부에 올린다. 채널이 시설마다 따로라 시설이 직접 쏜다.</summary>
        public virtual void ReportDwellIncome(int gold) { }

        [Header("Wear")]
        [SerializeField, Min(0), Tooltip("손님 한 명이 깎는 내구도. 수익 시설에만 쓴다.")]
        private int wearPerVisit = 1;
        [SerializeField, Min(0), Tooltip("깎인 내구도 한 점을 고치는 데 드는 기본 금화.")]
        private int repairCostPerWear = 4;

        private int repairCount;
        private float repairRemaining;

        /// <summary>정수로 깎기 전에 쌓아 둔 마모. 레벨 2 손님(1.25배)처럼 소수 마모도 버리지 않는다.</summary>
        private float _wearCarry;

        /// <summary>수리가 끝났을 때. 화면이 게이지를 다시 그린다.</summary>
        public event Action<Building> RepairCompleted;

        /// <summary>
        /// 손님이 닳게 하는 수익 시설인가. 부서지는 건물(함정·회복실)은 맞아서 깎이고 0이면 사라지므로
        /// 이 규칙을 따르지 않는다.
        /// </summary>
        public bool UsesServiceDurability => !destructible && DwellSeconds > 0f;

        public int MissingDurability => Mathf.Max(0, MaxDurability - currentDurability);

        /// <summary>다 닳아 영업을 멈춘 수익 시설인가. 수리만 할 수 있다.</summary>
        public bool IsWornOut => UsesServiceDurability && currentDurability <= 0;

        public bool IsRepairing => repairRemaining > 0f;
        public float RepairRemaining => repairRemaining;
        public int RepairCount => repairCount;

        /// <summary>지금 고친다면 드는 금화.</summary>
        public int RepairCost => UsesServiceDurability
            ? FacilityDurabilityRules.RepairCost(MissingDurability, repairCostPerWear, repairCount)
            : 0;

        /// <summary>지금 고친다면 걸리는 게임 시간(초).</summary>
        public float RepairSeconds => FacilityDurabilityRules.RepairSeconds(MissingDurability);

        public bool CanRepair => UsesServiceDurability && !isDestroyed && !IsRepairing && MissingDurability > 0;

        /// <summary>내구도에 따른 수입 배율. 수익 시설이 아니면 깎지 않는다.</summary>
        public float DurabilityIncomeMultiplier => UsesServiceDurability
            ? FacilityDurabilityRules.IncomeMultiplier(currentDurability, MaxDurability)
            : 1f;

        /// <summary>
        /// 손님이 다녀갔다. 쓰이는 만큼 내구도가 깎인다.
        /// 벌이와 수리비가 같은 원인에서 나오므로 "이 시설이 남는 장사인가"가 계산이 된다.
        /// </summary>
        /// <param name="multiplier">손님에 따른 마모 배율(<see cref="FacilityDurabilityRules.WearMultiplier"/>).</param>
        public void RecordVisitWear(float multiplier = 1f)
        {
            if (isDestroyed || !UsesServiceDurability)
                return;

            _wearCarry += Mathf.Max(0, wearPerVisit) * Mathf.Max(0f, multiplier);
            var wear = Mathf.FloorToInt(_wearCarry + 0.0001f);
            _wearCarry = Mathf.Max(0f, _wearCarry - wear);
            currentDurability = Mathf.Max(0, currentDurability - wear);
        }

        /// <summary>
        /// 손님이 아닌 이유(시설 방 전투·사건)로 내구도를 깎는다. 수익 시설에만 쓰고, 부서지는 건물은
        /// 맞아서 깎이는 규칙을 따른다. 실제로 깎인 양을 돌려준다.
        /// </summary>
        public int ApplyWear(int amount)
        {
            if (isDestroyed || !UsesServiceDurability || amount <= 0)
                return 0;

            var before = currentDurability;
            currentDurability = Mathf.Max(0, currentDurability - amount);
            return before - currentDurability;
        }

        /// <summary>손님이 실제로 머무는 시간. 내구도가 절반 아래면 늘어난다(대기열 지연).</summary>
        public float EffectiveDwellSeconds => UsesServiceDurability
            ? DwellSeconds * FacilityDurabilityRules.DwellTimeMultiplier(currentDurability, MaxDurability)
            : DwellSeconds;

        /// <summary>시설이 손님에게 주는 효과를 내구도에 맞춰 줄인다. 수익 시설이 아니면 그대로.</summary>
        protected int ScaleServiceEffect(int amount) => UsesServiceDurability
            ? FacilityDurabilityRules.ScaleEffect(amount, currentDurability, MaxDurability)
            : amount;

        /// <summary>
        /// 수리를 시작한다. 금화는 부르는 쪽이 <see cref="RepairCost"/>를 먼저 치렀다.
        /// 끝날 때까지 손님을 받지 않고, 끝나면 내구도가 가득 찬다.
        /// </summary>
        public bool BeginRepair()
        {
            if (!CanRepair)
                return false;

            repairRemaining = Mathf.Max(0.01f, RepairSeconds);
            repairCount++;
            return true;
        }

        /// <summary>저장에서 되돌릴 때. 남은 수리 시간이 있으면 이어서 고친다.</summary>
        public void RestoreRepair(float remaining, int count)
        {
            repairCount = Mathf.Max(0, count);
            repairRemaining = UsesServiceDurability ? Mathf.Max(0f, remaining) : 0f;
        }

        /// <summary>
        /// 옛 저장의 마모를 내구도로 옮긴다. 예전에는 내구도는 그대로 두고 마모만 쌓았다.
        /// </summary>
        public void RestoreWear(int savedWear)
        {
            if (savedWear <= 0 || !UsesServiceDurability)
                return;

            currentDurability = Mathf.Max(0, currentDurability - savedWear);
        }

        protected virtual void Update() => TickRepair(Time.deltaTime);

        /// <summary>방 칸 격자에서 차지하는 칸 수(가로×세로). 함정만 여러 칸을 쓴다.</summary>
        public virtual Vector2Int Footprint => Vector2Int.one;

        /// <summary>수리 시간을 흘린다. 다 고치면 내구도를 채우고 알린다.</summary>
        public void TickRepair(float deltaTime)
        {
            if (repairRemaining <= 0f)
                return;

            repairRemaining -= Mathf.Max(0f, deltaTime);
            if (repairRemaining > 0f)
                return;

            repairRemaining = 0f;
            currentDurability = MaxDurability;
            RepairCompleted?.Invoke(this);
        }

        protected virtual void Awake()
        {
            if (damageAnimationTarget == null)
                damageAnimationTarget = transform;

            currentDurability = MaxDurability;
        }

        public virtual void Initialize(BuildingDataSO data)
        {
            Data = data;
            DangerRating = data.BaseDanger;
            currentDurability = MaxDurability;
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
            // 수익 시설은 0(영업 정지)으로 저장될 수 있다. 부서지는 건물은 0이면 이미 사라졌다.
            var min = UsesServiceDurability ? 0 : 1;
            currentDurability = Mathf.Clamp(durability, min, MaxDurability);
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

            // 흔드는 중이 아니면 지금 자리를 기준으로 잡는다. Awake에서 잡으면 노드(배율 15)에 붙기 전 월드 좌표라
            // 루트를 흔드는 건물이 첫 피해에 방 밖 멀리 튀었다.
            if (damageTween != null && damageTween.IsActive())
            {
                damageTween.Kill();
                damageAnimationTarget.localPosition = damageAnimationBaseLocalPosition;
            }
            else
            {
                damageAnimationBaseLocalPosition = damageAnimationTarget.localPosition;
            }

            damageTween = damageAnimationTarget
                .DOLocalMoveX(damageAnimationBaseLocalPosition.x + damageShakeDistance, damageShakeDuration * 0.5f)
                .SetEase(Ease.InOutSine)
                .SetLoops(2, LoopType.Yoyo)
                .OnComplete(() => damageAnimationTarget.localPosition = damageAnimationBaseLocalPosition)
                .SetLink(gameObject);
        }

        private void OnDisable()
        {
            // 흔드는 중에 꺼질 때만 제자리로 되돌린다(흔든 적 없으면 기준 위치가 없다).
            if (damageTween != null && damageTween.IsActive())
            {
                damageTween.Kill();
                if (damageAnimationTarget != null)
                    damageAnimationTarget.localPosition = damageAnimationBaseLocalPosition;
            }

            damageTween = null;
        }
    }
}
