using Code.Stages;
using TMPro;
using UnityEngine;

namespace Code.UI.Stages
{
    /// <summary>
    /// 준비 단계에 고른 부하 한 명의 명령 막대(사수·기본·추격). 고정 크기이며 고른 부하가 없으면 통째로 숨긴다.
    /// 누구를 보여 줄지는 HUD가 정하고, 이 막대는 그리기와 누름 전달만 한다.
    /// </summary>
    public sealed class StageCommandBar : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private UiFrameButton hold;
        [SerializeField] private UiFrameButton normal;
        [SerializeField] private UiFrameButton pursue;
        private StageSession _session;
        private StageActor _actor;

        public void Show(StageSession session, StageActor actor)
        {
            if (session == null || actor == null)
            {
                Hide();
                return;
            }
            _session = session;
            _actor = actor;
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);
            label.text = $"{actor.Option?.Label} · {StageUnitCommandRules.Description(actor.Command)}";
            hold.SetSelected(actor.Command == StageUnitCommand.Hold);
            normal.SetSelected(actor.Command == StageUnitCommand.Default);
            pursue.SetSelected(actor.Command == StageUnitCommand.Pursue);
        }

        public void Hide()
        {
            _actor = null;
            if (gameObject.activeSelf)
                gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            Unbind();
            if (hold == null || normal == null || pursue == null || label == null)
            {
                Debug.LogError("스테이지 명령 막대의 글자·버튼 참조를 확인하세요", this);
                return;
            }
            hold.Button.onClick.AddListener(Hold);
            normal.Button.onClick.AddListener(Normal);
            pursue.Button.onClick.AddListener(Pursue);
        }

        private void OnDisable() => Unbind();

        private void Unbind()
        {
            if (hold != null && hold.Button != null)
                hold.Button.onClick.RemoveListener(Hold);
            if (normal != null && normal.Button != null)
                normal.Button.onClick.RemoveListener(Normal);
            if (pursue != null && pursue.Button != null)
                pursue.Button.onClick.RemoveListener(Pursue);
        }

        private void Hold() => Give(StageUnitCommand.Hold);
        private void Normal() => Give(StageUnitCommand.Default);
        private void Pursue() => Give(StageUnitCommand.Pursue);

        private void Give(StageUnitCommand command)
        {
            if (_session != null && _actor != null)
                _session.TrySetCommand(_actor, command);
        }
    }
}
