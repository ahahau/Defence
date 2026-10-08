using Code.UI;
using UnityEngine;
namespace Code.UI.Toolkit
{
    public static class RunEndToolkitBootstrap
    {
        /// <summary>
        /// 패배·승리 화면은 판의 마지막 화면이라 모든 uGUI 위에 떠야 한다. 공용 설정(Sort Order 0)이면
        /// 메인 캔버스(50)의 패널이 어두운 배경 위로 비친다. 팝업(10000)보다는 아래에 둔다.
        /// </summary>
        private const string PanelSettingsPath = "UI/Toolkit/RunEndPanelSettings";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install() =>
            ToolkitDocumentSpawner.Install<RunEndToolkitView>(
                TitleMenuActions.GameSceneName, "UI/Toolkit/RunEndPanel", "RunEndToolkitUi", 260,
                panelSettingsPath: PanelSettingsPath);
    }
}
