using NUnit.Framework;
using UnityEngine;
using Yoegoe.Data;
using Yoegoe.Economy;
using Yoegoe.UI;

namespace Yoegoe.Tests.EditMode
{
    /// <summary>10-2: 요구 말풍선 탭 → 상세에서 해당 음식 강조 — 가진 것만, 없으면 강조 없음. 황금 버전만 있으면 그걸 강조.</summary>
    public class RequestHighlightTests
    {
        GameObject ecoGO;
        GameEconomy eco;

        /// <summary>레시피 표의 첫 음식 — 시트에서 레시피가 바뀌어도 테스트가 특정 요리 id에 묶이지 않게.</summary>
        static Yoegoe.Cooking.CookingRecipe AnyFood => System.Linq.Enumerable.First(
            Yoegoe.Cooking.CookingRecipeCatalog.Recipes, r => r.Kind == Yoegoe.Cooking.CookingResultKind.Food);
        static string FoodId => AnyFood.Id;

        [SetUp]
        public void SetUp()
        {
            OfferingCatalog.Build(null);
            ecoGO = new GameObject("Eco");
            eco = ecoGO.AddComponent<GameEconomy>();
            eco.BecomeInstance();
            eco.ApplyStartingState(ScriptableObject.CreateInstance<StartingStateSettings>());
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(ecoGO);

        [Test]
        public void NotOwned_NoHighlight()
        {
            Assert.IsNull(DetailScreen.FindOwnedRequestTarget(FoodId));
        }

        [Test]
        public void Owned_HighlightsIt()
        {
            eco.AddOffering(OfferingCatalog.Find(FoodId), 1);
            Assert.AreEqual(FoodId, DetailScreen.FindOwnedRequestTarget(FoodId).offeringId);
        }

        [Test]
        public void OnlyGoldenOwned_HighlightsGolden()
        {
            eco.AddOffering(OfferingCatalog.FindGolden(FoodId), 1);
            Assert.AreEqual(OfferingCatalog.GoldenIdOf(FoodId), DetailScreen.FindOwnedRequestTarget(FoodId).offeringId);
        }
    }
}
