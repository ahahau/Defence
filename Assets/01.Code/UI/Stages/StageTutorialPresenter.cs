using Code.Buildings;
using Code.Stages;

namespace Code.UI.Stages
{
    /// <summary>첫 클리어 전의 현재 편성·배치·진행에서 다음 행동을 고른다. 게임 상태는 바꾸지 않는다.</summary>
    internal static class StageTutorialPresenter
    {
        internal static string MessageFor(StageSession session)
        {
            if (session != null && session.Catalog != null && session.Catalog.IsTrapCampaign)
                return session.Phase == StagePhase.Preparing
                    ? "함정 공략 준비\n바리케이드로 동선을 좁히고 속박·지속 함정을 이어 놓으세요.\n금색 화살표를 가리키고 R로 밀치기 방향을 바꿉니다.\n검은 구덩이로 밀어 떨어뜨릴 길을 남겨두세요."
                    : session.Phase == StagePhase.Fighting
                        ? "직접 발동 · 금색 장치 칸 클릭\n적이 모일 때 밀치기·낙석을 사용하세요.\n무거운 적·보스는 짧게 밀리며 부하 스킬은 자동입니다."
                        : string.Empty;
            if (session != null && session.Catalog != null && session.Catalog.IsTrapLab)
                return session.Phase == StagePhase.Preparing
                    ? "함정 공략 연습\n속박 수정과 핏빛 웅덩이를 통로에 배치하세요.\n적을 모아 금색 화살표로 주황 구역에 미세요.\n실험 진행은 저장되지 않습니다."
                    : session.Phase == StagePhase.Fighting
                        ? "직접 발동 · 금색 화살표 칸 클릭\n적이 모이면 눌러 주황 위험 구역으로 미세요.\n금색은 준비, 보라색 막대는 재사용 대기입니다."
                        : string.Empty;
            if (session == null || session.StageIndex != 0 || session.Definition == null
                || session.Profile == null || session.Catalog == null || session.Board == null
                || session.Placement == null || session.Profile.IsCleared(session.Definition.Id)
                || !string.IsNullOrEmpty(session.Profile.Error))
            {
                return string.Empty;
            }

            if (session.Phase == StagePhase.Fighting)
            {
                foreach (StageActor actor in session.Board.Actors)
                {
                    if (actor != null && !actor.IsEnemy && actor.IsAlive && actor.Skills != null && actor.Skills.HasUltimate)
                    {
                        return "첫 방어 4/5 · 전투\n배치·철거는 준비 때만 가능합니다.\n부하의 게이지가 금색이면 눌러 궁극기를 쓰세요.";
                    }
                }
                return "첫 방어 4/5 · 전투\n적이 코어에 닿으면 피해를 줍니다.\n쓰러진 부하는 웨이브가 끝나면 복구됩니다.";
            }
            if (session.Phase != StagePhase.Preparing)
            {
                return string.Empty;
            }
            if (session.WaveIndex > 0)
            {
                return session.Results.Latest != null
                    ? "첫 방어 5/5 · 코어 피해·금화를 확인하고 다음 배치를 준비하세요"
                    : "다음 방어 준비\n코어 체력은 이어집니다.\n배치를 보완하고 웨이브 시작을 누르세요.";
            }
            if (session.SelectedCount == 0)
            {
                return "첫 방어 1/5 · 금고와 편성\n코어가 0이면 패배합니다.\n노란 경로는 입구에서 코어로 가는 길입니다.\n왼쪽에서 부하·함정을 편성하세요.";
            }

            bool hasUnit = false;
            foreach (StageActor actor in session.Board.Actors)
            {
                if (actor != null && !actor.IsEnemy)
                {
                    hasUnit = true;
                    break;
                }
            }
            bool hasTrap = false;
            foreach (Trap trap in session.Traps)
            {
                if (trap != null && !trap.IsDestroyed)
                {
                    hasTrap = true;
                    break;
                }
            }
            bool hasWall = session.Board.Barricades.Count > 0;
            if (!hasWall && !hasUnit && !hasTrap && session.Gold >= session.Definition.BarricadeCost)
            {
                return "첫 방어 2/5 · 길 만들기\n바리케이드로 노란 동선을 바꿔 보세요.\n완전히 막으면 적이 부수고 들어옵니다.\n부하·함정부터 배치해도 됩니다.";
            }

            bool canBuyUnit = false;
            bool canBuyTrap = false;
            foreach (StageOption option in session.Catalog.Options)
            {
                if (option == null || !session.IsSelected(option) || option.Cost > session.Gold)
                {
                    continue;
                }
                canBuyUnit |= option.Unit != null && option.Unit.Prefab != null;
                canBuyTrap |= option.Trap != null && option.Trap.Prefab is Trap;
            }
            if (!hasUnit && canBuyUnit)
            {
                return "첫 방어 3/5 · 부하 배치\n편성한 부하의 배치 선택을 누르고 바닥을 누르세요.\n부하는 근처 적과 싸우고 원래 자리로 돌아옵니다.";
            }
            if (!hasTrap && canBuyTrap)
            {
                return "첫 방어 3/5 · 함정 배치\n편성한 함정의 배치 선택을 누르고 노란 경로에 놓아 보세요.\n범위·지속 함정은 아군도 다치게 합니다.";
            }
            if (!hasWall && !hasUnit && !hasTrap)
            {
                return "첫 방어 · 배치 준비\n편성과 금화를 확인하세요.\n다른 종류를 고르거나 스테이지 재도전으로 다시 준비할 수 있습니다.";
            }
            return "첫 방어 4/5 · 방어 시작\n준비됐다면 웨이브 시작을 누르세요.\n첫 시작 뒤에는 편성이 고정됩니다.\n배치·철거는 다음 준비 단계에도 가능합니다.";
        }
    }
}
