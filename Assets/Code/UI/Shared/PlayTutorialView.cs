using Code.Buildings;
using Code.Core;
using Code.Manager;
using Code.Dialogue;
using Code.MapCreateSystem;
using Code.Units;
using Code.UI.Popup;
using Code.UI.RoomManagement;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace Code.UI
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
    /// 눌러야 하는 곳만 남기고 화면을 덮는다. 어두워진 쪽은 덮개가 클릭을 받아 삼키므로,
    /// 밝은 구멍만 눌린다. 첫 판에 무엇부터 눌러야 하는지 모르는 사람에게는 글보다 이쪽이 확실하다.
    ///
    /// 그동안 누르는 것 말고는 다 잠근다 — 화면을 밀 수도 줌을 바꿀 수도 없다. 처음 하는
    /// 사람이 지도를 밀어 놓고 길을 잃으면, 무엇을 하라는 안내보다 "여기가 어디지"가 먼저 온다.
    /// 움직이는 법은 나머지를 다 해 본 뒤에 한 칸을 따로 내어 가르친다.
    ///
    /// 칸이 바뀔 때 화면도 그쪽으로 미끄러진다. 지도 반대편을 가리키면 덮개만 옮겨서는
    /// 무엇을 가리키는지 못 찾는다.
    ///
    /// 첫날 처음부터 시작한 판에서만 뜬다. 이어하기로 중간에 들어온 사람에게 "첫 방을 파세요"는
    /// 안내가 아니라 방해다.
    ///
    /// 덮개를 못 펴면 아무것도 안 덮는다. 한 칸에서 오래 막혀 있어도 스스로 걷는다.
    /// 안내가 틀리는 것보다 판이 멈추는 것이 훨씬 나쁘다.
    /// </summary>
    public class PlayTutorialView : MonoBehaviour
    {
        private enum Step
        {
            // 첫날 — 조작을 잠그고 순서대로 이끈다.
            BuildRoom,
            DeployUnit,
            BuildPortal,
            LearnMove,
            SurviveWave,

            // 첫 습격 뒤 — 실제 전투와 정산까지 보고 나서 첫 수업을 마친다.
            ObserveCombat,
            ReviewSettlement,
            PrepareNextDay,

            // 첫날이 끝난 뒤. 새 기능이 열릴 날을 기다리며 아무것도 하지 않는다.
            Idle,

            // 뒷날 수업 — 비추기만 하고 잠그지는 않는다. 이미 판을 할 줄 아는 사람이다.
            LearnMerchant,

            Done,
        }

        /// <summary>
        /// 방·유닛·포탈 칸 안의 잔걸음.
        ///
        /// 한 칸이 클릭 한 번으로 끝나지 않는다 — 방을 고르고, 하단 갈래 버튼을 누르고, 카드를 고르고,
        /// 설치를 누른다. 구멍과 안내 글이 같은 걸음을 보게 하려고 이름을 붙여 둔다.
        /// </summary>
        private enum BuildStage
        {
            PickNode,
            PickCategory,
            PickCard,
            PressInstall,
            ConfirmRoom,
            PickCell,
        }

        /// <summary>첫날의 이끄는 칸인가. 조작을 잠그는 것은 이 칸들뿐이다.</summary>
        private bool IsFirstDayStep => _step is Step.BuildRoom or Step.DeployUnit or
            Step.BuildPortal or Step.LearnMove or Step.SurviveWave;

        private bool IsBeforeFirstWave => _step is Step.BuildRoom or Step.DeployUnit or
            Step.BuildPortal or Step.LearnMove;

        [Header("UI")]
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text hintText;

        [SerializeField, Min(0.05f), Tooltip("판을 다시 살펴보는 간격(초).")]
        private float pollInterval = 0.25f;

        [SerializeField, Tooltip("눌러야 하는 곳만 남기고 화면을 덮는다.")]
        private bool forceStepOrder = true;

        [SerializeField, Min(5f),
         Tooltip("한 칸에서 이만큼 진행이 없으면 덮개를 걷는다. 안내가 길을 잘못 짚어도 판이 멈추지 않게 하는 안전장치.")]
        private float stuckReleaseSeconds = 40f;

        [Header("Spotlight")]
        [SerializeField, Tooltip("겨눈 곳만 남기고 화면을 덮는 네 장(위·아래·왼쪽·오른쪽).")]
        private RectTransform[] dimPanels = new RectTransform[4];

        [SerializeField, Min(0f), Tooltip("구멍 둘레에 더 두는 여유(화면 픽셀).")]
        private float spotlightPadding = 28f;

        [SerializeField, Tooltip("마지막 칸에서 겨눌 습격 시작 버튼을 들고 있는 화면.")]
        private WaveView waveView;
        [SerializeField, Tooltip("대화가 도는 동안 안내를 멈춘다. 비어 있으면 찾으며 경고한다(배선 뒤 폴백 제거).")]
        private DialogueRunner dialogueRunner;
        [SerializeField, Tooltip("상인 수업이 비출 상인 창. 비어 있으면 찾으며 경고한다(배선 뒤 폴백 제거).")]
        private MerchantPanelView merchantPanel;
        [SerializeField, Min(0.1f), Tooltip("방 하나를 덮을 월드 반지름. 구멍 크기를 재는 기준.")]
        private float spotlightWorldRadius = 2.4f;

        [Header("Pacing")]
        [SerializeField, Min(0f), Tooltip("한 칸을 최소 이만큼은 보여 준다. 조건이 이미 맞아도 여러 칸을 한꺼번에 건너뛰지 않게.")]
        private float minStepSeconds = 1.6f;

        [SerializeField, Min(0f), Tooltip("카메라가 다음 자리로 옮겨가는 데 걸리는 시간(초).")]
        private float cameraGlideSeconds = 0.7f;

        [SerializeField, Tooltip("방을 볼 때 카메라가 겨누는 지점의 어긋남. 싸움이 방 아래에서 벌어져 아래로 내려 잡는다.")]
        private Vector2 cameraFocusOffset = new(0f, -1.6f);

        [SerializeField, Min(0.1f), Tooltip("WASD 칸을 넘기려면 화면을 이만큼 밀어야 한다(월드 단위).")]
        private float moveLessonDistance = 3.5f;

        [SerializeField, Min(1f), Tooltip("뒷날 수업 한 편을 띄워 두는 시간(초). 안 눌러도 이만큼 지나면 걷는다.")]
        private float lessonSeconds = 10f;

        [SerializeField, Min(0.01f), Tooltip("구멍이 다음 자리로 옮겨가는 데 걸리는 시간(초). 툭 튀지 않게 한다.")]
        private float holeGlideSeconds = 0.28f;

        private const int MerchantLessonDay = 2;

        private bool _merchantTaught;
        private bool _settlementWasOpen;
        private Node _cameraTargetNode;
        private Vector3 _moveStartPosition;
        private Tween _cameraTween;
        private Rect _hole;
        private Rect _holeTarget;
        private bool _hasHole;
        private Step _step = Step.BuildRoom;
        private BuildStage _buildStage = BuildStage.PickNode;
        private RectTransform _buildUiTarget;

        // 방 확장 확인은 UI Toolkit 팝업이라 RectTransform이 없다. 이때는 팝업에게 버튼 위치를 묻는다.
        private bool _buildTargetIsPopup;

        // 방 관리 화면(설치 창·하단 갈래 버튼)도 UI Toolkit이라, 잔걸음을 잴 때 화면 사각형을 받아 둔다.
        private bool _hasBuildScreenRect;
        private Rect _buildScreenRect;
        private Node _buildNodeTarget;
        private float _timer;
        private float _stepAge;
        private bool _released;
        private Code.UI.Toolkit.TutorialOverlayToolkitView _overlay;
        /// <summary>안내 카드를 띄우라고 했는가. 덮개 화면이 아직 없을 때(EditMode 테스트)도 상태를 잃지 않는다.</summary>
        private bool _hintVisible;
        private bool _waitingForDialogue;
        private DialogueRunner _dialogue;

        public void SkipTutorial()
        {
            EnterStep(Step.Done);
        }

        private void OnEnable()
        {
            _timer = 0f;
            _stepAge = 0f;
            _released = false;
            _hasHole = false;
            _cameraTargetNode = null;
            ConfigureHint();
            if (dialogueRunner == null)
                dialogueRunner = SceneReferenceFallback.Find<DialogueRunner>(this, nameof(dialogueRunner));
            _dialogue = dialogueRunner;
            var camera = Camera.main;
            if (camera != null)
                _moveStartPosition = camera.transform.position;
            Render();

            // 첫 살피기(0.25초 뒤)까지 기다리면 그 사이에 버튼이 한 번 깜빡인다.
            ApplyStartButtonHold();
        }

        private void OnDisable()
        {
            ReleaseControlLocks();
            ReleaseStartButton();
            _cameraTween?.Kill();
            HideSpotlight();
            SetHintVisible(false);
        }

        private void OnDestroy()
        {
            if (_overlay == null)
                return;

            _overlay.SkipClicked -= SkipTutorial;
            Destroy(_overlay.gameObject);
            _overlay = null;
        }

        private void Update()
        {
            if (_step == Step.Done)
                return;

            if ((_dialogue != null && _dialogue.IsPlaying) || SettingsPanelView.IsOpen)
            {
                _waitingForDialogue = true;
                HideSpotlight();
                ReleaseControlLocks();
                _cameraTween?.Kill();
                SetHintVisible(false);
                return;
            }
            if (_waitingForDialogue)
            {
                _waitingForDialogue = false;
                _stepAge = 0f;
                Render();
            }

            _stepAge += Time.unscaledDeltaTime;

            ApplyControlLocks();
            ApplyStartButtonHold();

            // 판을 살피는 건 0.25초마다면 충분하다. 사람이 방을 짓는 속도에 견주면 즉시다.
            _timer -= Time.unscaledDeltaTime;
            if (_timer <= 0f)
            {
                _timer = pollInterval;

                // 첫날에만 가르친다. 하루가 넘어갔다면 무슨 일이 있었든 안내는 끝이다.
                //
                // 이 검사를 처음 한 번만 했다가 판을 멈춰 세운 적이 있다. 1일차에 시작해서
                // 안내가 끝나지 않은 채 2일차로 넘어가면, 덮개가 화면을 덮고 조작이 잠긴 채로
                // 그대로 남아 아무것도 못 하는 판이 됐다. 안내가 끝나는 조건을 습격 하나에만
                // 걸어 둔 것이 잘못이었다 — 날짜는 무슨 일이 있어도 넘어간다.
                if (IsFirstDayStep && (CurrentDay > 1 ||
                    (CurrentDay == 1 && DayManager.Current != null && DayManager.Current.IsStandby)))
                {
                    EnterStep(Step.Idle);
                    return;
                }

                // 한 칸을 최소한 이만큼은 보여 준다. 판을 시작할 때 이미 조건이 맞아 있는 칸이
                // 있으면(예: 방이 하나 지어진 채로 불러온 판) 안내가 여러 칸을 한 프레임에
                // 지나가 버려, 읽을 새도 없이 마지막 줄만 남는다.
                if (_stepAge >= minStepSeconds)
                {
                    var next = Resolve(_step);
                    if (next != _step)
                        EnterStep(next);
                }

                if (_step == Step.Done)
                    return;

                // 전투 안내는 같은 단계 안에서 내용이 바뀐다. 단계 진입 때만 그리면
                // 첫 문장만 남으므로 살피는 박자에 맞춰 갱신한다.
                if (_step == Step.ObserveCombat)
                    Render();

                // 구멍을 정하기 전에 잔걸음부터 다시 잰다. 순서가 뒤집히면 한 박자 늦은
                // 자리를 비추게 된다.
                RefreshBuildStage();
                ApplyGate();

                // 한 칸 안에서도 겨눌 방이 옮겨간다 — 봉인을 열면 그 다음은 그 방에 짓는 일이다.
                // 칸이 바뀔 때만 화면을 옮기면 그 사이를 못 따라가므로, 목표가 갈릴 때마다 옮긴다.
                FollowTargetNode();
            }

            // 구멍을 옮기는 건 매 프레임이다. 살피는 박자에 맞춰 움직이면 초당 네 번씩
            // 툭툭 건너뛰어, 안내가 따라오는 게 아니라 깜빡이는 것처럼 보인다.
            GlideHole();
        }

        /// <summary>
        /// 다음 칸으로 넘어간다.
        ///
        /// 넘어가는 순간에만 할 일이 몇 가지 있다 — 화면을 그쪽으로 옮기고, 시계를 되돌리고,
        /// WASD 칸이면 이동을 풀어 주고 기준 위치를 잡는다.
        /// </summary>
        private void EnterStep(Step next)
        {
            _step = next;
            _stepAge = 0f;
            _released = false;

            // 한 번 꺼낸 수업은 다시 꺼내지 않는다. 들어서는 순간 기록해야, 중간에 어떻게
            // 끝나든(눌렀든 시간이 지났든) 두 번 뜨지 않는다.
            if (_step == Step.LearnMerchant)
                _merchantTaught = true;
            else if (_step == Step.ReviewSettlement)
                _settlementWasOpen = ManagementSettlementManager.Current != null &&
                                     ManagementSettlementManager.Current.IsPanelOpen;

            // 잔걸음을 먼저 잰다. 칸에 들어서자마자 그리면 앞 칸의 걸음으로 한 줄이 스쳐 지나간다.
            _buildStage = IsBuildStep(_step)
                ? ResolveBuildStage(out _buildUiTarget, out _buildNodeTarget)
                : BuildStage.PickNode;

            Render();

            if (_step == Step.Done)
            {
                ReleaseControlLocks();
                ReleaseStartButton();
                HideSpotlight();
                return;
            }

            // 앞 칸의 화면 이동을 먼저 끊는다. 살아 있는 채로 기준 위치를 잡으면, WASD 칸이
            // 손도 대기 전에 "움직였다"고 판단해 저절로 넘어간다.
            _cameraTween?.Kill();

            var camera = Camera.main;
            if (camera != null)
                _moveStartPosition = camera.transform.position;

            _cameraTargetNode = ResolveTargetNode();
            GlideCameraTo(_cameraTargetNode);
        }

        /// <summary>
        /// 겨눈 방이 갈리면 화면도 따라 옮긴다.
        ///
        /// WASD 칸에서는 하지 않는다. 그 칸은 사람이 직접 미는 것을 배우는 자리라,
        /// 화면이 저 혼자 움직이면 자기가 민 것인지 안내가 민 것인지 구분되지 않는다.
        /// </summary>
        private void FollowTargetNode()
        {
            if (_step == Step.LearnMove)
                return;

            var target = ResolveTargetNode();
            if (target == _cameraTargetNode)
                return;

            _cameraTargetNode = target;
            GlideCameraTo(target);
        }

        /// <summary>
        /// 화면을 다음 목표 쪽으로 옮긴다.
        ///
        /// 안내가 지도 반대편을 가리키면, 덮개만 옮겨서는 무엇을 가리키는지 못 찾는다.
        /// 화면이 따라가 줘야 "저기구나"가 된다. 툭 순간이동하면 어디서 어디로 갔는지 모르므로
        /// 미끄러뜨린다 — 그 사이가 지금 어디를 보고 있었는지 알려 주는 유일한 단서다.
        /// </summary>
        private void GlideCameraTo(Node node)
        {
            var camera = Camera.main;
            if (camera == null || node == null)
                return;

            var from = camera.transform.position;

            // 방 한가운데가 아니라 조금 아래를 본다. 싸움은 방 아래쪽에서 벌어지므로,
            // 정가운데에 맞추면 정작 봐야 할 곳이 화면 아래로 밀린다.
            var to = new Vector3(
                node.transform.position.x + cameraFocusOffset.x,
                node.transform.position.y + cameraFocusOffset.y,
                from.z);

            _cameraTween?.Kill();
            _cameraTween = camera.transform
                .DOMove(to, cameraGlideSeconds)
                .SetEase(Ease.InOutSine)
                .SetUpdate(true)
                .SetLink(gameObject);
        }

        /// <summary>
        /// 지금 칸에 맞게 조작을 잠근다.
        ///
        /// 누르는 것 하나만 가르치는 동안에는 화면 이동도 줌도 잠근다. 처음 하는 사람이 화면을
        /// 밀어 놓고 길을 잃으면, 무엇을 하라는 안내보다 "여기가 어디지"가 먼저 온다.
        ///
        /// WASD 칸에서만 이동을 풀어 준다. 그 칸의 배울 거리가 바로 그것이다.
        /// </summary>
        private void ApplyControlLocks()
        {
            var guiding = forceStepOrder && !_released && IsFirstDayStep;
            Core.InputSystemCameraMover.ZoomLocked = guiding;
            Core.InputSystemCameraMover.MoveLocked = guiding && _step != Step.LearnMove;
        }

        private static void ReleaseControlLocks()
        {
            Core.InputSystemCameraMover.ZoomLocked = false;
            Core.InputSystemCameraMover.MoveLocked = false;
        }

        /// <summary>
        /// 습격 시작 버튼은 WASD 를 배운 뒤에야 나타난다.
        ///
        /// 그 앞 칸들은 덮개가 막아 주지만 WASD 칸에는 덮개가 없다 — 배울 거리가 화면을 미는
        /// 일이라 가릴 자리가 없다. 포탈만 세우면 버튼이 켜지므로, 막지 않으면 화면을 밀어 볼
        /// 생각도 하기 전에 습격이 시작된다.
        ///
        /// 순서를 강제하지 않기로 했거나(<see cref="forceStepOrder"/>) 한 칸에 너무 오래 막혀
        /// 덮개를 걷은 뒤에는 붙잡지 않는다. 안내가 틀렸을 때 판이 멈추는 쪽이 훨씬 나쁘다.
        /// </summary>
        private void ApplyStartButtonHold()
        {
            var view = ResolveWaveView();
            if (view == null)
                return;

            view.SetStartButtonHeld(forceStepOrder && !_released && IsBeforeFirstWave);
        }

        private void ReleaseStartButton()
        {
            var view = ResolveWaveView();
            if (view != null)
                view.SetStartButtonHeld(false);
        }

        /// <summary>
        /// 습격 화면을 구해 온다.
        ///
        /// 씬이 아니라 프리팹 안에 있어서 인스펙터에 미리 물려 두지 못할 수 있다. 한 번 찾으면
        /// 들고 있는다 — 판이 도는 동안 이 화면이 갈리지는 않는다.
        /// </summary>
        private WaveView ResolveWaveView()
        {
            // 습격 화면은 스스로 Current에 올라온다. 씬을 훑지 않고 그것을 쓴다.
            if (waveView == null)
                waveView = WaveView.Current;

            return waveView;
        }

        /// <summary>구멍을 목표 자리로 조금씩 옮긴다. 칸이 바뀔 때 다음 자리로 미끄러져 간다.</summary>
        private void GlideHole()
        {
            if (!_hasHole)
                return;

            var t = Mathf.Clamp01(Time.unscaledDeltaTime / Mathf.Max(0.01f, holeGlideSeconds));
            _hole = new Rect(
                Mathf.Lerp(_hole.x, _holeTarget.x, t),
                Mathf.Lerp(_hole.y, _holeTarget.y, t),
                Mathf.Lerp(_hole.width, _holeTarget.width, t),
                Mathf.Lerp(_hole.height, _holeTarget.height, t));

            ShowSpotlight(_hole);
        }

        /// <summary>
        /// 눌러야 하는 곳만 남기고 화면을 덮는다.
        ///
        /// 잠그는 방식을 먼저 썼다가 걷어냈다. 누를 수 있는 것을 코드로 하나하나 열어 주는
        /// 방식이라, 열어 줄 곳을 잘못 짚으면 아무것도 못 누르는 판이 되고 무엇이 왜 안 눌리는지도
        /// 보이지 않는다.
        ///
        /// 지금은 겨눌 곳을 빼고 네 장으로 화면을 덮는다. 어두워진 쪽은 판이 클릭을 받아 삼키고
        /// 밝은 구멍만 눌린다. 결과는 같은데 눈에 보이고, 덮개를 못 펴면 그냥 아무것도 안 덮여
        /// 판이 멀쩡히 굴러간다 — 틀렸을 때 잃는 것이 훨씬 적다.
        /// </summary>
        private void ApplyGate()
        {
            if (!forceStepOrder || _released)
            {
                HideSpotlight();
                return;
            }

            if (_stepAge >= stuckReleaseSeconds)
            {
                _released = true;
                HideSpotlight();
                Debug.LogWarning($"[튜토리얼] {_step} 에서 {stuckReleaseSeconds:0}초 동안 진행이 없어 덮개를 걷습니다.");
                return;
            }

            if (!TryResolveHole(out var rect))
            {
                HideSpotlight();
                return;
            }

            _holeTarget = rect;

            // 첫 구멍은 미끄러져 올 곳이 없으므로 그 자리에서 시작한다.
            if (!_hasHole)
            {
                _hole = rect;
                _hasHole = true;
            }
        }

        /// <summary>
        /// 이 칸에서 뚫어 둘 구멍. 못 정하면 아무것도 덮지 않는다.
        ///
        /// 앞의 세 칸은 지도 위의 방을 가리키고 마지막 칸만 화면의 버튼을 가리킨다.
        /// 둘은 좌표를 구하는 방법이 달라서 따로 잰다.
        /// </summary>
        private bool TryResolveHole(out Rect rect)
        {
            rect = default;

            switch (_step)
            {
                // 배울 것이 화면을 미는 일이라 가릴 자리가 없다. 덮개를 걷고 글만 남긴다.
                case Step.LearnMove:
                case Step.Idle:
                    return false;

                case Step.SurviveWave:
                    // 시작 버튼은 UI Toolkit 화면에 있다. 화면 사각형을 바로 받는다.
                    var wave = ResolveWaveView();
                    return wave != null && wave.TryGetStartButtonScreenRect(out rect);

                case Step.LearnMerchant:
                    // 상인 버튼은 HUD(UI Toolkit) 하단 바에 있다. 화면 사각형을 바로 받는다.
                    var hud = Code.UI.Toolkit.GameplayHudToolkitView.Current;
                    return hud != null && hud.TryGetMerchantButtonScreenRect(out rect);


                default:
                    return TryResolveBuildHole(out rect);
            }
        }

        /// <summary>
        /// 방·유닛·포탈 칸의 구멍. 지도에서 시작해 관리 창 안까지 따라간다.
        ///
        /// 예전에는 지도 위의 방만 가리켰다. 덮개가 UI를 못 막던 동안에는 그래도 굴러갔지만,
        /// 덮개를 창 위로 올린 뒤에는 그러면 안 된다 — 방만 뚫어 두면 그 다음에 눌러야 할
        /// 설치 버튼이 덮개에 막혀 아무 데도 못 간다.
        ///
        /// 그래서 눌러야 할 것을 차례로 따라간다. 방을 고르고 → 하단 갈래 버튼을 누르고 → 카드를 고르고
        /// → 설치를 누른다. 어느 단계인지는 방 관리 화면에게 묻는다. 그쪽이 그 상태를 들고 있다.
        /// </summary>
        private bool TryResolveBuildHole(out Rect rect)
        {
            rect = default;

            if (_buildTargetIsPopup)
                return PopupController.TryGetConfirmButtonScreenRect(out rect);

            if (_hasBuildScreenRect)
            {
                rect = _buildScreenRect;
                return true;
            }

            if (_buildUiTarget != null)
                return TryBuildRect(_buildUiTarget, out rect);

            return _buildNodeTarget != null && TryBuildNodeRect(_buildNodeTarget, out rect);
        }

        /// <summary>
        /// 첫날 칸 안의 잔걸음을 가려내고, 그 걸음에서 눌러야 할 곳을 함께 내놓는다.
        ///
        /// 구멍과 안내 글이 같은 곳에서 나와야 한다. 한동안 구멍만 창 안으로 따라 들어가고
        /// 글은 "밝은 타일을 눌러 첫 방을 파세요"에 머물러 있었다 — 빛나는 곳과 읽히는 문장이
        /// 서로 다른 것을 가리키면, 둘 다 못 믿게 된다.
        /// </summary>
        private BuildStage ResolveBuildStage(out RectTransform uiTarget, out Node nodeTarget)
        {
            uiTarget = null;
            nodeTarget = null;
            _buildTargetIsPopup = false;
            _hasBuildScreenRect = false;

            var panel = NodePanelView.Current;

            if (PopupController.IsShowing(DungeonGraphController.ExpandRoomPopupTag))
            {
                _buildTargetIsPopup = true;
                return BuildStage.ConfirmRoom;
            }
            // 옛 "유닛 관리" 고용 창은 없어졌다. 고용은 청산일 정산 화면의 유닛 상점에서 하고,
            // 여기서는 보유한 부하를 방 관리 화면의 배치로 세우는 것만 안내한다.
            if (_step == Step.DeployUnit)
            {
                if (panel != null && panel.IsChoosingUnitCell)
                {
                    nodeTarget = panel.SelectedNode;
                    return BuildStage.PickCell;
                }
            }

            nodeTarget = ResolveTargetNode();

            // 방을 이미 골라 뒀다면 다음은 방 관리 화면 차례다 — 하단 갈래 버튼 → 카드 → 설치.
            // 봉인된 방은 예외다. 그건 눌러서 여는 것이 곧 할 일이라 지도를 계속 가리켜야 한다.
            var controller = RoomManagementController.Current;
            if (panel != null
                && controller != null
                && nodeTarget != null
                && panel.SelectedNode == nodeTarget
                && !IsLocked(nodeTarget))
            {
                var kind = _step == Step.DeployUnit ? RoomInstallKind.Unit : RoomInstallKind.Building;
                _hasBuildScreenRect = controller.TryGetTutorialTarget(kind, out var stage, out _buildScreenRect);
                switch (stage)
                {
                    case RoomTutorialStage.PickCategory:
                        return BuildStage.PickCategory;
                    case RoomTutorialStage.PickCard:
                        return BuildStage.PickCard;
                    case RoomTutorialStage.PressInstall:
                        return BuildStage.PressInstall;
                }

                _hasBuildScreenRect = false;
            }

            return BuildStage.PickNode;
        }

        /// <summary>잔걸음을 다시 재고, 달라졌으면 안내 글도 그 걸음으로 바꾼다.</summary>
        private void RefreshBuildStage()
        {
            if (!IsBuildStep(_step))
            {
                _buildUiTarget = null;
                _buildNodeTarget = null;
                _buildTargetIsPopup = false;
                _hasBuildScreenRect = false;
                return;
            }

            var stage = ResolveBuildStage(out _buildUiTarget, out _buildNodeTarget);
            if (stage == _buildStage)
                return;

            _buildStage = stage;
            _stepAge = 0f;
            _released = false;
            Render();
        }

        private static bool IsBuildStep(Step step) =>
            step == Step.BuildRoom || step == Step.DeployUnit || step == Step.BuildPortal;

        /// <summary>이 칸에서 눌러야 할 방. 못 고르면 아무것도 덮지 않는다.</summary>
        private Node ResolveTargetNode() => _step switch
        {
            // 봉인을 여는 것과 그 방에 짓는 것이 한 칸 안에 함께 있다. 열린 빈 방이 있으면
            // 지을 차례이고, 없으면 아직 봉인을 열 차례다.
            Step.BuildRoom => FindLockedNode(),
            Step.DeployUnit => FindDefenseRoom(),
            Step.BuildPortal => FindEntranceCandidate(),
            _ => null,
        };

        /// <summary>
        /// 포탈을 세울 자리. 지도 바깥쪽 — 침입자가 들어오는 쪽 — 을 고른다.
        ///
        /// 그냥 "빈 방 아무거나"로 두면 첫 방 옆이 아니라 지도 반대편이 잡히기도 한다.
        /// 포탈은 입구니까 가장자리에 서야 말이 되고, 안내도 옆 칸을 가리켜야 따라가기 쉽다.
        ///
        /// 아직 열린 빈 방이 없으면 봉인된 것 중에서 같은 기준으로 고른다. 그래야 "옆 칸을
        /// 열고 → 거기에 포탈을 세운다"가 한 방향으로 이어진다.
        /// </summary>
        private static Node FindEntranceCandidate()
        {
            return PickOutermost(node => !IsLocked(node) && !node.HasAssignedBuilding
                    && node.AssignedUnitCount == 0 && node.Data != null
                    && node.Data.Type != DungeonNodeType.Entrance)
                   ?? PickOutermost(IsLocked);
        }

        /// <summary>가장 바깥(격자 x가 가장 작은) 방을 고른다.</summary>
        private static Node PickOutermost(System.Func<Node, bool> accept)
        {
            Node best = null;
            foreach (var node in Node.AllInstances)
            {
                if (node == null || !accept(node))
                    continue;

                if (best == null || node.GridPosition.x < best.GridPosition.x)
                    best = node;
            }

            return best;
        }

        private static bool IsLocked(Node node) =>
            node != null && node.name.StartsWith("LockedNode_", System.StringComparison.Ordinal);

        /// <summary>
        /// 습격 시작 버튼을 화면 네모로 잰다.
        ///
        /// 버튼은 이미 화면 위의 것이라 방처럼 투영할 필요가 없다. 다만 캔버스가 화면에 직접
        /// 그리는지 카메라를 거치는지에 따라 모서리를 화면 좌표로 옮기는 방법이 달라진다.
        /// </summary>
        private static bool TryBuildRect(RectTransform target, out Rect rect)
        {
            rect = default;

            if (target == null || !target.gameObject.activeInHierarchy)
                return false;

            var canvas = target.GetComponentInParent<Canvas>();
            if (canvas == null)
                return false;

            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

            var corners = new Vector3[4];
            target.GetWorldCorners(corners);

            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            for (var i = 0; i < corners.Length; i++)
            {
                var screen = RectTransformUtility.WorldToScreenPoint(camera, corners[i]);
                min = Vector2.Min(min, screen);
                max = Vector2.Max(max, screen);
            }

            rect = new Rect(min, max - min);
            return rect.width > 1f && rect.height > 1f;
        }

        /// <summary>방을 화면 좌표의 네모로 바꾼다. 카메라가 없거나 뒤에 있으면 실패로 둔다.</summary>
        private bool TryBuildNodeRect(Node node, out Rect rect)
        {
            rect = default;

            var camera = Camera.main;
            if (camera == null || node == null)
                return false;

            var center = camera.WorldToScreenPoint(node.transform.position);
            if (center.z <= 0f)
                return false;

            // 방 하나가 화면에서 차지하는 크기는 줌에 따라 달라진다. 월드 반지름을 화면으로
            // 한 번 더 투영해서 재야 멀리서 봐도 구멍이 방에 맞는다.
            var edge = camera.WorldToScreenPoint(node.transform.position + Vector3.right * spotlightWorldRadius);
            var radius = Mathf.Max(48f, Mathf.Abs(edge.x - center.x));

            rect = new Rect(center.x - radius, center.y - radius, radius * 2f, radius * 2f);
            return true;
        }

        /// <summary>
        /// 겨눈 네모만 남기고 네 조각 어둠으로 화면을 덮는다. 어둠은 UI Toolkit 덮개가 그린다 —
        /// 팝업 패널에 떠서 uGUI와 다른 Toolkit 화면을 함께 덮는다(예전 uGUI 덮개는 정렬을 매번 다시
        /// 걸어야 관리 창 위로 올라왔다).
        /// </summary>
        private void ShowSpotlight(Rect hole)
        {
            if (EnsureOverlay())
                _overlay.SetHole(hole, spotlightPadding);
        }

        private void HideSpotlight()
        {
            _hasHole = false;
            if (_overlay != null)
                _overlay.SetDimVisible(false);
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

        /// <summary>이 칸이 끝났는가. 끝났으면 다음 칸을, 아니면 그대로 돌려준다.</summary>
        private Step Resolve(Step step) => step switch
        {
            Step.BuildRoom => FindDefenseRoom() != null ? Step.DeployUnit : step,
            Step.DeployUnit => HasDeployedUnit ? Step.BuildPortal : step,
            Step.BuildPortal => HasPortal ? Step.LearnMove : step,
            Step.LearnMove => HasCameraMoved ? Step.SurviveWave : step,
            Step.SurviveWave => IsWaveRunning ? Step.ObserveCombat : step,
            Step.ObserveCombat => !IsWaveRunning ? Step.ReviewSettlement : step,
            Step.ReviewSettlement => ResolveSettlementLesson(),
            Step.PrepareNextDay => _stepAge >= lessonSeconds ? Step.Idle : step,
            Step.Idle => ResolveIdle(),

            // 뒷날 수업은 그 기능을 실제로 열어 보면 끝난다. 안 열어도 시간이 지나면 걷는다 —
            // 이미 아는 사람을 붙잡아 둘 이유가 없다.
            Step.LearnMerchant => MerchantOpened || _stepAge >= lessonSeconds ? Step.Idle : step,
            _ => Step.Done,
        };

        /// <summary>
        /// 정산표가 실제로 한 번 열린 뒤 닫힐 때까지 기다린다.
        /// 표가 없는 특수 전투에서도 안내가 영원히 남지 않도록 시간을 안전장치로 둔다.
        /// </summary>
        private Step ResolveSettlementLesson()
        {
            var settlement = ManagementSettlementManager.Current;
            if (settlement != null && settlement.IsPanelOpen)
            {
                _settlementWasOpen = true;
                return Step.ReviewSettlement;
            }

            if (_settlementWasOpen || _stepAge >= lessonSeconds)
                return Step.PrepareNextDay;

            return Step.ReviewSettlement;
        }

        /// <summary>
        /// 쉬는 동안 새로 열린 기능이 있는지 본다.
        ///
        /// 해금은 날짜로 정해져 있으므로 그 날이 오면 한 번씩 짚어 준다. 한 번 가르친 것은
        /// 다시 꺼내지 않는다 — 매일 같은 걸 알려 주면 안내가 아니라 잔소리다.
        /// </summary>
        private Step ResolveIdle()
        {
            // 가르칠 것이 더 없으면 아주 끝낸다. 안 그러면 판이 끝날 때까지 0.25초마다
            // 씬을 뒤지며 이미 다 가르친 것을 다시 찾는다.
            if (_merchantTaught)
                return Step.Done;

            var day = CurrentDay;

            if (!_merchantTaught && day >= MerchantLessonDay && IsMerchantHere)
                return Step.LearnMerchant;

            return Step.Idle;
        }

        private static int CurrentDay => DayManager.Current != null ? DayManager.Current.CurrentDay : 1;

        private bool MerchantOpened
        {
            get
            {
                var merchant = ResolveMerchant();
                return merchant != null && merchant.IsPanelOpen;
            }
        }

        /// <summary>오늘 상인이 와 있어 여는 버튼이 보이는가.</summary>
        private bool IsMerchantHere
        {
            get
            {
                var merchant = ResolveMerchant();
                return merchant != null && merchant.IsMerchantHere;
            }
        }

        // 매 프레임 판정이 부르므로 한 번 구하면 들고 있는다.
        private MerchantPanelView ResolveMerchant()
        {
            if (merchantPanel == null)
                merchantPanel = SceneReferenceFallback.Find<MerchantPanelView>(this, nameof(merchantPanel));
            return merchantPanel;
        }

        /// <summary>
        /// 화면을 직접 밀어 봤는가.
        ///
        /// 이 칸만은 무엇을 지었는지가 아니라 손을 써 봤는지를 본다. 움직이는 법은 결과가
        /// 판에 남지 않으므로, 카메라가 처음 자리에서 얼마나 떠났는지로 판단할 수밖에 없다.
        /// </summary>
        private bool HasCameraMoved
        {
            get
            {
                var camera = Camera.main;
                if (camera == null)
                    return true;

                return Vector2.Distance(camera.transform.position, _moveStartPosition) >= moveLessonDistance;
            }
        }

        private static bool HasDeployedUnit
        {
            get
            {
                foreach (var node in Node.ActiveNodes)
                    foreach (var placement in node.UnitPlacements)
                        if (placement?.Instance != null && placement.Instance is not MainUnit)
                            return true;
                return false;
            }
        }

        private static Node FindDefenseRoom() => PickOutermost(node => !IsLocked(node)
            && node.Data != null && node.Data.Type != DungeonNodeType.Entrance
            && node.AssignedBuilding is not Portal);

        private static bool HasPortal =>
            WaveManager.Current != null && WaveManager.Current.HasPortal;

        private static bool IsWaveRunning =>
            WaveManager.Current != null && WaveManager.Current.IsWaveRunning;

        private void Render()
        {
            var text = HintFor(_step);
            var show = !string.IsNullOrEmpty(text);

            SetHintVisible(show);

            if (show && EnsureOverlay())
                _overlay.SetHint(HeaderFor(_step), text);
        }

        private static string HeaderFor(Step step) => step switch
        {
            Step.BuildRoom => "처음 시작하기 · 1/5",
            Step.DeployUnit => "처음 시작하기 · 2/5",
            Step.BuildPortal => "처음 시작하기 · 3/5",
            Step.LearnMove => "처음 시작하기 · 4/5",
            Step.SurviveWave => "처음 시작하기 · 5/5",
            Step.ObserveCombat => "첫 영업 · 방문객 관찰",
            Step.ReviewSettlement => "첫 영업 · 정산 읽기",
            Step.PrepareNextDay => "다음 날 준비",
            Step.LearnMerchant => "새 기능 · 떠돌이 상인",
            _ => "던전 안내",
        };

        /// <summary>
        /// 안내 화면을 준비한다. 안내 글·어둠·건너뛰기는 UI Toolkit 덮개가 그리고, 씬의 옛 uGUI
        /// 안내 상자와 어둠 네 장은 씬 정리 전까지 감춘다(Toolkit 전환 4단계).
        /// </summary>
        private void ConfigureHint()
        {
            HideLegacyOverlay();
            EnsureOverlay();
        }

        private bool EnsureOverlay()
        {
            if (_overlay != null)
                return true;

            _overlay = Code.UI.Toolkit.ToolkitScreenSO.Spawn<Code.UI.Toolkit.TutorialOverlayToolkitView>(
                Code.UI.Toolkit.TutorialOverlayToolkitView.ScreenResourcePath, Code.UI.Toolkit.TutorialOverlayToolkitView.RootName);
            if (_overlay == null)
                return false;

            _overlay.SkipClicked += SkipTutorial;
            return true;
        }

        private void HideLegacyOverlay()
        {
            var target = root != null ? root : gameObject;
            // 이 컴포넌트가 안내 상자 오브젝트에 붙어 있어 끄지 않고 투명하게 만든다(끄면 안내도 멈춘다).
            if (!target.TryGetComponent<CanvasGroup>(out var group))
                group = target.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;

            if (dimPanels == null)
                return;

            foreach (var panel in dimPanels)
            {
                if (panel != null)
                    panel.gameObject.SetActive(false);
            }
        }

        private void SetHintVisible(bool visible)
        {
            _hintVisible = visible;
            if (EnsureOverlay())
                _overlay.SetHintVisible(visible);
        }

        private string HintFor(Step step) => step switch
        {
            Step.BuildRoom or Step.DeployUnit or Step.BuildPortal => BuildHint(step, _buildStage),
            Step.LearnMove => "W A S D 로 던전을 둘러보세요  ·  마우스 휠로 화면을 확대할 수 있습니다",
            Step.SurviveWave => "준비됐다면 던전 영업을 시작하세요  ·  모험가는 오른쪽 문으로 들어와 시설을 이용하고 금고를 노립니다",
            Step.ObserveCombat => CombatHint,
            Step.ReviewSettlement => "정산표에서 보상·시설 수입과 유지비·이자를 확인하세요  ·  확인을 누르면 다음 날이 시작됩니다",
            Step.PrepareNextDay => "매출과 유지비를 확인하세요  ·  시설 동선과 몬스터 배치를 고쳐 다음 영업을 준비하세요",
            Step.LearnMerchant => "떠돌이 상인이 왔습니다  ·  유물은 유닛보다 비싸지만 조합이 붙습니다",
            _ => string.Empty,
        };

        private string CombatHint => _stepAge switch
        {
            < 5f => "전투는 자동으로 진행됩니다  ·  적의 길과 던전 주인의 체력을 지켜보세요",
            < 10f => "유닛이나 적을 누르면 체력·공격력·특성을 확인할 수 있습니다",
            _ => "모험가가 어느 시설에서 돈을 쓰는지 지켜보세요  ·  영업이 끝나면 정산표가 열립니다",
        };

        /// <summary>
        /// 첫날 세 칸의 한 줄. 잔걸음마다 갈아 끼운다.
        ///
        /// 왜 하는지는 첫 걸음에만 붙인다. 창을 열고 갈래를 고르는 동안까지 같은 이유를 다시
        /// 읽히면, 정작 지금 눌러야 할 것이 문장 끝으로 밀린다.
        /// </summary>
        private static string BuildHint(Step step, BuildStage stage) => step switch
        {
            Step.BuildRoom => stage switch
            {
                BuildStage.ConfirmRoom => "확장을 눌러 첫 방을 만드세요",
                BuildStage.PickNode => "밝은 타일을 눌러 첫 방을 파세요",
                BuildStage.PickCategory => "하단의 건설을 눌러 무엇을 지을지 고르세요",
                BuildStage.PressInstall => "설치를 누르고 지도에서 자리를 고르세요",
                _ => "지을 건물 카드를 고르세요",
            },

            Step.DeployUnit => stage switch
            {
                BuildStage.PickCell => "방 안의 밝은 칸을 눌러 수비병을 배치하세요",
                BuildStage.ConfirmRoom => "확장을 눌러 수비병을 배치할 방을 만드세요",
                BuildStage.PickNode => "유닛을 세울 방을 고르세요  ·  유닛이 선 방이 방어선이 됩니다",
                BuildStage.PickCategory => "하단의 유닛 배치를 누르세요",
                BuildStage.PressInstall => "배치를 누르세요",
                _ => "이 방에 세울 유닛 카드를 고르세요",
            },

            Step.BuildPortal => stage switch
            {
                BuildStage.ConfirmRoom => "확장을 눌러 포탈을 설치할 방을 만드세요",
                BuildStage.PickNode => "입구가 될 바깥쪽 방을 고르세요  ·  모험가는 그곳으로 들어옵니다",
                BuildStage.PickCategory => "하단의 건설을 누르세요",
                BuildStage.PressInstall => "설치를 누르세요",
                _ => "포탈을 고르세요",
            },

            _ => string.Empty,
        };
    }
}
