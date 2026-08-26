#if UNITY_EDITOR
using _01.Code.Core;
using _01.Code.Manager;
using _01.Code.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace _01.Code.Editor
{
    public static class ExpeditionMapPrefabInstaller
    {
        private const string PrefabPath = "Assets/04.Prefab/UI/Panels/ExpeditionMapPanel.prefab";
        private const string ScenePath = "Assets/00.Scenes/SampleScene.unity";
        private static readonly Color Panel = new(0.055f, 0.034f, 0.025f, 0.98f);
        private static readonly Color Card = new(0.15f, 0.085f, 0.045f, 1f);

        public static void Install()
        {
            var root = BuildPrefab();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var canvas = FirstComponent<Canvas>(scene);
            var old = FirstComponent<ExpeditionMapPanelView>(scene);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), canvas.transform);
            instance.name = "ExpeditionMapPanel";
            var day = FirstComponent<DayManager>(scene);
            var cost = FirstComponent<CostManager>(scene);
            var waveEvent = ReadChannel(day, "waveEventChannel");
            var costEvent = ReadChannel(cost, "costEventChannel");
            var view = instance.GetComponent<ExpeditionMapPanelView>();
            view.Configure(waveEvent, costEvent, Child(instance.transform, "Panel").gameObject, ButtonOf(instance, "MapButton"), ButtonOf(instance, "Close"), ButtonOf(instance, "Depart"),
                new[] { ButtonOf(instance, "Village0"), ButtonOf(instance, "Village1"), ButtonOf(instance, "Village2") },
                new[] { ButtonOf(instance, "Unit0"), ButtonOf(instance, "Unit1"), ButtonOf(instance, "Unit2") },
                TextOf(instance, "Title"), TextOf(instance, "Detail"), TextOf(instance, "Roster"), TextOf(instance, "Result"));
            view.ConfigureResultModal(Child(instance.transform, "ExpeditionResultPanel").gameObject, TextOf(instance, "ResultTitle"), TextOf(instance, "ResultBody"), ButtonOf(instance, "ResultClose"));
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Debug.Log("Expedition map prefab and scene instance installed.");
        }

        /// <summary>
        /// 이미 설치된 프리팹의 마을 칸을 스크롤 목록으로 바꾼다.
        /// Install()로 통째로 다시 만들면 씬 인스턴스가 새로 꽂혀 모달 정렬 설정이 날아가므로,
        /// 프리팹 에셋만 제자리에서 고친다.
        /// </summary>
        public static void UpgradeVillageListToScrollView()
        {
            var contents = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var mapArea = Child(contents.transform, "MapArea");
                if (mapArea == null)
                    throw new System.InvalidOperationException("MapArea를 찾지 못했습니다.");

                if (Child(contents.transform, "VillageContent") != null)
                {
                    Debug.Log("Expedition village list is already a scroll view.");
                    return;
                }

                var scroll = Ui("VillageScroll", mapArea, new Vector2(.04f, .05f), new Vector2(.96f, .82f), Vector2.zero, Vector2.zero);
                var scrollRect = scroll.AddComponent<ScrollRect>();
                scrollRect.horizontal = false;
                scrollRect.vertical = true;
                scrollRect.movementType = ScrollRect.MovementType.Clamped;
                scrollRect.scrollSensitivity = 24f;

                var viewport = Ui("Viewport", scroll.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                viewport.AddComponent<RectMask2D>();
                var viewportRect = (RectTransform)viewport.transform;
                viewportRect.pivot = new Vector2(0f, 1f);

                // 위에서부터 쌓이도록 상단 고정 + 위쪽 피벗. 아래로 늘어나야 스크롤이 자연스럽다.
                var content = Ui("VillageContent", viewport.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
                var contentRect = (RectTransform)content.transform;
                contentRect.pivot = new Vector2(.5f, 1f);
                var grid = content.AddComponent<GridLayoutGroup>();
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = 1;
                grid.cellSize = new Vector2(470f, 56f);
                grid.spacing = new Vector2(0f, 8f);
                grid.padding = new RectOffset(8, 8, 8, 8);

                scrollRect.viewport = viewportRect;
                scrollRect.content = contentRect;

                // Village0을 원본으로 남기고 나머지 고정 칸은 치운다. 실제 칸은 런타임에 복제된다.
                var template = Child(contents.transform, "Village0");
                template.SetParent(content.transform, false);
                template.name = "VillageTemplate";
                template.gameObject.SetActive(false);
                foreach (var leftover in new[] { "Village1", "Village2" })
                {
                    var extra = Child(contents.transform, leftover);
                    if (extra != null)
                        Object.DestroyImmediate(extra.gameObject);
                }

                var view = contents.GetComponent<ExpeditionMapPanelView>();
                view.ConfigureVillageList(contentRect, template.GetComponent<Button>());

                PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log("Expedition village list upgraded to a scroll view.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        public static void Validate()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var view = prefab != null ? prefab.GetComponent<ExpeditionMapPanelView>() : null;
            if (view == null) throw new System.InvalidOperationException("ExpeditionMapPanel prefab is missing its view.");
            var serialized = new SerializedObject(view);
            RequireReference(serialized, "panelRoot"); RequireReference(serialized, "mapButton"); RequireReference(serialized, "closeButton");
            RequireReference(serialized, "departButton"); RequireReference(serialized, "titleText"); RequireReference(serialized, "detailText");
            RequireReference(serialized, "rosterText"); RequireReference(serialized, "resultText");
            RequireReference(serialized, "resultPanel"); RequireReference(serialized, "resultTitleText");
            RequireReference(serialized, "resultBodyText"); RequireReference(serialized, "resultCloseButton");
            RequireArray(serialized, "villageButtons", 3); RequireArray(serialized, "unitButtons", 3);
            Debug.Log("Expedition map prefab validation passed.");
        }

        private static GameObject BuildPrefab()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/08.Font/The Jamsil 5 Bold SDF.asset");
            var root = Ui("ExpeditionMapPanel", null, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero); root.AddComponent<ExpeditionMapPanelView>();
            var map = Button(root.transform, "MapButton", "작전 지도", new Vector2(0f, .5f), new Vector2(0f, .5f), new Vector2(85, -70), new Vector2(150, 46), Card, font);
            var panel = Ui("Panel", root.transform, new Vector2(.5f,.5f), new Vector2(.5f,.5f), Vector2.zero, new Vector2(1040,620)); panel.AddComponent<Image>().color = Panel;
            var outline = panel.AddComponent<Outline>(); outline.effectColor = new Color(.75f,.48f,.18f,1); outline.effectDistance = new Vector2(2,-2);
            Text(panel.transform,"Title","작전 지도",new Vector2(.04f,.88f),new Vector2(.7f,.98f),Vector2.zero,Vector2.zero,26,font);
            Button(panel.transform,"Close","닫기",new Vector2(.9f,.9f),new Vector2(.98f,.98f),Vector2.zero,Vector2.zero,Card,font);
            var mapArea=Ui("MapArea",panel.transform,new Vector2(.04f,.12f),new Vector2(.58f,.84f),Vector2.zero,Vector2.zero); mapArea.AddComponent<Image>().color=new Color(.08f,.11f,.09f,1);
            Text(mapArea.transform,"MapLabel","던전 주변 작전 지도",new Vector2(.05f,.86f),new Vector2(.95f,.98f),Vector2.zero,Vector2.zero,18,font);
            Button(mapArea.transform,"Village0","회색 교역촌",new Vector2(.12f,.55f),new Vector2(.42f,.72f),Vector2.zero,Vector2.zero,Card,font);
            Button(mapArea.transform,"Village1","북문 감시초소",new Vector2(.47f,.3f),new Vector2(.8f,.47f),Vector2.zero,Vector2.zero,Card,font);
            Button(mapArea.transform,"Village2","붉은 용병단",new Vector2(.2f,.12f),new Vector2(.53f,.29f),Vector2.zero,Vector2.zero,Card,font);
            Text(panel.transform,"Detail","",new Vector2(.62f,.5f),new Vector2(.96f,.84f),Vector2.zero,Vector2.zero,17,font);
            Text(panel.transform,"Roster","편성: 없음",new Vector2(.62f,.37f),new Vector2(.96f,.46f),Vector2.zero,Vector2.zero,16,font);
            for(var i=0;i<3;i++) Button(panel.transform,"Unit"+i,"유닛",new Vector2(.62f+i*.115f,.22f),new Vector2(.72f+i*.115f,.32f),Vector2.zero,Vector2.zero,Card,font);
            Button(panel.transform,"Depart","작전 출발",new Vector2(.7f,.08f),new Vector2(.96f,.17f),Vector2.zero,Vector2.zero,new Color(.3f,.17f,.07f,1),font);
            Text(panel.transform,"Result","",new Vector2(.04f,.02f),new Vector2(.62f,.1f),Vector2.zero,Vector2.zero,15,font);
            var resultOverlay = Ui("ExpeditionResultPanel", root.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var overlayImage = resultOverlay.AddComponent<Image>(); overlayImage.color = new Color(0f, 0f, 0f, 0.88f); overlayImage.raycastTarget = true;
            var resultWindow = Ui("ResultWindow", resultOverlay.transform, new Vector2(.5f,.5f), new Vector2(.5f,.5f), Vector2.zero, new Vector2(760, 450));
            var resultWindowImage = resultWindow.AddComponent<Image>(); resultWindowImage.color = new Color(.065f,.04f,.025f,1f);
            var resultOutline = resultWindow.AddComponent<Outline>(); resultOutline.effectColor = new Color(.85f,.55f,.18f,1f); resultOutline.effectDistance = new Vector2(3,-3);
            Text(resultWindow.transform,"ResultTitle","작전 결과",new Vector2(.08f,.78f),new Vector2(.92f,.92f),Vector2.zero,Vector2.zero,32,font).alignment=TextAlignmentOptions.Center;
            Text(resultWindow.transform,"ResultBody","",new Vector2(.12f,.28f),new Vector2(.88f,.7f),Vector2.zero,Vector2.zero,22,font).alignment=TextAlignmentOptions.Center;
            Button(resultWindow.transform,"ResultClose","확인",new Vector2(.34f,.09f),new Vector2(.66f,.2f),Vector2.zero,Vector2.zero,new Color(.34f,.17f,.06f,1),font);
            root.GetComponent<ExpeditionMapPanelView>().Configure(null, null, panel, ButtonOf(root, "MapButton"), ButtonOf(root, "Close"), ButtonOf(root, "Depart"),
                new[] { ButtonOf(root, "Village0"), ButtonOf(root, "Village1"), ButtonOf(root, "Village2") },
                new[] { ButtonOf(root, "Unit0"), ButtonOf(root, "Unit1"), ButtonOf(root, "Unit2") },
                TextOf(root, "Title"), TextOf(root, "Detail"), TextOf(root, "Roster"), TextOf(root, "Result"));
            root.GetComponent<ExpeditionMapPanelView>().ConfigureResultModal(resultOverlay, TextOf(root, "ResultTitle"), TextOf(root, "ResultBody"), ButtonOf(root, "ResultClose"));
            panel.SetActive(false); return root;
        }
        private static GameObject Ui(string name,Transform parent,Vector2 min,Vector2 max,Vector2 pos,Vector2 size){var go=new GameObject(name,typeof(RectTransform)); var r=(RectTransform)go.transform;r.SetParent(parent,false);r.anchorMin=min;r.anchorMax=max;r.anchoredPosition=pos;r.sizeDelta=size;return go;}
        private static Button Button(Transform p,string n,string label,Vector2 min,Vector2 max,Vector2 pos,Vector2 size,Color color,TMP_FontAsset font){var go=Ui(n,p,min,max,pos,size);var image=go.AddComponent<Image>();image.color=color;var b=go.AddComponent<Button>();b.targetGraphic=image;Text(go.transform,"Label",label,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,15,font).alignment=TextAlignmentOptions.Center;return b;}
        private static TMP_Text Text(Transform p,string n,string value,Vector2 min,Vector2 max,Vector2 pos,Vector2 size,float fs,TMP_FontAsset font){var go=Ui(n,p,min,max,pos,size);var t=go.AddComponent<TextMeshProUGUI>();t.font=font;t.text=value;t.fontSize=fs;t.color=new Color(.95f,.91f,.82f,1);t.alignment=TextAlignmentOptions.TopLeft;t.textWrappingMode=TextWrappingModes.Normal;return t;}
        private static Transform Child(Transform root,string n){foreach(var t in root.GetComponentsInChildren<Transform>(true))if(t.name==n)return t;return null;}
        private static Button ButtonOf(GameObject root,string n)=>Child(root.transform,n).GetComponent<Button>(); private static TMP_Text TextOf(GameObject root,string n)=>Child(root.transform,n).GetComponent<TMP_Text>();
        private static GameEventChannelSO ReadChannel(Object target,string property){var so=new SerializedObject(target);return so.FindProperty(property).objectReferenceValue as GameEventChannelSO;}
        private static T FirstComponent<T>(Scene scene) where T : Component { foreach (var root in scene.GetRootGameObjects()) { var component = root.GetComponentInChildren<T>(true); if (component != null) return component; } return null; }
        private static void RequireReference(SerializedObject serialized, string property) { if (serialized.FindProperty(property)?.objectReferenceValue == null) throw new System.InvalidOperationException($"Expedition map prefab reference is missing: {property}"); }
        private static void RequireArray(SerializedObject serialized, string property, int expected) { var value = serialized.FindProperty(property); if (value == null || !value.isArray || value.arraySize != expected) throw new System.InvalidOperationException($"Expedition map prefab array is invalid: {property}"); for (var i = 0; i < value.arraySize; i++) if (value.GetArrayElementAtIndex(i).objectReferenceValue == null) throw new System.InvalidOperationException($"Expedition map prefab array item is missing: {property}[{i}]"); }
    }
}
#endif
