using UnityEngine;

namespace KSpirits.Minigames.Yut
{
    /// <summary>윷 던지기 1회의 판정 결과와, 보너스 던지기(모/윷) 부여 여부.</summary>
    public readonly struct YutThrowOutcome
    {
        public readonly YutThrowResult Result;
        public readonly bool GrantsBonusThrow;

        public YutThrowOutcome(YutThrowResult result, bool grantsBonusThrow)
        {
            Result = result;
            GrantsBonusThrow = grantsBonusThrow;
        }
    }

    /// <summary>
    /// 도/개/걸/윷/모/빽도 확률 판정. 기획서 7-4 윷가락 판정 표 기준(균등 가정, 16분의):
    /// 모 1, 빽도 1, 도 3, 개 6, 걸 4, 윷 1. 모/윷은 보너스 던지기(하트 소모 없이 한 번 더)를
    /// 부여한다 — 하트 소모 여부는 호출부(하트를 들고 있는 쪽) 책임이고, 여기선 값만 알려준다.
    /// </summary>
    public static class YutThrowRoller
    {
        readonly struct Entry
        {
            public readonly YutThrowResult Result;
            public readonly int Weight;
            public readonly bool Bonus;

            public Entry(YutThrowResult result, int weight, bool bonus)
            {
                Result = result;
                Weight = weight;
                Bonus = bonus;
            }
        }

        static readonly Entry[] Table =
        {
            new(YutThrowResult.Mo, 1, true),
            new(YutThrowResult.Baekdo, 1, false),
            new(YutThrowResult.Do, 3, false),
            new(YutThrowResult.Gae, 6, false),
            new(YutThrowResult.Geol, 4, false),
            new(YutThrowResult.Yut, 1, true),
        };

        const int TotalWeight = 16; // Table 가중치 합과 일치해야 함

        public static YutThrowOutcome Roll()
        {
            int roll = Random.Range(0, TotalWeight);
            foreach (var entry in Table)
            {
                if (roll < entry.Weight)
                    return new YutThrowOutcome(entry.Result, entry.Bonus);
                roll -= entry.Weight;
            }
            var last = Table[^1];
            return new YutThrowOutcome(last.Result, last.Bonus);
        }

        public static string DisplayName(this YutThrowResult result) => result switch
        {
            YutThrowResult.Baekdo => "빽도",
            YutThrowResult.Do => "도",
            YutThrowResult.Gae => "개",
            YutThrowResult.Geol => "걸",
            YutThrowResult.Yut => "윷",
            YutThrowResult.Mo => "모",
            _ => result.ToString(),
        };
    }
}
