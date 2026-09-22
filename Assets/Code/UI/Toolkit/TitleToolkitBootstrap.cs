using UnityEngine;

namespace Code.UI.Toolkit
{
    /// <summary>
    /// 타이틀 화면의 UGUI를 UI Toolkit 문서로 교체하는 진입점.
    ///
    /// 씬 파일에 새 오브젝트를 직접 심지 않는다. 문서와 패널 설정은 Resources에서 읽고,
    /// Start 씬일 때만 만든다. 그래서 인게임의 기존 UGUI와 전환 작업이 서로 간섭하지 않는다.
    /// </summary>
    public static class TitleToolkitBootstrap
    {
        private const string DocumentPath = "UI/Toolkit/TitleScreen";
        private const string RootName = "TitleToolkitUi";
        private const int SortingOrder = 100;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            ToolkitDocumentSpawner.Install<TitleToolkitView>(
                TitleMenuActions.TitleSceneName, DocumentPath, RootName, SortingOrder, HideLegacyCanvases);
        }

        /// <summary>
        /// 새 문서가 정상적으로 만들어진 뒤에만 제목 씬의 옛 캔버스를 숨긴다.
        /// 다른 씬의 UGUI는 아직 전환 대상이 아니므로 건드리지 않는다.
        /// </summary>
        private static void HideLegacyCanvases()
        {
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                canvas.gameObject.SetActive(false);
        }
    }
}
