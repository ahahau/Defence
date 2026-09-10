using _01.Code.Buildings;
using _01.Code.MapCreateSystem;
using _01.Code.Tutorial;
using _01.Code.Manager;
using _01.Code.Units;
using TMPro;
using UnityEngine;

namespace _01.Code.UI
{
    /// <summary>
    /// 첫 판을 하는 동안 한 줄씩 따라오는 안내.
    ///
    /// 시작할 때 할 일을 전부 늘어놓는 방식이었는데, 여섯 줄을 읽고 나면 첫 줄이 기억나지 않는다.
    /// 읽는 것과 하는 것 사이가 멀수록 안 남으므로, 지금 할 일 하나만 띄우고 그것을 해내면
    /// 다음으로 넘어간다.
    ///
    /// 진행은 판의 상태를 직접 보고 정한다. 처음에는 게임이 쏘는 이벤트를 들었는데,
    /// BuildingInstalledEvent 는 방 패널(UI)에서만 쏜다. 그래서 UI를 거치지 않고 짓는 경로 —
    /// 자동 실측기가 그렇다 — 에서는 아무것도 안 짚였고, 튜토리얼이 첫 칸에 멈춘 채로 있었다.
    /// 무엇을 눌렀는지가 아니라 무엇이 생겼는지를 봐야 경로와 무관해진다.
    ///
    /// 지금 칸에서 할 일 말고는 누르지 못하게 막는다. 첫 판에 무엇부터 눌러야 하는지 모르는
    /// 사람에게는 글보다 이쪽이 확실하다. 잠그는 일 자체는 <see cref="TutorialInputGate"/>가
    /// 이미 하고 있고 화면들이 그것을 지키므로, 여기서는 칸마다 어디를 열어 둘지만 정한다.
    ///
    /// 잠그는 쪽은 잘못 짚었을 때의 대가가 크다 — 아무것도 못 누르는 판이 된다. 그래서 겨눌 곳을
    /// 못 찾으면 잠그지 않고, 한 칸에서 오래 막혀 있으면 스스로 풀어 준다. 안내가 틀리는 것보다
    /// 판이 멈추는 것이 훨씬 나쁘다.
    /// </summary>
    public sealed class PlayTutorialView : MonoBehaviour
    {
        private enum Step
        {
            BuildRoom,
            DeployUnit,
            BuildPortal,
            SurviveWave,
            Done,
        }

        [Header("UI")]
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text hintText;

        [SerializeField, Min(0.05f), Tooltip("판을 다시 살펴보는 간격(초).")]
        private float pollInterval = 0.25f;

        [SerializeField, Tooltip("지금 칸에서 할 일 말고는 누르지 못하게 막는다.")]
        private bool forceStepOrder = true;

        [SerializeField, Min(5f),
         Tooltip("한 칸에서 이만큼 막혀 있으면 잠금을 푼다. 안내가 길을 잘못 짚어도 판이 멈추지 않게 하는 안전장치.")]
        private float stuckReleaseSeconds = 40f;

        private Step _step = Step.BuildRoom;
        private float _timer;
        private float _stepAge;
        private bool _released;

        private void OnEnable()
        {
            _timer = 0f;
            _stepAge = 0f;
            _released = false;
            Render();
        }

        private void OnDisable()
        {
            TutorialInputGate.Clear();
        }

        private void Update()
        {
            if (_step == Step.Done)
                return;

            _stepAge += Time.unscaledDeltaTime;

            // 매 프레임 씬을 뒤질 일은 아니다. 사람이 방을 짓는 속도에 견주면 0.25초도 즉시다.
            _timer -= Time.unscaledDeltaTime;
            if (_timer > 0f)
                return;

            _timer = pollInterval;

            var next = Resolve(_step);
            if (next != _step)
            {
                _step = next;
                _stepAge = 0f;
                _released = false;
                Render();

                if (_step == Step.Done)
                {
                    TutorialInputGate.Clear();
                    return;
                }
            }

            ApplyGate();
        }

        /// <summary>
        /// 지금 칸에서 할 일만 누를 수 있게 잠근다.
        ///
        /// 잘못 짚으면 아무것도 못 누르는 판이 된다. 그래서 겨눌 곳을 못 찾으면 잠그지 않고,
        /// 한 칸에서 <see cref="stuckReleaseSeconds"/>만큼 막혀 있으면 스스로 잠금을 푼다.
        /// 안내가 틀리는 것보다 판이 멈추는 것이 훨씬 나쁘다.
        /// </summary>
        private void ApplyGate()
        {
            if (!forceStepOrder || _released)
                return;

            if (_stepAge >= stuckReleaseSeconds)
            {
                _released = true;
                TutorialInputGate.Clear();
                Debug.LogWarning($"[튜토리얼] {_step} 에서 {stuckReleaseSeconds:0}초 동안 진행이 없어 잠금을 풉니다.");
                return;
            }

            switch (_step)
            {
                case Step.BuildRoom:
                    // 봉인을 여는 것과 그 방에 짓는 것이 한 칸 안에 함께 있다. 지금 무엇을 할 수
                    // 있는 상태인지 보고 자물쇠를 옮겨야 중간에서 막히지 않는다.
                    var buildable = FindUnlockedEmptyNode();
                    if (buildable != null)
                        TutorialInputGate.OnlyUnlockedNode(buildable);
                    else if (FindLockedNode() is { } locked)
                        TutorialInputGate.OnlyLockedNode(locked);
                    else
                        TutorialInputGate.Clear();
                    break;

                case Step.DeployUnit:
                    // 고용과 배치 모두 이 칸이다. 겨눌 방을 못 고르면 잠그지 않는다.
                    var host = FindUnlockedBuiltNode();
                    if (host != null)
                        TutorialInputGate.OnlyDeployUnit(host, null);
                    else
                        TutorialInputGate.Clear();
                    break;

                case Step.BuildPortal:
                    var entrance = FindUnlockedEmptyNode();
                    if (entrance != null)
                        TutorialInputGate.OnlyInstallPortal(entrance);
                    else
                        TutorialInputGate.Clear();
                    break;

                case Step.SurviveWave:
                    TutorialInputGate.OnlyWaveStart();
                    break;

                default:
                    TutorialInputGate.Clear();
                    break;
            }
        }

        private static Node FindLockedNode()
        {
            foreach (var node in Node.AllInstances)
            {
                if (node != null && node.name.StartsWith("LockedNode_", System.StringComparison.Ordinal))
                    return node;
            }

            return null;
        }

        /// <summary>열려 있고 아직 비어 있는 방. 지을 자리를 겨눌 때 쓴다.</summary>
        private static Node FindUnlockedEmptyNode()
        {
            foreach (var node in Node.AllInstances)
            {
                if (node != null
                    && !node.name.StartsWith("LockedNode_", System.StringComparison.Ordinal)
                    && !node.HasAssignedBuilding)
                    return node;
            }

            return null;
        }

        /// <summary>이미 무언가 지어 둔 방. 유닛을 세울 자리를 겨눌 때 쓴다.</summary>
        private static Node FindUnlockedBuiltNode()
        {
            foreach (var node in Node.AllInstances)
            {
                if (node != null && node.HasAssignedBuilding && node.AssignedBuilding is not Portal)
                    return node;
            }

            return null;
        }

        /// <summary>이 칸이 끝났는가. 끝났으면 다음 칸을, 아니면 그대로 돌려준다.</summary>
        private static Step Resolve(Step step) => step switch
        {
            Step.BuildRoom => HasNonPortalBuilding ? Step.DeployUnit : step,
            Step.DeployUnit => HasDeployedUnit ? Step.BuildPortal : step,
            Step.BuildPortal => HasPortal ? Step.SurviveWave : step,
            Step.SurviveWave => IsWaveRunning ? Step.Done : step,
            _ => Step.Done,
        };

        /// <summary>포탈 말고 지어 둔 것이 있는가. 포탈은 따로 짚어야 하므로 여기서 뺀다.</summary>
        private static bool HasNonPortalBuilding
        {
            get
            {
                var buildings = FindObjectsByType<Building>(FindObjectsSortMode.None);
                for (var i = 0; i < buildings.Length; i++)
                {
                    if (buildings[i] != null && buildings[i] is not Portal)
                        return true;
                }

                return false;
            }
        }

        private static bool HasDeployedUnit =>
            FindObjectsByType<Unit>(FindObjectsSortMode.None).Length > 0;

        private static bool HasPortal =>
            WaveManager.Current != null && WaveManager.Current.HasPortal;

        private static bool IsWaveRunning =>
            WaveManager.Current != null && WaveManager.Current.IsWaveRunning;

        private void Render()
        {
            var text = HintFor(_step);
            var show = !string.IsNullOrEmpty(text);

            if (root != null)
                root.SetActive(show);

            if (show && hintText != null)
                hintText.text = text;
        }

        private static string HintFor(Step step) => step switch
        {
            Step.BuildRoom => "봉인된 타일을 눌러 첫 방을 만드십시오",
            Step.DeployUnit => "유닛을 고용해 방에 배치하십시오  ·  유닛이 선 방이 방어선입니다",
            Step.BuildPortal => "입구에 포탈을 세우십시오  ·  모험가는 그곳으로 들어옵니다",
            Step.SurviveWave => "습격을 막아내면 금화가, 못 막으면 빚이 남습니다",
            _ => string.Empty,
        };
    }
}
