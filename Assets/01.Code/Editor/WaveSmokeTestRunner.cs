using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace _01.Code.Editor
{
    /// <summary>
    /// PlayMode 도메인 리로드 뒤에도 콜백을 다시 등록해 대표 웨이브 회귀 검사 결과를 남긴다.
    /// </summary>
    [InitializeOnLoad]
    public static class WaveSmokeTestRunner
    {
        public const string DoneKey = "Defence.PlayMode.Result.Done";
        public const string PassKey = "Defence.PlayMode.Result.Pass";
        public const string FailKey = "Defence.PlayMode.Result.Fail";
        public const string SkipKey = "Defence.PlayMode.Result.Skip";
        public const string FailuresKey = "Defence.PlayMode.Result.Failures";
        private const string RunningKey = "Defence.PlayMode.Result.Running";

        private static TestRunnerApi _api;
        private static ResultCallbacks _callbacks;

        static WaveSmokeTestRunner()
        {
            if (SessionState.GetBool(RunningKey, false))
                EnsureCallbacks();
        }

        [MenuItem("Tools/Defence/Playtest/자동 회귀 검사 실행", priority = 230)]
        public static void Run()
        {
            if (SessionState.GetBool(RunningKey, false))
            {
                Debug.LogWarning("[Wave Playtest] 자동 회귀 검사가 이미 진행 중입니다.");
                return;
            }

            SessionState.SetBool(DoneKey, false);
            SessionState.SetBool(RunningKey, true);
            SessionState.SetInt(PassKey, 0);
            SessionState.SetInt(FailKey, 0);
            SessionState.SetInt(SkipKey, 0);
            SessionState.SetString(FailuresKey, string.Empty);

            EnsureCallbacks();
            _api.Execute(new ExecutionSettings(new Filter
            {
                testMode = TestMode.PlayMode
            }));
            Debug.Log("[Wave Playtest] 자동 회귀 검사를 시작했습니다.");
        }

        private static void EnsureCallbacks()
        {
            if (_api == null)
            {
                _api = ScriptableObject.CreateInstance<TestRunnerApi>();
                _api.hideFlags = HideFlags.HideAndDontSave;
            }

            _callbacks ??= new ResultCallbacks();
            _api.RegisterCallbacks(_callbacks);
        }

        private sealed class ResultCallbacks : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }

            public void RunFinished(ITestResultAdaptor result)
            {
                var failures = new StringBuilder();
                AppendFailures(result, failures);
                SessionState.SetInt(PassKey, result.PassCount);
                SessionState.SetInt(FailKey, result.FailCount);
                SessionState.SetInt(SkipKey, result.SkipCount + result.InconclusiveCount);
                SessionState.SetString(FailuresKey, failures.ToString());
                SessionState.SetBool(DoneKey, true);
                SessionState.SetBool(RunningKey, false);

                if (result.FailCount == 0)
                    Debug.Log($"[Wave Playtest][PASS] PlayMode {result.PassCount}개 통과, 건너뜀 {result.SkipCount + result.InconclusiveCount}개");
                else
                    Debug.LogError($"[Wave Playtest][FAIL] PlayMode {result.FailCount}개 실패\n{failures}");
            }

            private static void AppendFailures(ITestResultAdaptor result, StringBuilder text)
            {
                if (result.HasChildren)
                {
                    foreach (var child in result.Children)
                        AppendFailures(child, text);
                    return;
                }

                if (result.TestStatus != TestStatus.Passed)
                    text.AppendLine($"{result.FullName}: {result.Message}");
            }
        }
    }
}
