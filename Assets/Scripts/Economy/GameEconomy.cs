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

        /// <summary>일괄 수거 확정 → HUD 공덕으로 이동. multiplier=2 이면 광고/보상권 2배.</summary>
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
        /// <summary>기획 2·10장: 30분마다 1개 충전, 최대치에서는 카운트다운 없음(대기 없이 그대로 유지).</summary>
        public static readonly TimeSpan YutTokenRegenInterval = TimeSpan.FromMinutes(30);

        public int YutTokenMax { get; private set; } = 5;
        public int YutToken { get; private set; }
        /// <summary>다음 충전 예정 UTC ticks. 0이면 "충전 대기 없음"(가득 찼거나 아직 시작 안 함).</summary>
        public long YutTokenRegenNextUtcTicks { get; private set; }
        public event Action<int> OnYutTokenChanged;

        public void AddYutToken(int amount)
        {
            YutToken = Math.Min(YutTokenMax, YutToken + amount);
            if (YutToken >= YutTokenMax) YutTokenRegenNextUtcTicks = 0;
            OnYutTokenChanged?.Invoke(YutToken);
        }

        /// <summary>윷놀이 보물상자 전용 — 평소 상한(YutTokenMax)을 넘어 hardCap까지 쌓을 수 있다.</summary>
        public void AddYutTokenOverflow(int amount, int hardCap)
        {
            int cap = Math.Max(YutTokenMax, hardCap);
            YutToken = Math.Min(cap, YutToken + amount);
            if (YutToken >= YutTokenMax) YutTokenRegenNextUtcTicks = 0;
            OnYutTokenChanged?.Invoke(YutToken);
        }

        public bool TrySpendYutToken(int amount)
        {
            if (amount < 0 || YutToken < amount) return false;
            bool wasFull = YutToken >= YutTokenMax;
            YutToken -= amount;
            if (wasFull && YutToken < YutTokenMax)
                YutTokenRegenNextUtcTicks = DateTime.UtcNow.Add(YutTokenRegenInterval).Ticks;
            OnYutTokenChanged?.Invoke(YutToken);
            return true;
        }

        /// <summary>
        /// 벽시계 기준 30분마다 1개 충전 (기획 2·10장). Main의 Update/포그라운드 복귀 훅에서 호출한다 —
        /// 온라인 중에도, 백그라운드에 있다 돌아왔을 때도 이 한 곳만 거치면 된다.
        /// </summary>
        public void EnsureYutTokenFresh(DateTime utcNow)
        {
            if (YutToken >= YutTokenMax)
            {
                YutTokenRegenNextUtcTicks = 0;
                return;
            }

            if (YutTokenRegenNextUtcTicks <= 0)
            {
                YutTokenRegenNextUtcTicks = utcNow.Add(YutTokenRegenInterval).Ticks;
                return;
            }

            bool changed = false;
            while (YutToken < YutTokenMax && utcNow.Ticks >= YutTokenRegenNextUtcTicks)
            {
                YutToken++;
                changed = true;
                YutTokenRegenNextUtcTicks += YutTokenRegenInterval.Ticks;
            }

            if (YutToken >= YutTokenMax) YutTokenRegenNextUtcTicks = 0;
            if (changed) OnYutTokenChanged?.Invoke(YutToken);
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

        // ---------------- 요리 재료 (공양간) ----------------
        readonly Dictionary<int, int> MaterialCounts = new Dictionary<int, int>();
        public event Action OnMaterialsChanged;

        public int GetMaterialCount(Yoegoe.Cooking.CookingIngredientId id)
        {
            return MaterialCounts.TryGetValue((int)id, out int n) ? n : 0;
        }

        public void AddMaterial(Yoegoe.Cooking.CookingIngredientId id, int amount)
        {
            if (amount == 0) return;
            int key = (int)id;
            MaterialCounts.TryGetValue(key, out int cur);
            MaterialCounts[key] = Math.Max(0, cur + amount);
            OnMaterialsChanged?.Invoke();
        }

        public bool TrySpendMaterial(Yoegoe.Cooking.CookingIngredientId id, int amount)
        {
            if (amount < 0) return false;
            int key = (int)id;
            MaterialCounts.TryGetValue(key, out int cur);
            if (cur < amount) return false;
            MaterialCounts[key] = cur - amount;
            OnMaterialsChanged?.Invoke();
            return true;
        }

        /// <summary>요리 완성품을 공양물/음식 인벤에 id로 적재 (에셋 없어도 카운트 유지).</summary>
        public void AddCookingProduct(string productId, string displayName,
            Yoegoe.Cooking.CookingResultKind kind, int amount)
        {
            if (string.IsNullOrEmpty(productId) || amount <= 0) return;
            OfferingCounts.TryGetValue(productId, out int cur);
            OfferingCounts[productId] = cur + amount;
            OnOfferingsChanged?.Invoke();
            Debug.Log($"[GameEconomy] 요리 획득 {displayName} x{amount} ({kind}) id={productId}");
        }

        public void SeedStartingMaterials(int each = 5)
        {
            MaterialCounts.Clear();
            for (int i = 0; i < (int)Yoegoe.Cooking.CookingIngredientId.Count; i++)
            {
                var id = (Yoegoe.Cooking.CookingIngredientId)i;
                if (id == Yoegoe.Cooking.CookingIngredientId.Water) continue; // 물은 옹달샘
                MaterialCounts[i] = each;
            }
            OnMaterialsChanged?.Invoke();
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
            YutTokenRegenNextUtcTicks = YutToken < YutTokenMax
                ? DateTime.UtcNow.Add(YutTokenRegenInterval).Ticks
                : 0;

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

            SeedStartingMaterials(5);

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
            int propsPurchasedCount = 0, long yutTokenRegenNextUtcTicks = 0)
        {
            MeritPile = merit;
            PendingBatchMerit = pendingBatch;
            PropsPurchasedCount = Math.Max(0, propsPurchasedCount);
            Yeopjeon = yeopjeon;
            Hyang = hyang;
            PurifiedWater = purifiedWater;
            YutTokenMax = Mathf.Max(1, yutTokenMax);
            YutToken = Mathf.Clamp(yutToken, 0, YutTokenMax);
            // 0(구세이브·미기록)이면 EnsureYutTokenFresh가 다음 틱에 알아서 새 카운트다운을 시작한다.
            YutTokenRegenNextUtcTicks = yutTokenRegenNextUtcTicks;

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

namespace Yoegoe.Cooking
{
    /// <summary>재료 13종 (채집6·사냥6·물). Docs/00 부록 A.</summary>
    public enum CookingIngredientId
    {
        Water = 0,   // 물
        Chili,       // 고추
        Rice,        // 쌀
        RedBean,     // 팥
        Fruit,       // 과실
        Namul,       // 산나물
        Herb,        // 약재
        Honey,       // 꿀
        Boar,        // 멧돼지고기
        Bird,        // 새고기
        Fish,        // 물고기
        Egg,         // 새알
        Oil,         // 기름
        Count
    }

    public enum CookingCharmType
    {
        None = 0,
        PlusFive,    // +5초
        Diagonal,    // 대각선
        Clairvoyance,// 천리안
        Recycle,     // 회수
        Double,      // 몰빵
        Cancel       // 나가리 (게임 중)
    }

    public enum CookingResultKind
    {
        Food,
        Offering
    }
}

namespace Yoegoe.Cooking
{
    public readonly struct CookingRecipe
    {
        public readonly string Id;
        public readonly string DisplayName;
        public readonly CookingResultKind Kind;
        public readonly CookingIngredientId[] Ingredients; // sorted multiset

        public CookingRecipe(string id, string name, CookingResultKind kind, params CookingIngredientId[] ingredients)
        {
            Id = id;
            DisplayName = name;
            Kind = kind;
            Ingredients = ingredients ?? Array.Empty<CookingIngredientId>();
            Array.Sort(Ingredients);
        }
    }

    /// <summary>부록 A 레시피. 재료 순서는 무시(정렬 키 비교).</summary>
    public static class CookingRecipeCatalog
    {
        static readonly Dictionary<string, CookingRecipe> ByKey = new Dictionary<string, CookingRecipe>();
        static readonly List<CookingRecipe> All = new List<CookingRecipe>();
        static bool loaded;

        public static IReadOnlyList<CookingRecipe> Recipes
        {
            get { Ensure(); return All; }
        }

        public static void Ensure()
        {
            if (loaded) return;
            loaded = true;

            // —— 음식 36 (재료 2) ——
            Food("bap", "밥", CookingIngredientId.Water, CookingIngredientId.Rice);
            Food("kimchi", "김치", CookingIngredientId.Chili, CookingIngredientId.Namul);
            Food("sanchae", "산채", CookingIngredientId.Water, CookingIngredientId.Namul);
            Food("yakcha", "약차", CookingIngredientId.Water, CookingIngredientId.Herb);
            Food("kkulmul", "꿀물", CookingIngredientId.Water, CookingIngredientId.Honey);
            Food("suyuk", "수육", CookingIngredientId.Water, CookingIngredientId.Boar);
            Food("baeksuk", "백숙", CookingIngredientId.Water, CookingIngredientId.Bird);
            Food("saengsunjjim", "생선찜", CookingIngredientId.Water, CookingIngredientId.Fish);
            Food("salmedegg", "삶은 달걀", CookingIngredientId.Water, CookingIngredientId.Egg);
            Food("patjuk", "팥죽", CookingIngredientId.Water, CookingIngredientId.RedBean);
            Food("patteok", "팥떡", CookingIngredientId.Rice, CookingIngredientId.RedBean);
            Food("namulbap", "나물밥", CookingIngredientId.Rice, CookingIngredientId.Namul);
            Food("kkulteok", "꿀떡", CookingIngredientId.Rice, CookingIngredientId.Honey);
            Food("gogijuk_bird", "고기죽", CookingIngredientId.Rice, CookingIngredientId.Bird);
            Food("gogijuk_boar", "고기죽", CookingIngredientId.Rice, CookingIngredientId.Boar);
            Food("juak", "주악", CookingIngredientId.Rice, CookingIngredientId.Oil);
            Food("sujeonggwa", "수정과", CookingIngredientId.Fruit, CookingIngredientId.Herb);
            Food("dasik", "다식", CookingIngredientId.Fruit, CookingIngredientId.Honey);
            Food("sanjeok_boar", "산적", CookingIngredientId.Namul, CookingIngredientId.Boar);
            Food("sanjeok_bird", "산적", CookingIngredientId.Namul, CookingIngredientId.Bird);
            Food("hwajeon", "화전", CookingIngredientId.Namul, CookingIngredientId.Oil);
            Food("yukpo_boar", "육포", CookingIngredientId.Honey, CookingIngredientId.Boar);
            Food("yukpo_bird", "육포", CookingIngredientId.Honey, CookingIngredientId.Bird);
            Food("saengsungui", "생선구이", CookingIngredientId.Fish, CookingIngredientId.Oil);
            Food("dalgyalmar", "달걀말이", CookingIngredientId.Egg, CookingIngredientId.Oil);
            Food("saengsunjorim", "생선조림", CookingIngredientId.Chili, CookingIngredientId.Fish);
            Food("jeyuk", "제육볶음", CookingIngredientId.Chili, CookingIngredientId.Boar);
            Food("saegui", "새구이", CookingIngredientId.Bird, CookingIngredientId.Oil);
            Food("samgyeopsal", "삼겹살구이", CookingIngredientId.Boar, CookingIngredientId.Oil);
            Food("gotgam", "곶감", CookingIngredientId.Fruit, CookingIngredientId.Fruit);
            Food("saengchae", "생채", CookingIngredientId.Namul, CookingIngredientId.Namul);
            Food("yeot", "엿", CookingIngredientId.Honey, CookingIngredientId.Honey);
            Food("jeonggwa", "정과", CookingIngredientId.Herb, CookingIngredientId.Honey);
            Food("gwasilcha", "과실차", CookingIngredientId.Water, CookingIngredientId.Fruit);
            Food("dalgyaljuk", "달걀죽", CookingIngredientId.Rice, CookingIngredientId.Egg);
            Food("gyeranjjim", "계란찜", CookingIngredientId.Egg, CookingIngredientId.Egg);
            Food("donggeurangttaeng", "동그랑떙", CookingIngredientId.Boar, CookingIngredientId.Egg);
            Food("yanggaeng", "양갱", CookingIngredientId.Honey, CookingIngredientId.RedBean);
            Food("sseunyak", "쓴약", CookingIngredientId.Herb, CookingIngredientId.Herb);

            // —— 공양물 24 (재료 3) ——
            Off("yukjeon", "육전", CookingIngredientId.Boar, CookingIngredientId.Egg, CookingIngredientId.Oil);
            Off("samgyetang", "삼계탕", CookingIngredientId.Bird, CookingIngredientId.Herb, CookingIngredientId.Water);
            Off("baekseolgi", "백설기", CookingIngredientId.Rice, CookingIngredientId.Rice, CookingIngredientId.Water);
            Off("sinseollo", "신선로", CookingIngredientId.Boar, CookingIngredientId.Namul, CookingIngredientId.Water);
            Off("hanyak", "한약", CookingIngredientId.Herb, CookingIngredientId.Honey, CookingIngredientId.Herb);
            // 생고기: 멧/새 아무 3개 — 조합 4종
            Off("saenggogi_bbb", "생고기", CookingIngredientId.Boar, CookingIngredientId.Boar, CookingIngredientId.Boar);
            Off("saenggogi_bbd", "생고기", CookingIngredientId.Boar, CookingIngredientId.Boar, CookingIngredientId.Bird);
            Off("saenggogi_bdd", "생고기", CookingIngredientId.Boar, CookingIngredientId.Bird, CookingIngredientId.Bird);
            Off("saenggogi_ddd", "생고기", CookingIngredientId.Bird, CookingIngredientId.Bird, CookingIngredientId.Bird);
            Off("yakju", "약주", CookingIngredientId.Rice, CookingIngredientId.Herb, CookingIngredientId.Water);
            Off("sikhye", "식혜", CookingIngredientId.Rice, CookingIngredientId.Honey, CookingIngredientId.Water);
            Off("kkotmakgeolli", "꽃막걸리", CookingIngredientId.Rice, CookingIngredientId.Water, CookingIngredientId.Namul);
            Off("yakgwa", "약과", CookingIngredientId.Rice, CookingIngredientId.Honey, CookingIngredientId.Oil);
            Off("yaksik", "약식", CookingIngredientId.Rice, CookingIngredientId.Fruit, CookingIngredientId.Honey);
            Off("songpyeon", "송편", CookingIngredientId.Rice, CookingIngredientId.RedBean, CookingIngredientId.Water);
            Off("tteokguk", "떡국", CookingIngredientId.Rice, CookingIngredientId.Bird, CookingIngredientId.Water);
            Off("bibimbap", "비빔밥", CookingIngredientId.Rice, CookingIngredientId.Namul, CookingIngredientId.Egg);
            Off("gujeolpan", "구절판", CookingIngredientId.Namul, CookingIngredientId.Boar, CookingIngredientId.Egg);
            Off("galbijjim", "갈비찜", CookingIngredientId.Boar, CookingIngredientId.Fruit, CookingIngredientId.Water);
            Off("saegogigangjeong", "새고기강정", CookingIngredientId.Bird, CookingIngredientId.Chili, CookingIngredientId.Oil);
            Off("hwachae", "화채", CookingIngredientId.Fruit, CookingIngredientId.Honey, CookingIngredientId.Water);
            Off("yukgaejang", "육개장", CookingIngredientId.Boar, CookingIngredientId.Namul, CookingIngredientId.Chili);
            Off("maun_tteokbokki", "매운 떡볶음", CookingIngredientId.Rice, CookingIngredientId.Chili, CookingIngredientId.Honey);
            Off("maeuntang", "매운탕", CookingIngredientId.Fish, CookingIngredientId.Chili, CookingIngredientId.Water);
            Off("gochujangbulgogi", "고추장불고기", CookingIngredientId.Boar, CookingIngredientId.Chili, CookingIngredientId.Honey);
            Off("dakgalbi", "닭갈비", CookingIngredientId.Bird, CookingIngredientId.Chili, CookingIngredientId.Namul);
            Off("kimchijeon", "김치전", CookingIngredientId.Chili, CookingIngredientId.Namul, CookingIngredientId.Oil);
        }

        static void Food(string id, string name, params CookingIngredientId[] ings)
            => Add(new CookingRecipe(id, name, CookingResultKind.Food, ings));

        static void Off(string id, string name, params CookingIngredientId[] ings)
            => Add(new CookingRecipe(id, name, CookingResultKind.Offering, ings));

        static void Add(CookingRecipe r)
        {
            All.Add(r);
            string key = KeyOf(r.Ingredients);
            if (!ByKey.ContainsKey(key))
                ByKey[key] = r;
        }

        public static bool TryMatch(IList<CookingIngredientId> path, out CookingRecipe recipe)
        {
            Ensure();
            recipe = default;
            if (path == null || (path.Count != 2 && path.Count != 3)) return false;
            var arr = new CookingIngredientId[path.Count];
            for (int i = 0; i < path.Count; i++) arr[i] = path[i];
            Array.Sort(arr);
            return ByKey.TryGetValue(KeyOf(arr), out recipe);
        }

        /// <summary>현재 남은 칸으로 완성 가능한 레시피가 하나라도 있으면 true.</summary>
        public static bool AnyCompletable(CookingIngredientId?[,] grid, bool diagonal)
        {
            Ensure();
            int w = grid.GetLength(0);
            int h = grid.GetLength(1);
            var cells = new List<(int x, int y, CookingIngredientId id)>();
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                if (grid[x, y].HasValue)
                    cells.Add((x, y, grid[x, y].Value));
            }
            if (cells.Count < 2) return false;

            // 2~3칸 부분집합 + 연결성 검사 (작아서 전수 OK)
            for (int i = 0; i < cells.Count; i++)
            for (int j = i + 1; j < cells.Count; j++)
            {
                var two = new[] { cells[i].id, cells[j].id };
                Array.Sort(two);
                if (ByKey.ContainsKey(KeyOf(two))
                    && IsConnected(new[] { cells[i], cells[j] }, diagonal))
                    return true;

                for (int k = j + 1; k < cells.Count; k++)
                {
                    var three = new[] { cells[i].id, cells[j].id, cells[k].id };
                    Array.Sort(three);
                    if (ByKey.ContainsKey(KeyOf(three))
                        && IsConnected(new[] { cells[i], cells[j], cells[k] }, diagonal))
                        return true;
                }
            }
            return false;
        }

        static bool IsConnected((int x, int y, CookingIngredientId id)[] nodes, bool diagonal)
        {
            if (nodes.Length <= 1) return true;
            var seen = new bool[nodes.Length];
            var q = new Queue<int>();
            q.Enqueue(0);
            seen[0] = true;
            int found = 1;
            while (q.Count > 0)
            {
                int cur = q.Dequeue();
                for (int i = 0; i < nodes.Length; i++)
                {
                    if (seen[i]) continue;
                    if (!Adjacent(nodes[cur].x, nodes[cur].y, nodes[i].x, nodes[i].y, diagonal)) continue;
                    seen[i] = true;
                    found++;
                    q.Enqueue(i);
                }
            }
            return found == nodes.Length;
        }

        public static bool Adjacent(int x0, int y0, int x1, int y1, bool diagonal)
        {
            int dx = Math.Abs(x0 - x1);
            int dy = Math.Abs(y0 - y1);
            if (dx + dy == 0) return false;
            if (diagonal) return dx <= 1 && dy <= 1;
            return (dx + dy) == 1;
        }

        static string KeyOf(CookingIngredientId[] sorted)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < sorted.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append((int)sorted[i]);
            }
            return sb.ToString();
        }

        public static string DisplayName(CookingIngredientId id) => id switch
        {
            CookingIngredientId.Water => "물",
            CookingIngredientId.Chili => "고추",
            CookingIngredientId.Rice => "쌀",
            CookingIngredientId.RedBean => "팥",
            CookingIngredientId.Fruit => "과실",
            CookingIngredientId.Namul => "산나물",
            CookingIngredientId.Herb => "약재",
            CookingIngredientId.Honey => "꿀",
            CookingIngredientId.Boar => "멧돼지고기",
            CookingIngredientId.Bird => "새고기",
            CookingIngredientId.Fish => "물고기",
            CookingIngredientId.Egg => "새알",
            CookingIngredientId.Oil => "기름",
            _ => id.ToString()
        };
    }
}

namespace Yoegoe.Cooking
{
    /// <summary>한 판 공양간 세션. UI는 GongyangganScreen.</summary>
    public class CookingSession
    {
        public const int GridSize = 5;
        public const int EmptyCellCount = 5;
        public const float BaseSeconds = 15f;
        public const float AdExtendSeconds = 15f;

        public CookingIngredientId?[,] Grid { get; private set; }
        public bool[,] Locked { get; private set; }
        public float TimeLeft { get; private set; }
        public bool Running { get; private set; }
        public bool Finished { get; private set; }
        public CookingCharmType PreCharm { get; private set; }
        public bool AllowDiagonal => PreCharm == CookingCharmType.Diagonal;
        public bool AllowAdExtend =>
            PreCharm != CookingCharmType.Recycle && PreCharm != CookingCharmType.Double;
        public bool ShowNagari => Running && PreCharm == CookingCharmType.None;
        public bool ClairvoyanceActive { get; private set; }
        public float ClairvoyanceLeft { get; private set; }

        public readonly List<(CookingRecipe recipe, int count)> Results = new List<(CookingRecipe, int)>();
        public readonly List<CookingIngredientId> SpentOnBoard = new List<CookingIngredientId>();

        readonly List<(int x, int y)> path = new List<(int, int)>();
        public IReadOnlyList<(int x, int y)> Path => path;

        public event Action Changed;
        public event Action RoundEnded;

        public void Prepare(CookingCharmType charm)
        {
            PreCharm = charm;
            Finished = false;
            Running = false;
            Results.Clear();
            SpentOnBoard.Clear();
            path.Clear();
            ClairvoyanceActive = false;
            TimeLeft = ResolveLimit(charm);
            DealBoard();
            Changed?.Invoke();
        }

        static float ResolveLimit(CookingCharmType charm) => charm switch
        {
            CookingCharmType.PlusFive => BaseSeconds + 5f,
            CookingCharmType.Recycle => BaseSeconds - 4f,
            CookingCharmType.Double => 7f,
            _ => BaseSeconds
        };

        /// <summary>한 번도 완성 못 하는 판이 나오지 않도록, 풀리는 배치가 나올 때까지 재시도한다
        /// (인벤토리 자체는 다시 뽑지 않고 위치만 재배치 — 실제 재료 소모는 최종 배치 확정 후 1회).</summary>
        const int DealBoardMaxAttempts = 30;

        void DealBoard()
        {
            CookingIngredientId?[,] bestGrid = null;
            List<CookingIngredientId> bestSpent = null;

            for (int attempt = 0; attempt < DealBoardMaxAttempts; attempt++)
            {
                var grid = new CookingIngredientId?[GridSize, GridSize];
                var empties = new HashSet<int>();
                while (empties.Count < EmptyCellCount)
                    empties.Add(UnityEngine.Random.Range(0, GridSize * GridSize));

                var pool = BuildMaterialPool();
                var spent = new List<CookingIngredientId>();
                int pi = 0;
                for (int y = 0; y < GridSize; y++)
                for (int x = 0; x < GridSize; x++)
                {
                    int idx = y * GridSize + x;
                    if (empties.Contains(idx)) continue;
                    if (pi >= pool.Count) break;
                    var id = pool[pi++];
                    grid[x, y] = id;
                    spent.Add(id);
                }

                bestGrid = grid;
                bestSpent = spent;
                if (CookingRecipeCatalog.AnyCompletable(grid, AllowDiagonal)) break;
                // 재료가 워낙 부족/편중돼 있으면 끝까지 안 풀릴 수 있음 — 그때는 마지막 시도 그대로 사용.
            }

            Grid = bestGrid;
            Locked = new bool[GridSize, GridSize];
            SpentOnBoard.Clear();
            SpentOnBoard.AddRange(bestSpent);

            // 인벤에서 차감 (최종 확정된 배치 1회만)
            var eco = Yoegoe.Economy.GameEconomy.Instance;
            if (eco != null)
            {
                foreach (var id in SpentOnBoard)
                    eco.TrySpendMaterial(id, 1);
            }
        }

        List<CookingIngredientId> BuildMaterialPool()
        {
            var list = new List<CookingIngredientId>();
            var eco = Yoegoe.Economy.GameEconomy.Instance;
            if (eco != null)
            {
                for (int i = 0; i < (int)CookingIngredientId.Count; i++)
                {
                    var id = (CookingIngredientId)i;
                    int n = eco.GetMaterialCount(id);
                    for (int k = 0; k < n; k++) list.Add(id);
                }
            }
            // 테스트 폴백: 인벤 비면 랜덤 풀
            if (list.Count < 20)
            {
                while (list.Count < 40)
                    list.Add((CookingIngredientId)UnityEngine.Random.Range(0, (int)CookingIngredientId.Count));
            }

            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
            return list;
        }

        public void StartRound()
        {
            if (Finished || Running) return;
            Running = true;
            if (PreCharm == CookingCharmType.Clairvoyance)
            {
                ClairvoyanceActive = true;
                ClairvoyanceLeft = 1f;
            }
            Changed?.Invoke();
        }

        public void Tick(float dt)
        {
            if (!Running || Finished) return;
            if (ClairvoyanceActive)
            {
                ClairvoyanceLeft -= dt;
                if (ClairvoyanceLeft <= 0f) ClairvoyanceActive = false;
            }
            TimeLeft -= dt;
            if (TimeLeft <= 0f)
            {
                TimeLeft = 0f;
                EndRound(timeUp: true);
                return;
            }
            Changed?.Invoke();
        }

        public void ExtendByAd()
        {
            if (!AllowAdExtend || Finished) return;
            if (!Running && TimeLeft <= 0f) return;
            TimeLeft += AdExtendSeconds;
            if (!Running) Running = true;
            Finished = false;
            Changed?.Invoke();
        }

        public bool TryBeginPath(int x, int y)
        {
            if (!Running || Finished) return false;
            if (!InBounds(x, y) || Locked[x, y] || !Grid[x, y].HasValue) return false;
            path.Clear();
            path.Add((x, y));
            Changed?.Invoke();
            return true;
        }

        public bool TryExtendPath(int x, int y)
        {
            if (!Running || Finished || path.Count == 0) return false;
            if (!InBounds(x, y) || Locked[x, y] || !Grid[x, y].HasValue) return false;
            for (int i = 0; i < path.Count; i++)
                if (path[i].x == x && path[i].y == y) return false;
            var last = path[path.Count - 1];
            if (!CookingRecipeCatalog.Adjacent(last.x, last.y, x, y, AllowDiagonal)) return false;
            if (path.Count >= 3) return false;

            // 도달 가능성: 확장 후 완성 가능해야 함
            path.Add((x, y));
            if (!PathCanStillComplete())
            {
                path.RemoveAt(path.Count - 1);
                return false;
            }
            Changed?.Invoke();
            return true;
        }

        public void EndPath()
        {
            if (!Running || path.Count == 0) return;
            var ings = new List<CookingIngredientId>(path.Count);
            for (int i = 0; i < path.Count; i++)
            {
                var (px, py) = path[i];
                if (Grid[px, py].HasValue) ings.Add(Grid[px, py].Value);
            }

            if (CookingRecipeCatalog.TryMatch(ings, out var recipe))
            {
                int mult = PreCharm == CookingCharmType.Double ? 2 : 1;
                AddResult(recipe, mult);
                for (int i = 0; i < path.Count; i++)
                {
                    var (px, py) = path[i];
                    Grid[px, py] = null;
                    Locked[px, py] = true;
                }
                path.Clear();
                if (!CookingRecipeCatalog.AnyCompletable(Grid, AllowDiagonal))
                    EndRound(timeUp: false);
                else
                    Changed?.Invoke();
                return;
            }

            path.Clear();
            Changed?.Invoke();
        }

        bool PathCanStillComplete()
        {
            if (path.Count == 2 || path.Count == 3)
            {
                var ings = new List<CookingIngredientId>();
                for (int i = 0; i < path.Count; i++)
                    ings.Add(Grid[path[i].x, path[i].y].Value);
                if (CookingRecipeCatalog.TryMatch(ings, out _)) return true;
            }
            if (path.Count >= 3) return false;

            // 1칸: 이웃으로 레시피 확장 가능 여부
            var last = path[path.Count - 1];
            for (int y = 0; y < GridSize; y++)
            for (int x = 0; x < GridSize; x++)
            {
                if (Locked[x, y] || !Grid[x, y].HasValue) continue;
                bool onPath = false;
                for (int i = 0; i < path.Count; i++)
                    if (path[i].x == x && path[i].y == y) { onPath = true; break; }
                if (onPath) continue;
                if (!CookingRecipeCatalog.Adjacent(last.x, last.y, x, y, AllowDiagonal)) continue;

                var trial = new List<CookingIngredientId>();
                for (int i = 0; i < path.Count; i++)
                    trial.Add(Grid[path[i].x, path[i].y].Value);
                trial.Add(Grid[x, y].Value);
                if (CookingRecipeCatalog.TryMatch(trial, out _)) return true;

                // 2칸 경로면 한 칸 더 필요한 3재료 레시피도 허용
                if (path.Count == 1)
                {
                    // BFS one more step
                    for (int y2 = 0; y2 < GridSize; y2++)
                    for (int x2 = 0; x2 < GridSize; x2++)
                    {
                        if ((x2 == x && y2 == y) || Locked[x2, y2] || !Grid[x2, y2].HasValue) continue;
                        bool on = false;
                        for (int i = 0; i < path.Count; i++)
                            if (path[i].x == x2 && path[i].y == y2) { on = true; break; }
                        if (on) continue;
                        if (!CookingRecipeCatalog.Adjacent(x, y, x2, y2, AllowDiagonal)) continue;
                        var t3 = new List<CookingIngredientId>(trial) { Grid[x2, y2].Value };
                        if (CookingRecipeCatalog.TryMatch(t3, out _)) return true;
                    }
                }
            }
            return false;
        }

        void AddResult(CookingRecipe recipe, int count)
        {
            for (int i = 0; i < Results.Count; i++)
            {
                if (Results[i].recipe.Id == recipe.Id)
                {
                    Results[i] = (recipe, Results[i].count + count);
                    return;
                }
            }
            Results.Add((recipe, count));
        }

        public void CancelNagari()
        {
            if (!ShowNagari || Finished) return;
            // 결과 취소 + 재료 전량 반환
            Results.Clear();
            var eco = Yoegoe.Economy.GameEconomy.Instance;
            if (eco != null)
            {
                foreach (var id in SpentOnBoard)
                    eco.AddMaterial(id, 1);
            }
            SpentOnBoard.Clear();
            EndRound(timeUp: false, nagari: true);
        }

        void EndRound(bool timeUp, bool nagari = false)
        {
            if (Finished) return;
            Running = false;
            Finished = true;
            path.Clear();

            var eco = Yoegoe.Economy.GameEconomy.Instance;
            if (!nagari)
            {
                if (PreCharm == CookingCharmType.Recycle && eco != null)
                {
                    // 남은 재료 반환
                    for (int y = 0; y < GridSize; y++)
                    for (int x = 0; x < GridSize; x++)
                    {
                        if (Grid[x, y].HasValue)
                            eco.AddMaterial(Grid[x, y].Value, 1);
                    }
                }
                // 완성품 → 인벤 (음식/공양물 id)
                if (eco != null)
                {
                    foreach (var (recipe, count) in Results)
                        eco.AddCookingProduct(recipe.Id, recipe.DisplayName, recipe.Kind, count);
                }
            }

            Changed?.Invoke();
            RoundEnded?.Invoke();
        }

        static bool InBounds(int x, int y) =>
            x >= 0 && y >= 0 && x < GridSize && y < GridSize;
    }
}
