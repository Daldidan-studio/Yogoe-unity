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
            eco.AddMaterial(CookingIngredientId.Rice, 1);
            eco.AddMaterial(CookingIngredientId.RedBean, 1);
            eco.AddMaterial(CookingIngredientId.Fruit, 2); // 판이 바로 끝나지 않게 곶감 재료
            var session = new CookingSession();
            session.Prepare(CookingCharmType.None);
            Assert.IsTrue(session.StartRound());
            Assume.That(CompleteRiceBean(session), "판 배치가 연결되지 않으면(드묾) 건너뜀");
            Assume.That(!session.Finished, "남은 판이 막혔으면 건너뜀");

            session.CancelNagari();
            Assert.IsFalse(CookingCodex.IsDiscovered("patteok"), "이번 판 발견은 다시 잠김");
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
