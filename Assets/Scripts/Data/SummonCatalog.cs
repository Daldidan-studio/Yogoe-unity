using UnityEngine;

namespace KSpirits.Data
{
    public readonly struct SummonEntry
    {
        public readonly string Id;
        public readonly string DisplayName;
        public readonly Color Accent;
        public readonly string Flavor;

        public SummonEntry(string id, string displayName, Color accent, string flavor)
        {
            Id = id;
            DisplayName = displayName;
            Accent = accent;
            Flavor = flavor;
        }
    }

    /// <summary>보장 3인방 + 이후 랜덤 풀(프로토타입).</summary>
    public static class SummonCatalog
    {
        static readonly SummonEntry[] Guaranteed =
        {
            new("taejagui", "태자귀", new Color(0.55f, 0.72f, 0.95f), "말 많은 아이 요괴"),
            new("gumiho", "구미호", new Color(0.95f, 0.55f, 0.72f), "은은한 향을 풍기는 여우"),
            new("samjok-o", "삼족오", new Color(0.72f, 0.88f, 0.45f), "세 갈래 꼬리의 숲 요괴")
        };

        static readonly SummonEntry[] RandomPool =
        {
            new("dokkaebi", "도깨비", new Color(0.95f, 0.78f, 0.35f), "장난기 많은 존재"),
            new("cheollima", "천마", new Color(0.85f, 0.9f, 0.95f), "하늘을 가르는 기운"),
            new("haetae", "해태", new Color(0.45f, 0.75f, 0.95f), "불꽃 같은 수호자")
        };

        public static SummonEntry Pick(int summonIndex)
        {
            if (summonIndex < Guaranteed.Length)
                return Guaranteed[summonIndex];

            var pool = RandomPool[summonIndex % RandomPool.Length];
            return pool;
        }
    }
}
