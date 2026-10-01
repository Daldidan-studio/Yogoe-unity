using System;
using System.Collections.Generic;
using Yoegoe.Core;
using Yoegoe.Data;
using UnityEngine;

namespace Yoegoe.Cooking
{
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
            Food("gogijuk", "고기죽", CookingIngredientId.Rice, CookingIngredientId.Bird);
            Food("gogijuk", "고기죽", CookingIngredientId.Rice, CookingIngredientId.Boar);
            Food("juak", "주악", CookingIngredientId.Rice, CookingIngredientId.Oil);
            Food("sujeonggwa", "수정과", CookingIngredientId.Fruit, CookingIngredientId.Herb);
            Food("dasik", "다식", CookingIngredientId.Fruit, CookingIngredientId.Honey);
            Food("sanjeok", "산적", CookingIngredientId.Namul, CookingIngredientId.Boar);
            Food("sanjeok", "산적", CookingIngredientId.Namul, CookingIngredientId.Bird);
            Food("hwajeon", "화전", CookingIngredientId.Namul, CookingIngredientId.Oil);
            Food("yukpo", "육포", CookingIngredientId.Honey, CookingIngredientId.Boar);
            Food("yukpo", "육포", CookingIngredientId.Honey, CookingIngredientId.Bird);
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
            // 생고기: 멧/새 아무 3개 — 조합 4종이지만 결과물 id는 하나
            Off("saenggogi", "생고기", CookingIngredientId.Boar, CookingIngredientId.Boar, CookingIngredientId.Boar);
            Off("saenggogi", "생고기", CookingIngredientId.Boar, CookingIngredientId.Boar, CookingIngredientId.Bird);
            Off("saenggogi", "생고기", CookingIngredientId.Boar, CookingIngredientId.Bird, CookingIngredientId.Bird);
            Off("saenggogi", "생고기", CookingIngredientId.Bird, CookingIngredientId.Bird, CookingIngredientId.Bird);
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

        /// <summary>조합별로 id가 갈려 있던 구세이브 id → 현재 결과물 id (예: saenggogi_bbb → saenggogi).</summary>
        public static string CanonicalProductId(string id)
        {
            if (string.IsNullOrEmpty(id)) return id;
            foreach (var prefix in LegacyVariantPrefixes)
                if (id.StartsWith(prefix + "_", StringComparison.Ordinal))
                    return prefix;
            return id;
        }

        static readonly string[] LegacyVariantPrefixes = { "saenggogi", "gogijuk", "sanjeok", "yukpo" };

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

        /// <summary>재료 개수(count)만으로 이 레시피를 만들 수 있는지 — 같은 재료 2개(곶감 등)는 2개 필요.</summary>
        public static bool CanMakeWith(in CookingRecipe recipe, Func<CookingIngredientId, int> count)
        {
            if (recipe.Ingredients == null || recipe.Ingredients.Length == 0 || count == null) return false;
            var ings = recipe.Ingredients; // 정렬돼 있어 같은 재료가 붙어 있다
            for (int i = 0; i < ings.Length;)
            {
                int j = i;
                while (j < ings.Length && ings[j] == ings[i]) j++;
                if (count(ings[i]) < j - i) return false;
                i = j;
            }
            return true;
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
