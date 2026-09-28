using Code.UI;
using UnityEngine;
namespace Code.UI.Toolkit
{
 public static class RunEndToolkitBootstrap
 {
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] private static void Install() =>
   ToolkitDocumentSpawner.Install<RunEndToolkitView>(TitleMenuActions.GameSceneName, "UI/Toolkit/RunEndPanel", "RunEndToolkitUi", 260);
 }
}
