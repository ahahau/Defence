using System;
using Code.Core;
using Code.Progression;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Code.Manager
{
    /// <summary>
    /// 씬의 매니저를 한곳에 모아 건네는 조립 루트. 씬에 하나 둔다.
    ///
    /// 매니저마다 static <c>Current</c>를 두면 누가 무엇에 기대는지 코드를 다 읽어야 알 수 있고, 테스트가
    /// 매니저를 갈아 끼울 수 없다. 씬 컴포넌트는 이것 하나만 받고, 실행 중에 생기는 것은 만드는 쪽이 넘긴다.
    /// static 접근자를 두지 않는다. 설계는 <c>Docs/AI/GameScopeDesign.md</c>.
    /// </summary>
    public sealed class GameScope : MonoBehaviour
    {
        /// <summary>쓰는 쪽 <c>scope</c> 필드에 다는 툴팁.</summary>
        public const string FieldTooltip = "씬의 조립 루트(GameScope). 비어 있으면 매니저를 찾으며 경고한다(배선 뒤 폴백 제거).";

        [SerializeField] private FacilityClosureSystem facilityClosure;
        [SerializeField] private UnitRevivalSystem unitRevival;
        [SerializeField] private RunSummarySystem runSummary;
        [SerializeField] private DungeonGradeManager dungeonGrade;
        [SerializeField] private MoralePolicyManager moralePolicy;

        public FacilityClosureSystem FacilityClosure => facilityClosure;
        public UnitRevivalSystem UnitRevival => unitRevival;
        public RunSummarySystem RunSummary => runSummary;
        public DungeonGradeManager DungeonGrade => dungeonGrade;
        public MoralePolicyManager MoralePolicy => moralePolicy;

        /// <summary>
        /// 조립 루트에서 매니저를 고른다. 루트가 비었거나 그 칸이 비었으면 씬에서 찾으며 경고한다.
        /// 전환이 끝나 씬에 루트가 배선되면 폴백 쪽은 지운다. 부르는 쪽은 결과를 들고 있는다.
        /// </summary>
        public static T Resolve<T>(GameScope scope, Func<GameScope, T> pick, Object owner, string fieldName)
            where T : Object
        {
            var picked = scope != null ? pick(scope) : null;
            return picked != null ? picked : SceneReferenceFallback.Find<T>(owner, fieldName);
        }
    }
}
