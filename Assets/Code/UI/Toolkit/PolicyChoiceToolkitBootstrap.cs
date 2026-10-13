using Code.UI;
using UnityEngine;

namespace Code.UI.Toolkit
{
    public static class PolicyChoiceToolkitBootstrap
    {
        /// <summary>
        /// 운영 방침은 시간을 세우는 강제 선택이라 모든 uGUI 위에 떠야 한다. 공용 설정(Sort Order 0)이면 하루 안내
        /// 배너 같은 메인 캔버스(50) 요소가 선택지를 덮는다. 패배 화면과 같은 패널을 쓰되 문서 순서는 그보다 낮게 둔다.
        /// </summary>
        private const string PanelSettingsPath = "UI/Toolkit/RunEndPanelSettings";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            ToolkitDocumentSpawner.Install<PolicyChoiceToolkitView>(TitleMenuActions.GameSceneName,
                "UI/Toolkit/PolicyChoicePanel", "PolicyChoiceToolkitUi", 240, HideLegacyView, PanelSettingsPath);
        }

        private static void HideLegacyView()
        {
            foreach (var view in Object.FindObjectsByType<PolicyChoicePanelView>(FindObjectsInactive.Exclude))
                view.enabled = false;
        }
    }
}
