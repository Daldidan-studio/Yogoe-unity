using NUnit.Framework;
using UnityEngine;
using Yoegoe.Characters;
using Yoegoe.Data;
using Yoegoe.Economy;

namespace Yoegoe.Tests.EditMode
{
    /// <summary>소환 슬롯 (기획 2·9장): 3번째 = 고라니, 4번째 잠긴 칸은 엽전 99로 연 뒤 구미호.</summary>
    public class SummonSlotTests
    {
        GameObject ecoGO;
        GameEconomy eco;

        [SetUp]
        public void SetUp()
        {
            CharacterSummon.ResetFromSave(false);
            ecoGO = new GameObject("Eco");
            eco = ecoGO.AddComponent<GameEconomy>();
            eco.BecomeInstance();
            var s = ScriptableObject.CreateInstance<StartingStateSettings>();
            s.startingYeopjeon = 100;
            s.startingHyang = 3;
            eco.ApplyStartingState(s);
        }

        [TearDown]
        public void TearDown()
        {
            CharacterSummon.ResetFromSave(false);
            Object.DestroyImmediate(ecoGO);
        }

        [Test]
        public void Gumiho_NeedsUnlockedSlot()
        {
            Assert.IsTrue(CharacterSummon.HasOpenSlotFor(CharacterId.Gorani));
            Assert.IsFalse(CharacterSummon.HasOpenSlotFor(CharacterId.Gumiho));
            Assert.IsFalse(CharacterSummon.CanSummon(CharacterId.Gumiho));
        }

        [Test]
        public void Unlock_Costs99Yeopjeon_Once()
        {
            Assert.IsTrue(CharacterSummon.TryUnlockLockedSlot(eco));
            Assert.AreEqual(1, eco.Yeopjeon);
            Assert.IsTrue(CharacterSummon.LockedSlotUnlocked);
            Assert.IsTrue(CharacterSummon.CanSummon(CharacterId.Gumiho)); // 향 3 보유
            Assert.IsFalse(CharacterSummon.TryUnlockLockedSlot(eco));     // 이미 열림
            Assert.AreEqual(1, eco.Yeopjeon);
        }

        [Test]
        public void Unlock_FailsWithoutYeopjeon()
        {
            eco.TrySpendYeopjeon(10); // 90 남음
            Assert.IsFalse(CharacterSummon.TryUnlockLockedSlot(eco));
            Assert.AreEqual(90, eco.Yeopjeon);
            Assert.IsFalse(CharacterSummon.LockedSlotUnlocked);
        }

        [Test]
        public void Summon_NeedsThreeHyang()
        {
            CharacterSummon.TryUnlockLockedSlot(eco);
            eco.TrySpendHyang(1);
            Assert.IsFalse(CharacterSummon.CanSummon(CharacterId.Gumiho));
            Assert.IsFalse(CharacterSummon.CanSummon(CharacterId.Gorani));
        }
    }
}
