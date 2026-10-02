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
            Assert.IsNull(DetailScreen.FindOwnedRequestTarget("bap"));
        }

        [Test]
        public void Owned_HighlightsIt()
        {
            eco.AddOffering(OfferingCatalog.Find("bap"), 1);
            Assert.AreEqual("bap", DetailScreen.FindOwnedRequestTarget("bap").offeringId);
        }

        [Test]
        public void OnlyGoldenOwned_HighlightsGolden()
        {
            eco.AddOffering(OfferingCatalog.FindGolden("bap"), 1);
            Assert.AreEqual(OfferingCatalog.GoldenIdOf("bap"), DetailScreen.FindOwnedRequestTarget("bap").offeringId);
        }
    }
}
