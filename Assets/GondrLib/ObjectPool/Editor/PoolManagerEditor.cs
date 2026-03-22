using System;
using System.Collections.Generic;
using System.IO;
using GondrLib.ObjectPool.Editor;
using GondrLib.ObjectPool.Runtime;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

public class PoolManagerEditor : EditorWindow
{
    [SerializeField] private VisualTreeAsset visualTreeAsset = default;
    [SerializeField] private PoolManagerSO poolManager = default;
    [SerializeField] private VisualTreeAsset itemAsset = default;

    private string _rootFolderPath;
    private Button _createBtn;
    private ScrollView _itemView;
    private ObjectField _poolManagerField;

    private List<PoolItemUI> _itemList;
    private PoolItemUI _selectedItem;

    private UnityEditor.Editor _cachedEditor;
    private VisualElement _inspectorView;

    [MenuItem("Tools/PoolManager")]
    public static void ShowWindow()
    {
        PoolManagerEditor wnd = GetWindow<PoolManagerEditor>();
        wnd.titleContent = new GUIContent("PoolManagerEditor");
    }

    private void InitializeRootFolder()
    {
        MonoScript script = MonoScript.FromScriptableObject(this);
        string scriptPath = AssetDatabase.GetAssetPath(script);
        string dataPath = Application.dataPath;
        _rootFolderPath = Directory.GetParent(Path.GetDirectoryName(scriptPath)).FullName.Replace("\\", "/");

        if (_rootFolderPath.StartsWith(dataPath))
        {
            _rootFolderPath = "Assets" + _rootFolderPath.Substring(dataPath.Length);
        }

        if (poolManager == null)
        {
            string filePath = $"{_rootFolderPath}/PoolManager.asset";
            poolManager = AssetDatabase.LoadAssetAtPath<PoolManagerSO>(filePath);
            if (poolManager == null)
            {
                Debug.LogWarning("PoolManager so is not exist, create new one");
                poolManager = ScriptableObject.CreateInstance<PoolManagerSO>();
                AssetDatabase.CreateAsset(poolManager, filePath);
            }
        }

        visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>($"{_rootFolderPath}/Editor/PoolManagerEditor.uxml");
        Debug.Assert(visualTreeAsset != null, "Visual tree asset is null");
        itemAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>($"{_rootFolderPath}/Editor/PoolItemUI.uxml");
        Debug.Assert(itemAsset != null, "Item asset is null");
    }

    public void CreateGUI()
    {
        InitializeRootFolder();

        VisualElement root = rootVisualElement;
        root.Clear();
        visualTreeAsset.CloneTree(root);

        InitializeItems(root);
        InitializePoolManagerSelector(root);
    }

    private void InitializeItems(VisualElement root)
    {
        _createBtn = root.Q<Button>("CreateBtn");
        _createBtn.clicked += HandleCreateItem;
        _itemView = root.Q<ScrollView>("ItemView");
        _itemView.Clear();
        _itemList = new List<PoolItemUI>();
        _inspectorView = root.Q<VisualElement>("InspectorView");

        GeneratePoolingItems();
    }

    private void InitializePoolManagerSelector(VisualElement root)
    {
        _poolManagerField = new ObjectField("Pool Manager")
        {
            objectType = typeof(PoolManagerSO),
            allowSceneObjects = false,
            value = poolManager
        };

        _poolManagerField.RegisterValueChangedCallback(evt =>
        {
            PoolManagerSO selectedManager = evt.newValue as PoolManagerSO;
            if (selectedManager == null || selectedManager == poolManager)
            {
                return;
            }

            poolManager = selectedManager;
            _selectedItem = null;
            GeneratePoolingItems();
        });

        root.Insert(0, _poolManagerField);
    }

    private void HandleCreateItem()
    {
        if (poolManager == null)
        {
            return;
        }

        string itemName = Guid.NewGuid().ToString();
        PoolingItemSO newItemSO = ScriptableObject.CreateInstance<PoolingItemSO>();
        newItemSO.poolingName = itemName;

        if (Directory.Exists($"{_rootFolderPath}/Items") == false)
        {
            Directory.CreateDirectory($"{_rootFolderPath}/Items");
        }

        AssetDatabase.CreateAsset(newItemSO, $"{_rootFolderPath}/Items/{itemName}.asset");

        poolManager.itemList.Add(newItemSO);
        EditorUtility.SetDirty(poolManager);
        AssetDatabase.SaveAssets();

        GeneratePoolingItems();
    }

    private void GeneratePoolingItems()
    {
        _itemView.Clear();
        _itemList.Clear();
        _inspectorView.Clear();

        if (poolManager == null)
        {
            return;
        }

        foreach (PoolingItemSO item in poolManager.itemList)
        {
            var itemTemplate = itemAsset.Instantiate();
            PoolItemUI itemUI = new PoolItemUI(itemTemplate, item);

            _itemView.Add(itemTemplate);
            _itemList.Add(itemUI);

            itemUI.Name = item.poolingName;

            if (_selectedItem != null && _selectedItem.poolItem == item)
            {
                itemUI.IsActive = true;
                _selectedItem = itemUI;
            }

            itemUI.OnSelectEvent += HandleSelectEvent;
            itemUI.OnDeleteEvent += HandleDeleteEvent;
        }
    }

    private void HandleSelectEvent(PoolItemUI target)
    {
        if (_selectedItem != null)
        {
            _selectedItem.IsActive = false;
        }

        _selectedItem = target;
        _selectedItem.IsActive = true;
        DrawInspector();
    }

    private void DrawInspector()
    {
        _inspectorView.Clear();
        UnityEditor.Editor.CreateCachedEditor(_selectedItem.poolItem, null, ref _cachedEditor);
        VisualElement inspector = _cachedEditor.CreateInspectorGUI();

        SerializedObject serializedObject = new SerializedObject(_selectedItem.poolItem);
        inspector.Bind(serializedObject);
        inspector.TrackSerializedObjectValue(serializedObject, so =>
        {
            _selectedItem.Name = so.FindProperty("poolingName").stringValue;
        });
        _inspectorView.Add(inspector);
    }

    private void HandleDeleteEvent(PoolItemUI target)
    {
        if (poolManager == null)
        {
            return;
        }

        if (EditorUtility.DisplayDialog("Warning", "Are you sure to delete this item?", "OK", "Cancel") == false)
        {
            return;
        }

        poolManager.itemList.Remove(target.poolItem);
        AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(target.poolItem));
        EditorUtility.SetDirty(poolManager);
        AssetDatabase.SaveAssets();

        if (target == _selectedItem)
        {
            _selectedItem = null;
        }

        GeneratePoolingItems();
    }
}
