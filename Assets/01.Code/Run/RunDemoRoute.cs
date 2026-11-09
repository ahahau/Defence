namespace Code.Run
{
    /// <summary>시연판 고정 지도의 칸 진입과 결과 확정을 관리한다.</summary>
    public sealed class RunDemoRoute
    {
        public enum Node
        {
            ThiefDefense,
            Merchant,
            StoryEvent,
            SapperDefense,
            OutpostRaid,
            EliteDefense,
        }

        public enum Result
        {
            Pending,
            Completed,
            Victory,
            Defeat,
            Retreat,
        }

        public Node Current { get; private set; } = Node.ThiefDefense;
        public Result CurrentResult { get; private set; }
        public Node? ChosenBranch { get; private set; }
        public Result RaidResult { get; private set; }
        public bool IsFinished => CurrentResult != Result.Pending
            && (Current == Node.EliteDefense || (Current != Node.OutpostRaid && CurrentResult == Result.Defeat));

        /// <summary>현재 칸의 결과를 한 번만 확정한다.</summary>
        public bool TryResolve(Result result)
        {
            if (CurrentResult != Result.Pending) return false;
            bool allowed = Current switch
            {
                Node.Merchant or Node.StoryEvent => result == Result.Completed,
                Node.OutpostRaid => result is Result.Victory or Result.Defeat or Result.Retreat,
                _ => result is Result.Victory or Result.Defeat,
            };
            if (!allowed) return false;

            CurrentResult = result;
            if (Current == Node.OutpostRaid) RaidResult = result;
            return true;
        }

        /// <summary>지도에서 다음으로 열리는 칸인지 확인한다.</summary>
        public bool CanEnter(Node node)
        {
            if (CurrentResult == Result.Pending || IsFinished) return false;
            return Current switch
            {
                Node.ThiefDefense => node is Node.Merchant or Node.StoryEvent,
                Node.Merchant or Node.StoryEvent => node == Node.SapperDefense,
                Node.SapperDefense => node == Node.OutpostRaid,
                Node.OutpostRaid => node == Node.EliteDefense,
                _ => false,
            };
        }

        /// <summary>현재 칸을 해결한 뒤 열리는 칸으로만 전진한다.</summary>
        public bool TryEnter(Node node)
        {
            if (!CanEnter(node)) return false;
            Current = node;
            CurrentResult = Result.Pending;
            if (node is Node.Merchant or Node.StoryEvent) ChosenBranch = node;
            return true;
        }

        internal static RunDemoRoute Restore(Node current, Result currentResult, Node? branch, Result raidResult)
        {
            RunDemoRoute route = new();
            if (current != Node.ThiefDefense)
            {
                if (branch != Node.Merchant && branch != Node.StoryEvent) return null;
                route.TryResolve(Result.Victory);
                route.TryEnter(branch.Value);
                if (current != branch)
                {
                    route.TryResolve(Result.Completed);
                    route.TryEnter(Node.SapperDefense);
                    if (current is Node.OutpostRaid or Node.EliteDefense)
                    {
                        route.TryResolve(Result.Victory);
                        route.TryEnter(Node.OutpostRaid);
                        if (current == Node.EliteDefense
                            && (!route.TryResolve(raidResult) || !route.TryEnter(Node.EliteDefense))) return null;
                    }
                }
            }
            if (route.Current != current || route.ChosenBranch != branch) return null;
            if (currentResult != Result.Pending && !route.TryResolve(currentResult)) return null;
            return route.RaidResult == raidResult ? route : null;
        }
    }
}
