using NUnit.Framework;
using UnityEngine;
using Yoegoe.Characters;
using Yoegoe.Data;

namespace Yoegoe.Tests.EditMode
{
    /// <summary>Docs/07: 머리 위 말풍선을 눌러도 몸 탭과 동일 — 혼잣말이 떠 있으면 다음 대사로 바뀌고 10초 연장.</summary>
    public class OverheadTapTests
    {
        GameObject go;
        CharacterAgent agent;

        [SetUp]
        public void SetUp()
        {
            go = new GameObject("OverheadTapAgent");
            agent = go.AddComponent<CharacterAgent>();
            agent.Data = ScriptableObject.CreateInstance<CharacterData>();
            agent.Data.id = CharacterId.SamjokO;
            agent.Data.monologueLines = new[] { "하나", "둘", "셋" };
            agent.Stats.State = ActionState.Walking;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var suffix in new[] { "_Bubble", "_BubbleBg" })
            {
                var b = GameObject.Find(go.name + suffix);
                if (b != null) Object.DestroyImmediate(b);
            }
            Object.DestroyImmediate(agent.Data);
            Object.DestroyImmediate(go);
        }

        static TextMesh Bubble(CharacterAgent a) => GameObject.Find(a.name + "_Bubble")?.GetComponent<TextMesh>();

        [Test]
        public void NoBubble_OverheadIsNotHit()
        {
            Assert.IsFalse(agent.HitOverhead(agent.transform.position + Vector3.up * 1.2f, 0.05f));
        }

        [Test]
        public void TapCyclesLine_AndBubbleIsTappable()
        {
            agent.OnTapped();
            var text = Bubble(agent);
            Assert.IsNotNull(text);
            Assert.AreEqual("하나", text.text);

            var bubblePos = text.transform.position;
            Assert.IsTrue(agent.HitOverhead(bubblePos, 0.05f), "말풍선 위치를 누르면 그 요괴 탭");

            agent.OnTapped();
            Assert.AreEqual("둘", text.text, "떠 있을 때 탭하면 다음 대사");
        }
    }
}
