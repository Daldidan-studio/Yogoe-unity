using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Yoegoe.Characters;
using Yoegoe.Data;

namespace Yoegoe.Tests.EditMode
{
    /// <summary>
    /// 공양 규칙: 기절은 물로만 깨어남(0→1) / 기력 최대면 기력은 멈추고 친밀도만 오름.
    /// 공양물 카탈로그: 공양간 레시피 결과물이 전부 등록되고, 랜덤 풀은 공양물 24종.
    /// </summary>
    public class OfferingRulesTests
    {
        GameObject go;
        CharacterAgent agent;

        /// <summary>레시피 표의 첫 음식 — 시트에서 레시피가 바뀌어도 테스트가 특정 요리 id에 묶이지 않게.</summary>
        static Yoegoe.Cooking.CookingRecipe AnyFood => System.Linq.Enumerable.First(
            Yoegoe.Cooking.CookingRecipeCatalog.Recipes, r => r.Kind == Yoegoe.Cooking.CookingResultKind.Food);
        static string FoodId => AnyFood.Id;

        [SetUp]
        public void SetUp()
        {
            go = new GameObject("Agent_OfferingRules");
            agent = go.AddComponent<CharacterAgent>();
            agent.Data = ScriptableObject.CreateInstance<CharacterData>();
            agent.Stats.State = ActionState.Walking;
            agent.Stats.Intimacy = 50f;
            agent.Stats.Stamina = 75f; // 25 + 50 = 최대
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(agent.Data);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void Fainted_GeneralOffering_DoesNothing()
        {
            agent.Stats.State = ActionState.Fainted;
            agent.Stats.Stamina = 0f;

            agent.ReceiveOffering(3, 5f, OfferingKind.General);

            Assert.AreEqual(ActionState.Fainted, agent.Stats.State);
            Assert.AreEqual(0f, agent.Stats.Stamina);
            Assert.AreEqual(50f, agent.Stats.Intimacy);
        }

        [Test]
        public void Fainted_Water_WakesWithOneStamina()
        {
            agent.Stats.State = ActionState.Fainted;
            agent.Stats.Stamina = 0f;

            agent.ReceiveOffering(1, 0f, OfferingKind.Water);

            Assert.AreNotEqual(ActionState.Fainted, agent.Stats.State);
            Assert.AreEqual(1f, agent.Stats.Stamina, 0.0001f);
        }

        [Test]
        public void FullStamina_PreferredOffering_StaminaStopsAtMax_IntimacyRises()
        {
            agent.ReceiveOffering(3, 5f, OfferingKind.Preferred);

            Assert.AreEqual(55f, agent.Stats.Intimacy, 0.0001f);
            Assert.AreEqual(75f, agent.Stats.Stamina, 0.0001f); // 먹일 때 최대(75)였으니 기력은 거기서 끝
        }

        [Test]
        public void PreferredOffering_NoFeedLimit_IntimacyKeepsRising()
        {
            for (int i = 0; i < 20; i++)
                agent.ReceiveOffering(3, 5f, OfferingKind.Preferred);

            Assert.AreEqual(100f, agent.Stats.Intimacy, 0.0001f);
            Assert.LessOrEqual(agent.Stats.Stamina, agent.MaxStamina + 0.0001f);
        }

        [Test]
        public void Catalog_RegistersEveryRecipeProduct_AndPoolIsAllOfferings()
        {
            var all = OfferingCatalog.Build(null);

            // 레시피 결과물마다 1개 + 음식마다 황금음식 1개, 랜덤 풀 = 공양물 전부 (지금 36 · 24 · 36)
            var products = Yoegoe.Cooking.CookingRecipeCatalog.Recipes
                .GroupBy(r => r.Id).Select(g => g.First()).ToList();
            int foods = products.Count(r => r.Kind == Yoegoe.Cooking.CookingResultKind.Food);
            Assert.AreEqual(products.Count + foods, all.Length);
            Assert.AreEqual(products.Count - foods, OfferingCatalog.RandomPool.Count);
            foreach (var o in OfferingCatalog.RandomPool)
                Assert.AreEqual(OfferingKind.General, o.kind, o.offeringId);
            Assert.IsNotNull(OfferingCatalog.Find("sinseollo"));
            Assert.AreEqual(OfferingKind.Food, OfferingCatalog.Find(FoodId).kind);
        }

        [Test]
        public void Catalog_AssetGivesIconOnly_RecipeGivesNameAndKind()
        {
            var recipe = System.Linq.Enumerable.First(Yoegoe.Cooking.CookingRecipeCatalog.Recipes,
                r => r.Kind == Yoegoe.Cooking.CookingResultKind.Offering);
            var tex = new Texture2D(2, 2);
            var icon = Sprite.Create(tex, new Rect(0, 0, 2, 2), Vector2.zero);
            var asset = ScriptableObject.CreateInstance<OfferingData>();
            asset.offeringId = recipe.Id;
            asset.displayName = "옛 에셋 이름";
            asset.kind = OfferingKind.Food; // 일부러 틀린 종류 — 레시피가 이겨야 함
            asset.icon = icon;
            var orphan = ScriptableObject.CreateInstance<OfferingData>();
            orphan.offeringId = "not_a_recipe";
            orphan.kind = OfferingKind.General;
            try
            {
                OfferingCatalog.Build(new[] { asset, orphan });
                var o = OfferingCatalog.Find(recipe.Id);
                Assert.AreNotSame(asset, o, "에셋을 그대로 쓰지 않는다");
                Assert.AreEqual(recipe.DisplayName, o.displayName);
                Assert.AreEqual(OfferingKind.General, o.kind);
                Assert.AreSame(icon, o.icon, "그림은 에셋에서");
                Assert.IsNull(OfferingCatalog.Find("not_a_recipe"), "레시피에 없는 옛 에셋은 안 넣음");
            }
            finally
            {
                Object.DestroyImmediate(asset);
                Object.DestroyImmediate(orphan);
                Object.DestroyImmediate(icon);
                Object.DestroyImmediate(tex);
                OfferingCatalog.Build(null);
            }
        }
    }
}
