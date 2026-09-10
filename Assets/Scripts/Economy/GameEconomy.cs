using System;
using System.Collections.Generic;
using Yoegoe.Core;
using Yoegoe.Data;
using UnityEngine;

namespace Yoegoe.Economy
{
    /// <summary>
    /// 재화·공양물 인벤토리. 시작값은 StartingStateSettings.asset 에서 적용.
    /// Main이 부팅 시 GameObject 하나에 붙여서 만든다 (씬에 하나만 존재).
    /// </summary>
    public class GameEconomy : MonoBehaviour
    {
        public static GameEconomy Instance { get; private set; }

        private void Awake() => BecomeInstance();

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// 싱글턴 등록. EditMode 테스트는 AddComponent 시 Awake가 안 불리므로
        /// SetUp에서 이 메서드를 직접 호출한다.
        /// </summary>
        public void BecomeInstance()
        {
            if (Instance != null && Instance != this)
            {
                if (Application.isPlaying) Destroy(gameObject);
                else DestroyImmediate(gameObject);
                return;
            }
            Instance = this;
        }

        // ---------------- 공덕 (플레이어 지갑 — 수거된 공덕. 기물 더미는 PropSlot.PendingMerit) ----------------
        public BigNumber MeritPile { get; private set; } = BigNumber.Zero;
        public event Action<BigNumber> OnMeritChanged;

        /// <summary>
        /// 앱 재시작 일괄 수거 대기분 (7-2). 콜드스타트 시 기물 더미를 여기로 모은다.
        /// 백그라운드 복귀만으로는 채우지 않는다.
        /// </summary>
        public BigNumber PendingBatchMerit { get; private set; } = BigNumber.Zero;
        public event Action OnBatchMeritChanged;
        public bool HasPendingBatchMerit => PendingBatchMerit.Mantissa != 0;

        public void AddMerit(BigNumber amount)
        {
            MeritPile += amount;
            OnMeritChanged?.Invoke(MeritPile);
        }

        public bool TrySpendMerit(BigNumber amount)
        {
            if (amount.Mantissa < 0) return false;
            if (MeritPile < amount) return false;
            MeritPile -= amount;
            OnMeritChanged?.Invoke(MeritPile);
            return true;
        }

        /// <summary>플레이어가 구매로 지은 기물 수 (prebuilt 제외). 다음 구매 n = 이 값 + 1.</summary>
        public int PropsPurchasedCount { get; private set; }
        public event Action OnPropsPurchasedCountChanged;

        public void IncrementPropsPurchasedCount()
        {
            PropsPurchasedCount++;
            OnPropsPurchasedCountChanged?.Invoke();
        }

        public void SetPropsPurchasedCount(int count)
        {
            PropsPurchasedCount = Math.Max(0, count);
            OnPropsPurchasedCountChanged?.Invoke();
        }

        public void SetPendingBatchMerit(BigNumber amount)
        {
            PendingBatchMerit = amount;
            OnBatchMeritChanged?.Invoke();
        }

        public void AddPendingBatchMerit(BigNumber amount)
        {
            if (amount.Mantissa == 0) return;
            PendingBatchMerit += amount;
            OnBatchMeritChanged?.Invoke();
        }

        /// <summary>일괄 수거 확정 → HUD 공덕으로 이동. multiplier=3 이면 광고/보상권 3배.</summary>
        public bool TryClaimBatchMerit(int multiplier = 1)
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
        public int Yeopjeon { get; private set; }
        public event Action<int> OnYeopjeonChanged;
        public void AddYeopjeon(int amount) { Yeopjeon += amount; OnYeopjeonChanged?.Invoke(Yeopjeon); }
        public bool TrySpendYeopjeon(int amount)
        {
            if (amount < 0 || Yeopjeon < amount) return false;
            Yeopjeon -= amount;
            OnYeopjeonChanged?.Invoke(Yeopjeon);
            return true;
        }

        // ---------------- 향 ----------------
        public int Hyang { get; private set; }
        public event Action<int> OnHyangChanged;
        public void AddHyang(int amount) { Hyang += amount; OnHyangChanged?.Invoke(Hyang); }
        public bool TrySpendHyang(int amount)
        {
            if (amount < 0 || Hyang < amount) return false;
            Hyang -= amount;
            OnHyangChanged?.Invoke(Hyang);
            return true;
        }

        // ---------------- 정화수 ----------------
        public int PurifiedWater { get; private set; }
        public event Action<int> OnPurifiedWaterChanged;
        public void AddPurifiedWater(int amount) { PurifiedWater += amount; OnPurifiedWaterChanged?.Invoke(PurifiedWater); }
        public bool TrySpendPurifiedWater(int amount)
        {
            if (amount < 0 || PurifiedWater < amount) return false;
            PurifiedWater -= amount;
            OnPurifiedWaterChanged?.Invoke(PurifiedWater);
            return true;
        }

        // ---------------- 윷 토큰 ----------------
        public int YutTokenMax { get; private set; } = 5;
        public int YutToken { get; private set; }
        public event Action<int> OnYutTokenChanged;
        public void AddYutToken(int amount)
        {
            YutToken = Math.Min(YutTokenMax, YutToken + amount);
            OnYutTokenChanged?.Invoke(YutToken);
        }
        public bool TrySpendYutToken(int amount)
        {
            if (amount < 0 || YutToken < amount) return false;
            YutToken -= amount;
            OnYutTokenChanged?.Invoke(YutToken);
            return true;
        }

        // ---------------- 공양물 인벤토리 (정화수 제외) ----------------
        private readonly Dictionary<string, int> OfferingCounts = new Dictionary<string, int>();
        public event Action OnOfferingsChanged;

        public int GetOfferingCount(OfferingData offering)
        {
            if (offering == null) return 0;
            string key = OfferingKey(offering);
            return OfferingCounts.TryGetValue(key, out int n) ? n : 0;
        }

        public void AddOffering(OfferingData offering, int amount)
        {
            if (offering == null || amount == 0) return;
            string key = OfferingKey(offering);
            OfferingCounts.TryGetValue(key, out int cur);
            OfferingCounts[key] = Math.Max(0, cur + amount);
            OnOfferingsChanged?.Invoke();
        }

        public bool TrySpendOffering(OfferingData offering, int amount)
        {
            if (offering == null || amount < 0) return false;
            string key = OfferingKey(offering);
            OfferingCounts.TryGetValue(key, out int cur);
            if (cur < amount) return false;
            OfferingCounts[key] = cur - amount;
            OnOfferingsChanged?.Invoke();
            return true;
        }

        public int GetOfferingCount(string offeringId)
        {
            if (string.IsNullOrEmpty(offeringId)) return 0;
            return OfferingCounts.TryGetValue(offeringId, out int n) ? n : 0;
        }

        /// <summary>세이브용 스냅샷. count&gt;0 만 포함.</summary>
        public void CaptureOfferingCounts(List<KeyValuePair<string, int>> into)
        {
            if (into == null) return;
            into.Clear();
            foreach (var kv in OfferingCounts)
            {
                if (kv.Value > 0) into.Add(kv);
            }
        }

        /// <summary>세이브 복원 — 인벤을 통째로 교체한다.</summary>
        public void ReplaceOfferingCounts(IReadOnlyList<KeyValuePair<string, int>> entries)
        {
            OfferingCounts.Clear();
            if (entries != null)
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    var kv = entries[i];
                    if (string.IsNullOrEmpty(kv.Key) || kv.Value <= 0) continue;
                    OfferingCounts[kv.Key] = kv.Value;
                }
            }
            OnOfferingsChanged?.Invoke();
        }

        private static string OfferingKey(OfferingData offering)
        {
            return !string.IsNullOrEmpty(offering.offeringId) ? offering.offeringId : offering.name;
        }

        /// <summary>StartingStateSettings 기준으로 재화·인벤을 덮어쓴다. Main 부팅 시 1회 호출.</summary>
        public void ApplyStartingState(StartingStateSettings s)
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

        /// <summary>세이브 스냅샷으로 재화를 덮어쓴다. 공양물 인벤은 ReplaceOfferingCounts로 별도 복원.</summary>
        public void ApplySaveSnapshot(BigNumber merit, BigNumber pendingBatch,
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

        public void ResetForTesting()
        {
            ApplyStartingState(StartingStateSettings.Get());
        }
    }
}
