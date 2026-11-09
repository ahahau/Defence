namespace Code.Run
{
    /// <summary>한 실행의 DungeonRun 흐름이 소유한다. 사용자 저장이나 SO에 기록하지 않는다.</summary>
    public class RunDeploymentMemory
    {
        private RunDeployment _previous;

        public bool HasPrevious => _previous != null;
        public void Remember(RunDeployment deployment) => _previous = deployment.Copy();
        public bool Restore(RunDeployment deployment) => deployment.Load(_previous);
    }
}
