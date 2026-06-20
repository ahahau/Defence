using System.Collections;
using System.Collections.Generic;
using System.Text;
using _01.Code.Buildings;
using _01.Code.Core;
using _01.Code.Events;
using _01.Code.Manager;
using _01.Code.MapCreateSystem;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace _01.Code.UI
{
    /// <summary>
    /// Applies the sample_ui.png style HUD composition without changing game flow.
    /// </summary>
    public sealed class SampleUiLayoutController : MonoBehaviour
    {
        private const string LayoutRootName = "SampleUiLayoutFrame";
        private static SampleUiLayoutController instance;
        private readonly List<GameEventChannelSO> subscribedChannels = new();
        private RectTransform infoPanel;
        private TextMeshProUGUI infoTitleText;
        private TextMeshProUGUI infoText;
        private Button installButton;
        private Button demolishButton;
        private Node selectedNode;
        private DayManager dayManager;

        // Legacy runtime UI overlay disabled. This controller used to spawn the
        // "SampleUiLayoutFrame" overlay (dark sidebar / bottom bar / right info panel)
        // at runtime, which covered the screen and conflicted with the prefab UI.
        // Re-enable by restoring the [RuntimeInitializeOnLoadMethod] attribute below.
        // [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            InstallForActiveScene();
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            InstallForActiveScene();
        }

        private static void InstallForActiveScene()
        {
            if (SceneManager.GetActiveScene().name != "SampleScene")
                return;

            if (instance != null)
                return;

            var host = new GameObject(nameof(SampleUiLayoutController));
            DontDestroyOnLoad(host);
            instance = host.AddComponent<SampleUiLayoutController>();
        }

        private void Start()
        {
            StartCoroutine(ApplyAfterUiAwake());
        }

        private IEnumerator ApplyAfterUiAwake()
        {
            yield return null;
            ApplyLayout();
            dayManager = FindFirstObjectByType<DayManager>();
            SubscribeToLoadedEventChannels();
            SetInfoText(null);
        }

        private void OnDestroy()
        {
            UnsubscribeFromEventChannels();

            if (instance == this)
                instance = null;
        }

        private void ApplyLayout()
        {
            var canvas = FindPrimaryCanvas();
            if (canvas == null)
                return;

            var canvasRect = canvas.transform as RectTransform;
            if (canvasRect == null)
                return;

            var frameRoot = EnsureFrameRoot(canvasRect);
            frameRoot.SetAsFirstSibling();

            CreateFrame(frameRoot, "RightSidebar", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-438f, 0f), new Vector2(0f, 0f), new Vector2(1f, 0f), new Color(0.06f, 0.04f, 0.10f, 0.92f));
            CreateFrame(frameRoot, "BottomBar", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(-438f, 118f), new Vector2(0f, 0f), new Color(0.05f, 0.04f, 0.08f, 0.88f));
            CreateInfoPanel(canvasRect);

            LayoutTopRightMetric("MagicPanel", -232f, 198f);
            LayoutTopRightMetric("DangerPanel", -28f, 198f);
            LayoutRightPanel("GoldCostPanel", -28f, -84f, 392f, 62f);
            LayoutRightPanel("MoraleHud", -28f, -154f, 392f, 68f);
            LayoutRightPanel("TimeSpeedControl", -28f, -228f, 392f, 42f);

            LayoutBottom("ArtifactPanel", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(18f, 18f), new Vector2(270f, 92f), new Vector2(0f, 0f));
            LayoutBottom("PlayerStatusHud", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-330f, 18f), new Vector2(720f, 92f), new Vector2(0.5f, 0f));
            LayoutBottom("DayText", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(252f, 18f), new Vector2(170f, 72f), new Vector2(0.5f, 0f));
            LayoutBottom("UnitDeploy", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-238f, 20f), new Vector2(200f, 92f), new Vector2(1f, 0f));
            LayoutBottom("SkipDayButton", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-28f, 20f), new Vector2(190f, 72f), new Vector2(1f, 0f));

            SetUnitDeployButtonSize();
            ApplyExistingUiSprites();
            BringInteractiveHudToFront();
        }

        private static Canvas FindPrimaryCanvas()
        {
            var canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Canvas best = null;
            foreach (var canvas in canvases)
            {
                if (canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                    continue;

                if (best == null || canvas.sortingOrder > best.sortingOrder)
                    best = canvas;
            }

            return best;
        }

        private static RectTransform EnsureFrameRoot(RectTransform canvas)
        {
            var existing = canvas.Find(LayoutRootName) as RectTransform;
            if (existing != null)
                return existing;

            var root = new GameObject(LayoutRootName, typeof(RectTransform));
            var rect = root.transform as RectTransform;
            rect.SetParent(canvas, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            return rect;
        }

        private static void CreateFrame(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, Vector2 pivot, Color color)
        {
            var rect = EnsureChildImage(parent, name, color);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            rect.pivot = pivot;
        }

        private void CreateInfoPanel(RectTransform parent)
        {
            var rect = EnsureChildImage(parent, "RightInfoPanel", new Color(0.045f, 0.035f, 0.06f, 0.96f));
            infoPanel = rect;
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.offsetMin = new Vector2(-420f, 130f);
            rect.offsetMax = new Vector2(-28f, -278f);

            var outline = rect.GetComponent<Outline>() ?? rect.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.48f, 0.32f, 0.82f, 0.88f);
            outline.effectDistance = new Vector2(2.5f, -2.5f);

            var header = rect.Find("Header") as RectTransform;
            if (header == null)
            {
                var headerObject = new GameObject("Header", typeof(RectTransform), typeof(TextMeshProUGUI));
                header = headerObject.transform as RectTransform;
                header.SetParent(rect, false);
            }

            header.anchorMin = new Vector2(0f, 1f);
            header.anchorMax = new Vector2(1f, 1f);
            header.pivot = new Vector2(0.5f, 1f);
            header.offsetMin = new Vector2(26f, -70f);
            header.offsetMax = new Vector2(-26f, -22f);

            infoTitleText = header.GetComponent<TextMeshProUGUI>();
            infoTitleText.raycastTarget = false;
            infoTitleText.color = new Color(0.85f, 0.75f, 1f, 1f);
            infoTitleText.fontSize = 24f;
            infoTitleText.fontStyle = FontStyles.Bold;
            infoTitleText.alignment = TextAlignmentOptions.Left;
            infoTitleText.text = "방 정보";

            var label = rect.Find("Label") as RectTransform;
            if (label == null)
            {
                var labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                label = labelObject.transform as RectTransform;
                label.SetParent(rect, false);
            }

            label.anchorMin = Vector2.zero;
            label.anchorMax = Vector2.one;
            label.offsetMin = new Vector2(26f, 116f);
            label.offsetMax = new Vector2(-26f, -86f);

            infoText = label.GetComponent<TextMeshProUGUI>();
            infoText.raycastTarget = false;
            infoText.color = new Color(0.88f, 0.90f, 0.86f, 1f);
            infoText.fontSize = 20f;
            infoText.lineSpacing = 8f;
            infoText.alignment = TextAlignmentOptions.TopLeft;
            infoText.text = "각 방을 클릭하면\n상세 내용이 여기에 표시됩니다.\n\n방의 배치나 설치 내용을\n이곳에서 확인하고 수정할 수 있습니다.";

            installButton = EnsureActionButton(rect, "InstallButton", "설치", new Vector2(26f, 28f), new Vector2(164f, 66f), new Color(0.2f, 0.36f, 0.25f, 0.96f));
            demolishButton = EnsureActionButton(rect, "DemolishButton", "철거", new Vector2(202f, 28f), new Vector2(164f, 66f), new Color(0.36f, 0.15f, 0.16f, 0.96f));

            installButton.onClick.RemoveListener(HandleInstallClicked);
            installButton.onClick.AddListener(HandleInstallClicked);
            demolishButton.onClick.RemoveListener(HandleDemolishClicked);
            demolishButton.onClick.AddListener(HandleDemolishClicked);
        }

        private static RectTransform EnsureChildImage(RectTransform parent, string name, Color color)
        {
            var child = parent.Find(name) as RectTransform;
            if (child == null)
            {
                var childObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                child = childObject.transform as RectTransform;
                child.SetParent(parent, false);
            }

            var image = child.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return child;
        }

        private static Button EnsureActionButton(RectTransform parent, string name, string label, Vector2 anchoredPosition, Vector2 size, Color color)
        {
            var rect = parent.Find(name) as RectTransform;
            if (rect == null)
            {
                var buttonObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                rect = buttonObject.transform as RectTransform;
                rect.SetParent(parent, false);
            }

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            var image = rect.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = true;
            image.type = Image.Type.Sliced;

            var button = rect.GetComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = color;
            colors.highlightedColor = Color.Lerp(color, Color.white, 0.16f);
            colors.pressedColor = Color.Lerp(color, Color.black, 0.18f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(color.r * 0.55f, color.g * 0.55f, color.b * 0.55f, 0.42f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.06f;
            button.colors = colors;

            var textRect = rect.Find("Label") as RectTransform;
            if (textRect == null)
            {
                var textObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                textRect = textObject.transform as RectTransform;
                textRect.SetParent(rect, false);
            }

            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var text = textRect.GetComponent<TextMeshProUGUI>();
            text.raycastTarget = false;
            text.text = label;
            text.color = new Color(0.96f, 0.96f, 0.92f, 1f);
            text.fontSize = 24f;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;

            var outline = rect.GetComponent<Outline>() ?? rect.gameObject.AddComponent<Outline>();
            outline.effectColor = Color.Lerp(color, new Color(1f, 0.9f, 0.45f, 1f), 0.45f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            return button;
        }

        private static void LayoutTopRightMetric(string objectName, float right, float width)
        {
            var rect = FindRect(objectName);
            if (rect == null)
                return;

            rect.anchorMin = Vector2.one;
            rect.anchorMax = Vector2.one;
            rect.pivot = Vector2.one;
            rect.anchoredPosition = new Vector2(right, -20f);
            rect.sizeDelta = new Vector2(width, 52f);
        }

        private static void LayoutRightPanel(string objectName, float right, float top, float width, float height)
        {
            var rect = FindRect(objectName);
            if (rect == null)
                return;

            rect.anchorMin = Vector2.one;
            rect.anchorMax = Vector2.one;
            rect.pivot = Vector2.one;
            rect.anchoredPosition = new Vector2(right, top);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static void LayoutBottom(string objectName, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size, Vector2 pivot)
        {
            var rect = FindRect(objectName);
            if (rect == null)
                return;

            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static RectTransform FindRect(string objectName)
        {
            var target = GameObject.Find(objectName);
            return target != null ? target.transform as RectTransform : null;
        }

        private static void SetUnitDeployButtonSize()
        {
            var unitDeploy = GameObject.Find("UnitDeploy");
            if (unitDeploy == null)
                return;

            var button = unitDeploy.transform.Find("UnitDeployToggleButton") as RectTransform;
            if (button == null)
                return;

            button.anchorMin = new Vector2(1f, 0.5f);
            button.anchorMax = new Vector2(1f, 0.5f);
            button.pivot = new Vector2(1f, 0.5f);
            button.anchoredPosition = Vector2.zero;
            button.sizeDelta = new Vector2(190f, 72f);
        }

        private void ApplyExistingUiSprites()
        {
            var panelSprite = ResolveSprite("GoldCostPanel", "MoraleHud", "PlayerStatusHud", "ArtifactPanel");
            if (panelSprite != null && infoPanel != null)
            {
                var image = infoPanel.GetComponent<Image>();
                image.sprite = panelSprite;
                image.type = Image.Type.Sliced;
            }

            var buttonSprite = ResolveSprite("SkipDayButton", "UnitDeployToggleButton", "UnitDeploy");
            ApplyButtonSprite(installButton, buttonSprite);
            ApplyButtonSprite(demolishButton, buttonSprite);
        }

        private static Sprite ResolveSprite(params string[] objectNames)
        {
            foreach (var objectName in objectNames)
            {
                var target = GameObject.Find(objectName);
                if (target == null)
                    continue;

                var image = target.GetComponent<Image>() ?? target.GetComponentInChildren<Image>(true);
                if (image != null && image.sprite != null)
                    return image.sprite;
            }

            return null;
        }

        private static void ApplyButtonSprite(Button button, Sprite sprite)
        {
            if (button == null || sprite == null)
                return;

            var image = button.targetGraphic as Image;
            if (image == null)
                return;

            image.sprite = sprite;
            image.type = Image.Type.Sliced;
        }

        private static void BringInteractiveHudToFront()
        {
            BringToFront("RightInfoPanel");
            BringToFront("TimeSpeedControl");
            BringToFront("GoldCostPanel");
            BringToFront("MoraleHud");
            BringToFront("DangerPanel");
            BringToFront("MagicPanel");
            BringToFront("ArtifactPanel");
            BringToFront("PlayerStatusHud");
            BringToFront("DayText");
            BringToFront("UnitDeploy");
            BringToFront("SkipDayButton");
        }

        private static void BringToFront(string objectName)
        {
            var target = GameObject.Find(objectName);
            if (target != null)
                target.transform.SetAsLastSibling();
        }

        private void SubscribeToLoadedEventChannels()
        {
            UnsubscribeFromEventChannels();

            foreach (var channel in Resources.FindObjectsOfTypeAll<GameEventChannelSO>())
            {
                if (channel == null || subscribedChannels.Contains(channel))
                    continue;

                channel.AddListener<UnlockedNodeClickedEvent>(HandleNodeClicked);
                channel.AddListener<UnitAssignedToNodeEvent>(HandleUnitAssigned);
                channel.AddListener<UnitReturnedFromNodeEvent>(HandleUnitReturned);
                channel.AddListener<BuildingInstalledEvent>(HandleBuildingInstalled);
                channel.AddListener<WaveStartedEvent>(HandleWaveStateChanged);
                channel.AddListener<WaveEndedEvent>(HandleWaveStateChanged);
                subscribedChannels.Add(channel);
            }
        }

        private void UnsubscribeFromEventChannels()
        {
            foreach (var channel in subscribedChannels)
            {
                if (channel == null)
                    continue;

                channel.RemoveListener<UnlockedNodeClickedEvent>(HandleNodeClicked);
                channel.RemoveListener<UnitAssignedToNodeEvent>(HandleUnitAssigned);
                channel.RemoveListener<UnitReturnedFromNodeEvent>(HandleUnitReturned);
                channel.RemoveListener<BuildingInstalledEvent>(HandleBuildingInstalled);
                channel.RemoveListener<WaveStartedEvent>(HandleWaveStateChanged);
                channel.RemoveListener<WaveEndedEvent>(HandleWaveStateChanged);
            }

            subscribedChannels.Clear();
        }

        private void HandleNodeClicked(UnlockedNodeClickedEvent evt)
        {
            selectedNode = evt.Node;
            SetInfoText(selectedNode);
        }

        private void HandleUnitAssigned(UnitAssignedToNodeEvent evt)
        {
            if (evt.Node == selectedNode)
                SetInfoText(selectedNode);
        }

        private void HandleUnitReturned(UnitReturnedFromNodeEvent evt)
        {
            if (evt.Node == selectedNode)
                SetInfoText(selectedNode);
        }

        private void HandleWaveStateChanged(WaveStartedEvent evt)
        {
            StartCoroutine(RefreshActionsNextFrame());
        }

        private void HandleWaveStateChanged(WaveEndedEvent evt)
        {
            StartCoroutine(RefreshActionsNextFrame());
        }

        private IEnumerator RefreshActionsNextFrame()
        {
            yield return null;
            RefreshActionButtons();
        }

        private void HandleBuildingInstalled(BuildingInstalledEvent evt)
        {
            if (evt.Node == selectedNode)
                SetInfoText(selectedNode);
        }

        private void SetInfoText(Node node)
        {
            if (infoText == null)
                return;

            if (node == null || node.Data == null)
            {
                infoText.text = "각 방을 클릭하면\n상세 내용이 여기에 표시됩니다.\n\n방의 배치나 설치 내용을\n이곳에서 확인하고 수정할 수 있습니다.";
                RefreshActionButtons();
                return;
            }

            var builder = new StringBuilder();
            builder.AppendLine(GetRoomTitle(node));
            builder.AppendLine();

            if (!node.HasInstallation)
            {
                builder.AppendLine("상태: 비어 있음");
                builder.AppendLine("설치된 유닛/건물 없음");
                builder.AppendLine();
                builder.AppendLine("오른쪽 설치 버튼으로");
                builder.AppendLine("배치할 대상을 선택하세요.");
                infoText.text = builder.ToString();
                RefreshActionButtons();
                return;
            }

            builder.AppendLine($"위치: {node.GridPosition.x}, {node.GridPosition.y}");
            builder.AppendLine($"위험도: {node.DangerLevel}");

            if (node.AssignedUnit != null)
            {
                builder.AppendLine();
                builder.AppendLine("유닛");
                builder.AppendLine(node.AssignedUnit.Name);
                builder.AppendLine($"등급: {(int)node.AssignedUnit.Grade}");
                builder.AppendLine($"마력: {node.AssignedUnit.MagicCost}");
                builder.AppendLine($"방어: {node.AssignedUnit.Defense}");
                builder.AppendLine($"회피: {Mathf.RoundToInt(node.AssignedUnit.EvasionChance * 100f)}%");
                builder.AppendLine($"위험도: {node.AssignedUnit.BaseDanger}");
            }

            if (node.AssignedBuilding != null)
            {
                builder.AppendLine();
                builder.AppendLine("설치물");
                builder.AppendLine(GetBuildingName(node.AssignedBuilding));
                builder.AppendLine($"위험도: {node.AssignedBuilding.DangerRating}");
            }

            infoText.text = builder.ToString();
            RefreshActionButtons();
        }

        private void RefreshActionButtons()
        {
            var canManage = dayManager == null || dayManager.IsStandby;
            if (installButton != null)
                installButton.interactable = canManage && selectedNode != null && !selectedNode.HasInstallation;

            if (demolishButton != null)
            {
                var canReturnUnit = selectedNode != null
                                    && selectedNode.HasAssignedUnit
                                    && selectedNode.AssignedUnitInstance != null
                                    && !selectedNode.AssignedUnitInstance.NeedsRecovery
                                    && (NodePanelView.Current == null || NodePanelView.Current.CanReturnSelectedUnit());
                demolishButton.interactable = canManage
                                               && selectedNode != null
                                               && (selectedNode.HasAssignedBuilding || canReturnUnit);
                SetActionButtonLabel(
                    demolishButton,
                    selectedNode != null && selectedNode.HasAssignedUnit
                        ? canReturnUnit ? "회수" : "회복 필요"
                        : "철거");
            }
        }

        private void HandleInstallClicked()
        {
            if (selectedNode == null || selectedNode.HasInstallation)
                return;

            NodePanelView.Current?.ShowSelectedNodeInstallOptions();
        }

        private void HandleDemolishClicked()
        {
            if (selectedNode == null || (dayManager != null && !dayManager.IsStandby))
                return;

            if (selectedNode.HasAssignedUnit)
                NodePanelView.Current?.ReturnSelectedUnit();
            else if (selectedNode.HasAssignedBuilding)
                NodePanelView.Current?.DemolishSelectedBuilding();

            SetInfoText(selectedNode);
        }

        private static void SetActionButtonLabel(Button button, string value)
        {
            var label = button != null ? button.GetComponentInChildren<TMP_Text>(true) : null;
            if (label != null)
                label.text = value;
        }

        private static string GetRoomTitle(Node node)
        {
            return node.Data.Type switch
            {
                DungeonNodeType.Corridor => "복도",
                DungeonNodeType.Entrance => "입구",
                DungeonNodeType.Trap => "트랩 방",
                DungeonNodeType.Lair => "소굴",
                DungeonNodeType.Treasury => "보물 방",
                DungeonNodeType.Boss => "보스 방",
                _ => node.Data.Type.ToString()
            };
        }

        private static string GetBuildingName(Building building)
        {
            return building switch
            {
                Portal => "포탈",
                Trap => "트랩",
                _ => building.GetType().Name
            };
        }
    }
}
