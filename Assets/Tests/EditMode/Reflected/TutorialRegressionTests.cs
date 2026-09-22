using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tests.EditMode.UI
{
    public class TutorialRegressionTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private readonly List<Object> created = new();
        private static Type Tutorial => Type.GetType("Code.UI.PlayTutorialView, DungeonKeeper.Runtime", true);
        private static Type Step => Tutorial.GetNestedType("Step", BindingFlags.NonPublic);

        private Component CreateTutorial()
        {
            var go = new GameObject("Tutorial regression", typeof(RectTransform));
            go.SetActive(false);
            created.Add(go);
            var component = go.AddComponent(Tutorial);
            Tutorial.GetField("root", Private).SetValue(component, go);
            return component;
        }

        [TearDown]
        public void Cleanup()
        {
            foreach (var obj in created)
            {
                if (obj is GameObject go)
                {
                    var nodeType = Type.GetType("Code.MapCreateSystem.Node, DungeonKeeper.Runtime", true);
                    var node = go.GetComponent(nodeType);
                    if (node != null) nodeType.GetMethod("OnDisable", Private).Invoke(node, null);
                }
                Object.DestroyImmediate(obj);
            }
            created.Clear();
        }

        [Test]
        public void Idle_HidesHintWithoutDisablingFutureLessons()
        {
            var tutorial = CreateTutorial();
            tutorial.gameObject.SetActive(true);
            Tutorial.GetMethod("EnterStep", Private).Invoke(tutorial, new[] { Enum.Parse(Step, "Idle") });
            Assert.That(tutorial.gameObject.activeInHierarchy, Is.True);
            Assert.That(((Behaviour)tutorial).isActiveAndEnabled, Is.True);
            Assert.That(tutorial.GetComponent<CanvasGroup>().alpha, Is.Zero);
            Tutorial.GetMethod("EnterStep", Private).Invoke(tutorial, new[] { Enum.Parse(Step, "LearnMerchant") });
            Assert.That(tutorial.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
        }

        [Test]
        public void Skip_ReleasesHintAndKeepsControllerAlive()
        {
            var tutorial = CreateTutorial();
            tutorial.gameObject.SetActive(true);
            Tutorial.GetMethod("SkipTutorial").Invoke(tutorial, null);
            Assert.That(tutorial.gameObject.activeInHierarchy, Is.True);
            Assert.That(tutorial.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
            Assert.That(Tutorial.GetField("_step", Private).GetValue(tutorial).ToString(), Is.EqualTo("Done"));
        }

        [Test]
        public void BuiltEmptyRoom_AdvancesToHireWithoutRequiringAnotherBuilding()
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameModules/Prefabs/Map/Node.prefab");
            var go = Object.Instantiate(prefab);
            created.Add(go);
            var nodeType = Type.GetType("Code.MapCreateSystem.Node, DungeonKeeper.Runtime", true);
            // EditMode does not run the runtime registration lifecycle automatically.
            nodeType.GetMethod("OnEnable", Private).Invoke(go.GetComponent(nodeType), null);
            var modelType = Type.GetType("Code.MapCreateSystem.DungeonNode, DungeonKeeper.Runtime", true);
            var kind = Type.GetType("Code.MapCreateSystem.DungeonNodeType, DungeonKeeper.Runtime", true);
            var model = Activator.CreateInstance(modelType, Enum.Parse(kind, "Corridor"), new Vector2Int(999, 999), 4);
            nodeType.GetMethod("Initialize", new[] { modelType, typeof(float) })
                .Invoke(go.GetComponent(nodeType), new[] { model, (object)1f });
            var next = Tutorial.GetMethod("Resolve", Private)
                .Invoke(CreateTutorial(), new[] { Enum.Parse(Step, "BuildRoom") });
            Assert.That(next.ToString(), Is.EqualTo("DeployUnit"));
        }

        [Test]
        public void SettlementLesson_WaitsUntilTheReportHasBeenSeenAndClosed()
        {
            var managerType = Type.GetType("Code.Manager.ManagementSettlementManager, DungeonKeeper.Runtime", true);
            var managerObject = new GameObject("Settlement tutorial regression");
            created.Add(managerObject);
            var panel = new GameObject("Settlement panel");
            panel.transform.SetParent(managerObject.transform, false);
            var manager = managerObject.AddComponent(managerType);
            managerType.GetField("panelRoot", Private).SetValue(manager, panel);
            managerType.GetMethod("OnEnable", Private).Invoke(manager, null);

            panel.SetActive(true);
            var tutorial = CreateTutorial();
            var review = Enum.Parse(Step, "ReviewSettlement");
            Tutorial.GetMethod("EnterStep", Private).Invoke(tutorial, new[] { review });

            var whileOpen = Tutorial.GetMethod("Resolve", Private).Invoke(tutorial, new[] { review });
            Assert.That(whileOpen.ToString(), Is.EqualTo("ReviewSettlement"));

            panel.SetActive(false);
            var afterClose = Tutorial.GetMethod("Resolve", Private).Invoke(tutorial, new[] { review });
            Assert.That(afterClose.ToString(), Is.EqualTo("PrepareNextDay"));
        }
    }
}
