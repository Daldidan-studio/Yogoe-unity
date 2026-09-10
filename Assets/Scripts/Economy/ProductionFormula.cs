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

        public static double LevelMultiplier(int level) =>
            Math.Pow(LevelGrowth, Math.Max(1, level) - 1);

        /// <summary>친밀도 보정 = 1 + 친밀도/100. 넋은 친밀도 없음 → ×1.</summary>
        public static double IntimacyMultiplier(GrowthStage stage, float intimacy) =>
            stage == GrowthStage.Neok ? 1.0 : 1.0 + intimacy / 100.0;

        /// <summary>주인이 자기 엔딩 기물에 앉으면 ×2.</summary>
        public static double EndingMultiplier(bool isEndingProp, bool sameOwner) =>
            isEndingProp && sameOwner ? 2.0 : 1.0;

        public static double PerMinute(
            double baseProductionPerMinute, int level,
            GrowthStage stage, float intimacy,
            bool isEndingProp, bool sameOwner)
        {
            return baseProductionPerMinute
                   * LevelMultiplier(level)
                   * IntimacyMultiplier(stage, intimacy)
                   * EndingMultiplier(isEndingProp, sameOwner);
        }
    }
}
