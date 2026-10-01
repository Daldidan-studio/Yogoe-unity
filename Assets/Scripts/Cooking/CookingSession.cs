using System;
using System.Collections.Generic;
using Yoegoe.Core;
using Yoegoe.Data;
using UnityEngine;

namespace Yoegoe.Cooking
{
    /// <summary>한 판 공양간 세션. UI는 GongyangganScreen.</summary>
    public class CookingSession
    {
        public const int GridSize = 5;
        public const int EmptyCellCount = 5;
        public const float BaseSeconds = 15f;
        public const float AdExtendSeconds = 15f;
        /// <summary>판에 올라가는 재료 최대 개수(25칸 − 빈칸 5). 이보다 적으면 시작 전 확인 팝업.</summary>
        public const int FullBoardMaterials = GridSize * GridSize - EmptyCellCount;
        /// <summary>이보다 적으면 어떤 레시피도 못 만들어 시작 불가.</summary>
        public const int MinMaterialsToCook = 2;

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
        /// <summary>판에 올라간 재료. 시작(<see cref="StartRound"/>) 때 인벤에서 차감된다 — 미리보기 중엔 차감 전.</summary>
        public readonly List<CookingIngredientId> SpentOnBoard = new List<CookingIngredientId>();
        public int MaterialsOnBoard => SpentOnBoard.Count;
        public bool IsShortBoard => MaterialsOnBoard < FullBoardMaterials;
        public bool CanStart => !Running && !Finished && MaterialsOnBoard >= MinMaterialsToCook;

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

        /// <summary>한 번도 완성 못 하는 판이 나오지 않도록, 풀리는 배치가 나올 때까지 재시도한다.
        /// 여기서는 배치(미리보기)만 — 인벤 차감은 <see cref="StartRound"/>에서 1회.</summary>
        const int DealBoardMaxAttempts = 30;

        void DealBoard()
        {
            CookingIngredientId?[,] bestGrid = null;
            List<CookingIngredientId> bestSpent = null;

            for (int attempt = 0; attempt < DealBoardMaxAttempts; attempt++)
            {
                var pool = BuildMaterialPool();
                var grid = LayoutBoard(pool, UnityEngine.Random.Range);
                var spent = new List<CookingIngredientId>();
                foreach (var cell in grid)
                    if (cell.HasValue) spent.Add(cell.Value);

                bestGrid = grid;
                bestSpent = spent;
                if (CookingRecipeCatalog.AnyCompletable(grid, AllowDiagonal)) break;
                // 재료가 워낙 부족/편중돼 있으면 끝까지 안 풀릴 수 있음 — 그때는 마지막 시도 그대로 사용.
            }

            Grid = bestGrid;
            Locked = new bool[GridSize, GridSize];
            SpentOnBoard.Clear();
            SpentOnBoard.AddRange(bestSpent);
        }

        /// <summary>
        /// 판 배치 (19장): 재료(최대 20) + 빈칸 5를 <b>맨 아래 줄부터</b> 채운다. 재료가 모자라면 위쪽은 비고,
        /// 빈칸 5개는 채운 영역 안에서 랜덤. y=0이 맨 위, y=GridSize−1이 맨 아래.
        /// pool은 이미 섞인 순서대로 쓴다. randRange(min, maxExclusive).
        /// </summary>
        public static CookingIngredientId?[,] LayoutBoard(IReadOnlyList<CookingIngredientId> pool,
            Func<int, int, int> randRange)
        {
            var grid = new CookingIngredientId?[GridSize, GridSize];
            int materials = Math.Min(pool != null ? pool.Count : 0, FullBoardMaterials);
            if (materials == 0) return grid;
            int used = materials + EmptyCellCount;

            // 아래 줄부터 채울 칸 순서
            var cells = new List<(int x, int y)>(used);
            for (int y = GridSize - 1; y >= 0 && cells.Count < used; y--)
            for (int x = 0; x < GridSize && cells.Count < used; x++)
                cells.Add((x, y));

            var empties = new HashSet<int>();
            while (empties.Count < EmptyCellCount)
                empties.Add(randRange(0, used));

            int pi = 0;
            for (int i = 0; i < used; i++)
            {
                if (empties.Contains(i)) continue;
                var (x, y) = cells[i];
                grid[x, y] = pool[pi++];
            }
            return grid;
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
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
            return list;
        }

        /// <summary>판 시작 — 판에 올라간 재료를 이때 인벤에서 뺀다. 인벤이 그사이 줄어 모자라면 false(아무것도 안 뺌).</summary>
        public bool StartRound()
        {
            if (!CanStart) return false;
            var eco = Yoegoe.Economy.GameEconomy.Instance;
            if (eco != null)
            {
                var need = new Dictionary<CookingIngredientId, int>();
                foreach (var id in SpentOnBoard)
                    need[id] = need.TryGetValue(id, out int n) ? n + 1 : 1;
                foreach (var kv in need)
                    if (eco.GetMaterialCount(kv.Key) < kv.Value) return false;
                foreach (var kv in need)
                    eco.TrySpendMaterial(kv.Key, kv.Value);
            }
            Running = true;
            if (PreCharm == CookingCharmType.Clairvoyance)
            {
                ClairvoyanceActive = true;
                ClairvoyanceLeft = 1f;
            }
            Changed?.Invoke();
            return true;
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
