using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Yoegoe.Characters;
using Yoegoe.Data;
using Yoegoe.Economy;
using Yoegoe.UI;
using PressTarget = Yoegoe.Characters.MapPointerRouter.PressTarget;

namespace Yoegoe.Tests.EditMode
{
    /// <summary>기물 길게 누르기 → 개별 업그레이드 팝업 (8·14장), 맵 탭 우선순위 (Docs/07: 요괴 몸이 기물보다 먼저).</summary>
    public class PropUpgradePopupTests
    {
        const string PrefabPath = "Assets/Prefabs/UI/PropUpgradePopup.prefab";

        [Test]
        public void Prefab_HasAllReferencesWired()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(prefab, PrefabPath);
            var popup = prefab.GetComponent<PropUpgradePopup>();
            Assert.IsNotNull(popup);

            var so = new SerializedObject(popup);
            foreach (var field in new[] { "panel", "titleText", "effectText", "costText", "upgradeButton", "closeButton", "dimButton" })
                Assert.IsNotNull(so.FindProperty(field).objectReferenceValue, field);
        }

        [Test]
        public void MainScene_HasPopupInstance()
        {
            string guid = AssetDatabase.AssetPathToGUID(PrefabPath);
            string scene = System.IO.File.ReadAllText("Assets/Scenes/Main.unity");
            StringAssert.Contains(guid, scene);
        }

        [Test]
        public void CharacterOnProp_WinsOverEveryProp()
        {
            Assert.AreEqual(PressTarget.Character, MapPointerRouter.ClassifyPress(true, true, true, false));
            Assert.AreEqual(PressTarget.Character, MapPointerRouter.ClassifyPress(true, true, true, true));  // 화덕 위
            Assert.AreEqual(PressTarget.Character, MapPointerRouter.ClassifyPress(true, true, false, false)); // 자물쇠 근처
        }

        [Test]
        public void PropOnly_ClassifiesByKind()
        {
            Assert.AreEqual(PressTarget.Prop, MapPointerRouter.ClassifyPress(false, true, true, false));
            Assert.AreEqual(PressTarget.GongyangganProp, MapPointerRouter.ClassifyPress(false, true, true, true));
            Assert.AreEqual(PressTarget.LockedProp, MapPointerRouter.ClassifyPress(false, true, false, false));
            Assert.AreEqual(PressTarget.Empty, MapPointerRouter.ClassifyPress(false, false, false, false));
        }

        [Test]
        public void EffectText_ResourceProp_ShowsCapacityAndOverflow()
        {
            var c = new PropProduction.Config { Type = PropResourceType.Water, BaseCapacity = 6, CycleMinutes = 30 };
            // 보관은 10급마다 +1, 넘침 확률은 급 끝자리 × 10%
            Assert.AreEqual("9급 → 10급\n보관 6개 → 7개 · 넘침 확률 90% → 0%", PropUpgradePopup.EffectText(c, 9));
        }

        [Test]
        public void EffectText_MeritProp_ShowsPerMinute()
        {
            var c = new PropProduction.Config { Type = PropResourceType.Merit, MeritPerMinute = 10, LevelGrowth = 1.15 };
            string t = PropUpgradePopup.EffectText(c, 1);
            StringAssert.StartsWith("1급 → 2급\n분당 공덕 ", t);
        }
    }
}
