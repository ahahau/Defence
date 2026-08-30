namespace _01.Code.Core.Stats
{
    /// <summary>
    /// 스탯 창구. 다른 모듈은 이 계약만 알고 쓰므로 스탯 보관 방식이 바뀌어도 호출부는 그대로다.
    /// </summary>
    public interface IStatModule
    {
        StatSO[] GetAllStats();
        StatSO GetStat(int statIndex);
        bool TryGetStat(int statIndex, out StatSO stat);

        void AddModifier(int statIndex, object key, float value);
        void SetModifier(int statIndex, object key, float additive, float multiplier = 1f);
        void RemoveModifier(int statIndex, object key);

        /// <summary>
        /// 값 변화를 구독하고 현재 값을 돌려준다. 스탯이 없으면 기본값을 그대로 돌려주므로,
        /// 스탯을 안 붙인 프리팹도 동작이 멈추지 않는다.
        /// </summary>
        float SubscribeStat(int statIndex, StatSO.ValueChangeHandler handler, float fallbackValue);

        void UnsubscribeStat(int statIndex, StatSO.ValueChangeHandler handler);
    }
}
