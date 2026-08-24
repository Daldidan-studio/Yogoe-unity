using KSpirits.Core;
using KSpirits.Data;
using KSpirits.Model;

namespace KSpirits.Systems
{
    public static class SummonService
    {
        public static bool CanSummon(GameState state) =>
            state != null && state.Wallet.Incense >= GameConstants.IncensePerSummon;

        public static bool TrySummon(GameState state, out YokaiInstance summoned, out SummonEntry entry)
        {
            summoned = null;
            entry = default;

            if (!CanSummon(state))
                return false;

            entry = SummonCatalog.Pick(state.TotalSummons);
            state.Wallet.Incense -= GameConstants.IncensePerSummon;
            state.TotalSummons++;

            summoned = new YokaiInstance(entry.Id, entry.DisplayName)
            {
                Stage = YokaiStage.Spirit,
                Energy = 0,
                Intimacy = 0
            };
            return true;
        }
    }
}
