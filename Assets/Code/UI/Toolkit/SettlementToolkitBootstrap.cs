using UnityEngine;

namespace Code.UI.Toolkit
{
    /// <summary>
    /// 인게임의 정산·대출 화면을 UI Toolkit 문서로 교체하는 진입점.
    ///
    /// 옛 UGUI 패널을 여기서 끄지 않는다. 문서가 만들어진 뒤 화면 쪽에서 정산 관리자에게
    /// 자기가 맡았다고 알린다 — 문서가 없으면 옛 패널이 그대로 살아 있어야 한다.
    /// </summary>
    public static class SettlementToolkitBootstrap
    {
        private const string DocumentPath = "UI/Toolkit/SettlementPanel";
        private const string RootName = "SettlementToolkitUi";

        /// <summary>운영 HUD(100)보다 위에 떠야 정산 중에 HUD가 정산표를 가리지 않는다.</summary>
        private const int SortingOrder = 110;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            ToolkitDocumentSpawner.Install<SettlementToolkitView>(
                TitleMenuActions.GameSceneName, DocumentPath, RootName, SortingOrder);
        }
    }
}
