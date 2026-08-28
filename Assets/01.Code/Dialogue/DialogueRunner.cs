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

        /// <summary>이번 판에서 아직 안 나온 이벤트들. 섞어 두고 하나씩 꺼내 같은 판에서 겹치지 않게 한다.</summary>
        private readonly List<DialogueSequenceSO> _shuffledEventQueue = new();
        [SerializeField] private bool playScheduledEvents = true;
        [Header("Guided Start Tutorial")]
        [SerializeField] private bool useGuidedStartTutorial;
        [SerializeField] private GameEventChannelSO guidedNodeEventChannel;
        [SerializeField] private string tutorialSpeakerName = "임프 참모";

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
        [Header("Guided Tutorial Dependencies")]
        [SerializeField] private UnitDeployPanelView unitDeployPanelView;
        [SerializeField] private NodePanelView nodePanelView;
        [SerializeField] private WaveView waveView;
        [SerializeField] private BuildConfirmPanelView buildConfirmPanelView;
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
                "첫 지령 · 봉인된 타일을 열어 던전의 첫 방을 만드십시오.");
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
                    "방이 준비됐습니다. 수비대 관리에서 첫 부하를 영입하십시오.");
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
                "수비대 배치 완료. 봉인된 타일을 하나 더 여십시오.");
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
                    "두 번째 습격 · 수비대와 함정이 모험가의 경계를 끌어올립니다.");
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

        /// <summary>
        /// 이번에 띄울 정기 이벤트를 뽑는다.
        /// 예전에는 일차로 첨자를 계산해서, 20일 한 판이면 목록 앞쪽 몇 개만 늘 같은 순서로 나왔다.
        /// 섞은 뒤 하나씩 꺼내 쓰면 판마다 다른 조합을 보게 되고, 한 판 안에서는 겹치지 않는다.
        /// </summary>
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
            foreach (var sequence in scheduledEventSequences)
            {
                if (sequence != null)
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
