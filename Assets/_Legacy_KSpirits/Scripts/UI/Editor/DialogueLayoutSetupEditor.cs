using KSpirits.Core;
using KSpirits.Data;
using KSpirits.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace KSpirits.UI.Editor
{
    public static class DialogueLayoutSetupEditor
    {
        const string CatalogPath = "Assets/Resources/Settings/DialogueLayoutCatalog.asset";

        [MenuItem("KSpirits/Add Dialogue Layout Anchors")]
        public static void AddDialogueLayoutAnchors()
        {
            var ui = Object.FindFirstObjectByType<ScrollScreenUI>();
            if (ui == null)
            {
                EditorUtility.DisplayDialog("KSpirits", "ScrollScreenUI를 찾을 수 없습니다.", "OK");
                return;
            }

            EnsureCatalogAsset();
            EnsureAnchors(ui);
            EnsureManager(ui);

            EditorSceneManager.MarkSceneDirty(ui.gameObject.scene);
            Selection.activeGameObject = ui.transform.Find("DialogueAnchors")?.gameObject ?? ui.gameObject;

            EditorUtility.DisplayDialog("KSpirits",
                "DialogueAnchors + DialogueLayoutManager 적용 완료.\n\n" +
                "Hierarchy: ScrollScreenUI → DialogueAnchors\n" +
                "슬롯 위치를 Scene에서 드래그해 조정하세요.",
                "OK");
        }

        public static void EnsureOnScrollScreenUI(ScrollScreenUI ui)
        {
            EnsureCatalogAsset();
            EnsureAnchors(ui);
            EnsureManager(ui);
        }

        static void EnsureAnchors(ScrollScreenUI ui)
        {
            if (ui.transform.Find("DialogueAnchors") != null)
                return;

            ScrollScreenUIBuilder.BuildDialogueAnchorsPublic(ui.transform);
        }

        static void EnsureManager(ScrollScreenUI ui)
        {
            var manager = ui.GetComponent<DialogueLayoutManager>();
            if (manager == null)
                manager = ui.gameObject.AddComponent<DialogueLayoutManager>();

            var catalog = AssetDatabase.LoadAssetAtPath<DialogueLayoutCatalog>(CatalogPath);
            if (catalog != null)
            {
                var so = new SerializedObject(manager);
                so.FindProperty("_catalog").objectReferenceValue = catalog;
                so.FindProperty("_anchorRoot").objectReferenceValue = ui.transform.Find("DialogueAnchors");
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            manager.CacheAnchors();
            EditorUtility.SetDirty(ui.gameObject);
        }

        static void EnsureCatalogAsset()
        {
            if (AssetDatabase.LoadAssetAtPath<DialogueLayoutCatalog>(CatalogPath) != null)
                return;

            System.IO.Directory.CreateDirectory("Assets/Resources/Settings");

            var catalog = ScriptableObject.CreateInstance<DialogueLayoutCatalog>();
            catalog.defaultLayout = DialogueLayoutId.BottomWide;
            catalog.narrationLayout = DialogueLayoutId.TopNarration;
            catalog.oktoSpeakerLayout = DialogueLayoutId.NearYokai;
            catalog.imugiSpeakerLayout = DialogueLayoutId.AboveMortar;
            catalog.sectionRules = new[]
            {
                new DialogueLayoutCatalog.SectionRule
                    { sectionId = OktoDialogueSection.MemoryMoon, layout = DialogueLayoutId.AboveMortar },
                new DialogueLayoutCatalog.SectionRule
                    { sectionId = OktoDialogueSection.MemoryEarth, layout = DialogueLayoutId.AboveMortar },
                new DialogueLayoutCatalog.SectionRule
                    { sectionId = OktoDialogueSection.MemoryShop, layout = DialogueLayoutId.AboveMortar },
                new DialogueLayoutCatalog.SectionRule
                    { sectionId = OktoDialogueSection.TrainingIntro, layout = DialogueLayoutId.BottomWide },
                new DialogueLayoutCatalog.SectionRule
                    { sectionId = OktoDialogueSection.ImugiRestore, layout = DialogueLayoutId.AboveMortar },
            };

            AssetDatabase.CreateAsset(catalog, CatalogPath);
            AssetDatabase.SaveAssets();
        }
    }
}
