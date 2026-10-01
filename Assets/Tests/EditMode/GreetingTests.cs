using NUnit.Framework;
using UnityEngine;
using Yoegoe.Characters;
using Yoegoe.Data;

namespace Yoegoe.Tests.EditMode
{
    /// <summary>접속 인사 (6장): 놀고 있던 요괴만, 대사는 시트 character_lines type=greeting.</summary>
    public class GreetingTests
    {
        GameObject go;
        CharacterAgent agent;

        [SetUp]
        public void SetUp()
        {
            CharacterCatalog.EnsureLoaded();
            go = new GameObject("Agent");
            agent = go.AddComponent<CharacterAgent>();
            agent.Data = ScriptableObject.CreateInstance<CharacterData>();
            agent.Data.id = CharacterId.Rabbit;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(agent.Data);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void OnlyPlayingYokaiGreet()
        {
            agent.Stats.State = ActionState.Playing;
            Assert.IsTrue(Greeting.ShouldGreet(agent));
            foreach (var s in new[] { ActionState.Walking, ActionState.Staying, ActionState.Fainted })
            {
                agent.Stats.State = s;
                Assert.IsFalse(Greeting.ShouldGreet(agent), s.ToString());
            }
        }

        [Test]
        public void LineComesFromSheetGreetingLines()
        {
            Assert.IsTrue(CharacterCatalog.TryGet(CharacterId.Rabbit, out var entry));
            Assert.IsNotNull(entry.greetingLines);
            Assert.IsNotEmpty(entry.greetingLines);
            for (int i = 0; i < 10; i++)
                CollectionAssert.Contains(entry.greetingLines, Greeting.PickLine(agent));
        }
    }
}
