using NUnit.Framework;
using UnityEngine;
using Yoegoe.Characters;
using Yoegoe.Data;

namespace Yoegoe.Tests.EditMode
{
    /// <summary>
    /// 공양 규칙: 기절은 정화수로만 깨어남(0→1) / 기력 최대면 기력은 멈추고 친밀도만 오름.
    /// 공양물 카탈로그: 공양간 레시피 결과물이 전부 등록되고, 랜덤 풀은 공양물 24종.
    /// </summary>
    public class OfferingRulesTests
    {
        GameObject go;
        CharacterAgent agent;

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
        public void Fainted_PurifiedWater_WakesWithOneStamina()
        {
            agent.Stats.State = ActionState.Fainted;
            agent.Stats.Stamina = 0f;

            agent.ReceiveOffering(1, 0f, OfferingKind.PurifiedWater);

            Assert.AreNotEqual(ActionState.Fainted, agent.Stats.State);
            Assert.AreEqual(1f, agent.Stats.Stamina, 0.0001f);
        }

        [Test]
        public void FullStamina_PreferredOffering_OnlyIntimacyRises_Repeatedly()
        {
            for (int i = 0; i < 4; i++)
                agent.ReceiveOffering(3, 5f, OfferingKind.Preferred);

            Assert.AreEqual(70f, agent.Stats.Intimacy, 0.0001f);
            Assert.AreEqual(75f, agent.Stats.Stamina, 0.0001f); // 기력은 최대(75)에서 끝
        }

        [Test]
        public void Catalog_RegistersEveryRecipeProduct_AndPoolIs24Offerings()
        {
            var all = OfferingCatalog.Build(null);

            Assert.AreEqual(36 + 24, all.Length);
            Assert.AreEqual(24, OfferingCatalog.RandomPool.Count);
            foreach (var o in OfferingCatalog.RandomPool)
                Assert.AreEqual(OfferingKind.General, o.kind, o.offeringId);
            Assert.IsNotNull(OfferingCatalog.Find("sinseollo"));
            Assert.AreEqual(OfferingKind.Food, OfferingCatalog.Find("bap").kind);
        }

        [Test]
        public void Catalog_AssetWinsOverRuntimeEntry()
        {
            var asset = ScriptableObject.CreateInstance<OfferingData>();
            asset.offeringId = "yakgwa";
            asset.displayName = "약과(에셋)";
            asset.kind = OfferingKind.General;
            try
            {
                OfferingCatalog.Build(new[] { asset });
                Assert.AreSame(asset, OfferingCatalog.Find("yakgwa"));
                CollectionAssert.Contains(new System.Collections.Generic.List<OfferingData>(OfferingCatalog.RandomPool), asset);
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }
    }
}
