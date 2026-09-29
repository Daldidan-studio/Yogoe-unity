using System;
using System.Collections.Generic;
using Yoegoe.Core;
using Yoegoe.Data;
using UnityEngine;

namespace Yoegoe.Economy
{
    /// <summary>공양간 인벤토리 — 요리 재료·요리 부적 개수, 요리 완성품 적재. (GameEconomy 분할 — 본체는 GameEconomy.cs,
    /// 요리판 규칙·레시피는 Assets/Scripts/Cooking/)</summary>
    public partial class GameEconomy
    {
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

        /// <summary>세이브용: 재료 개수를 CookingIngredientId 순서 배열로.</summary>
        public int[] CaptureMaterialCounts()
        {
            var arr = new int[(int)Yoegoe.Cooking.CookingIngredientId.Count];
            for (int i = 0; i < arr.Length; i++)
                MaterialCounts.TryGetValue(i, out arr[i]);
            return arr;
        }

        /// <summary>세이브 로드: 재료 개수 통째로 교체.</summary>
        public void ReplaceMaterialCounts(int[] counts)
        {
            MaterialCounts.Clear();
            if (counts != null)
                for (int i = 0; i < counts.Length && i < (int)Yoegoe.Cooking.CookingIngredientId.Count; i++)
                    if (counts[i] > 0) MaterialCounts[i] = counts[i];
            OnMaterialsChanged?.Invoke();
        }

        // ---------------- 요리 부적 (사전 장착 5종 — 나가리 제외) ----------------
        readonly Dictionary<int, int> CharmCounts = new Dictionary<int, int>();
        public event Action OnCharmsChanged;

        public static bool IsInventoryCharm(Yoegoe.Cooking.CookingCharmType type) =>
            type == Yoegoe.Cooking.CookingCharmType.PlusFive
            || type == Yoegoe.Cooking.CookingCharmType.Diagonal
            || type == Yoegoe.Cooking.CookingCharmType.Clairvoyance
            || type == Yoegoe.Cooking.CookingCharmType.Recycle
            || type == Yoegoe.Cooking.CookingCharmType.Double;

        public int GetCharmCount(Yoegoe.Cooking.CookingCharmType type)
        {
            if (!IsInventoryCharm(type)) return 0;
            return CharmCounts.TryGetValue((int)type, out int n) ? n : 0;
        }

        public void AddCharm(Yoegoe.Cooking.CookingCharmType type, int amount)
        {
            if (!IsInventoryCharm(type) || amount == 0) return;
            int key = (int)type;
            CharmCounts.TryGetValue(key, out int cur);
            CharmCounts[key] = Math.Max(0, cur + amount);
            OnCharmsChanged?.Invoke();
        }

        public bool TrySpendCharm(Yoegoe.Cooking.CookingCharmType type, int amount = 1)
        {
            if (!IsInventoryCharm(type) || amount < 0) return false;
            int key = (int)type;
            CharmCounts.TryGetValue(key, out int cur);
            if (cur < amount) return false;
            CharmCounts[key] = cur - amount;
            OnCharmsChanged?.Invoke();
            return true;
        }

        /// <summary>세이브용: CookingCharmType 순서 배열(None=0 슬롯 포함).</summary>
        public int[] CaptureCharmCounts()
        {
            int len = (int)Yoegoe.Cooking.CookingCharmType.Cancel + 1;
            var arr = new int[len];
            for (int i = 0; i < len; i++)
                CharmCounts.TryGetValue(i, out arr[i]);
            return arr;
        }

        public void ReplaceCharmCounts(int[] counts)
        {
            CharmCounts.Clear();
            if (counts != null)
            {
                for (int i = 0; i < counts.Length; i++)
                {
                    if (counts[i] <= 0) continue;
                    var type = (Yoegoe.Cooking.CookingCharmType)i;
                    if (!IsInventoryCharm(type)) continue;
                    CharmCounts[i] = counts[i];
                }
            }
            OnCharmsChanged?.Invoke();
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
    }
}
