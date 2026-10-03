using NUnit.Framework;
using UnityEngine;
using Yoegoe.Characters;
using Yoegoe.Data;
using Yoegoe.Economy;

namespace Yoegoe.Tests.EditMode
{
    /// <summary>7장: 보관이 가득 찬(만창) 기물은 "가서 일할 수 있는 기물" 후보가 아니다 — 만창 기물에 앉은 요괴는 다른 기물로 간다.</summary>
    public class HaltedPropMoveTests
    {
        GameObject managerGO, fullGO, emptyGO, agentGO;
        PropManager manager;
        PropSlot full, empty;
        PropData waterData, meritData;
        CharacterAgent agent;

        static PropData MakeData(string id, PropResourceType type)
        {
            var d = ScriptableObject.CreateInstance<PropData>();
            d.propId = id;
            d.resourceType = type;
            d.baseCapacity = 2;
            d.cycleMinutes = 10f;
            d.baseProductionPerMinute = type == PropResourceType.Merit ? 1 : 0;
            return d;
        }

        static PropSlot MakeProp(string name, PropData data, out GameObject go)
        {
            go = new GameObject(name);
            var p = go.AddComponent<PropSlot>();
            p.data = data;
            p.ConfigureBuiltState(true);
            return p;
        }

        [SetUp]
        public void SetUp()
        {
            managerGO = new GameObject("PropManager_Test");
            manager = managerGO.AddComponent<PropManager>();
            waterData = MakeData("test_halted_water", PropResourceType.Water);
            meritData = MakeData("test_halted_merit", PropResourceType.Merit);
            full = MakeProp("Full", waterData, out fullGO);
            empty = MakeProp("Empty", meritData, out emptyGO);
            manager.Register(full);
            manager.Register(empty);

            agentGO = new GameObject("Agent");
            agent = agentGO.AddComponent<CharacterAgent>();
            agent.Data = ScriptableObject.CreateInstance<CharacterData>();
            agent.Data.id = CharacterId.SamjokO;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(agent.Data);
            foreach (var go in new[] { agentGO, fullGO, emptyGO, managerGO }) Object.DestroyImmediate(go);
            Object.DestroyImmediate(waterData);
            Object.DestroyImmediate(meritData);
        }

        [Test]
        public void FullStorageProp_IsNotAWalkTarget()
        {
            full.RestoreStorage(2, 0f, true, null); // 보관 2/2 + 넘침 판정 끝 → 만창
            Assert.IsTrue(full.IsStorageHalted);

            for (int i = 0; i < 30; i++)
                Assert.AreSame(empty, manager.GetRandomAvailableProp(agent, null));
        }

        [Test]
        public void OnlyFullPropsLeft_NoTarget_SoAgentStaysPut()
        {
            full.RestoreStorage(2, 0f, true, null);
            Assert.IsNull(manager.GetRandomAvailableProp(agent, empty));
        }

        [Test]
        public void NotYetFull_IsStillATarget()
        {
            full.RestoreStorage(1, 0f, false, null);
            Assert.IsFalse(full.IsStorageHalted);
            Assert.AreSame(full, manager.GetRandomAvailableProp(agent, empty));
        }
    }
}
