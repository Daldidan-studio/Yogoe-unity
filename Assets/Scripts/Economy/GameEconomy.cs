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
        // ---------------- 공덕 (플레이어 지갑 — 수거된 공덕. 기물 더미는 PropSlot.PendingMerit) ----------------
        public static BigNumber MeritPile { get; private set; } = BigNumber.Zero;
        public static event Action<BigNumber> OnMeritChanged;

        /// <summary>
        /// 앱 재시작 일괄 수거 대기분 (7-2). 콜드스타트 시 기물 더미를 여기로 모은다.
        /// 백그라운드 복귀만으로는 채우지 않는다.
        /// </summary>
        public static BigNumber PendingBatchMerit { get; private set; } = BigNumber.Zero;
        public static event Action OnBatchMeritChanged;
        public static bool HasPendingBatchMerit => PendingBatchMerit.Mantissa != 0;

        public static void AddMerit(BigNumber amount)
        {
            MeritPile += amount;
            OnMeritChanged?.Invoke(MeritPile);
        }

        public static bool TrySpendMerit(BigNumber amount)
        {
            if (amount.Mantissa < 0) return false;
            if (MeritPile < amount) return false;
            MeritPile -= amount;
            OnMeritChanged?.Invoke(MeritPile);
            return true;
        }

        /// <summary>플레이어가 구매로 지은 기물 수 (prebuilt 제외). 다음 구매 n = 이 값 + 1.</summary>
        public static int PropsPurchasedCount { get; private set; }
        public static event Action OnPropsPurchasedCountChanged;

        public static void IncrementPropsPurchasedCount()
        {
            PropsPurchasedCount++;
            OnPropsPurchasedCountChanged?.Invoke();
        }

        public static void SetPropsPurchasedCount(int count)
        {
            PropsPurchasedCount = Math.Max(0, count);
            OnPropsPurchasedCountChanged?.Invoke();
        }

        public static void SetPendingBatchMerit(BigNumber amount)
        {
            PendingBatchMerit = amount;
            OnBatchMeritChanged?.Invoke();
        }

        public static void AddPendingBatchMerit(BigNumber amount)
        {
            if (amount.Mantissa == 0) return;
            PendingBatchMerit += amount;
            OnBatchMeritChanged?.Invoke();
        }

        /// <summary>일괄 수거 확정 → HUD 공덕으로 이동. multiplier=3 이면 광고/보상권 3배.</summary>
        public static bool TryClaimBatchMerit(int multiplier = 1)
        {
            if (!HasPendingBatchMerit) return false;
            if (multiplier < 1) multiplier = 1;
            var claim = PendingBatchMerit * (double)multiplier;
            PendingBatchMerit = BigNumber.Zero;
            OnBatchMeritChanged?.Invoke();
            AddMerit(claim);
            return true;
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
            PendingBatchMerit = BigNumber.Zero;
            PropsPurchasedCount = 0;
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
            OnBatchMeritChanged?.Invoke();
            OnPropsPurchasedCountChanged?.Invoke();
            OnYeopjeonChanged?.Invoke(Yeopjeon);
            OnHyangChanged?.Invoke(Hyang);
            OnPurifiedWaterChanged?.Invoke(PurifiedWater);
            OnYutTokenChanged?.Invoke(YutToken);
            GiftBundle.ResetFromSave(0, false, 0);
            ShopStock.ResetFromSave("", "", 0);
        }

        /// <summary>세이브 스냅샷으로 재화만 덮어쓴다 (공양물 인벤은 이후 패스).</summary>
        public static void ApplySaveSnapshot(BigNumber merit, BigNumber pendingBatch,
            int yeopjeon, int hyang, int purifiedWater, int yutToken, int yutTokenMax,
            int propsPurchasedCount = 0)
        {
            MeritPile = merit;
            PendingBatchMerit = pendingBatch;
            PropsPurchasedCount = Math.Max(0, propsPurchasedCount);
            Yeopjeon = yeopjeon;
            Hyang = hyang;
            PurifiedWater = purifiedWater;
            YutTokenMax = Mathf.Max(1, yutTokenMax);
            YutToken = Mathf.Clamp(yutToken, 0, YutTokenMax);

            OnMeritChanged?.Invoke(MeritPile);
            OnBatchMeritChanged?.Invoke();
            OnPropsPurchasedCountChanged?.Invoke();
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
