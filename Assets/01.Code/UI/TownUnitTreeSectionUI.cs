using System.Collections.Generic;
using _01.Code.TownPanels;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _01.Code.UI
{
    public class TownUnitTreeSectionUI : MonoBehaviour
    {
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private RectTransform viewport;
        [SerializeField] private RectTransform contentRoot;
        [SerializeField] private TownTreeLineRenderer lineRenderer;
        [SerializeField] private TownUnitTreeNodeUI nodePrefab;
        [SerializeField] private Vector2 nodeSize = new(320f, 148f);
        [SerializeField] private Vector2 contentPadding = new(180f, 140f);
        [SerializeField] private Color backgroundColor = new(0.16f, 0.18f, 0.22f, 0.72f);
        [SerializeField] private Color viewportColor = new(0.07f, 0.08f, 0.10f, 0.18f);
        [SerializeField] private Color lineColor = new(0.86f, 0.75f, 0.36f, 0.9f);
        [SerializeField] private float defaultConnectionBendOffset = 140f;
        [SerializeField] private TMP_FontAsset fallbackFontAsset;
        [SerializeField] private Sprite fallbackIconSprite;

        private readonly List<TownUnitTreeNodeUI> _runtimeNodes = new();
        private readonly Dictionary<string, RectTransform> _nodeRectsById = new();
        public void Render(TownUnitTreePanelSectionSO section, TownInteriorScreenUI owner)
        {
            EnsureReferences();
            ClearRuntimeNodes();

            if (section == null || contentRoot == null)
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(transform as RectTransform);
            LayoutRebuilder.ForceRebuildLayoutImmediate(viewport);
            ResizeContent(section.Nodes);
            CreateNodes(section.Nodes, owner);
            DrawConnections(section);

            if (scrollRect != null)
            {
                scrollRect.horizontalNormalizedPosition = 0f;
                scrollRect.verticalNormalizedPosition = 1f;
            }
        }

        public void Hide()
        {
            ClearRuntimeNodes();
            gameObject.SetActive(false);
        }

        private void EnsureReferences()
        {
            if (scrollRect == null || viewport == null || contentRoot == null || lineRenderer == null)
            {
                return;
            }

            scrollRect.horizontal = true;
            scrollRect.vertical = true;
            lineRenderer.color = lineColor;

            if (fallbackFontAsset == null)
            {
                TextMeshProUGUI existingText = GetComponentInParent<Canvas>(true)?.GetComponentInChildren<TextMeshProUGUI>(true);
                if (existingText != null)
                {
                    fallbackFontAsset = existingText.font;
                }
            }

            fallbackFontAsset ??= TMP_Settings.defaultFontAsset;

            fallbackIconSprite ??= Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
        }

        private void ResizeContent(List<TownUnitTreeNodeEntry> nodes)
        {
            Vector2 origin = CalculateNodeOrigin(nodes);
            float width = 0f;
            float height = 0f;

            if (nodes != null)
            {
                for (int i = 0; i < nodes.Count; i++)
                {
                    TownUnitTreeNodeEntry node = nodes[i];
                    if (node == null)
                    {
                        continue;
                    }

                    width = Mathf.Max(width, node.CanvasPosition.x - origin.x + nodeSize.x);
                    height = Mathf.Max(height, node.CanvasPosition.y - origin.y + nodeSize.y);
                }
            }

            if (viewport != null)
            {
                width = Mathf.Max(width + contentPadding.x * 2f, viewport.rect.width);
                height = Mathf.Max(height + contentPadding.y * 2f, viewport.rect.height);
            }

            contentRoot.anchorMin = new Vector2(0f, 1f);
            contentRoot.anchorMax = new Vector2(0f, 1f);
            contentRoot.pivot = new Vector2(0f, 1f);
            contentRoot.sizeDelta = new Vector2(width, height);

            if (lineRenderer != null)
            {
                RectTransform lineRect = lineRenderer.rectTransform;
                lineRect.anchorMin = new Vector2(0f, 1f);
                lineRect.anchorMax = new Vector2(0f, 1f);
                lineRect.pivot = new Vector2(0f, 1f);
                lineRect.anchoredPosition = Vector2.zero;
                lineRect.sizeDelta = contentRoot.sizeDelta;
                lineRenderer.gameObject.SetActive(true);
                lineRect.SetAsFirstSibling();
            }
        }

        private void CreateNodes(List<TownUnitTreeNodeEntry> nodes, TownInteriorScreenUI owner)
        {
            if (nodes == null)
            {
                return;
            }

            Vector2 origin = CalculateNodeOrigin(nodes);
            for (int i = 0; i < nodes.Count; i++)
            {
                TownUnitTreeNodeEntry entry = nodes[i];
                if (entry == null)
                {
                    continue;
                }

                TownUnitTreeNodeUI nodeUi = CreateNodeInstance();
                RectTransform nodeRect = nodeUi.transform as RectTransform;
                if (nodeRect == null)
                {
                    continue;
                }

                string nodeId = entry.GetNodeId();
                nodeUi.name = string.IsNullOrWhiteSpace(nodeId) ? $"UnitTreeNode_{i}" : $"UnitTreeNode_{nodeId}";
                nodeRect.SetParent(contentRoot, false);
                nodeRect.anchorMin = new Vector2(0f, 1f);
                nodeRect.anchorMax = new Vector2(0f, 1f);
                nodeRect.pivot = new Vector2(0f, 1f);
                nodeRect.sizeDelta = nodeSize;
                nodeRect.anchoredPosition = new Vector2(
                    contentPadding.x + entry.CanvasPosition.x - origin.x,
                    -contentPadding.y - (entry.CanvasPosition.y - origin.y));

                nodeUi.EnableFor(entry, owner);
                nodeRect.SetAsLastSibling();
                _runtimeNodes.Add(nodeUi);

                if (!string.IsNullOrWhiteSpace(nodeId) && !_nodeRectsById.ContainsKey(nodeId))
                {
                    _nodeRectsById.Add(nodeId, nodeRect);
                }
            }
        }

        private void DrawConnections(TownUnitTreePanelSectionSO section)
        {
            if (lineRenderer == null)
            {
                return;
            }

            List<Vector2> points = new();
            List<TownUnitTreeConnectionEntry> connections = ResolveConnections(section);
            for (int i = 0; i < connections.Count; i++)
            {
                TownUnitTreeConnectionEntry connection = connections[i];
                if (connection == null ||
                    string.IsNullOrWhiteSpace(connection.FromNodeId) ||
                    string.IsNullOrWhiteSpace(connection.ToNodeId) ||
                    !_nodeRectsById.TryGetValue(connection.FromNodeId, out RectTransform sourceRect) ||
                    !_nodeRectsById.TryGetValue(connection.ToNodeId, out RectTransform targetRect))
                {
                    continue;
                }

                Vector2 sourceCenter = GetRightCenter(sourceRect);
                Vector2 targetCenter = GetLeftCenter(targetRect);
                float bendOffset = connection.BendOffset > 0f ? connection.BendOffset : defaultConnectionBendOffset;
                float bendX = Mathf.Min(sourceCenter.x + bendOffset, targetCenter.x - 40f);
                Vector2 firstCorner = new Vector2(bendX, sourceCenter.y);
                Vector2 secondCorner = new Vector2(bendX, targetCenter.y);

                AppendSegment(points, ContentPointToLinePoint(sourceCenter), ContentPointToLinePoint(firstCorner));
                AppendSegment(points, ContentPointToLinePoint(firstCorner), ContentPointToLinePoint(secondCorner));
                AppendSegment(points, ContentPointToLinePoint(secondCorner), ContentPointToLinePoint(targetCenter));
            }

            lineRenderer.SetSegments(points);
        }

        private List<TownUnitTreeConnectionEntry> ResolveConnections(TownUnitTreePanelSectionSO section)
        {
            List<TownUnitTreeConnectionEntry> connections = new();
            if (section == null)
            {
                return connections;
            }

            if (section.Connections != null && section.Connections.Count > 0)
            {
                connections.AddRange(section.Connections);
                return connections;
            }

            if (section.Nodes == null)
            {
                return connections;
            }

            for (int i = 0; i < section.Nodes.Count; i++)
            {
                TownUnitTreeNodeEntry node = section.Nodes[i];
                if (node == null)
                {
                    continue;
                }

                string sourceId = node.GetNodeId();
                for (int nextIndex = 0; nextIndex < node.NextNodeIds.Count; nextIndex++)
                {
                    string nextId = node.NextNodeIds[nextIndex];
                    if (string.IsNullOrWhiteSpace(sourceId) || string.IsNullOrWhiteSpace(nextId))
                    {
                        continue;
                    }

                    connections.Add(new TownUnitTreeConnectionEntry(sourceId, nextId, defaultConnectionBendOffset));
                }
            }

            return connections;
        }

        private void AppendSegment(List<Vector2> points, Vector2 start, Vector2 end)
        {
            points.Add(start);
            points.Add(end);
        }

        private Vector2 ContentPointToLinePoint(Vector2 contentPoint)
        {
            if (contentRoot == null || lineRenderer == null)
            {
                return contentPoint;
            }

            Vector3 worldPoint = contentRoot.TransformPoint(contentPoint);
            return lineRenderer.rectTransform.InverseTransformPoint(worldPoint);
        }

        private Vector2 CalculateNodeOrigin(List<TownUnitTreeNodeEntry> nodes)
        {
            if (nodes == null || nodes.Count == 0)
            {
                return Vector2.zero;
            }

            bool hasNode = false;
            Vector2 origin = Vector2.zero;
            for (int i = 0; i < nodes.Count; i++)
            {
                TownUnitTreeNodeEntry node = nodes[i];
                if (node == null)
                {
                    continue;
                }

                if (!hasNode)
                {
                    origin = node.CanvasPosition;
                    hasNode = true;
                    continue;
                }

                origin.x = Mathf.Min(origin.x, node.CanvasPosition.x);
                origin.y = Mathf.Min(origin.y, node.CanvasPosition.y);
            }

            return hasNode ? origin : Vector2.zero;
        }

        private Vector2 GetRightCenter(RectTransform rectTransform)
        {
            return new Vector2(
                rectTransform.anchoredPosition.x + rectTransform.rect.width,
                rectTransform.anchoredPosition.y - rectTransform.rect.height * 0.5f);
        }

        private Vector2 GetLeftCenter(RectTransform rectTransform)
        {
            return new Vector2(
                rectTransform.anchoredPosition.x,
                rectTransform.anchoredPosition.y - rectTransform.rect.height * 0.5f);
        }

        private TownUnitTreeNodeUI CreateNodeInstance()
        {
            if (nodePrefab != null)
            {
                TownUnitTreeNodeUI prefabInstance = Instantiate(nodePrefab);
                EnsureNodeTemplate(prefabInstance);
                return prefabInstance;
            }

            GameObject nodeObject = new("UnitTreeNode", typeof(RectTransform), typeof(Image), typeof(TownUnitTreeNodeUI));
            RectTransform nodeRect = nodeObject.GetComponent<RectTransform>();
            nodeRect.sizeDelta = nodeSize;

            Image backgroundImage = nodeObject.GetComponent<Image>();
            backgroundImage.color = backgroundColor;
            backgroundImage.raycastTarget = true;

            GameObject iconObject = new("Icon", typeof(RectTransform), typeof(Image));
            RectTransform iconRect = iconObject.GetComponent<RectTransform>();
            iconRect.SetParent(nodeRect, false);
            iconRect.anchorMin = new Vector2(0f, 1f);
            iconRect.anchorMax = new Vector2(0f, 1f);
            iconRect.pivot = new Vector2(0f, 1f);
            iconRect.anchoredPosition = new Vector2(16f, -16f);
            iconRect.sizeDelta = new Vector2(64f, 64f);
            Image iconImage = iconObject.GetComponent<Image>();
            iconImage.sprite = fallbackIconSprite;
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;
            iconImage.color = new Color(1f, 0.82f, 0.28f, 1f);

            GameObject titleObject = new("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
            RectTransform titleRect = titleObject.GetComponent<RectTransform>();
            titleRect.SetParent(nodeRect, false);
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0f, 1f);
            titleRect.offsetMin = new Vector2(92f, -44f);
            titleRect.offsetMax = new Vector2(-16f, -16f);
            TextMeshProUGUI titleText = titleObject.GetComponent<TextMeshProUGUI>();
            if (fallbackFontAsset != null)
            {
                titleText.font = fallbackFontAsset;
            }
            titleText.fontSize = 28f;
            titleText.overflowMode = TextOverflowModes.Ellipsis;
            titleText.color = Color.white;
            titleText.raycastTarget = false;

            GameObject descriptionObject = new("Description", typeof(RectTransform), typeof(TextMeshProUGUI));
            RectTransform descriptionRect = descriptionObject.GetComponent<RectTransform>();
            descriptionRect.SetParent(nodeRect, false);
            descriptionRect.anchorMin = new Vector2(0f, 0f);
            descriptionRect.anchorMax = new Vector2(1f, 1f);
            descriptionRect.pivot = new Vector2(0f, 1f);
            descriptionRect.offsetMin = new Vector2(16f, 16f);
            descriptionRect.offsetMax = new Vector2(-16f, -84f);
            TextMeshProUGUI descriptionText = descriptionObject.GetComponent<TextMeshProUGUI>();
            if (fallbackFontAsset != null)
            {
                descriptionText.font = fallbackFontAsset;
            }
            descriptionText.fontSize = 20f;
            descriptionText.textWrappingMode = TextWrappingModes.Normal;
            descriptionText.overflowMode = TextOverflowModes.Overflow;
            descriptionText.color = new Color(0.92f, 0.92f, 0.92f, 1f);
            descriptionText.raycastTarget = false;

            return nodeObject.GetComponent<TownUnitTreeNodeUI>();
        }

        private void EnsureNodeTemplate(TownUnitTreeNodeUI nodeUi)
        {
            if (nodeUi == null)
            {
                return;
            }

            RectTransform nodeRect = nodeUi.transform as RectTransform;
            if (nodeRect == null)
            {
                return;
            }

            Image backgroundImage = nodeUi.GetComponent<Image>();
            if (backgroundImage == null)
            {
                backgroundImage = nodeUi.gameObject.AddComponent<Image>();
            }

            backgroundImage.color = backgroundColor;
            backgroundImage.raycastTarget = true;

            EnsureIconChild(nodeRect);
            EnsureTitleChild(nodeRect);
            EnsureDescriptionChild(nodeRect);
        }

        private void EnsureIconChild(RectTransform nodeRect)
        {
            Transform existingTransform = nodeRect.Find("Icon");
            RectTransform iconRect;
            Image iconImage;
            if (existingTransform == null)
            {
                GameObject iconObject = new("Icon", typeof(RectTransform), typeof(Image));
                iconRect = iconObject.GetComponent<RectTransform>();
                iconRect.SetParent(nodeRect, false);
                iconImage = iconObject.GetComponent<Image>();
            }
            else
            {
                iconRect = existingTransform as RectTransform;
                iconImage = existingTransform.GetComponent<Image>();
                if (iconImage == null)
                {
                    iconImage = existingTransform.gameObject.AddComponent<Image>();
                }
            }

            iconRect.anchorMin = new Vector2(0f, 1f);
            iconRect.anchorMax = new Vector2(0f, 1f);
            iconRect.pivot = new Vector2(0f, 1f);
            iconRect.anchoredPosition = new Vector2(16f, -16f);
            iconRect.sizeDelta = new Vector2(64f, 64f);
            if (iconImage.sprite == null)
            {
                iconImage.sprite = fallbackIconSprite;
            }

            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;
            iconImage.color = new Color(1f, 0.82f, 0.28f, 1f);
        }

        private void EnsureTitleChild(RectTransform nodeRect)
        {
            Transform existingTransform = nodeRect.Find("Title");
            RectTransform titleRect;
            TextMeshProUGUI titleText;
            if (existingTransform == null)
            {
                GameObject titleObject = new("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
                titleRect = titleObject.GetComponent<RectTransform>();
                titleRect.SetParent(nodeRect, false);
                titleText = titleObject.GetComponent<TextMeshProUGUI>();
            }
            else
            {
                titleRect = existingTransform as RectTransform;
                titleText = existingTransform.GetComponent<TextMeshProUGUI>();
                if (titleText == null)
                {
                    titleText = existingTransform.gameObject.AddComponent<TextMeshProUGUI>();
                }
            }

            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0f, 1f);
            titleRect.offsetMin = new Vector2(92f, -44f);
            titleRect.offsetMax = new Vector2(-16f, -16f);
            if (fallbackFontAsset != null)
            {
                titleText.font = fallbackFontAsset;
            }

            titleText.fontSize = 28f;
            titleText.overflowMode = TextOverflowModes.Ellipsis;
            titleText.color = Color.white;
            titleText.raycastTarget = false;
        }

        private void EnsureDescriptionChild(RectTransform nodeRect)
        {
            Transform existingTransform = nodeRect.Find("Description");
            RectTransform descriptionRect;
            TextMeshProUGUI descriptionText;
            if (existingTransform == null)
            {
                GameObject descriptionObject = new("Description", typeof(RectTransform), typeof(TextMeshProUGUI));
                descriptionRect = descriptionObject.GetComponent<RectTransform>();
                descriptionRect.SetParent(nodeRect, false);
                descriptionText = descriptionObject.GetComponent<TextMeshProUGUI>();
            }
            else
            {
                descriptionRect = existingTransform as RectTransform;
                descriptionText = existingTransform.GetComponent<TextMeshProUGUI>();
                if (descriptionText == null)
                {
                    descriptionText = existingTransform.gameObject.AddComponent<TextMeshProUGUI>();
                }
            }

            descriptionRect.anchorMin = new Vector2(0f, 0f);
            descriptionRect.anchorMax = new Vector2(1f, 1f);
            descriptionRect.pivot = new Vector2(0f, 1f);
            descriptionRect.offsetMin = new Vector2(16f, 16f);
            descriptionRect.offsetMax = new Vector2(-16f, -84f);
            if (fallbackFontAsset != null)
            {
                descriptionText.font = fallbackFontAsset;
            }

            descriptionText.fontSize = 20f;
            descriptionText.textWrappingMode = TextWrappingModes.Normal;
            descriptionText.overflowMode = TextOverflowModes.Overflow;
            descriptionText.color = new Color(0.92f, 0.92f, 0.92f, 1f);
            descriptionText.raycastTarget = false;
        }

        private void ClearRuntimeNodes()
        {
            for (int i = 0; i < _runtimeNodes.Count; i++)
            {
                if (_runtimeNodes[i] != null)
                {
                    Destroy(_runtimeNodes[i].gameObject);
                }
            }

            _runtimeNodes.Clear();
            _nodeRectsById.Clear();
            lineRenderer?.SetSegments(null);
        }
    }
}
