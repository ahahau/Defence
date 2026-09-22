using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Code.UI.Toolkit
{
    /// <summary>
    /// UI Toolkit 화면을 정해진 씬에 세워 두는 공통 진입 절차.
    ///
    /// 진입점(<see cref="RuntimeInitializeOnLoadMethod"/>)은 게임이 시작한 직후 한 번만 불린다.
    /// 그것만 보면 제목에서 인게임으로 넘어간 씬에는 문서가 하나도 붙지 않는다 — 에디터에서 인게임
    /// 씬을 직접 재생할 때만 보이고 실제 진행에서는 사라진다. 그래서 씬이 새로 열릴 때마다 다시 본다.
    /// </summary>
    public static class ToolkitDocumentSpawner
    {
        private const string PanelSettingsPath = "UI/Toolkit/RuntimePanelSettings";

        private static readonly List<Registration> Registrations = new();
        private static bool _listening;

        /// <summary>
        /// 지금 씬과 앞으로 열릴 씬 모두에 문서를 세운다.
        /// <paramref name="onCreated"/>는 문서가 실제로 만들어진 뒤에만 불린다 — 옛 UGUI를 끄는 일은
        /// 새 화면이 확실히 뜬 다음에 해야 둘 다 없는 상태가 생기지 않는다.
        /// </summary>
        public static void Install<TView>(
            string sceneName,
            string documentPath,
            string rootName,
            int sortingOrder,
            Action onCreated = null)
            where TView : Component
        {
            var registration = new Registration(sceneName, documentPath, rootName, sortingOrder, typeof(TView), onCreated);

            // 도메인 리로드를 끈 설정에서는 진입점이 다시 불려도 static이 살아 있다. 같은 화면을 두 번 세우지 않는다.
            Registrations.RemoveAll(entry => entry.RootName == rootName);
            Registrations.Add(registration);

            if (!_listening)
            {
                SceneManager.sceneLoaded += HandleSceneLoaded;
                _listening = true;
            }

            if (SceneManager.GetActiveScene().name == sceneName)
                Create(registration);
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            foreach (var registration in Registrations)
            {
                if (registration.SceneName == scene.name)
                    Create(registration);
            }
        }

        private static void Create(Registration registration)
        {
            var panelSettings = Resources.Load<PanelSettings>(PanelSettingsPath);
            var documentAsset = Resources.Load<VisualTreeAsset>(registration.DocumentPath);
            if (panelSettings == null || documentAsset == null)
            {
                Debug.LogError($"UI Toolkit assets are missing for {registration.DocumentPath}.");
                return;
            }

            var root = new GameObject(registration.RootName);
            var document = root.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            document.visualTreeAsset = documentAsset;
            document.sortingOrder = registration.SortingOrder;
            root.AddComponent(registration.ViewType);

            registration.OnCreated?.Invoke();
        }

        private readonly struct Registration
        {
            public Registration(
                string sceneName,
                string documentPath,
                string rootName,
                int sortingOrder,
                Type viewType,
                Action onCreated)
            {
                SceneName = sceneName;
                DocumentPath = documentPath;
                RootName = rootName;
                SortingOrder = sortingOrder;
                ViewType = viewType;
                OnCreated = onCreated;
            }

            public string SceneName { get; }
            public string DocumentPath { get; }
            public string RootName { get; }
            public int SortingOrder { get; }
            public Type ViewType { get; }
            public Action OnCreated { get; }
        }
    }
}
