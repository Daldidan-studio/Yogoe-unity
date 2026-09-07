using System;
using Yoegoe.Core;

namespace Yoegoe.Economy
{
    /// <summary>
    /// 공덕 더미를 담아두는 최소 단위 정적 저장소 (7장). 아직 UI/수거 시스템이 없어서
    /// 우선 상태머신의 생산량이 어디론가 쌓이도록 자리만 잡아둔 것 — 나중에 세이브/수거 붙이면서 확장.
    /// </summary>
    public static class GameEconomy
    {
        public static BigNumber MeritPile { get; private set; } = BigNumber.Zero;
        public static event Action<BigNumber> OnMeritChanged;

        public static void AddMerit(BigNumber amount)
        {
            MeritPile += amount;
            OnMeritChanged?.Invoke(MeritPile);
        }

        public static void ResetForTesting() => MeritPile = BigNumber.Zero;
    }
}
