using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace _01.Code.Editor
{
    /// <summary>
    /// 윈도우 플레이어를 하나 낸다. <c>-executeMethod _01.Code.Editor.PlayerBuilder.BuildWindows</c>.
    ///
    /// 에디터에서 느린 것이 빌드에서도 느린지 재려면 실물이 있어야 한다.
    /// 결과물은 저장소 밖(Build/)에 떨어지고 .gitignore 가 이미 막고 있다.
    /// </summary>
    public static class PlayerBuilder
    {
        private const string OutputPath = "Build/Defence.exe";

        [MenuItem("Tools/Defence/윈도우 빌드", priority = 300)]
        public static void BuildWindows()
        {
            var scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                Debug.LogError("[빌드] 빌드 설정에 켜진 씬이 없습니다.");
                EditorApplication.Exit(1);
                return;
            }

            Debug.Log("[빌드] 씬 " + scenes.Length + "개: " + string.Join(", ", scenes));

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = OutputPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };

            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;

            Debug.Log("[빌드] 결과 " + summary.result
                      + " · " + (summary.totalSize / (1024 * 1024)) + "MB"
                      + " · " + summary.totalTime.TotalSeconds.ToString("F0") + "초"
                      + " · 오류 " + summary.totalErrors);

            if (Application.isBatchMode)
                EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
