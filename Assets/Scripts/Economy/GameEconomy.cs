using System;
using System.Collections.Generic;
using Yoegoe.Core;
using Yoegoe.Data;
using UnityEngine;

namespace Yoegoe.Economy
{
    /// <summary>
    /// 재화·공양물 인벤토리. 시작값은 StartingStateSettings.asset 에서 적용.
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

        // ---------------- 엽전 ----------------
        public static int Yeopjeon { get; private set; }
        public static event Action<int> OnYeopjeonChanged;
        public static void AddYeopjeon(int amount) { Yeopjeon += amount; OnYeopjeonChanged?.Invoke(Yeopjeon); }
        public static bool TrySpendYeopjeon(int amount)
        {
            if (amount < 0 || Yeopjeon < amount) return false;
            Yeopjeon -= amount;
            OnYeopjeonChanged?.Invoke(Yeopjeon);
            return true;
        }

        // ---------------- 향 ----------------
        public static int Hyang { get; private set; }
        public static event Action<int> OnHyangChanged;
        public static void AddHyang(int amount) { Hyang += amount; OnHyangChanged?.Invoke(Hyang); }
        public static bool TrySpendHyang(int amount)
        {
            if (amount < 0 || Hyang < amount) return false;
            Hyang -= amount;
            OnHyangChanged?.Invoke(Hyang);
            return true;
        }

        // ---------------- 정화수 ----------------
        public static int PurifiedWater { get; private set; }
        public static event Action<int> OnPurifiedWaterChanged;
        public static void AddPurifiedWater(int amount) { PurifiedWater += amount; OnPurifiedWaterChanged?.Invoke(PurifiedWater); }
        public static bool TrySpendPurifiedWater(int amount)
        {
            if (amount < 0 || PurifiedWater < amount) return false;
            PurifiedWater -= amount;
            OnPurifiedWaterChanged?.Invoke(PurifiedWater);
            return true;
        }

        // ---------------- 윷 토큰 ----------------
        public static int YutTokenMax { get; private set; } = 5;
        public static int YutToken { get; private set; }
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

        // ---------------- 공양물 인벤토리 (정화수 제외) ----------------
        private static readonly Dictionary<string, int> OfferingCounts = new Dictionary<string, int>();
        public static event Action OnOfferingsChanged;

        public static int GetOfferingCount(OfferingData offering)
        {
            if (offering == null) return 0;
            string key = OfferingKey(offering);
            return OfferingCounts.TryGetValue(key, out int n) ? n : 0;
        }

        public static void AddOffering(OfferingData offering, int amount)
        {
            if (offering == null || amount == 0) return;
            string key = OfferingKey(offering);
            OfferingCounts.TryGetValue(key, out int cur);
            OfferingCounts[key] = Math.Max(0, cur + amount);
            OnOfferingsChanged?.Invoke();
        }

        public static bool TrySpendOffering(OfferingData offering, int amount)
        {
            if (offering == null || amount < 0) return false;
            string key = OfferingKey(offering);
            OfferingCounts.TryGetValue(key, out int cur);
            if (cur < amount) return false;
            OfferingCounts[key] = cur - amount;
            OnOfferingsChanged?.Invoke();
            return true;
        }

        private static string OfferingKey(OfferingData offering)
        {
            return !string.IsNullOrEmpty(offering.offeringId) ? offering.offeringId : offering.name;
        }

        /// <summary>StartingStateSettings 기준으로 재화·인벤을 덮어쓴다. Main 부팅 시 1회 호출.</summary>
        public static void ApplyStartingState(StartingStateSettings s)
        {
            if (s == null) s = StartingStateSettings.Get();

            MeritPile = s.startingMerit;
            Yeopjeon = s.startingYeopjeon;
            Hyang = s.startingHyang;
            PurifiedWater = s.startingPurifiedWater;
            YutTokenMax = Mathf.Max(1, s.yutTokenMax);
            YutToken = Mathf.Clamp(s.startingYutToken, 0, YutTokenMax);

            OfferingCounts.Clear();
            if (s.startingOfferings != null)
            {
                int each = Mathf.Max(0, s.startingOfferingCountEach);
                foreach (var o in s.startingOfferings)
                {
                    if (o == null) continue;
                    if (o.kind == OfferingKind.PurifiedWater) continue; // 정화수는 재화 칸
                    AddOffering(o, each);
                }
            }

            OnMeritChanged?.Invoke(MeritPile);
            OnYeopjeonChanged?.Invoke(Yeopjeon);
            OnHyangChanged?.Invoke(Hyang);
            OnPurifiedWaterChanged?.Invoke(PurifiedWater);
            OnYutTokenChanged?.Invoke(YutToken);
        }

        public static void ResetForTesting()
        {
            ApplyStartingState(StartingStateSettings.Get());
        }
    }
}
