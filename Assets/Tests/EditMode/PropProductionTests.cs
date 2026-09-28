using System;
using NUnit.Framework;
using UnityEngine;
using Yoegoe.Characters;
using Yoegoe.Cooking;
using Yoegoe.Data;
using Yoegoe.Economy;
using Yoegoe.Save;

namespace Yoegoe.Tests.EditMode
{
    /// <summary>
    /// 기물별 산출 (Docs/00 §6-2·6-3): 보관·만창·오버플로우, 시트(props.json) 값, 탭 수거, 오프라인 정산.
    /// </summary>
    public class PropProductionTests
    {
        // ---------------- PropStorage (순수 규칙) ----------------

        [TestCase(1, 9)]
        [TestCase(9, 9)]
        [TestCase(10, 10)]
        [TestCase(21, 11)]
        public void Capacity_GainsOnePerTenLevels(int level, int expected)
        {
            Assert.AreEqual(expected, PropStorage.Capacity(9, level));
        }

        [TestCase(1, 0.1f)]
        [TestCase(9, 0.9f)]
        [TestCase(10, 0f)]
        [TestCase(11, 0.1f)]
        public void OverflowChance_ResetsEveryTenLevels(int level, float expected)
        {
            Assert.AreEqual(expected, PropStorage.OverflowChance(level), 0.0001f);
        }

        [Test]
        public void Advance_FillsOnePerCycle_ThenJudgesOnceAndHalts_OnFailedRoll()
        {
            var s = new PropStorage.State();
            // 20분 주기, 보관 9: 9개(180분) + 한 사이클 더(20분) → 판정 실패 → 정지
            float worked = PropStorage.Advance(ref s, 1200f, 9, 0.5f, 100000f, () => 0.99f, null);

            Assert.AreEqual(9, s.Stored);
            Assert.IsTrue(PropStorage.IsHalted(s, 9));
            Assert.AreEqual(10 * 1200f, worked, 0.01f); // 정지 이후는 일하지 않음(기력 소모 없음)
            Assert.AreEqual(0f, PropStorage.Advance(ref s, 1200f, 9, 0.5f, 5000f, () => 0f, null));
        }

        [Test]
        public void Advance_OverflowSuccess_StoresCapacityPlusOne()
        {
            var s = new PropStorage.State();
            PropStorage.Advance(ref s, 60f, 1, 0.9f, 100000f, () => 0.1f, null);
            Assert.AreEqual(2, s.Stored);
            Assert.IsTrue(PropStorage.IsHalted(s, 1));
        }

        [Test]
        public void TakeAll_Resumes_NextFullCycleJudgesAgain()
        {
            var s = new PropStorage.State();
            PropStorage.Advance(ref s, 60f, 2, 0f, 100000f, () => 0.5f, null);
            Assert.AreEqual(2, PropStorage.TakeAll(ref s));
            Assert.IsFalse(PropStorage.IsHalted(s, 2));

            PropStorage.Advance(ref s, 60f, 2, 0f, 60f, () => 0.5f, null);
            Assert.AreEqual(1, s.Stored);
        }

        [Test]
        public void LevelUpRaisingCapacity_KeepsOverflowItems_AndReinterprets()
        {
            // 활터 Lv9 오버플로우 10/9 → Lv10 기본 보관 10 → 10/10 (삭제 없음, 계속 정지)
            var s = new PropStorage.State { Stored = 10, OverflowJudged = true };
            Assert.IsTrue(PropStorage.IsHalted(s, PropStorage.Capacity(9, 10)));
            Assert.AreEqual(10, s.Stored);
        }

        // ---------------- props.json (시트) ----------------

        [Test]
        public void Catalog_HasSpecValues()
        {
            Assert.IsTrue(PropCatalog.TryGet("옹달샘", out var well));
            Assert.AreEqual(PropResourceType.PurifiedWater, well.ResourceType);
            Assert.AreEqual(30f, well.cycleMinutes, 0.001f);
            Assert.AreEqual(6, well.baseCapacity);

            Assert.IsTrue(PropCatalog.TryGet("갯바위", out var rock));
            Assert.AreEqual(PropResourceType.Yeopjeon, rock.ResourceType);
            Assert.AreEqual(1, rock.baseCapacity);

            Assert.IsTrue(PropCatalog.TryGet("화덕", out var oven));
            Assert.IsFalse(oven.upgradable);
        }

        [Test]
        public void RollIngredient_FollowsGatherWeights()
        {
            Assert.AreEqual(CookingIngredientId.Rice, PropCatalog.RollIngredient(PropResourceType.Gather, 0f));
            Assert.AreEqual(CookingIngredientId.Rice, PropCatalog.RollIngredient(PropResourceType.Gather, 0.39f));
            Assert.AreEqual(CookingIngredientId.RedBean, PropCatalog.RollIngredient(PropResourceType.Gather, 0.999f));
            Assert.AreEqual(CookingIngredientId.Egg, PropCatalog.RollIngredient(PropResourceType.Hunt, 0f));
        }

        // ---------------- PropSlot 온라인 + 수거 ----------------

        GameObject economyGO;
        GameEconomy economy;
        GameObject propGO;
        PropSlot prop;
        PropData data;

        void MakeProp(PropResourceType type, float cycleMinutes, int capacity)
        {
            economyGO = new GameObject("GameEconomy_Test");
            economy = economyGO.AddComponent<GameEconomy>();
            economy.BecomeInstance();
            var settings = ScriptableObject.CreateInstance<StartingStateSettings>();
            settings.startingPurifiedWater = 0;
            settings.startingYeopjeon = 0;
            economy.ApplyStartingState(settings);

            data = ScriptableObject.CreateInstance<PropData>();
            data.propId = "test_" + type;
            data.resourceType = type;
            data.cycleMinutes = cycleMinutes;
            data.baseCapacity = capacity;
            propGO = new GameObject("Prop_Test");
            prop = propGO.AddComponent<PropSlot>();
            prop.data = data;
            prop.ConfigureBuiltState(true);
        }

        [TearDown]
        public void TearDown()
        {
            if (propGO != null) UnityEngine.Object.DestroyImmediate(propGO);
            if (data != null) UnityEngine.Object.DestroyImmediate(data);
            if (economyGO != null) UnityEngine.Object.DestroyImmediate(economyGO);
            propGO = null; data = null; economyGO = null;
        }

        [Test]
        public void Well_ProducesWater_TapCollectsIntoWallet()
        {
            MakeProp(PropResourceType.PurifiedWater, 30f, 6);
            prop.ProduceWhileStaying(3 * 1800f, 0);

            Assert.AreEqual(3, prop.StoredResources);
            Assert.IsTrue(prop.TryCollect());
            Assert.AreEqual(3, economy.PurifiedWater);
            Assert.AreEqual(0, prop.StoredResources);
        }

        [Test]
        public void HerbField_StoresRolledIngredients_CollectAddsMaterials()
        {
            MakeProp(PropResourceType.Gather, 20f, 9);
            int before = 0;
            for (int i = 0; i < (int)CookingIngredientId.Count; i++)
                before += economy.GetMaterialCount((CookingIngredientId)i);

            prop.ProduceWhileStaying(4 * 1200f, 0);
            Assert.AreEqual(4, prop.PendingIngredients.Count);
            prop.TryCollect();

            int after = 0;
            for (int i = 0; i < (int)CookingIngredientId.Count; i++)
                after += economy.GetMaterialCount((CookingIngredientId)i);
            Assert.AreEqual(before + 4, after);
        }

        [Test]
        public void Rock_FullStorage_HaltsProduction()
        {
            MakeProp(PropResourceType.Yeopjeon, 60f, 1);
            // 1개(60분) + 판정 사이클(60분) 이후엔 멈춘다
            float worked = prop.ProduceWhileStaying(10 * 3600f, 0);
            Assert.IsTrue(prop.IsStorageHalted);
            Assert.AreEqual(2 * 3600f, worked, 0.01f);
        }

        [Test]
        public void MeritPile_StopsAtCapacityMinutes()
        {
            MakeProp(PropResourceType.Merit, 0f, 0);
            data.baseProductionPerMinute = 100;
            data.meritCapacityMinutes = 30f;

            float worked = prop.ProduceWhileStaying(3600f, 100);
            Assert.AreEqual(1800f, worked, 0.5f);
            Assert.AreEqual(3000.0, prop.PendingMerit.ToDouble(), 0.5);
            Assert.IsTrue(prop.IsStorageHalted);
        }

        // ---------------- 오프라인 ----------------

        [Test]
        public void Offline_FullWell_StopsStaminaDrain()
        {
            var now = DateTime.UtcNow;
            var data = new GameSaveData
            {
                savedAtUtcTicks = now.AddHours(-10).Ticks,
                props = new[] { new PropSave { propId = "옹달샘", isBuilt = true, level = 1 } },
                agents = new[]
                {
                    new AgentSave
                    {
                        characterId = "SamjokO", stamina = 75f, intimacy = 50f,
                        state = ActionState.Staying, occupiedPropId = "옹달샘"
                    }
                }
            };

            OfflineSimulator.Simulate(data, now);

            // 6개(3시간) + 판정 사이클(30분)만 일함 → 기력 210분 / 10분 = 21 소모
            Assert.GreaterOrEqual(data.props[0].storedResources, 6);
            Assert.IsTrue(data.props[0].overflowJudged);
            Assert.AreEqual(75f - 21f, data.agents[0].stamina, 0.05f);
            Assert.AreEqual(ActionState.Staying, data.agents[0].state);
        }
    }
}
