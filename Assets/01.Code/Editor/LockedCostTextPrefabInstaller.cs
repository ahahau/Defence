using _01.Code.MapCreateSystem;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace _01.Code.Editor
{
    public static class LockedCostTextPrefabInstaller
    {
        private const string NodePrefabPath = "Assets/04.Prefab/Map/Node.prefab";

        [MenuItem("Tools/Defence/Install Locked Cost Text Prefab")]
        public static void Install()
        {
            AssignLockedVisualsToNodePrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void AssignLockedVisualsToNodePrefab()
        {
            var prefabRoot = PrefabUtility.LoadPrefabContents(NodePrefabPath);
            try
            {
                var node = prefabRoot.GetComponent<Node>();
                if (node == null)
                    return;

                var serializedNode = new SerializedObject(node);
                var lockedRoot = EnsureChild(prefabRoot.transform, "LockedRoot", typeof(Transform));
                lockedRoot.transform.localPosition = Vector3.zero;
                lockedRoot.transform.localRotation = Quaternion.identity;
                lockedRoot.transform.localScale = Vector3.one;
                lockedRoot.SetActive(false);

                var lockedOverlay = EnsureChild(lockedRoot.transform, "LockedOverlay", typeof(SpriteRenderer));
                lockedOverlay.transform.localPosition = new Vector3(0f, 0f, -0.05f);
                lockedOverlay.transform.localRotation = Quaternion.identity;
                lockedOverlay.transform.localScale = Vector3.one;

                var overlayRenderer = lockedOverlay.GetComponent<SpriteRenderer>();
                overlayRenderer.enabled = true;
                overlayRenderer.sprite = serializedNode.FindProperty("lockedCandidateSprite").objectReferenceValue as Sprite;
                overlayRenderer.color = new Color(1f, 1f, 1f, 1f);
                overlayRenderer.sortingLayerID = 0;
                overlayRenderer.sortingOrder = 1;

                var costTextObject = EnsureChild(lockedRoot.transform, "LockedCostText", typeof(RectTransform), typeof(MeshRenderer), typeof(TextMeshPro));
                costTextObject.transform.localPosition = new Vector3(0f, -0.56f, -0.08f);
                costTextObject.transform.localRotation = Quaternion.identity;
                costTextObject.transform.localScale = new Vector3(0.12f, 0.12f, 0.12f);
                costTextObject.SetActive(false);

                var costText = costTextObject.GetComponent<TextMeshPro>();
                costText.alignment = TextAlignmentOptions.Center;
                costText.textWrappingMode = TextWrappingModes.NoWrap;
                costText.fontSize = 2.6f;
                costText.color = new Color(1f, 0.84f, 0.28f, 1f);
                costText.text = string.Empty;
                costText.sortingLayerID = 0;
                costText.sortingOrder = 3;
                costText.rectTransform.sizeDelta = new Vector2(8f, 2f);

                serializedNode.FindProperty("lockedRoot").objectReferenceValue = lockedRoot;
                serializedNode.FindProperty("lockedOverlayRenderer").objectReferenceValue = overlayRenderer;
                serializedNode.FindProperty("lockedCostText").objectReferenceValue = null;
                serializedNode.FindProperty("lockedCostText").objectReferenceValue = costText;
                serializedNode.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, NodePrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static GameObject EnsureChild(Transform parent, string name, params System.Type[] componentTypes)
        {
            var child = GetDirectChild(parent, name);
            var gameObject = child != null ? child.gameObject : new GameObject(name, componentTypes);
            gameObject.transform.SetParent(parent, false);

            foreach (var componentType in componentTypes)
            {
                if (gameObject.GetComponent(componentType) == null)
                    gameObject.AddComponent(componentType);
            }

            return gameObject;
        }

        private static Transform GetDirectChild(Transform parent, string name)
        {
            for (var i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child.name == name)
                    return child;
            }

            return null;
        }
    }
}
