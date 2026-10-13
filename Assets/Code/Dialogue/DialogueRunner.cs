using System;
using System.Collections.Generic;
using Code.Buildings;
using Code.Manager;
using Code.Core;
using Code.Events;
using Code.MapCreateSystem;
using Code.Persistence;
using Code.Tutorial;
using Code.UI;
using Code.Units;
using Code.UI.Popup;
using UnityEngine;

namespace Code.Dialogue
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

        /// <summary>이번 판에서 아직 안 나온 이벤트들. 섞어 두고 하나씩 꺼내 같은 판에서 겹치지 않게 한다.</summary>
        private readonly List<DialogueSequenceSO> _shuffledEventQueue = new();

        /// <summary>사건을 띄우며 시간을 세워 두었는가. 대화가 끝나면 푼다.</summary>
        private bool _suspendedForEvent;
        [SerializeField] private bool playScheduledEvents = true;
        [Header("Guided Start Tutorial")]
        [SerializeField] private bool useGuidedStartTutorial;
        [SerializeField] private GameEventChannelSO guidedNodeEventChannel;
        [SerializeField] private string tutorialSpeakerName = "임프 참모";

        private readonly DialogueSequencePlayer player = new();
        private DialogueSequenceSO pendingScheduledEventSequence;
        private int lastScheduledEventDay;

        /// <summary>띄울 차례를 기다리는 상황 사건. 내용은 띄우는 순간의 수치로 만든다.</summary>
        private SituationKind _pendingSituation;

        /// <summary>
        /// 지금 도는 대화가 일반 사건(정기 이벤트)인가. 일반 사건에만 "아무것도 하지 않음"을 붙이고,
        /// 처음 보는 선택지의 결과를 가리고 고른 결과를 기록한다. 튜토리얼 대화는 손대지 않는다.
        /// </summary>
        private bool _playingNormalEvent;

        private const string SkipChoiceText = "아무것도 하지 않음";
        private const string HiddenResultText = "결과는 골라 봐야 안다";
        private const string KnownResultSuffix = "  · 확인함";
        private GuidedStartTutorialStep guidedStep;
        private Node guidedLockedNode;
        private Node guidedBuiltNode;
        private Node guidedPortalNode;
        private Node guidedTrapNode;
        private UnitDataSO guidedHiredUnit;
        private DialogueSequenceSO guidedRuntimeSequence;
        [Header("Guided Tutorial Dependencies")]
        [SerializeField] private UnitDeployPanelView unitDeployPanelView;
        [SerializeField] private NodePanelView nodePanelView;
        [SerializeField] private WaveView waveView;
        [SerializeField] private PolicyChoicePanelView policyChoicePanelView;
        [SerializeField] private ManagementSettlementManager managementSettlementManager;
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
            RegisterListeners();
        }

        private void OnDisable()
        {
            UnregisterListeners();
            ReleaseEventSuspend();

            if (useGuidedStartTutorial)
                TutorialInputGate.Clear();
        }

        private void RegisterListeners()
        {
            scheduledDayEventChannel?.AddListener<DayChangedEvent>(HandleDayChanged);
            scheduledWaveEventChannel?.AddListener<WaveEndedEvent>(HandleWaveEnded);
            guidedNodeEventChannel?.AddListener<NodeBuiltEvent>(HandleGuidedNodeBuilt);
            guidedNodeEventChannel?.AddListener<UnlockedNodeClickedEvent>(HandleGuidedNodeClicked);
            guidedNodeEventChannel?.AddListener<UnitAssignedToNodeEvent>(HandleGuidedUnitAssigned);
            guidedNodeEventChannel?.AddListener<PortalInstalledEvent>(HandleGuidedPortalInstalled);
            guidedNodeEventChannel?.AddListener<BuildingInstalledEvent>(HandleGuidedBuildingInstalled);
            costEventChannel?.AddListener<RosterHirePaidEvent>(HandleGuidedRosterHirePaid);
            costEventChannel?.AddListener<CreditStatusChangedEvent>(HandleDayLedgerClosed);
            scheduledWaveEventChannel?.AddListener<WaveStartedEvent>(HandleGuidedWaveStarted);
        }

        private void UnregisterListeners()
        {
            scheduledDayEventChannel?.RemoveListener<DayChangedEvent>(HandleDayChanged);
            scheduledWaveEventChannel?.RemoveListener<WaveEndedEvent>(HandleWaveEnded);
            guidedNodeEventChannel?.RemoveListener<NodeBuiltEvent>(HandleGuidedNodeBuilt);
            guidedNodeEventChannel?.RemoveListener<UnlockedNodeClickedEvent>(HandleGuidedNodeClicked);
            guidedNodeEventChannel?.RemoveListener<UnitAssignedToNodeEvent>(HandleGuidedUnitAssigned);
            guidedNodeEventChannel?.RemoveListener<PortalInstalledEvent>(HandleGuidedPortalInstalled);
            guidedNodeEventChannel?.RemoveListener<BuildingInstalledEvent>(HandleGuidedBuildingInstalled);
            costEventChannel?.RemoveListener<RosterHirePaidEvent>(HandleGuidedRosterHirePaid);
            costEventChannel?.RemoveListener<CreditStatusChangedEvent>(HandleDayLedgerClosed);
            scheduledWaveEventChannel?.RemoveListener<WaveStartedEvent>(HandleGuidedWaveStarted);
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

            // 이어하기에서는 첫날 설명을 되풀이하지 않는다. 새 게임은 시작 전에 저장을 지우므로
            // 이 시점의 저장 유무가 새 판과 이어하기를 가장 정확하게 가른다.
            if (playOnStart && initialSequence != null && !RunSaveSystem.HasSave)
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

        public void Play(DialogueSequenceSO sequence) => PlaySequence(sequence, false);

        /// <summary>
        /// 대화를 시작한다. 일반 사건이면 첫 줄부터 결과 가림·"아무것도 하지 않음"을 입혀 그린다.
        /// </summary>
        private void PlaySequence(DialogueSequenceSO sequence, bool normalEvent)
        {
            _playingNormalEvent = normalEvent;
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

            // 상황 사건은 정산이 끝난 금화·빚을 봐야 하므로 비용 채널이 있으면 정산 마지막 신호에서 고른다.
            if (costEventChannel == null)
                ScheduleEventForDay(evt.Day);
        }

        /// <summary>영업 종료 정산의 맨 끝(청산·부채 한도 판정 뒤). 이 전환의 사건을 띄운다.</summary>
        private void HandleDayLedgerClosed(CreditStatusChangedEvent evt)
        {
            if (scheduledWaveEventChannel == null || evt.Outcome == Manager.Economy.CreditDayEndOutcome.Bankrupt)
                return;

            ScheduleEventForDay(DayManager.Current != null ? DayManager.Current.CurrentDay : 0);
        }

        private void ScheduleEventForDay(int day)
        {
            if (!playScheduledEvents || day <= 0 || day == lastScheduledEventDay)
                return;

            // 한 전환에는 사건 하나만. 청산 위기 → 운영 방침 → 상황 사건 → 일반 사건 순이고,
            // 일반·상황 사건 뒤에는 4~6회 쉰다. 사건 관리자가 없으면(테스트 씬 등) 예전처럼 일정 간격으로 띄운다.
            var events = MoralePolicyManager.Current;
            if (events != null)
            {
                var kind = events.ResolveTransitionEvent(day);
                if (kind == TransitionEventKind.Urgent || kind == TransitionEventKind.Situation)
                {
                    lastScheduledEventDay = day;
                    _pendingSituation = events.CurrentSituation;
                    TryPlayPendingScheduledEvent();
                    return;
                }

                if (kind != TransitionEventKind.Normal)
                    return;
            }
            else if (scheduledEventIntervalDays <= 0 || day % scheduledEventIntervalDays != 0)
            {
                return;
            }

            lastScheduledEventDay = day;
            pendingScheduledEventSequence = ResolveScheduledEventSequence();
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
                "밝은 타일을 눌러 던전의 첫 방을 파세요.");
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
                    "방이 준비됐습니다. 유닛 관리에서 첫 부하를 영입하십시오.");
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
                    "새 방을 선택해 입구 포탈을 준비하십시오.");
                TutorialInputGate.OnlyUnlockedNode(guidedPortalNode);
                HighlightNode(guidedPortalNode);
                return;
            }

            if (IsGuidedStep(GuidedStartTutorialStep.BuildTrapRoom))
            {
                guidedTrapNode = evt.Node;
                guidedStep = GuidedStartTutorialStep.SelectTrapRoom;
                PlayGuidedMessage(
                    "새 방을 선택해 함정실로 만드십시오.");
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
                "부하가 대기 중입니다. 첫 방을 선택하십시오.");
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
                    "지정된 첫 방을 선택하십시오.");
                return;
            }

            guidedStep = GuidedStartTutorialStep.DeployUnit;
            PlayGuidedMessage(
                "설치 · 유닛에서 영입한 부하를 배치하십시오.");
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
                    "입구 포탈을 둘 새 방을 선택하십시오.");
                HighlightNode(guidedPortalNode);
                return;
            }

            guidedStep = GuidedStartTutorialStep.InstallPortal;
            PlayGuidedMessage(
                "설치 · 건물에서 입구 포탈을 세우십시오.");
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
                    "함정실로 만들 새 방을 선택하십시오.");
                HighlightNode(guidedTrapNode);
                return;
            }

            guidedStep = GuidedStartTutorialStep.InstallTrap;
            PlayGuidedMessage(
                "설치 · 함정에서 표시된 장치를 배치하십시오.");
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
                "유닛 배치 완료. 봉인된 타일을 하나 더 여십시오.");
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
                "입구가 열렸습니다. 습격 개시로 모험가를 들이십시오.");
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
                    "습격 개시 · 모험가의 경계와 탐욕은 이동과 건물 조우마다 변합니다.");
                TutorialInputGate.Clear();
                view?.HideSpotlight();
                return;
            }

            if (IsGuidedStep(GuidedStartTutorialStep.StartSecondWave))
            {
                guidedStep = GuidedStartTutorialStep.SecondWaveRunning;
                PlayGuidedMessage(
                    "두 번째 습격 · 유닛과 함정이 모험가의 경계를 끌어올립니다.");
                TutorialInputGate.Clear();
                view?.HideSpotlight();
                return;
            }

            if (IsGuidedStep(GuidedStartTutorialStep.StartThirdWave))
            {
                guidedStep = GuidedStartTutorialStep.ThirdWaveRunning;
                TutorialInputGate.Clear();
                PlayGuidedMessage(
                    "세 번째 습격 · 살아남아 던전 운영권을 증명하십시오.");
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
                    "함정실 준비 완료. 두 번째 습격을 개시하십시오.");
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
                    "첫 습격을 막았습니다. 새 방을 열어 함정실을 만드십시오.");
                guidedLockedNode = ResolvePreferredLockedNode();
                TutorialInputGate.OnlyLockedNode(guidedLockedNode);
                HighlightBuildTarget();
                return;
            }

            if (IsGuidedStep(GuidedStartTutorialStep.SecondWaveRunning))
            {
                guidedStep = GuidedStartTutorialStep.StartThirdWave;
                PlayGuidedMessage(
                    "방어선이 작동합니다. 세 번째 습격을 개시하십시오.");
                TutorialInputGate.OnlyWaveStart();
                HighlightWaveStartButton();
                return;
            }

            if (IsGuidedStep(GuidedStartTutorialStep.ThirdWaveRunning))
            {
                guidedStep = GuidedStartTutorialStep.ChoosePolicy;
                hasSeenPolicyChoicePanel = false;
                PlayGuidedMessage(
                    "지하 왕국이 자리를 잡았습니다. 첫 운영 지침을 선택하십시오.");
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
            guidedRuntimeSequence.ConfigureTitle("KEEPER'S ORDER");
            guidedRuntimeSequence.Configure(new DialogueLine(tutorialSpeakerName, message));

            Play(guidedRuntimeSequence);
            view?.SetBackgroundRaycastBlocking(false);
            view?.SetNextButtonVisible(allowNextButton);
            view?.SetCloseButtonVisible(allowNextButton);
        }

        private Node ResolvePreferredLockedNode()
        {
            Node fallback = null;
            Node centralPortalCandidate = null;
            foreach (var node in Node.AllInstances)
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
            if (PopupController.IsShowing(DungeonGraphController.ExpandRoomPopupTag)
                && PopupController.TryGetConfirmButtonScreenRect(out var confirmRect))
            {
                view?.SetSpotlightScreenRect(confirmRect, 24f);
                return;
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
            return unitDeployPanelView != null ? unitDeployPanelView.FirstOwnedUnit : null;
        }

        private void HighlightUnitDeployTarget()
        {
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

        private void HighlightWaveStartButton()
        {
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
            if (policyChoicePanelView == null)
            {
                view?.HideSpotlight();
                return;
            }

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
                "임명 완료 · 이제 이 던전은 키퍼님의 것입니다.",
                true);
            view?.HideSpotlight();
        }

        private void RestoreWaveStartVisuals()
        {
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

            // 일반 사건의 마지막 칸은 "아무것도 하지 않음"이다. 아무 효과 없이 사건을 닫는다.
            if (_playingNormalEvent && sequence != null && sequence.TryGetLine(lineIndex, out var currentLine)
                && currentLine.HasChoices && choiceIndex == currentLine.ChoiceCount)
            {
                Stop();
                return;
            }

            if (!player.SelectChoice(choiceIndex, out var choice, out var matchedRoute, out var ended, out var displayData))
            {
                Stop();
                return;
            }

            if (_playingNormalEvent && sequence != null)
                MoralePolicyManager.Current?.Events.MarkChosen(
                    RunEventLedger.DialogueChoiceKey(sequence.name, lineIndex, choiceIndex));

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
            _playingNormalEvent = false;
            var endedSequence = player.Stop();
            view?.Hide();
            ReleaseEventSuspend();

            if (endedSequence != null)
            {
                DialogueEnded?.Invoke(endedSequence);
                SituationEventFactory.Release(endedSequence);
            }

            TryPlayPendingScheduledEvent();
        }

        private void Show(DialogueDisplayData displayData)
        {
            displayData = DecorateNormalEventChoices(displayData);
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

        /// <summary>
        /// 이번에 띄울 정기 이벤트를 뽑는다.
        /// 예전에는 일차로 첨자를 계산해서, 20일 한 판이면 목록 앞쪽 몇 개만 늘 같은 순서로 나왔다.
        /// 섞은 뒤 하나씩 꺼내 쓰면 판마다 다른 조합을 보게 되고, 한 판 안에서는 겹치지 않는다.
        /// </summary>
        /// <summary>씬에 걸린 일반 사건 전부(메타 잠금 포함). 결과 화면이 새로 열린 사건을 셀 때 읽는다.</summary>
        public IReadOnlyList<DialogueSequenceSO> ScheduledEventSequences =>
            scheduledEventSequences ?? System.Array.Empty<DialogueSequenceSO>();

        private DialogueSequenceSO ResolveScheduledEventSequence()
        {
            if (scheduledEventSequences == null || scheduledEventSequences.Length == 0)
                return null;

            if (_shuffledEventQueue.Count == 0)
                RefillShuffledEventQueue();

            while (_shuffledEventQueue.Count > 0)
            {
                var next = _shuffledEventQueue[0];
                _shuffledEventQueue.RemoveAt(0);
                if (next != null)
                    return next;
            }

            return null;
        }

        private void RefillShuffledEventQueue()
        {
            _shuffledEventQueue.Clear();
            // 메타 진행도가 모자란 사건은 이번 판 후보에서 뺀다.
            foreach (var sequence in scheduledEventSequences)
            {
                if (sequence != null && Code.Progression.MetaUnlockRules.IsMet(sequence.MetaProgressRequired))
                    _shuffledEventQueue.Add(sequence);
            }

            for (var i = _shuffledEventQueue.Count - 1; i > 0; i--)
            {
                var swap = UnityEngine.Random.Range(0, i + 1);
                (_shuffledEventQueue[i], _shuffledEventQueue[swap]) = (_shuffledEventQueue[swap], _shuffledEventQueue[i]);
            }
        }

        private void TryPlayPendingScheduledEvent()
        {
            if (!playScheduledEvents || IsPlaying)
                return;

            // 상황 사건은 띄우는 순간의 금화로 금액을 정한다(앞선 대화가 금화를 바꿨을 수 있음).
            if (_pendingSituation != SituationKind.None)
            {
                var situation = _pendingSituation;
                _pendingSituation = SituationKind.None;
                var built = SituationEventFactory.Build(situation, costEventChannel);
                if (built != null)
                {
                    SuspendForEvent();
                    PlaySequence(built, false);
                    return;
                }
            }

            if (pendingScheduledEventSequence == null)
                return;

            var sequence = pendingScheduledEventSequence;
            pendingScheduledEventSequence = null;
            SuspendForEvent();
            PlaySequence(sequence, true);
        }

        /// <summary>
        /// 사건(일반·상황)은 고를 때까지 시간을 세운다(기획서: 주요 이벤트가 있으면 선택 종료까지 정지).
        /// 튜토리얼 말풍선은 세우지 않는다 — 배치·건설을 직접 해 보라는 안내라 시간이 흘러야 한다.
        /// </summary>
        private void SuspendForEvent()
        {
            if (_suspendedForEvent || GameSpeedController.Current == null)
                return;

            _suspendedForEvent = true;
            GameSpeedController.Current.Suspend(this);
        }

        private void ReleaseEventSuspend()
        {
            if (!_suspendedForEvent)
                return;

            _suspendedForEvent = false;
            GameSpeedController.Current?.Release(this);
        }

        /// <summary>
        /// 일반 사건의 선택지를 꾸민다. 처음 보는 선택지는 결과를 가리고, 고른 적 있는 선택지는
        /// "확인함"을 붙이며, 맨 끝에 "아무것도 하지 않음"을 더한다(기획서: 일반 사건은 안 할 수 있다).
        /// </summary>
        private DialogueDisplayData DecorateNormalEventChoices(DialogueDisplayData data)
        {
            var sequence = player.CurrentSequence;
            if (!_playingNormalEvent || !data.HasChoices || sequence == null)
                return data;

            var events = MoralePolicyManager.Current?.Events;
            var choices = new DialogueChoice[data.ChoiceCount + 1];
            for (var i = 0; i < data.ChoiceCount; i++)
            {
                var choice = data.Choices[i];
                var key = RunEventLedger.DialogueChoiceKey(sequence.name, player.CurrentLineIndex, i);
                var summary = choice.EffectSummary;
                if (!string.IsNullOrWhiteSpace(summary))
                    summary = events != null && events.HasChosen(key) ? summary + KnownResultSuffix : HiddenResultText;
                choices[i] = choice.WithEffectSummary(summary);
            }

            choices[data.ChoiceCount] = new DialogueChoice(SkipChoiceText);
            return new DialogueDisplayData(data.Title, data.SpeakerName, data.Text, data.Progress, data.EnterActions, choices);
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
