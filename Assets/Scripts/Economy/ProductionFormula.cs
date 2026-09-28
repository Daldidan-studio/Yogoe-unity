using System;
using Yoegoe.Data;

namespace Yoegoe.Economy
{
    /// <summary>
    /// 기물 분당 생산 공식 (기획 7-1) 단일 소스.
    /// 온라인 틱(<see cref="Yoegoe.Characters.CharacterAgent"/>)과 오프라인 정산
    /// (<see cref="Yoegoe.Save.OfflineSimulator"/>)이 반드시 이 클래스를 통해서만 계산해야 한다 —
    /// 각자 재구현하면 온라인/오프라인 생산량이 갈라지는 버그가 생긴다.
    /// </summary>
    public static class ProductionFormula
    {
        /// <summary>레벨당 생산 성장률. 8장: 500 × 1.15^(L-1)은 업그레이드 "비용"이고, 이건 별개인
        /// 레벨당 "생산" 성장률이다 (<see cref="PropEconomy"/>와 혼동하지 말 것).</summary>
        public const double LevelGrowth = 1.1;

        public static double LevelMultiplier(int level, double growth = LevelGrowth) =>
            Math.Pow(growth, Math.Max(1, level) - 1);

        /// <summary>친밀도 보정 = 1 + 친밀도/100.</summary>
        public static double IntimacyMultiplier(float intimacy) => 1.0 + intimacy / 100.0;

        /// <summary>주인이 자기 엔딩 기물에 앉으면 ×ownerMultiplier (시트 props.ownerMultiplier).</summary>
        public static double EndingMultiplier(bool isEndingProp, bool sameOwner, double ownerMultiplier = 2.0) =>
            isEndingProp && sameOwner ? ownerMultiplier : 1.0;

        /// <summary>공덕 기물 분당 산출. 보정 on/off·배율은 시트 props 탭 값.</summary>
        public static double PerMinute(
            double baseProductionPerMinute, int level,
            float intimacy,
            bool isEndingProp, bool sameOwner,
            double levelGrowth = LevelGrowth, bool intimacyBonus = true, double ownerMultiplier = 2.0)
        {
            return baseProductionPerMinute
                   * LevelMultiplier(level, levelGrowth)
                   * (intimacyBonus ? IntimacyMultiplier(intimacy) : 1.0)
                   * EndingMultiplier(isEndingProp, sameOwner, ownerMultiplier);
        }

        /// <summary>공덕 더미 보관(만창) = 레벨 기준 분당 산출(보정 전) × capacityMinutes. 0 이하면 무제한.</summary>
        public static double MeritCapacity(double baseProductionPerMinute, int level, double levelGrowth, float capacityMinutes) =>
            capacityMinutes > 0f
                ? baseProductionPerMinute * LevelMultiplier(level, levelGrowth) * capacityMinutes
                : double.PositiveInfinity;
    }
}
