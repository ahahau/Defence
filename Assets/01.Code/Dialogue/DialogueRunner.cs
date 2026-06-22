using System;
using System.Collections.Generic;
using _01.Code.Buildings;
using _01.Code.Core;
using _01.Code.Events;
using _01.Code.Manager;
using _01.Code.MapCreateSystem;
using _01.Code.Tutorial;
using _01.Code.UI;
using _01.Code.Units;
using UnityEngine;

namespace _01.Code.Dialogue
{
    public class DialogueRunner : MonoBehaviour
    {
        [SerializeField] private DialogueSequenceSO initialSequence;
        [SerializeField] private DialogueValueTableSO valueTable;
        [SerializeField] private GameEventChannelSO costEventChannel;
        [SerializeField] private DialogueView view;
        [SerializeField] private bool playOnStart = true;
        [Header("Scheduled Events")]
        [SerializeField] private GameEventChannelSO scheduledDayEventChannel;
        [SerializeField] private GameEventChannelSO scheduledWaveEventChannel;
        [SerializeField] private DialogueSequenceSO[] scheduledEventSequences;
        [SerializeField, Min(1)] private int scheduledEventIntervalDays = 5;
        [SerializeField] private bool playScheduledEvents = true;
        [Header("Guided Start Tutorial")]
        [SerializeField] private bool useGuidedStartTutorial;
        [SerializeField] private GameEventChannelSO guidedNodeEventChannel;
        [SerializeField] private string tutorialSpeakerName = "관리자";

        private readonly DialogueSequencePlayer player = new();
        private DialogueSequenceSO pendingScheduledEventSequence;
        private int lastScheduledEventDay;
        private GuidedStartTutorialStep guidedStep;
        private Node guidedLockedNode;
        private Node guidedBuiltNode;
        private Node guidedPortalNode;
        private Node guidedTrapNode;
        private UnitDataSO guidedHiredUnit;
        private DialogueSequenceSO guidedRuntimeSequence;
        private UnitDeployPanelView unitDeployPanelView;
        private NodePanelView nodePanelView;
        private WaveView waveView;
        private BuildConfirmPanelView buildConfirmPanelView;
        private WaveRewardPanelView waveRewardPanelView;
        private PolicyChoicePanelView policyChoicePanelView;
        private ManagementSettlementManager managementSettlementManager;
        private bool hasSeenPolicyChoicePanel;

        public event Action<DialogueSequenceSO> DialogueStarted;
        public event Action<DialogueSequenceSO, int, DialogueDisplayData> LineChanged;
        public event Action<DialogueSequenceSO, int, DialogueChoice> ChoiceSelected;
        public event Action<DialogueSequenceSO> DialogueEnded;

        public DialogueSequenceSO CurrentSequence => player.CurrentSequence;
        public int CurrentLineIndex => player.CurrentLineIndex;
        public bool IsPlaying => player.IsPlaying;

        public void Configure(DialogueSequenceSO sequence, DialogueView dialogueView, bool shouldPlayOnStart)
        {
            initialSequence = sequence;
            view = dialogueView;
            playOnStart = shouldPlayOnStart;
            player.SetValueTable(valueTable);
            view?.Initialize(this);
        }

        private void OnEnable()
        {
            scheduledDayEventChannel?.AddListener<DayChangedEvent>(HandleDayChanged);
            scheduledWaveEventChannel?.AddListener<WaveEndedEvent>(HandleWaveEnded);
            guidedNodeEventChannel?.AddListener<NodeBuiltEvent>(HandleGuidedNodeBuilt);
            guidedNodeEventChannel?.AddListener<UnlockedNodeClickedEvent>(HandleGuidedNodeClicked);
            guidedNodeEventChannel?.AddListener<UnitAssignedToNodeEvent>(HandleGuidedUnitAssigned);
            guidedNodeEventChannel?.AddListener<PortalInstalledEvent>(HandleGuidedPortalInstalled);
            guidedNodeEventChannel?.AddListener<BuildingInstalledEvent>(HandleGuidedBuildingInstalled);
            costEventChannel?.AddListener<RosterHirePaidEvent>(HandleGuidedRosterHirePaid);
            scheduledWaveEventChannel?.AddListener<WaveStartedEvent>(HandleGuidedWaveStarted);
        }

        private void OnDisable()
        {
            scheduledDayEventChannel?.RemoveListener<DayChangedEvent>(HandleDayChanged);
            scheduledWaveEventChannel?.RemoveListener<WaveEndedEvent>(HandleWaveEnded);
            guidedNodeEventChannel?.RemoveListener<NodeBuiltEvent>(HandleGuidedNodeBuilt);
            guidedNodeEventChannel?.RemoveListener<UnlockedNodeClickedEvent>(HandleGuidedNodeClicked);
            guidedNodeEventChannel?.RemoveListener<UnitAssignedToNodeEvent>(HandleGuidedUnitAssigned);
            guidedNodeEventChannel?.RemoveListener<PortalInstalledEvent>(HandleGuidedPortalInstalled);
            guidedNodeEventChannel?.RemoveListener<BuildingInstalledEvent>(HandleGuidedBuildingInstalled);
            costEventChannel?.RemoveListener<RosterHirePaidEvent>(HandleGuidedRosterHirePaid);
            scheduledWaveEventChannel?.RemoveListener<WaveStartedEvent>(HandleGuidedWaveStarted);

            if (useGuidedStartTutorial)
                TutorialInputGate.Clear();
        }

        private void Awake()
        {
            player.SetValueTable(valueTable);
            view?.Initialize(this);
        }

        public void SetDialogueValue(string key, bool value)
        {
            valueTable?.SetValue(key, value);
        }

        public bool CanSelect(DialogueChoice choice)
        {
            return player.CanSelect(choice);
        }

        private void Start()
        {
            if (useGuidedStartTutorial)
            {
                BeginGuidedStartTutorial();
                return;
            }

            if (playOnStart && initialSequence != null)
                Play(initialSequence);
            else
                view?.Hide();
        }

        private void Update()
        {
            if (!useGuidedStartTutorial)
                return;

            switch (guidedStep)
            {
                case GuidedStartTutorialStep.BuildFirstRoom:
                case GuidedStartTutorialStep.BuildPortalRoom:
                case GuidedStartTutorialStep.BuildTrapRoom:
                    HighlightBuildTarget();
                    break;
                case GuidedStartTutorialStep.HireUnit:
                    HighlightUnitHireTarget();
                    break;
                case GuidedStartTutorialStep.SelectBuiltRoom:
                    HighlightNode(guidedBuiltNode);
                    break;
                case GuidedStartTutorialStep.DeployUnit:
                    HighlightUnitDeployTarget();
                    break;
                case GuidedStartTutorialStep.SelectPortalRoom:
                    HighlightNode(guidedPortalNode);
                    break;
                case GuidedStartTutorialStep.InstallPortal:
                    HighlightPortalInstallTarget();
                    break;
                case GuidedStartTutorialStep.SelectTrapRoom:
                    HighlightNode(guidedTrapNode);
                    break;
                case GuidedStartTutorialStep.InstallTrap:
                    HighlightTrapInstallTarget();
                    break;
                case GuidedStartTutorialStep.StartWave:
                case GuidedStartTutorialStep.StartSecondWave:
                case GuidedStartTutorialStep.StartThirdWave:
                    HighlightWaveStartButton();
                    break;
                case GuidedStartTutorialStep.ChoosePolicy:
                    UpdatePolicyTutorial();
                    break;
            }
        }

        public void Play(DialogueSequenceSO sequence)
        {
            view?.SetBackgroundRaycastBlocking(true);
            view?.SetNextButtonVisible(true);
            view?.SetCloseButtonVisible(true);

            if (!player.Play(sequence, out var displayData))
            {
                Stop();
                return;
            }

            DialogueStarted?.Invoke(player.CurrentSequence);
            ExecuteActions(displayData.EnterActions);
            Show(displayData);
        }

        public void PlayInitial()
        {
            Play(initialSequence);
        }

        private void HandleDayChanged(DayChangedEvent evt)
        {
            if (scheduledWaveEventChannel == null)
                ScheduleEventForDay(evt.Day);
        }

        private void HandleWaveEnded(WaveEndedEvent evt)
        {
            HandleGuidedWaveEnded(evt);
            ScheduleEventForDay(evt.Day);
        }

        private void ScheduleEventForDay(int day)
        {
            if (!playScheduledEvents || scheduledEventIntervalDays <= 0)
                return;

            if (day <= 0 || day % scheduledEventIntervalDays != 0 || day == lastScheduledEventDay)
                return;

            lastScheduledEventDay = day;
            pendingScheduledEventSequence = ResolveScheduledEventSequence(day);
            TryPlayPendingScheduledEvent();
        }

        private void BeginGuidedStartTutorial()
        {
            if (!playOnStart)
            {
                view?.Hide();
                return;
            }

            guidedStep = GuidedStartTutorialStep.BuildFirstRoom;
            PlayGuidedMessage(
                "자물쇠 타일을 클릭한 뒤 확인을 눌러 첫 방을 확장하세요.\n\n직접 클릭을 완료하면 다음 안내로 넘어갑니다.");
            guidedLockedNode = ResolvePreferredLockedNode();
            TutorialInputGate.OnlyLockedNode(guidedLockedNode);
            HighlightBuildTarget();
        }

        private void HandleGuidedNodeBuilt(NodeBuiltEvent evt)
        {
            if (evt.Node == null)
                return;

            if (IsGuidedStep(GuidedStartTutorialStep.BuildFirstRoom))
            {
                guidedBuiltNode = evt.Node;
                guidedStep = GuidedStartTutorialStep.HireUnit;
                PlayGuidedMessage(
                    "확장 완료. 다음은 유닛 고용입니다.\n\n유닛 고용 패널을 열고, 밝게 표시된 보유 유닛 카드를 두 번 클릭해 고용하세요.");
                guidedHiredUnit = ResolveFirstHireUnit();
                TutorialInputGate.OnlyHireUnit(guidedHiredUnit);
                HighlightUnitHireTarget();
                return;
            }

            if (IsGuidedStep(GuidedStartTutorialStep.BuildPortalRoom))
            {
                guidedPortalNode = evt.Node;
                guidedStep = GuidedStartTutorialStep.SelectPortalRoom;
                PlayGuidedMessage(
                    "포탈을 놓을 빈 방이 생겼습니다.\n\n방금 확장한 타일을 클릭해서 설치 메뉴를 여세요.");
                TutorialInputGate.OnlyUnlockedNode(guidedPortalNode);
                HighlightNode(guidedPortalNode);
                return;
            }

            if (IsGuidedStep(GuidedStartTutorialStep.BuildTrapRoom))
            {
                guidedTrapNode = evt.Node;
                guidedStep = GuidedStartTutorialStep.SelectTrapRoom;
                PlayGuidedMessage(
                    "트랩을 놓을 빈 방이 생겼습니다.\n\n방금 확장한 타일을 클릭해서 설치 메뉴를 여세요.");
                TutorialInputGate.OnlyUnlockedNode(guidedTrapNode);
                HighlightNode(guidedTrapNode);
            }
        }

        private void HandleGuidedRosterHirePaid(RosterHirePaidEvent evt)
        {
            if (!IsGuidedStep(GuidedStartTutorialStep.HireUnit))
                return;

            guidedStep = GuidedStartTutorialStep.SelectBuiltRoom;
            guidedHiredUnit = evt.Unit;
            PlayGuidedMessage(
                "유닛이 준비됐습니다.\n\n방금 확장한 빈 타일을 클릭해서 설치 메뉴를 여세요.");
            TutorialInputGate.OnlyUnlockedNode(guidedBuiltNode);
            HighlightNode(guidedBuiltNode);
        }

        private void HandleGuidedNodeClicked(UnlockedNodeClickedEvent evt)
        {
            if (!IsGuidedStep(GuidedStartTutorialStep.SelectBuiltRoom) || evt.Node == null)
            {
                if (IsGuidedStep(GuidedStartTutorialStep.SelectPortalRoom))
                    HandleGuidedPortalRoomClicked(evt.Node);
                else if (IsGuidedStep(GuidedStartTutorialStep.SelectTrapRoom))
                    HandleGuidedTrapRoomClicked(evt.Node);
                return;
            }

            if (guidedBuiltNode != null && evt.Node != guidedBuiltNode)
            {
                PlayGuidedMessage(
                    "이번에는 방금 확장한 빈 타일을 클릭해 주세요.\n\n그 타일에서 유닛 배치를 진행합니다.");
                return;
            }

            guidedStep = GuidedStartTutorialStep.DeployUnit;
            PlayGuidedMessage(
                "설치 버튼을 누르고, 유닛 카테고리를 선택한 뒤 밝게 표시된 유닛 카드를 클릭하세요.\n\n배치가 완료되면 다음 단계로 넘어갑니다.");
            TutorialInputGate.OnlyDeployUnit(guidedBuiltNode, guidedHiredUnit);
            HighlightUnitDeployTarget();
        }

        private void HandleGuidedPortalRoomClicked(Node node)
        {
            if (node == null)
                return;

            if (guidedPortalNode != null && node != guidedPortalNode)
            {
                PlayGuidedMessage(
                    "이번에는 포탈을 놓을 방금 확장한 빈 타일을 클릭해 주세요.");
                HighlightNode(guidedPortalNode);
                return;
            }

            guidedStep = GuidedStartTutorialStep.InstallPortal;
            PlayGuidedMessage(
                "설치 버튼을 누르고, 빌딩 카테고리에서 포탈을 선택하세요.\n\n포탈이 설치되면 웨이브 시작으로 넘어갑니다.");
            TutorialInputGate.OnlyInstallPortal(guidedPortalNode);
            HighlightPortalInstallTarget();
        }

        private void HandleGuidedTrapRoomClicked(Node node)
        {
            if (node == null)
                return;

            if (guidedTrapNode != null && node != guidedTrapNode)
            {
                PlayGuidedMessage(
                    "이번에는 트랩을 놓을 방금 확장한 빈 타일을 클릭해 주세요.");
                HighlightNode(guidedTrapNode);
                return;
            }

            guidedStep = GuidedStartTutorialStep.InstallTrap;
            PlayGuidedMessage(
                "설치 버튼을 누르고, 트랩 카테고리에서 밝게 표시된 트랩을 선택하세요.\n\n트랩이 설치되면 2일차 웨이브를 시작합니다.");
            TutorialInputGate.OnlyInstallTrap(guidedTrapNode);
            HighlightTrapInstallTarget();
        }

        private void HandleGuidedUnitAssigned(UnitAssignedToNodeEvent evt)
        {
            if (!IsGuidedStep(GuidedStartTutorialStep.DeployUnit))
                return;

            if (guidedBuiltNode != null && evt.Node != guidedBuiltNode)
                return;

            guidedStep = GuidedStartTutorialStep.BuildPortalRoom;
            PlayGuidedMessage(
                "좋습니다. 유닛 배치까지 완료했습니다.\n\n이제 다른 자물쇠 타일을 클릭해 포탈을 놓을 빈 방을 하나 더 확장하세요.");
            guidedLockedNode = ResolvePreferredLockedNode();
            TutorialInputGate.OnlyLockedNode(guidedLockedNode);
            HighlightBuildTarget();
        }

        private void HandleGuidedPortalInstalled(PortalInstalledEvent evt)
        {
            if (!IsGuidedStep(GuidedStartTutorialStep.InstallPortal))
                return;

            guidedStep = GuidedStartTutorialStep.StartWave;
            PlayGuidedMessage(
                "포탈 설치 완료.\n\n이제 웨이브 시작 버튼을 눌러 첫 전투를 시작하세요.");
            TutorialInputGate.OnlyWaveStart();
            HighlightWaveStartButton();
        }

        private void HandleGuidedWaveStarted(WaveStartedEvent evt)
        {
            RestoreWaveStartVisuals();

            if (IsGuidedStep(GuidedStartTutorialStep.StartWave))
            {
                guidedStep = GuidedStartTutorialStep.FirstWaveRunning;
                PlayGuidedMessage(
                    "1일차 웨이브가 시작됐습니다.\n\n전투가 끝나면 보상을 받고, 다음 방어 준비에서 트랩을 직접 설치해 봅니다.");
                TutorialInputGate.Clear();
                view?.HideSpotlight();
                return;
            }

            if (IsGuidedStep(GuidedStartTutorialStep.StartSecondWave))
            {
                guidedStep = GuidedStartTutorialStep.SecondWaveRunning;
                PlayGuidedMessage(
                    "2일차 웨이브가 시작됐습니다.\n\n이번에는 유닛과 트랩이 함께 막아주는 흐름을 확인하세요.");
                TutorialInputGate.Clear();
                view?.HideSpotlight();
                return;
            }

            if (IsGuidedStep(GuidedStartTutorialStep.StartThirdWave))
            {
                guidedStep = GuidedStartTutorialStep.ThirdWaveRunning;
                TutorialInputGate.Clear();
                PlayGuidedMessage(
                    "3일차 웨이브가 시작됐습니다.\n\n클리어 후 원정/운영 선택까지 진행하면 튜토리얼이 완료됩니다.");
                view?.HideSpotlight();
            }
        }

        private void HandleGuidedBuildingInstalled(BuildingInstalledEvent evt)
        {
            if (evt.Building == null)
                return;

            if (IsGuidedStep(GuidedStartTutorialStep.InstallTrap) && evt.Building.Category == InstallCategory.Trap)
            {
                if (guidedTrapNode != null && evt.Node != guidedTrapNode)
                    return;

                guidedStep = GuidedStartTutorialStep.StartSecondWave;
                PlayGuidedMessage(
                    "트랩 설치 완료.\n\n이제 웨이브 시작 버튼을 눌러 2일차 전투를 시작하세요.");
                TutorialInputGate.OnlyWaveStart();
                HighlightWaveStartButton();
            }
        }

        private void HandleGuidedWaveEnded(WaveEndedEvent evt)
        {
            if (IsGuidedStep(GuidedStartTutorialStep.FirstWaveRunning))
            {
                guidedStep = GuidedStartTutorialStep.BuildTrapRoom;
                PlayGuidedMessage(
                    "1일차 방어가 끝났습니다.\n\n이번에는 자물쇠 타일을 하나 더 확장하고, 새 방에 트랩을 설치해 보세요.");
                guidedLockedNode = ResolvePreferredLockedNode();
                TutorialInputGate.OnlyLockedNode(guidedLockedNode);
                HighlightBuildTarget();
                return;
            }

            if (IsGuidedStep(GuidedStartTutorialStep.SecondWaveRunning))
            {
                guidedStep = GuidedStartTutorialStep.StartThirdWave;
                PlayGuidedMessage(
                    "2일차 방어가 끝났습니다.\n\n유닛, 포탈, 트랩은 이제 자유롭게 쓸 수 있습니다. 웨이브 시작 버튼을 눌러 3일차 전투를 시작하세요.");
                TutorialInputGate.OnlyWaveStart();
                HighlightWaveStartButton();
                return;
            }

            if (IsGuidedStep(GuidedStartTutorialStep.ThirdWaveRunning))
            {
                guidedStep = GuidedStartTutorialStep.ChoosePolicy;
                hasSeenPolicyChoicePanel = false;
                PlayGuidedMessage(
                    "3일차 방어가 끝났습니다.\n\n이제 운영 선택을 진행합니다. 밝게 표시된 첫 선택지를 눌러 원정/운영 결정을 완료하세요.");
                TutorialInputGate.OnlyPolicyChoice();
                UpdatePolicyTutorial();
            }
        }

        private bool IsGuidedStep(GuidedStartTutorialStep step)
        {
            return useGuidedStartTutorial && guidedStep == step;
        }

        private void PlayGuidedMessage(string message, bool allowNextButton = false)
        {
            guidedRuntimeSequence ??= ScriptableObject.CreateInstance<DialogueSequenceSO>();
            guidedRuntimeSequence.ConfigureTitle("튜토리얼");
            guidedRuntimeSequence.Configure(new DialogueLine(tutorialSpeakerName, message));

            Play(guidedRuntimeSequence);
            view?.SetBackgroundRaycastBlocking(false);
            view?.SetNextButtonVisible(allowNextButton);
            view?.SetCloseButtonVisible(allowNextButton);
        }

        private Node ResolvePreferredLockedNode()
        {
            var nodes = FindObjectsByType<Node>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            Node fallback = null;
            Node centralPortalCandidate = null;
            foreach (var node in nodes)
            {
                if (node == null || !node.name.StartsWith("LockedNode_", StringComparison.Ordinal))
                    continue;

                if (IsGuidedStep(GuidedStartTutorialStep.BuildPortalRoom)
                    && node.GridPosition.y == 0
                    && (guidedBuiltNode == null || node.GridPosition.x < guidedBuiltNode.GridPosition.x))
                {
                    if (centralPortalCandidate == null || node.GridPosition.x < centralPortalCandidate.GridPosition.x)
                        centralPortalCandidate = node;
                    continue;
                }

                if (node.GridPosition == new Vector2Int(-1, 0))
                    return node;

                if (fallback == null || node.GridPosition.x > fallback.GridPosition.x)
                    fallback = node;
            }

            if (centralPortalCandidate != null)
                return centralPortalCandidate;

            return fallback;
        }

        private void HighlightBuildTarget()
        {
            buildConfirmPanelView ??= FindFirstObjectByType<BuildConfirmPanelView>(FindObjectsInactive.Include);
            if (buildConfirmPanelView != null && buildConfirmPanelView.IsOpen)
            {
                if (TryBuildRectTransformScreenRect(buildConfirmPanelView.ConfirmButtonRect, out var confirmRect))
                {
                    view?.SetSpotlightScreenRect(confirmRect, 24f);
                    return;
                }
            }

            if (guidedLockedNode == null)
                guidedLockedNode = ResolvePreferredLockedNode();

            TutorialInputGate.OnlyLockedNode(guidedLockedNode);
            HighlightNode(guidedLockedNode);
        }

        private void HighlightNode(Node node)
        {
            if (!TryBuildNodeScreenRect(node, out var rect))
            {
                view?.HideSpotlight();
                return;
            }

            view?.SetSpotlightScreenRect(rect, 44f);
        }

        private void HighlightUnitHireTarget()
        {
            unitDeployPanelView ??= FindFirstObjectByType<UnitDeployPanelView>(FindObjectsInactive.Include);
            if (unitDeployPanelView == null)
            {
                view?.HideSpotlight();
                return;
            }

            if (guidedHiredUnit == null)
                guidedHiredUnit = unitDeployPanelView.FirstOwnedUnit;

            TutorialInputGate.OnlyHireUnit(guidedHiredUnit);

            var targetEntry = unitDeployPanelView.GetEntryRect(guidedHiredUnit);
            var target = unitDeployPanelView.IsPanelOpen && targetEntry != null
                ? targetEntry
                : unitDeployPanelView.ToggleButtonRect;

            if (!TryBuildRectTransformScreenRect(target, out var rect))
            {
                view?.HideSpotlight();
                return;
            }

            view?.SetSpotlightScreenRect(rect, 24f);
        }

        private UnitDataSO ResolveFirstHireUnit()
        {
            unitDeployPanelView ??= FindFirstObjectByType<UnitDeployPanelView>(FindObjectsInactive.Include);
            return unitDeployPanelView != null ? unitDeployPanelView.FirstOwnedUnit : null;
        }

        private void HighlightUnitDeployTarget()
        {
            nodePanelView ??= FindFirstObjectByType<NodePanelView>(FindObjectsInactive.Include);
            if (nodePanelView == null)
            {
                HighlightNode(guidedBuiltNode);
                return;
            }

            TutorialInputGate.OnlyDeployUnit(guidedBuiltNode, guidedHiredUnit);
            nodePanelView.HighlightCurrentTutorialUnitTarget();

            var target = nodePanelView.FirstDeployEntryRect != null
                ? nodePanelView.FirstDeployEntryRect
                : nodePanelView.UnitCategoryCardRect != null
                    ? nodePanelView.UnitCategoryCardRect
                    : nodePanelView.InstallButtonRect;

            if (!TryBuildRectTransformScreenRect(target, out var rect))
            {
                HighlightNode(guidedBuiltNode);
                return;
            }

            view?.SetSpotlightScreenRect(rect, 24f);
        }

        private void HighlightPortalInstallTarget()
        {
            nodePanelView ??= FindFirstObjectByType<NodePanelView>(FindObjectsInactive.Include);
            if (nodePanelView == null)
            {
                HighlightNode(guidedPortalNode);
                return;
            }

            nodePanelView.HighlightCurrentTutorialInstallTarget();

            var target = nodePanelView.PortalInstallCardRect != null
                ? nodePanelView.PortalInstallCardRect
                : nodePanelView.BuildingCategoryCardRect != null
                    ? nodePanelView.BuildingCategoryCardRect
                    : nodePanelView.InstallButtonRect;

            if (!TryBuildRectTransformScreenRect(target, out var rect))
            {
                HighlightNode(guidedPortalNode);
                return;
            }

            view?.SetSpotlightScreenRect(rect, 24f);
        }

        private void HighlightTrapInstallTarget()
        {
            nodePanelView ??= FindFirstObjectByType<NodePanelView>(FindObjectsInactive.Include);
            if (nodePanelView == null)
            {
                HighlightNode(guidedTrapNode);
                return;
            }

            var trapData = nodePanelView.FirstTrapInstallData;
            TutorialInputGate.OnlyInstallTrap(guidedTrapNode, trapData);
            nodePanelView.HighlightCurrentTutorialInstallTarget();

            var target = nodePanelView.FirstTrapInstallCardRect != null
                ? nodePanelView.FirstTrapInstallCardRect
                : nodePanelView.InstallButtonRect;

            if (!TryBuildRectTransformScreenRect(target, out var rect))
            {
                HighlightNode(guidedTrapNode);
                return;
            }

            view?.SetSpotlightScreenRect(rect, 24f);
        }

        private void HighlightUnlockRewardTarget(UnlockRewardKind rewardKind)
        {
            waveRewardPanelView ??= FindFirstObjectByType<WaveRewardPanelView>(FindObjectsInactive.Include);
            if (waveRewardPanelView == null || !waveRewardPanelView.IsShowingReward)
            {
                view?.HideSpotlight();
                return;
            }

            TutorialInputGate.OnlyUnlockReward(rewardKind);
            if (!TryBuildRectTransformScreenRect(waveRewardPanelView.CurrentUnlockTutorialRect, out var rect))
            {
                view?.HideSpotlight();
                return;
            }

            view?.SetSpotlightScreenRect(rect, 24f);
        }

        private void HighlightWaveStartButton()
        {
            waveView ??= FindFirstObjectByType<WaveView>(FindObjectsInactive.Include);
            if (waveView == null || !TryBuildRectTransformScreenRect(waveView.StartButtonRect, out var rect))
            {
                view?.HideSpotlight();
                return;
            }

            waveView.HighlightTutorialStartButton();
            view?.SetSpotlightScreenRect(rect, 24f);
        }

        private void UpdatePolicyTutorial()
        {
            policyChoicePanelView ??= FindFirstObjectByType<PolicyChoicePanelView>(FindObjectsInactive.Include);
            if (policyChoicePanelView == null)
            {
                view?.HideSpotlight();
                return;
            }

            managementSettlementManager ??= FindFirstObjectByType<ManagementSettlementManager>(FindObjectsInactive.Include);
            if (managementSettlementManager != null && managementSettlementManager.IsPanelOpen)
                managementSettlementManager.ForceHidePanel();

            if (!policyChoicePanelView.IsPanelOpen)
            {
                view?.HideSpotlight();
                if (hasSeenPolicyChoicePanel)
                    CompleteGuidedTutorial();
                return;
            }

            hasSeenPolicyChoicePanel = true;
            TutorialInputGate.OnlyPolicyChoice();
            policyChoicePanelView.BringToFront();

            if (!TryBuildRectTransformScreenRect(policyChoicePanelView.FirstPolicyButtonRect, out var rect))
            {
                view?.HideSpotlight();
                return;
            }

            view?.SetSpotlightScreenRect(rect, 24f);
        }

        private void CompleteGuidedTutorial()
        {
            if (guidedStep == GuidedStartTutorialStep.Complete)
                return;

            guidedStep = GuidedStartTutorialStep.Complete;
            TutorialInputGate.Clear();
            PlayGuidedMessage(
                "튜토리얼 완료.\n\n방 확장, 유닛 배치, 포탈 설치, 3일차 운영 선택까지 확인했습니다. 이제 자유롭게 방어선을 운영하세요.",
                true);
            view?.HideSpotlight();
        }

        private void RestoreWaveStartVisuals()
        {
            waveView ??= FindFirstObjectByType<WaveView>(FindObjectsInactive.Include);
            waveView?.ClearTutorialHighlight();
            view?.HideSpotlight();
        }

        private bool TryBuildNodeScreenRect(Node node, out Rect rect)
        {
            rect = default;
            if (node == null)
                return false;

            var camera = Camera.main;
            if (camera == null)
                return false;

            var bounds = node.ClickCollider != null
                ? node.ClickCollider.bounds
                : new Bounds(node.transform.position, Vector3.one);

            var min = camera.WorldToScreenPoint(bounds.min);
            var max = camera.WorldToScreenPoint(bounds.max);
            rect = Rect.MinMaxRect(
                Mathf.Min(min.x, max.x),
                Mathf.Min(min.y, max.y),
                Mathf.Max(min.x, max.x),
                Mathf.Max(min.y, max.y));
            return rect.width > 1f && rect.height > 1f;
        }

        private bool TryBuildRectTransformScreenRect(RectTransform target, out Rect rect)
        {
            rect = default;
            if (target == null)
                return false;

            var corners = new Vector3[4];
            target.GetWorldCorners(corners);
            var min = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
            var max = min;
            for (var i = 1; i < corners.Length; i++)
            {
                var point = RectTransformUtility.WorldToScreenPoint(null, corners[i]);
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }

            rect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            return rect.width > 1f && rect.height > 1f;
        }

        public void Next()
        {
            if (!IsPlaying)
            {
                PlayInitial();
                return;
            }

            if (!player.Next(out var displayData))
            {
                Stop();
                return;
            }

            ExecuteActions(displayData.EnterActions);
            Show(displayData);
        }

        public void SelectChoice(int choiceIndex)
        {
            if (!IsPlaying)
                return;

            var sequence = player.CurrentSequence;
            var lineIndex = player.CurrentLineIndex;
            if (!player.SelectChoice(choiceIndex, out var choice, out var matchedRoute, out var ended, out var displayData))
            {
                Stop();
                return;
            }

            ChoiceSelected?.Invoke(sequence, lineIndex, choice);
            ExecuteActions(choice.Actions);
            ExecuteActions(matchedRoute.Actions);

            if (ended)
            {
                Stop();
                return;
            }

            ExecuteActions(displayData.EnterActions);
            Show(displayData);
        }

        public void Stop()
        {
            var endedSequence = player.Stop();
            view?.Hide();

            if (endedSequence != null)
                DialogueEnded?.Invoke(endedSequence);

            TryPlayPendingScheduledEvent();
        }

        private void Show(DialogueDisplayData displayData)
        {
            view?.Show(displayData);
            LineChanged?.Invoke(player.CurrentSequence, player.CurrentLineIndex, displayData);
        }

        private void ExecuteActions(IReadOnlyList<DialogueActionSO> actions)
        {
            if (actions == null || actions.Count == 0)
                return;

            var context = new DialogueActionContext(this, costEventChannel, valueTable);
            foreach (var action in actions)
                action?.Execute(context);
        }

        private DialogueSequenceSO ResolveScheduledEventSequence(int day)
        {
            if (scheduledEventSequences == null || scheduledEventSequences.Length == 0)
                return null;

            var startIndex = Mathf.Max(0, day / scheduledEventIntervalDays - 1);
            for (var i = 0; i < scheduledEventSequences.Length; i++)
            {
                var index = (startIndex + i) % scheduledEventSequences.Length;
                if (scheduledEventSequences[index] != null)
                    return scheduledEventSequences[index];
            }

            return null;
        }

        private void TryPlayPendingScheduledEvent()
        {
            if (!playScheduledEvents || IsPlaying || pendingScheduledEventSequence == null)
                return;

            var sequence = pendingScheduledEventSequence;
            pendingScheduledEventSequence = null;
            Play(sequence);
        }

        private enum GuidedStartTutorialStep
        {
            None,
            BuildFirstRoom,
            HireUnit,
            SelectBuiltRoom,
            DeployUnit,
            BuildPortalRoom,
            SelectPortalRoom,
            InstallPortal,
            StartWave,
            FirstWaveRunning,
            BuildTrapRoom,
            SelectTrapRoom,
            InstallTrap,
            StartSecondWave,
            SecondWaveRunning,
            StartThirdWave,
            ThirdWaveRunning,
            ChoosePolicy,
            Complete
        }
    }
}
