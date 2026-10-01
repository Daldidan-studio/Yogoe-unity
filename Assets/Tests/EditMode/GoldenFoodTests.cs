using NUnit.Framework;
using UnityEngine;
using Yoegoe.Characters;
using Yoegoe.Cooking;
using Yoegoe.Data;
using Yoegoe.Economy;

namespace Yoegoe.Tests.EditMode
{
    /// <summary>
    /// 황금음식 (공양간 기획서): 황금쌀·황금꿀이 들어간 음식 → 먹이면 5분간 황금색 + 이동·생산 2배.
    /// </summary>
    public class GoldenFoodTests
    {
        GameObject ecoGO, agentGO;
        GameEconomy eco;
        CharacterAgent agent;

        [SetUp]
        public void SetUp()
        {
            OfferingCatalog.Build(null);
            ecoGO = new GameObject("Eco");
            eco = ecoGO.AddComponent<GameEconomy>();
            eco.BecomeInstance();
            eco.ApplyStartingState(ScriptableObject.CreateInstance<StartingStateSettings>());
            for (int i = 0; i < (int)CookingIngredientId.Count; i++)
            {
                var id = (CookingIngredientId)i;
                eco.TrySpendMaterial(id, eco.GetMaterialCount(id));
            }

            agentGO = new GameObject("Agent");
            agent = agentGO.AddComponent<CharacterAgent>();
            agent.Data = ScriptableObject.CreateInstance<CharacterData>();
            agent.Data.id = CharacterId.SamjokO;
            agent.Stats.State = ActionState.Walking;
            agent.Stats.Intimacy = 50f;
            agent.Stats.Stamina = 40f;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(agent.Data);
            Object.DestroyImmediate(agentGO);
            Object.DestroyImmediate(ecoGO);
        }

        [Test]
        public void Catalog_HasGoldenVariantForFoodsOnly()
        {
            var g = OfferingCatalog.FindGolden("bap");
            Assert.IsNotNull(g);
            Assert.IsTrue(g.golden);
            Assert.AreEqual(OfferingKind.Food, g.kind);
            Assert.AreEqual("bap", g.BaseId);
            Assert.AreEqual("황금 밥", g.displayName);
            Assert.IsNull(OfferingCatalog.FindGolden("yukjeon"), "공양물은 황금 버전 없음");
        }

        [Test]
        public void FeedingGoldenFood_GivesFoodStamina_AndFiveMinuteBuff()
        {
            var g = OfferingCatalog.FindGolden("bap");
            eco.AddOffering(g, 1);
            Assert.IsFalse(agent.IsGolden);
            Assert.AreEqual(1f, agent.SpeedMultiplier);

            var r = agent.TryFeed(g, eco);
            Assert.IsTrue(r.Success);
            Assert.IsTrue(r.GoldenBuff);
            Assert.AreEqual(8, r.StaminaGain);
            Assert.IsTrue(agent.IsGolden);
            Assert.AreEqual(2f, agent.SpeedMultiplier);
            Assert.That(agent.GoldenSecondsLeft, Is.InRange(299f, 300.5f));
        }

        [Test]
        public void GoldenFood_CanBeFedEvenWithFullStamina()
        {
            agent.Stats.Stamina = agent.MaxStamina;
            var g = OfferingCatalog.FindGolden("bap");
            eco.AddOffering(g, 1);
            Assert.IsTrue(agent.TryFeed(g, eco).Success);
            Assert.IsTrue(agent.IsGolden);
        }

        [Test]
        public void Cooking_GoldenRiceMakesGoldenFood_AndIsSpentAtStart()
        {
            eco.AddSpecialItem(SpecialItemId.GoldenRice, 1);
            eco.AddMaterial(CookingIngredientId.RedBean, 1); // 쌀 + 팥 = 팥떡
            Assert.AreEqual(1, eco.GetBoardMaterialCount(CookingIngredientId.Rice));

            var session = new CookingSession();
            session.Prepare(CookingCharmType.None);
            Assert.AreEqual(2, session.MaterialsOnBoard);
            Assert.IsTrue(session.StartRound());
            Assert.AreEqual(0, eco.GetSpecialItemCount(SpecialItemId.GoldenRice));
            Assert.AreEqual(0, eco.GetMaterialCount(CookingIngredientId.RedBean));

            (int x, int y)? rice = null, bean = null;
            for (int y = 0; y < CookingSession.GridSize; y++)
            for (int x = 0; x < CookingSession.GridSize; x++)
            {
                if (session.Grid[x, y] == CookingIngredientId.Rice) { rice = (x, y); Assert.IsTrue(session.Golden[x, y]); }
                if (session.Grid[x, y] == CookingIngredientId.RedBean) bean = (x, y);
            }
            Assume.That(CookingRecipeCatalog.Adjacent(rice.Value.x, rice.Value.y, bean.Value.x, bean.Value.y, false),
                "판 배치가 연결되지 않으면(드묾) 건너뜀");

            Assert.IsTrue(session.TryBeginPath(rice.Value.x, rice.Value.y));
            Assert.IsTrue(session.TryExtendPath(bean.Value.x, bean.Value.y));
            session.EndPath();
            Assert.IsTrue(CodexTests.CollectAllPerfect(session));

            Assert.IsTrue(session.Finished, "더 만들 게 없으면 꺼낸 뒤 정산");
            // 김 오를 때 꺼내면 ×2
            Assert.AreEqual(2, eco.GetOfferingCount(OfferingCatalog.GoldenIdOf("patteok")));
            Assert.AreEqual(0, eco.GetOfferingCount("patteok"));
        }

        [Test]
        public void Cooking_Nagari_ReturnsGoldenRice()
        {
            eco.AddSpecialItem(SpecialItemId.GoldenRice, 1);
            eco.AddMaterial(CookingIngredientId.RedBean, 1);
            var session = new CookingSession();
            session.Prepare(CookingCharmType.None);
            Assert.IsTrue(session.StartRound());
            session.CancelNagari();
            Assert.AreEqual(1, eco.GetSpecialItemCount(SpecialItemId.GoldenRice));
            Assert.AreEqual(0, eco.GetMaterialCount(CookingIngredientId.Rice));
            Assert.AreEqual(1, eco.GetMaterialCount(CookingIngredientId.RedBean));
        }
    }
}
