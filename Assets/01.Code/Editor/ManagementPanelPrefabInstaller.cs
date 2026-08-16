#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace _01.Code.Editor
{
    /// <summary>
    /// 운영 패널의 고정 레이아웃을 프리팹 자산에 저장한다. 런타임 UI 코드는 카드의 내용만 채운다.
    /// Unity CLI: -executeMethod _01.Code.Editor.ManagementPanelPrefabInstaller.Apply
    /// </summary>
    public static class ManagementPanelPrefabInstaller
    {
        private const string NodePrefabPath = "Assets/04.Prefab/UI/Node.prefab";
        private const string RosterEntryPrefabPath = "Assets/04.Prefab/UI/RosterDeployEntry.prefab";
        private const string UnitDeployPrefabPath = "Assets/04.Prefab/UI/UnitDeploy.prefab";
        private const string UnitDeployEntryPrefabPath = "Assets/04.Prefab/UI/UnitDeployEntry.prefab";
        private const string SampleScenePath = "Assets/00.Scenes/SampleScene.unity";

        private static readonly Vector2 CardSize = new(176f, 230f);
        private static readonly Vector2 CardSpacing = new(12f, 12f);
        private static readonly Color DrawerColor = new(0.055f, 0.034f, 0.025f, 0.975f);
        private static readonly Color CardColor = new(0.13f, 0.075f, 0.045f, 0.98f);
        private static readonly Color AccentColor = new(0.84f, 0.56f, 0.2f, 1f);
        private static readonly Color ActionBandColor = new(0.075f, 0.042f, 0.025f, 0.94f);
        private static readonly Color PrimaryTextColor = new(0.96f, 0.91f, 0.82f, 1f);

        public static void Apply()
        {
            ConfigureNodePrefab();
            ConfigureRosterEntryPrefab();
            ConfigureUnitDeployPrefab();
            ConfigureUnitDeployEntryPrefab();
            ConfigureUnitDeploySceneInstance();
            AssetDatabase.SaveAssets();
            Debug.Log("Management panel prefabs updated with shared drawer and card layout.");
        }

        private static void ConfigureNodePrefab()
        {
            EditPrefab(NodePrefabPath, root =>
            {
                var panel = Find(root.transform, "NodePanel");
                ApplyDrawer(panel);

                ConfigureScroll(Find(root.transform, "UnitHireScrollView"), 3);
                ConfigureScroll(Find(root.transform, "BuildingHireScrollView"), 3);

                var installTemplate = Find(root.transform, "PortalInstallButton");
                ApplyCard(installTemplate);
            });
        }

        private static void ConfigureRosterEntryPrefab()
        {
            EditPrefab(RosterEntryPrefabPath, root =>
            {
                ApplyCard(root.transform);
                SetAnchor(Find(root.transform, "NameText"), new Vector2(0.08f, 0.25f), new Vector2(0.92f, 0.5f));
                StyleActionButton(Find(root.transform, "DeployButton"));
            });
        }

        private static void ConfigureUnitDeployPrefab()
        {
            EditPrefab(UnitDeployPrefabPath, root =>
            {
                var rootRect = root.transform as RectTransform;
                if (rootRect != null)
                {
                    ApplyUnitDeployRoot(rootRect);
                    rootRect.localScale = Vector3.one;
                }

                var panel = Find(root.transform, "UnitDeployPanel");
                ApplyDrawer(panel);
                ConfigureCentralDeployPanel(panel as RectTransform);
                ConfigureUnitDeployToggle(Find(root.transform, "UnitDeployToggleButton") as RectTransform);
                ConfigureScroll(Find(panel, "ScrollView"), 5);
            });
        }

        private static void ConfigureUnitDeploySceneInstance()
        {
            var scene = EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);
            var canvas = FirstScreenCanvas(scene);
            var deploy = canvas != null ? FindDirect(canvas.transform, "UnitDeploy") as RectTransform : null;
            if (deploy == null)
            {
                Debug.LogWarning("UnitDeploy scene instance is missing.");
                return;
            }

            ApplyUnitDeployRoot(deploy);
            ConfigureCentralDeployPanel(Find(deploy, "UnitDeployPanel") as RectTransform);
            ConfigureUnitDeployToggle(Find(deploy, "UnitDeployToggleButton") as RectTransform);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void ApplyUnitDeployRoot(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
        }

        private static void ConfigureCentralDeployPanel(RectTransform rect)
        {
            if (rect == null) return;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(1180f, 680f);
        }

        private static void ConfigureUnitDeployToggle(RectTransform rect)
        {
            if (rect == null) return;
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-355f, 20f);
            rect.sizeDelta = new Vector2(200f, 92f);
        }

        private static void ConfigureUnitDeployEntryPrefab()
        {
            EditPrefab(UnitDeployEntryPrefabPath, root =>
            {
                ApplyCard(root.transform);
                SetAnchor(Find(root.transform, "UnitIcon"), new Vector2(0.18f, 0.62f), new Vector2(0.82f, 0.94f));
                SetAnchor(Find(root.transform, "NameText"), new Vector2(0.08f, 0.43f), new Vector2(0.92f, 0.59f));
                SetAnchor(Find(root.transform, "CostText"), new Vector2(0.08f, 0.25f), new Vector2(0.92f, 0.38f));
                SetAnchor(Find(root.transform, "HireArea"), new Vector2(0.08f, 0.06f), new Vector2(0.92f, 0.19f));
            });
        }

        private static void ConfigureScroll(Transform scrollTransform, int columns)
        {
            if (scrollTransform == null)
                return;

            var scroll = scrollTransform.GetComponent<ScrollRect>();
            if (scroll == null || scroll.content == null)
                return;

            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            var content = scroll.content;
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(0f, 1f);
            content.pivot = new Vector2(0f, 1f);
            content.anchoredPosition = Vector2.zero;

            if (content.TryGetComponent<ContentSizeFitter>(out var fitter))
                fitter.enabled = false;

            var grid = content.GetComponent<GridLayoutGroup>();
            if (grid == null)
                grid = content.gameObject.AddComponent<GridLayoutGroup>();

            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperLeft;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = columns;
            grid.cellSize = CardSize;
            grid.spacing = CardSpacing;
            grid.padding = new RectOffset(12, 12, 12, 12);
        }

        private static void ApplyDrawer(Transform target)
        {
            if (target == null)
                return;

            var image = target.GetComponent<Image>();
            if (image != null)
            {
                image.sprite = null;
                image.type = Image.Type.Simple;
                image.color = DrawerColor;
            }

            var outline = target.GetComponent<Outline>() ?? target.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.66f, 0.41f, 0.14f, 1f);
            outline.effectDistance = new Vector2(2f, -2f);
        }

        private static void ApplyCard(Transform target)
        {
            if (target == null)
                return;

            if (target is RectTransform rect)
                rect.sizeDelta = CardSize;

            var layout = target.GetComponent<LayoutElement>() ?? target.gameObject.AddComponent<LayoutElement>();
            layout.minWidth = CardSize.x;
            layout.preferredWidth = CardSize.x;
            layout.minHeight = CardSize.y;
            layout.preferredHeight = CardSize.y;

            var image = target.GetComponent<Image>();
            if (image != null)
            {
                image.sprite = null;
                image.type = Image.Type.Simple;
                image.color = CardColor;
            }

            var outline = target.GetComponent<Outline>() ?? target.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.66f, 0.41f, 0.14f, 0.95f);
            outline.effectDistance = new Vector2(1f, -1f);

            EnsureDecoration(target, "CardTopRule", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -5f), new Vector2(0f, 5f), AccentColor);
            EnsureDecoration(target, "CardActionBand", new Vector2(0.04f, 0.035f), new Vector2(0.96f, 0.215f), Vector2.zero, Vector2.zero, ActionBandColor);
            StyleCardText(target);
        }

        private static void EnsureDecoration(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPosition,
            Vector2 sizeDelta,
            Color color)
        {
            var decoration = FindDirect(parent, name);
            if (decoration == null)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                decoration = go.transform;
                decoration.SetParent(parent, false);
                decoration.SetAsFirstSibling();
            }

            var rect = (RectTransform)decoration;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
            rect.pivot = new Vector2(0.5f, 0.5f);

            var image = decoration.GetComponent<Image>();
            image.sprite = null;
            image.type = Image.Type.Simple;
            image.color = color;
            image.raycastTarget = false;
        }

        private static void StyleCardText(Transform target)
        {
            foreach (var text in target.GetComponentsInChildren<TMP_Text>(true))
            {
                text.color = PrimaryTextColor;
                text.enableAutoSizing = true;
                text.fontSizeMin = 12f;
                text.fontSizeMax = 20f;
                text.textWrappingMode = TextWrappingModes.Normal;
                text.overflowMode = TextOverflowModes.Ellipsis;
                text.raycastTarget = false;
            }

            foreach (var text in target.GetComponentsInChildren<Text>(true))
            {
                text.color = PrimaryTextColor;
                text.resizeTextForBestFit = true;
                text.resizeTextMinSize = 11;
                text.resizeTextMaxSize = 18;
                text.alignment = TextAnchor.MiddleCenter;
                text.raycastTarget = false;
            }
        }

        private static void StyleActionButton(Transform buttonTransform)
        {
            if (buttonTransform == null)
                return;

            SetAnchor(buttonTransform, new Vector2(0.08f, 0.06f), new Vector2(0.92f, 0.19f));
            var image = buttonTransform.GetComponent<Image>();
            if (image != null)
            {
                image.sprite = null;
                image.type = Image.Type.Simple;
                image.color = new Color(0.31f, 0.17f, 0.07f, 1f);
            }

            var outline = buttonTransform.GetComponent<Outline>() ?? buttonTransform.gameObject.AddComponent<Outline>();
            outline.effectColor = AccentColor;
            outline.effectDistance = new Vector2(1f, -1f);
        }

        private static void SetAnchor(Transform target, Vector2 anchorMin, Vector2 anchorMax)
        {
            if (target is not RectTransform rect)
                return;

            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
        }

        private static void EditPrefab(string path, System.Action<GameObject> edit)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                edit(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static Transform Find(Transform root, string name)
        {
            if (root == null)
                return null;

            foreach (var child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == name)
                    return child;

            return null;
        }

        private static Transform FindDirect(Transform root, string name)
        {
            if (root == null)
                return null;

            for (var index = 0; index < root.childCount; index++)
            {
                var child = root.GetChild(index);
                if (child.name == name)
                    return child;
            }

            return null;
        }

        private static Canvas FirstScreenCanvas(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
                    if (canvas.renderMode != RenderMode.WorldSpace)
                        return canvas;
            return null;
        }
    }
}
#endif
