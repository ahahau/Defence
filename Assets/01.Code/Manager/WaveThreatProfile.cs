using _01.Code.BT;
using _01.Code.Enemies;

namespace _01.Code.Manager
{
    /// <summary>대기 화면에서 보여 줄 다음 습격의 핵심 문제와 대응법.</summary>
    public readonly struct WaveThreatPreview
    {
        public WaveThreatPreview(string title, string counterHint)
        {
            Title = title ?? string.Empty;
            CounterHint = counterHint ?? string.Empty;
        }

        public string Title { get; }
        public string CounterHint { get; }
        public bool IsEmpty => string.IsNullOrWhiteSpace(Title) && string.IsNullOrWhiteSpace(CounterHint);
    }

    /// <summary>
    /// 파티 역할을 플레이어가 바로 이해할 수 있는 위협 문장으로 바꾼다.
    /// 작성자가 일자별 문구를 넣으면 그 문구가 우선하고, 빠졌을 때만 역할 조합으로 추론한다.
    /// </summary>
    public static class WaveThreatProfile
    {
        public static WaveThreatPreview Build(
            string authoredTitle,
            string authoredCounterHint,
            AdventurerPartySO party)
        {
            var melee = 0;
            var ranged = 0;
            var support = 0;
            var tank = 0;

            if (party?.Members != null)
            {
                foreach (EnemyDataSO member in party.Members)
                {
                    if (member == null)
                        continue;

                    switch (member.Role)
                    {
                        case BattleRole.Ranged: ranged++; break;
                        case BattleRole.Support: support++; break;
                        case BattleRole.Tank: tank++; break;
                        default: melee++; break;
                    }
                }
            }

            return BuildForRoleCounts(authoredTitle, authoredCounterHint, melee, ranged, support, tank);
        }

        public static WaveThreatPreview BuildForRoleCounts(
            string authoredTitle,
            string authoredCounterHint,
            int melee,
            int ranged,
            int support,
            int tank)
        {
            var title = authoredTitle;
            var hint = authoredCounterHint;
            var total = melee + ranged + support + tank;

            if (string.IsNullOrWhiteSpace(title))
            {
                title = support > 0 ? "치유사 호위대"
                    : ranged >= 2 ? "원거리 사격대"
                    : tank > 0 || melee >= 2 ? "중장 전열"
                    : total >= 4 ? "다수 공세"
                    : "혼성 모험가 파티";
            }

            if (string.IsNullOrWhiteSpace(hint))
            {
                hint = support > 0 ? "공격 명령으로 치유사를 먼저 끊으세요"
                    : ranged >= 2 ? "경계 명령으로 후열을 지키고 돌격대로 궁수를 압박하세요"
                    : tank > 0 || melee >= 2 ? "공격 명령으로 전열 너머 후방을 노리세요"
                    : total >= 4 ? "함정과 던전 권능으로 수적 우세를 줄이세요"
                    : "적 역할을 확인하고 배치와 명령을 조정하세요";
            }

            return new WaveThreatPreview(title, hint);
        }
    }
}
