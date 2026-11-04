using Code.Stages;
using Code.UI.Popup;

namespace Code.UI.Stages
{
    /// <summary>
    /// 마지막 스테이지를 처음 깼을 때 한 번 띄우는 엔딩(2026-11-02 사용자 결정: 유한 종료, 엔딩 뒤 자유 재도전).
    /// 따로 기록하지 않고 이번 클리어가 초회 보상이었는지(<see cref="StageWaveResults.RewardPreviouslyClaimed"/>)로 한 번만 띄운다.
    /// </summary>
    public sealed class StageEndingPopup
    {
        private const string Tag = "stage-ending";
        private readonly StageSession _session;
        private bool _shown;

        public StageEndingPopup(StageSession session) => _session = session;

        /// <summary>설정·유물 창·다른 팝업이 있으면 <paramref name="blocked"/>로 미룬다. 초회 유물 후보가 남아 있으면 고른 뒤에 띄운다.</summary>
        public void Tick(bool blocked)
        {
            // 같은 클리어 화면에서는 한 번만. 클리어 화면을 떠나면 푼다(다시 깨면 초회가 아니라 어차피 뜨지 않는다).
            if (_session.Phase != StagePhase.Cleared)
            {
                _shown = false;
                return;
            }
            if (_shown)
                return;
            StageWaveResults results = _session.Results;
            if (blocked || PopupController.IsOpen || _session.HasArtifactChoice || !results.RewardConfirmed
                || results.RewardPreviouslyClaimed || _session.StageIndex != _session.Catalog.Stages.Length - 1)
                return;
            _shown = true;
            int stages = _session.Catalog.Stages.Length;
            // 팝업 본문은 508×104(17pt)라 한 줄 29자 안쪽 4줄로 쓴다
            string message = "임프 참모: \"마지막 금고까지 지켜냈습니다.\"\n"
                + $"스테이지 {stages}/{stages} 방어 완료\n"
                + $"강화 재화 {_session.Profile.Currency} · 유물 {_session.Profile.Artifacts.Count}개\n"
                + "이제 어느 스테이지든 다시 도전할 수 있습니다";
            PopupController.Show(PopupRequest.Notice("금고를 지켜냈습니다", message).WithTag(Tag));
        }
    }
}
