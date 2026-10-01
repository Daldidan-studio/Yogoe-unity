using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Yoegoe.Cooking;
using Yoegoe.Data;
using Yoegoe.Economy;
using Yoegoe.Save;
using Yoegoe.UI;

namespace Yoegoe.Tests.EditMode
{
    /// <summary>요리책(도감) 19장: 수집 60 = 음식 36 + 공양물 24, 처음 완성하면 발견, 나가리면 그 판 발견만 다시 잠금.</summary>
    public class CodexTests
    {
        GameObject ecoGO;
        GameEconomy eco;

        [SetUp]
        public void SetUp()
        {
            OfferingCatalog.Build(null);
            ecoGO = new GameObject("Eco");
            eco = ecoGO.AddComponent<GameEconomy>();
            eco.BecomeInstance();
            eco.ApplyStartingState(ScriptableObject.CreateInstance<StartingStateSettings>());
            for (int i = 0; i < (int)CookingIngredientId.Count; i++)
            {
                var id = (CookingIngredientId)i;
                eco.TrySpendMaterial(id, eco.GetMaterialCount(id));
            }
            CookingCodex.ResetFromSave(null);
        }

        [TearDown]
        public void TearDown()
        {
            CookingCodex.ResetFromSave(null);
            Object.DestroyImmediate(ecoGO);
        }

        [Test]
        public void Total_Is60_Foods36_Offerings24()
        {
            Assert.AreEqual(60, CookingCodex.Total);
            int foods = CookingCodex.ProductIds.Count(id =>
                CookingCodex.TryGetRecipe(id, out var r) && r.Kind == CookingResultKind.Food);
            Assert.AreEqual(36, foods);
        }

        [Test]
        public void Discover_GoldenCountsAsBase_AndSaveRoundTrips()
        {
            Assert.IsTrue(CookingCodex.Discover(OfferingCatalog.GoldenIdOf("bap")));
            Assert.IsTrue(CookingCodex.IsDiscovered("bap"));
            Assert.IsFalse(CookingCodex.Discover("bap"), "이미 발견");
            Assert.IsFalse(CookingCodex.Discover("not_a_recipe"));

            var saved = CookingCodex.CaptureToSave();
            CookingCodex.ResetFromSave(null);
            Assert.AreEqual(0, CookingCodex.DiscoveredCount);
            CookingCodex.ResetFromSave(saved);
            Assert.AreEqual(1, CookingCodex.DiscoveredCount);
        }

        [Test]
        public void OldSave_SeedsFromOwnedDishes()
        {
            eco.AddOffering(OfferingCatalog.Find("patteok"), 2);
            GameSaveBridge.RestoreCodex(null, eco);
            Assert.IsTrue(CookingCodex.IsDiscovered("patteok"));
            Assert.AreEqual(1, CookingCodex.DiscoveredCount);
        }

        [Test]
        public void ComboText_ListsEveryCombination()
        {
            Assert.AreEqual("쌀 + 팥", CookingCodex.ComboText("patteok"));
            StringAssert.Contains(" / ", CookingCodex.ComboText("gogijuk")); // 쌀+새고기 / 쌀+멧돼지고기
        }

        CookingSession StartRiceBeanBoard()
        {
            eco.AddMaterial(CookingIngredientId.Rice, 1);
            eco.AddMaterial(CookingIngredientId.RedBean, 1);
            var session = new CookingSession();
            session.Prepare(CookingCharmType.None);
            Assert.IsTrue(session.StartRound());
            return session;
        }

        static bool CompleteRiceBean(CookingSession session)
        {
            (int x, int y)? rice = null, bean = null;
            for (int y = 0; y < CookingSession.GridSize; y++)
            for (int x = 0; x < CookingSession.GridSize; x++)
            {
                if (session.Grid[x, y] == CookingIngredientId.Rice) rice = (x, y);
                if (session.Grid[x, y] == CookingIngredientId.RedBean) bean = (x, y);
            }
            if (!CookingRecipeCatalog.Adjacent(rice.Value.x, rice.Value.y, bean.Value.x, bean.Value.y, false)) return false;
            session.TryBeginPath(rice.Value.x, rice.Value.y);
            session.TryExtendPath(bean.Value.x, bean.Value.y);
            session.EndPath();
            return true;
        }

        [Test]
        public void Completing_Discovers_AndShowsInResult()
        {
            var session = StartRiceBeanBoard();
            Assume.That(CompleteRiceBean(session), "판 배치가 연결되지 않으면(드묾) 건너뜀");
            Assert.IsTrue(CookingCodex.IsDiscovered("patteok"));
            CollectionAssert.Contains(session.NewlyDiscovered, "patteok");
            StringAssert.Contains("새로 얻은 레시피", GongyangganScreen.BuildResultText(session));
        }

        [Test]
        public void Nagari_RelocksOnlyThisRoundsDiscoveries()
        {
            CookingCodex.Discover("kimchi"); // 예전에 발견
            eco.AddMaterial(CookingIngredientId.Fruit, 12); // 과실 2 = 곶감, 12개면 곶감을 만들어도 판이 남는다
            var session = new CookingSession();
            session.Prepare(CookingCharmType.None);
            Assert.IsTrue(session.StartRound());

            // 붙어 있는 과실 두 칸으로 곶감
            bool made = false;
            for (int y = 0; y < CookingSession.GridSize && !made; y++)
            for (int x = 0; x < CookingSession.GridSize - 1 && !made; x++)
            {
                if (session.Grid[x, y] != CookingIngredientId.Fruit || session.Grid[x + 1, y] != CookingIngredientId.Fruit) continue;
                session.TryBeginPath(x, y);
                session.TryExtendPath(x + 1, y);
                session.EndPath();
                made = true;
            }
            Assert.IsTrue(made, "과실 12개가 아래 세 줄에 깔리면 가로로 붙은 쌍이 반드시 있다");
            Assert.IsTrue(CookingCodex.IsDiscovered("gotgam"));
            Assert.IsFalse(session.Finished);

            session.CancelNagari();
            Assert.IsFalse(CookingCodex.IsDiscovered("gotgam"), "이번 판 발견은 다시 잠김");
            Assert.IsTrue(CookingCodex.IsDiscovered("kimchi"), "예전 발견은 유지");
        }

        [Test]
        public void MakeableLine_ShowsQuestionMarkForUnknown()
        {
            eco.AddMaterial(CookingIngredientId.Fruit, 2);
            var session = new CookingSession();
            session.Prepare(CookingCharmType.None);
            Assume.That(session.MakeableNow().Count > 0, "과실 2개가 붙어 있어야 함");
            Assert.AreEqual("만들 수 있는 요리: ?", GongyangganScreen.MakeableLine(session));
            CookingCodex.Discover("gotgam");
            Assert.AreEqual("만들 수 있는 요리: 곶감", GongyangganScreen.MakeableLine(session));
        }

        [Test]
        public void Descriptions_LoadById()
        {
            CodexDescriptions.LoadFromJson("{\"entries\":[{\"id\":\"bap\",\"description\":\"갓 지은 밥.\"}]}");
            Assert.AreEqual("갓 지은 밥.", CodexDescriptions.Get("bap"));
            Assert.AreEqual("", CodexDescriptions.Get("kimchi"));
            CodexDescriptions.LoadFromJson(Resources.Load<TextAsset>("codex")?.text);
        }

        [Test]
        public void SheetCsv_HasEveryCodexCell()
        {
            // Tools/sheets/codex.csv = 시트 codex 탭 사본 (npm run codex:push 로 생성) — 레시피가 바뀌면 다시 push
            var lines = System.IO.File.ReadAllLines("Tools/sheets/codex.csv");
            var ids = new System.Collections.Generic.HashSet<string>();
            for (int i = 1; i < lines.Length; i++)
            {
                var cols = lines[i].Split(',');
                if (cols.Length > 1) ids.Add(cols[1]);
            }
            foreach (var id in CookingCodex.ProductIds)
                Assert.IsTrue(ids.Contains(id), "시트에 없는 요리: " + id);
            for (int i = 0; i < (int)CookingIngredientId.Count; i++)
                Assert.IsTrue(ids.Contains(((CookingIngredientId)i).ToString()));
            Assert.AreEqual(CookingCodex.Total + (int)CookingIngredientId.Count + 2, ids.Count);
        }

        [Test]
        public void Prefabs_AreWired()
        {
            const string codexPath = "Assets/Prefabs/UI/CodexScreen.prefab";
            var codex = AssetDatabase.LoadAssetAtPath<GameObject>(codexPath);
            Assert.IsNotNull(codex);
            var so = new SerializedObject(codex.GetComponent<CodexScreen>());
            foreach (var f in new[] { "panel", "rateText", "closeButton", "content", "detailPanel", "detailName", "detailCombo", "detailDesc", "detailCloseButton" })
                Assert.IsNotNull(so.FindProperty(f).objectReferenceValue, f);
            StringAssert.Contains(AssetDatabase.AssetPathToGUID(codexPath), System.IO.File.ReadAllText("Assets/Scenes/Main.unity"));

            var g = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/GongyangganScreen.prefab");
            var root = g.transform.Find("Canvas_Gongyanggan/Root");
            Assert.IsNotNull(root.Find("CodexButton")?.GetComponent<UnityEngine.UI.Button>());
            Assert.IsNotNull(root.Find("Makeable")?.GetComponent<UnityEngine.UI.Text>());
            Assert.IsNotNull(root.Find("ResultPopup/Box/Rekindle")?.GetComponent<UnityEngine.UI.Button>());
            Assert.AreEqual("ResultPopup", root.GetChild(root.childCount - 1).name, "결과창이 맨 위에 그려져야 함");
        }
    }
}
