using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode.Rules
{
    /// <summary>
    /// 상인의 매대가 갈 때마다 달라지는지 본다.
    ///
    /// 물약은 사도 소지품에 남지 않아 후보에서 빠지지 않는다. 그래서 제한이 없으면
    /// 영구 유물이 팔려 나갈수록 매대가 물약으로만 채워지고, 매번 같은 것을 보게 된다.
    /// </summary>
    public class ArtifactShopTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private readonly List<UnityEngine.Object> created = new();

        private static Type Resolve(string name) => Type.GetType(name + ", Assembly-CSharp", true);

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in created)
                UnityEngine.Object.DestroyImmediate(obj);
            created.Clear();
        }

        private ScriptableObject Artifact(string name, int price, bool consumable)
        {
            var artifact = ScriptableObject.CreateInstance(Resolve("_01.Code.Artifacts.ArtifactDataSO"));
            artifact.name = name;
            created.Add(artifact);
            Set(artifact, "<DisplayName>k__BackingField", name);
            Set(artifact, "<Price>k__BackingField", price);
            Set(artifact, "<IsConsumable>k__BackingField", consumable);
            return artifact;
        }

        private static void Set(object target, string field, object value) =>
            target.GetType().GetField(field, PrivateInstance).SetValue(target, value);

        /// <summary>
        /// 영구 유물 여덟과 물약 셋을 취급하는 상인.
        /// 재고를 넉넉히 두는 이유가 있다 — 후보가 칸 수에 가까우면 겹치지 않게 채울 방법이 없어
        /// 지난 물건을 다시 꺼내는 것이 설계상 옳고, 그러면 "안 겹친다"를 검사할 수 없다.
        /// </summary>
        private ScriptableObject Catalog()
        {
            var catalog = ScriptableObject.CreateInstance(Resolve("_01.Code.Artifacts.ArtifactShopCatalogSO"));
            created.Add(catalog);

            var stock = new List<object>();
            for (var i = 0; i < 8; i++)
                stock.Add(Artifact("유물" + i, 100, false));
            for (var i = 0; i < 3; i++)
                stock.Add(Artifact("물약" + i, 30, true));

            var listType = typeof(List<>).MakeGenericType(Resolve("_01.Code.Artifacts.ArtifactDataSO"));
            var typed = (System.Collections.IList)Activator.CreateInstance(listType);
            foreach (var item in stock)
                typed.Add(item);

            catalog.GetType().GetMethod("ReplaceStock").Invoke(catalog, new object[] { typed });
            return catalog;
        }

        private static System.Collections.IList Roll(ScriptableObject catalog, object previous)
        {
            var method = catalog.GetType().GetMethod("RollDisplay");
            return (System.Collections.IList)method.Invoke(catalog, new[] { null, previous });
        }

        private static int CountConsumables(System.Collections.IList display)
        {
            var count = 0;
            foreach (var entry in display)
                if ((bool)entry.GetType().GetProperty("IsConsumable").GetValue(entry))
                    count++;

            return count;
        }

        [Test]
        public void Display_NeverFillsUpWithConsumables()
        {
            var catalog = Catalog();

            // 무작위라 한 번으로는 못 믿는다. 여러 번 굴려서 한 번이라도 넘치면 잡힌다.
            for (var attempt = 0; attempt < 50; attempt++)
            {
                var display = Roll(catalog, null);
                Assert.That(CountConsumables(display), Is.LessThanOrEqualTo(1),
                    "물약이 매대를 뒤덮었습니다: " + attempt + "번째 시도");
            }
        }

        [Test]
        public void Display_DoesNotRepeatThePreviousVisit_WhenEnoughStockRemains()
        {
            var catalog = Catalog();

            // 영구 유물 넷 + 물약 한 칸이면 세 칸을 겹치지 않게 채울 여유가 있다.
            for (var attempt = 0; attempt < 50; attempt++)
            {
                var first = Roll(catalog, null);
                var second = Roll(catalog, first);

                foreach (var entry in second)
                    Assert.That(first, Does.Not.Contain(entry),
                        "지난번에 깔았던 물건이 또 나왔습니다: " + attempt + "번째 시도");
            }
        }
    }
}
