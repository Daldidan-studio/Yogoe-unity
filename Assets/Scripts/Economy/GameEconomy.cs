using System;
using Yoegoe.Core;

namespace Yoegoe.Economy
{
    /// <summary>
    /// 재화 저장소 (2장/3장). 공덕은 무한 확장(BigNumber)이 필요해서 따로 두고, 나머지 4종
    /// (엽전/향/정화수/윷토큰)은 기획서상 상한이 있거나 정수로 충분해서 int로 둠.
    /// 아직 UI/수거/소비 화면이 없어서 우선 값이 어디론가 쌓이고 빠질 자리만 잡아둔 것 —
    /// 나중에 세이브/상점/윷놀이 보상 붙이면서 확장.
    /// </summary>
    public static class GameEconomy
    {
        // ---------------- 공덕 (7장, 무한 확장) ----------------
        public static BigNumber MeritPile { get; private set; } = BigNumber.Zero;
        public static event Action<BigNumber> OnMeritChanged;

        public static void AddMerit(BigNumber amount)
        {
            MeritPile += amount;
            OnMeritChanged?.Invoke(MeritPile);
        }

        // ---------------- 엽전 (2장 시작 0, 상점에서 소비) ----------------
        public static int Yeopjeon { get; private set; } = 0;
        public static event Action<int> OnYeopjeonChanged;
        public static void AddYeopjeon(int amount) { Yeopjeon += amount; OnYeopjeonChanged?.Invoke(Yeopjeon); }
        public static bool TrySpendYeopjeon(int amount)
        {
            if (amount < 0 || Yeopjeon < amount) return false;
            Yeopjeon -= amount;
            OnYeopjeonChanged?.Invoke(Yeopjeon);
            return true;
        }

        // ---------------- 향 (2장 시작 2, 소환 3개 소모) ----------------
        public static int Hyang { get; private set; } = 2;
        public static event Action<int> OnHyangChanged;
        public static void AddHyang(int amount) { Hyang += amount; OnHyangChanged?.Invoke(Hyang); }
        public static bool TrySpendHyang(int amount)
        {
            if (amount < 0 || Hyang < amount) return false;
            Hyang -= amount;
            OnHyangChanged?.Invoke(Hyang);
            return true;
        }

        // ---------------- 정화수 (2장 시작 10, 공양으로 기력 회복/진화에 사용) ----------------
        public static int PurifiedWater { get; private set; } = 10;
        public static event Action<int> OnPurifiedWaterChanged;
        public static void AddPurifiedWater(int amount) { PurifiedWater += amount; OnPurifiedWaterChanged?.Invoke(PurifiedWater); }
        public static bool TrySpendPurifiedWater(int amount)
        {
            if (amount < 0 || PurifiedWater < amount) return false;
            PurifiedWater -= amount;
            OnPurifiedWaterChanged?.Invoke(PurifiedWater);
            return true;
        }

        // ---------------- 윷 토큰 (2장: 시작5/최대5/30분마다 1충전) ----------------
        public const int YutTokenMax = 5;
        public static int YutToken { get; private set; } = YutTokenMax;
        public static event Action<int> OnYutTokenChanged;
        public static void AddYutToken(int amount)
        {
            YutToken = Math.Min(YutTokenMax, YutToken + amount);
            OnYutTokenChanged?.Invoke(YutToken);
        }
        public static bool TrySpendYutToken(int amount)
        {
            if (amount < 0 || YutToken < amount) return false;
            YutToken -= amount;
            OnYutTokenChanged?.Invoke(YutToken);
            return true;
        }

        public static void ResetForTesting()
        {
            MeritPile = BigNumber.Zero;
            Yeopjeon = 0;
            Hyang = 2;
            PurifiedWater = 10;
            YutToken = YutTokenMax;
        }
    }
}
