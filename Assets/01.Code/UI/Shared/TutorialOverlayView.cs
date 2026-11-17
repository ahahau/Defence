using TMPro;
using UnityEngine;

namespace Code.UI
{
    /// <summary>
    /// 첫날 안내 덮개(uGUI, A안). 어느 칸인지·어디를 뚫을지는 <see cref="PlayTutorialView"/>가 정하고,
    /// 이 화면은 안내 카드와 네 조각 어둠을 그리고 건너뛰기만 돌려준다.
    /// 팝업(캔버스 100)보다 낮은 순서라 방 확장 확인 팝업은 덮개 위에서 눌린다.
    /// </summary>
    public sealed class TutorialOverlayView : MonoBehaviour
    {
        [SerializeField] private PlayTutorialView tutorial;
        [SerializeField] private GameObject hint;
        [SerializeField] private TMP_Text header;
        [SerializeField] private TMP_Text body;
        [SerializeField] private UiFrameButton skipButton;
        [SerializeField] private UiSpotlightDim spotlight;

        private void OnEnable()
        {
            skipButton?.Button.onClick.AddListener(HandleSkip);
            if (tutorial != null)
                tutorial.OverlayChanged += Render;
            Render();
        }

        private void OnDisable()
        {
            skipButton?.Button.onClick.RemoveListener(HandleSkip);
            if (tutorial != null)
                tutorial.OverlayChanged -= Render;
        }

        private void Render()
        {
            // 안내가 꺼져 있으면(이어하기·씬에서 끔) 아무것도 덮지 않는다.
            var active = tutorial != null && tutorial.isActiveAndEnabled;
            var showHint = active && tutorial.IsHintVisible;
            if (hint != null && hint.activeSelf != showHint)
                hint.SetActive(showHint);

            if (showHint)
            {
                SetText(header, tutorial.HintHeader);
                SetText(body, tutorial.HintText);
            }

            if (active && tutorial.IsSpotlightVisible)
                spotlight?.Show(tutorial.SpotlightHole, tutorial.SpotlightPadding);
            else
                spotlight?.Hide();
        }

        private void HandleSkip()
        {
            if (tutorial != null)
                tutorial.SkipTutorial();
        }

        private static void SetText(TMP_Text label, string value)
        {
            if (label != null && label.text != (value ?? string.Empty))
                label.text = value ?? string.Empty;
        }
    }
}
