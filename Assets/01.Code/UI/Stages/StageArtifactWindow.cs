using Code.Stages;
using TMPro;
using UnityEngine;

namespace Code.UI.Stages
{
    /// <summary>
    /// 가운데 유물 창. 장착 모드는 가진 유물을 장착/해제하고, 보상 모드는 첫 클리어 후보 셋 중 하나를 고른다.
    /// 저장 가능한 상태에서 고르지 않은 후보가 있으면 스스로 열리고 고를 때까지 닫히지 않는다.
    /// 이 오브젝트는 켜 둔 채 패널만 여닫아 세션 변경을 계속 듣는다. 저장 오류 중에는 닫기와 타이틀 복귀를 허용한다.
    /// </summary>
    public sealed class StageArtifactWindow : MonoBehaviour, IEscapeClosable
    {
        [SerializeField] private StageSession session;
        [SerializeField, Tooltip("보상 모드에서만 켜는 화면 전체 클릭 차단 바탕.")]
        private GameObject blocker;
        [SerializeField] private GameObject panel;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text hint;
        [SerializeField] private UiFrameButton closeButton;
        [SerializeField] private StageArtifactRow[] rows;
        private StageArtifactPresenter _presenter;
        private bool _open;
        private bool _reward;
        private bool _rendered;
        private int _renderedKey;

        public bool IsOpen => _open;

        public void Open()
        {
            if (!EnsureRuntimeState())
                return;
            SetOpen(true);
            Refresh();
        }

        public void Close()
        {
            if (_reward && session != null && session.RequiresArtifactChoice)
                return;
            SetOpen(false);
        }

        private void SetOpen(bool open)
        {
            _open = open;
            _rendered = false;
            if (panel != null)
                panel.SetActive(open);
            if (blocker != null)
                blocker.SetActive(open && _reward && session != null && session.RequiresArtifactChoice);
        }

        internal void Choose(StageArtifact artifact)
        {
            if (session == null || session.Profile == null)
                return;
            if (!_reward)
            {
                session.ToggleArtifact(artifact);
                return;
            }
            if (session.TryPickArtifact(artifact))
            {
                _reward = false;
                SetOpen(false);
            }
        }

        private void Awake() => EnsureRuntimeState();

        private void OnEnable()
        {
            Unsubscribe();
            if (!EnsureRuntimeState())
                return;
            session.Changed += Refresh;
            closeButton.Button.onClick.AddListener(Close);
            foreach (StageArtifactRow row in rows)
                if (row != null)
                    row.Bind(this);
            SetOpen(_open);
            Refresh();
        }

        private void OnDisable()
        {
            Unsubscribe();
            if (rows != null)
                foreach (StageArtifactRow row in rows)
                    if (row != null)
                        row.Unbind();
        }

        private void Unsubscribe()
        {
            if (session is not null)
                session.Changed -= Refresh;
            if (closeButton != null && closeButton.Button != null)
                closeButton.Button.onClick.RemoveListener(Close);
        }

        private bool EnsureRuntimeState()
        {
            if (session == null || panel == null || title == null || hint == null || closeButton == null || rows == null)
            {
                Debug.LogError("스테이지 유물 창의 세션·패널·글자·버튼 참조를 확인하세요", this);
                return false;
            }
            _presenter ??= new StageArtifactPresenter();
            return true;
        }

        private void Refresh()
        {
            if (!isActiveAndEnabled || session.Profile == null || session.Catalog == null || _presenter == null)
                return;
            bool reward = session.HasArtifactChoice;
            bool requiresChoice = session.RequiresArtifactChoice;
            if (requiresChoice && !_open)
            {
                _reward = true;
                SetOpen(true);
            }
            if (!_open)
                return;
            StageCatalogSO catalog = session.Catalog;
            int slots = catalog.ArtifactSlots;
            int equipped = session.Artifacts.Equipped.Count;
            // 세션은 전투 중 적이 오갈 때마다 바뀐다. 줄 내용을 정하는 값이 그대로면 다시 쓰지 않는다.
            int key = System.HashCode.Combine(reward, session.CanChoose, equipped,
                session.Profile.Artifacts.Count, session.Profile.ArtifactChoices.Count, requiresChoice,
                session.Profile.CanWrite, session.Profile.Error);
            if (_rendered && key == _renderedKey)
                return;
            _rendered = true;
            _renderedKey = key;
            _reward = reward;
            if (blocker != null)
                blocker.SetActive(requiresChoice);
            int used = 0;
            if (_reward)
            {
                foreach (string id in session.Profile.ArtifactChoices)
                {
                    StageArtifact artifact = catalog.FindArtifact(id);
                    if (artifact != null && used < rows.Length)
                        rows[used++].Show(artifact, _presenter.Row(artifact, catalog), "고르기", session.Profile.CanWrite, false);
                }
            }
            else
            {
                foreach (StageArtifact artifact in catalog.Artifacts)
                {
                    if (!session.HasArtifact(artifact) || used >= rows.Length)
                        continue;
                    bool on = session.Artifacts.IsEquipped(artifact);
                    bool can = session.CanChoose && (on || equipped < slots);
                    rows[used++].Show(artifact, _presenter.Row(artifact, catalog), on ? "해제" : "장착", can, on);
                }
            }
            for (int i = used; i < rows.Length; i++)
                if (rows[i] != null)
                    rows[i].Hide();
            closeButton.SetVisible(!requiresChoice);
            closeButton.SetInteractable(!requiresChoice);
            title.text = _reward ? "유물 보상" : "유물 장착";
            hint.text = _reward && !requiresChoice ? "유물 저장 오류 · 후보는 유지됩니다\n닫기 후 타이틀로 돌아갈 수 있습니다"
                : _reward ? "스테이지 첫 클리어 보상입니다. 하나를 골라 계속 가집니다"
                : used == 0 ? "아직 가진 유물이 없습니다. 스테이지를 처음 클리어하면 셋 중 하나를 얻습니다"
                : session.CanChoose ? $"장착 {equipped}/{slots} · 둘 다 장착해야 조합이 성립합니다 · 첫 웨이브 전까지 바꿀 수 있습니다"
                : $"장착 {equipped}/{slots} · 편성이 고정되어 지금은 바꿀 수 없습니다";
        }
    }
}
