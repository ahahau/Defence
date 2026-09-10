using _01.Code.Buildings;
using _01.Code.MapCreateSystem;
using _01.Code.Manager;
using _01.Code.Units;
using DG.Tweening;
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
    /// 덮개를 못 펴면 아무것도 안 덮는다. 한 칸에서 오래 막혀 있어도 스스로 걷는다.
    /// 안내가 틀리는 것보다 판이 멈추는 것이 훨씬 나쁘다.
    /// </summary>
    public sealed class PlayTutorialView : MonoBehaviour
    {
        private enum Step
        {
            BuildRoom,
            DeployUnit,
            BuildPortal,
            LearnMove,
            SurviveWave,
            Done,
        }

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
        [SerializeField, Min(0.1f), Tooltip("방 하나를 덮을 월드 반지름. 구멍 크기를 재는 기준.")]
        private float spotlightWorldRadius = 2.4f;

        [Header("Pacing")]
        [SerializeField, Min(0f), Tooltip("한 칸을 최소 이만큼은 보여 준다. 조건이 이미 맞아도 여러 칸을 한꺼번에 건너뛰지 않게.")]
        private float minStepSeconds = 1.6f;

        [SerializeField, Min(0f), Tooltip("카메라가 다음 자리로 옮겨가는 데 걸리는 시간(초).")]
        private float cameraGlideSeconds = 0.7f;

        [SerializeField, Min(0.1f), Tooltip("WASD 칸을 넘기려면 화면을 이만큼 밀어야 한다(월드 단위).")]
        private float moveLessonDistance = 3.5f;

        [SerializeField, Min(0.01f), Tooltip("구멍이 다음 자리로 옮겨가는 데 걸리는 시간(초). 툭 튀지 않게 한다.")]
        private float holeGlideSeconds = 0.28f;

        private Vector3 _moveStartPosition;
        private Tween _cameraTween;
        private Rect _hole;
        private Rect _holeTarget;
        private bool _hasHole;
        private Step _step = Step.BuildRoom;
        private float _timer;
        private float _stepAge;
        private bool _released;

        private void OnEnable()
        {
            _timer = 0f;
            _stepAge = 0f;
            _released = false;
            _hasHole = false;
            var camera = Camera.main;
            if (camera != null)
                _moveStartPosition = camera.transform.position;
            Render();
        }

        private void OnDisable()
        {
            ReleaseControlLocks();
            _cameraTween?.Kill();
            HideSpotlight();
        }

        private void Update()
        {
            if (_step == Step.Done)
                return;

            _stepAge += Time.unscaledDeltaTime;

            ApplyControlLocks();

            // 판을 살피는 건 0.25초마다면 충분하다. 사람이 방을 짓는 속도에 견주면 즉시다.
            _timer -= Time.unscaledDeltaTime;
            if (_timer <= 0f)
            {
                _timer = pollInterval;

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

                ApplyGate();
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
            Render();

            if (_step == Step.Done)
            {
                ReleaseControlLocks();
                HideSpotlight();
                return;
            }

            // 앞 칸의 화면 이동을 먼저 끊는다. 살아 있는 채로 기준 위치를 잡으면, WASD 칸이
            // 손도 대기 전에 "움직였다"고 판단해 저절로 넘어간다.
            _cameraTween?.Kill();

            var camera = Camera.main;
            if (camera != null)
                _moveStartPosition = camera.transform.position;

            GlideCameraTo(ResolveTargetNode());
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
            var to = new Vector3(node.transform.position.x, node.transform.position.y, from.z);

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
            var guiding = forceStepOrder && !_released;
            Core.InputSystemCameraMover.ZoomLocked = guiding;
            Core.InputSystemCameraMover.MoveLocked = guiding && _step != Step.LearnMove;
        }

        private static void ReleaseControlLocks()
        {
            Core.InputSystemCameraMover.ZoomLocked = false;
            Core.InputSystemCameraMover.MoveLocked = false;
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
            if (_step == Step.LearnMove)
            {
                // 배울 것이 화면을 미는 일이라 가릴 자리가 없다. 덮개를 걷고 글만 남긴다.
                rect = default;
                return false;
            }

            if (_step == Step.SurviveWave)
                return TryBuildButtonRect(out rect);

            rect = default;
            var node = ResolveTargetNode();
            return node != null && TryBuildNodeRect(node, out rect);
        }

        /// <summary>이 칸에서 눌러야 할 방. 못 고르면 아무것도 덮지 않는다.</summary>
        private Node ResolveTargetNode() => _step switch
        {
            // 봉인을 여는 것과 그 방에 짓는 것이 한 칸 안에 함께 있다. 열린 빈 방이 있으면
            // 지을 차례이고, 없으면 아직 봉인을 열 차례다.
            Step.BuildRoom => FindUnlockedEmptyNode() ?? FindLockedNode(),
            Step.DeployUnit => FindUnlockedBuiltNode(),
            Step.BuildPortal => FindUnlockedEmptyNode(),
            _ => null,
        };

        /// <summary>
        /// 습격 시작 버튼을 화면 네모로 잰다.
        ///
        /// 버튼은 이미 화면 위의 것이라 방처럼 투영할 필요가 없다. 다만 캔버스가 화면에 직접
        /// 그리는지 카메라를 거치는지에 따라 모서리를 화면 좌표로 옮기는 방법이 달라진다.
        /// </summary>
        private bool TryBuildButtonRect(out Rect rect)
        {
            rect = default;

            // 습격 화면은 씬이 아니라 프리팹 안에 있어서 미리 물려 둘 수가 없다. 한 번 찾아
            // 들고 있는다 — 판이 도는 동안 이 화면이 갈리지는 않는다.
            if (waveView == null)
                waveView = FindAnyObjectByType<WaveView>();

            var target = waveView != null ? waveView.StartButtonRect : null;
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

        /// <summary>겨눈 네모만 남기고 네 장으로 화면을 덮는다.</summary>
        private void ShowSpotlight(Rect hole)
        {
            if (dimPanels == null || dimPanels.Length < 4)
                return;

            var canvasRect = dimPanels[0] != null ? dimPanels[0].parent as RectTransform : null;
            if (canvasRect == null)
                return;

            // 구멍은 화면 픽셀로 재 왔고 덮개는 캔버스 단위로 놓인다. 캔버스 배율이 1이 아니면
            // (해상도에 맞춰 늘리는 설정이면 늘 1이 아니다) 두 값의 단위가 달라 구멍이 어긋난다.
            var canvas = canvasRect.GetComponentInParent<Canvas>();
            var scale = canvas != null && canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;

            var size = canvasRect.rect.size;
            var pad = spotlightPadding / scale;

            var left = Mathf.Clamp(hole.xMin / scale - pad, 0f, size.x);
            var right = Mathf.Clamp(hole.xMax / scale + pad, 0f, size.x);
            var bottom = Mathf.Clamp(hole.yMin / scale - pad, 0f, size.y);
            var top = Mathf.Clamp(hole.yMax / scale + pad, 0f, size.y);

            Place(dimPanels[0], 0f, top, size.x, size.y - top);       // 위
            Place(dimPanels[1], 0f, 0f, size.x, bottom);              // 아래
            Place(dimPanels[2], 0f, bottom, left, top - bottom);      // 왼쪽
            Place(dimPanels[3], right, bottom, size.x - right, top - bottom); // 오른쪽

            SetSpotlightVisible(true);
        }

        private static void Place(RectTransform panel, float x, float y, float width, float height)
        {
            if (panel == null)
                return;

            panel.anchorMin = Vector2.zero;
            panel.anchorMax = Vector2.zero;
            panel.pivot = Vector2.zero;
            panel.anchoredPosition = new Vector2(x, y);
            panel.sizeDelta = new Vector2(Mathf.Max(0f, width), Mathf.Max(0f, height));
        }

        private void HideSpotlight()
        {
            _hasHole = false;
            SetSpotlightVisible(false);
        }

        private void SetSpotlightVisible(bool visible)
        {
            if (dimPanels == null)
                return;

            for (var i = 0; i < dimPanels.Length; i++)
            {
                if (dimPanels[i] != null && dimPanels[i].gameObject.activeSelf != visible)
                    dimPanels[i].gameObject.SetActive(visible);
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
        private Step Resolve(Step step) => step switch
        {
            Step.BuildRoom => HasNonPortalBuilding ? Step.DeployUnit : step,
            Step.DeployUnit => HasDeployedUnit ? Step.BuildPortal : step,
            Step.BuildPortal => HasPortal ? Step.LearnMove : step,
            Step.LearnMove => HasCameraMoved ? Step.SurviveWave : step,
            Step.SurviveWave => IsWaveRunning ? Step.Done : step,
            _ => Step.Done,
        };

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
            Step.BuildRoom => "밝은 타일을 눌러 첫 방을 파세요",
            Step.DeployUnit => "유닛을 고용해 방에 세우세요  ·  유닛이 선 방이 방어선이 됩니다",
            Step.BuildPortal => "입구에 포탈을 세우세요  ·  모험가는 그곳으로 들어옵니다",
            Step.LearnMove => "W A S D 로 던전을 둘러보세요",
            Step.SurviveWave => "준비됐다면 습격을 시작하세요  ·  막아내면 금화가, 뚫리면 빚이 남습니다",
            _ => string.Empty,
        };
    }
}
