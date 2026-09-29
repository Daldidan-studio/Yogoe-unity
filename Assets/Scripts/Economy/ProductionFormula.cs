using System;

namespace Yoegoe.Economy
{
    /// <summary>
    /// 기물 생산 수식 조각(레벨 성장·친밀도 보정). 규칙 조합·만창·주인 배율은
    /// <see cref="PropProduction"/>이 단일 소스 — 온라인·오프라인 모두 그쪽만 호출한다.
    /// </summary>
    public static class ProductionFormula
    {
        /// <summary>구세이브 기본 레벨당 생산 성장률(시트 props.levelGrowth가 우선). 8장: 500 × 1.15^(L-1)은
        /// 업그레이드 "비용"이고, 이건 별개인 레벨당 "생산" 성장률이다 (<see cref="PropEconomy"/>와 혼동하지 말 것).</summary>
        public const double LevelGrowth = 1.1;

        public static double LevelMultiplier(int level, double growth = LevelGrowth) =>
            Math.Pow(growth, Math.Max(1, level) - 1);

        /// <summary>친밀도 보정 = 1 + 친밀도/100 (시트 intimacyBonus가 켜진 기물만).</summary>
        public static double IntimacyMultiplier(float intimacy) => 1.0 + intimacy / 100.0;
    }
}
