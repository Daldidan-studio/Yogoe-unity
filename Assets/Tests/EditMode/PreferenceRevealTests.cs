using NUnit.Framework;
using UnityEngine;
using Yoegoe.Characters;
using Yoegoe.Save;

namespace Yoegoe.Tests.EditMode
{
    /// <summary>선호 공양물: 최초 비공개, 먹이면 영구 공개, 세이브 왕복 유지.</summary>
    public class PreferenceRevealTests
    {
        [Test]
        public void NewStats_NothingRevealed()
        {
            var stats = new CharacterRuntimeStats();
            Assert.IsFalse(stats.IsPreferenceRevealed("yakju"));
        }

        [Test]
        public void Reveal_FirstTimeTrue_ThenFalse_CaseInsensitive()
        {
            var stats = new CharacterRuntimeStats();
            Assert.IsTrue(stats.RevealPreference("Yakju"));
            Assert.IsFalse(stats.RevealPreference("yakju"));
            Assert.IsTrue(stats.IsPreferenceRevealed("YAKJU"));
            Assert.AreEqual(1, stats.RevealedPreferredOfferingIds.Count);
        }

        [Test]
        public void SetRevealed_NullOrEmptyIds_Ignored()
        {
            var stats = new CharacterRuntimeStats();
            stats.RevealPreference("old");
            stats.SetRevealedPreferences(new[] { "a", null, "", "A" });
            Assert.IsFalse(stats.IsPreferenceRevealed("old"));
            Assert.AreEqual(1, stats.RevealedPreferredOfferingIds.Count);

            stats.SetRevealedPreferences(null);
            Assert.AreEqual(0, stats.RevealedPreferredOfferingIds.Count);
        }

        [Test]
        public void OldSaveJson_WithoutField_LoadsAsEmpty()
        {
            var agent = JsonUtility.FromJson<AgentSave>("{\"characterId\":\"1\",\"intimacy\":50}");
            Assert.IsNotNull(agent.revealedPreferredOfferingIds);
            Assert.AreEqual(0, agent.revealedPreferredOfferingIds.Length);
        }

        [Test]
        public void AgentSave_JsonRoundTrip_KeepsRevealed()
        {
            var json = JsonUtility.ToJson(new AgentSave { revealedPreferredOfferingIds = new[] { "sinseollo", "hwachae" } });
            var back = JsonUtility.FromJson<AgentSave>(json);
            CollectionAssert.AreEqual(new[] { "sinseollo", "hwachae" }, back.revealedPreferredOfferingIds);
        }
    }
}
