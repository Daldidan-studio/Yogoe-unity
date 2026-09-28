using System.IO;
using UnityEditor;
using UnityEngine;
using Yoegoe.Data;

namespace Yoegoe.Characters.EditorTools
{
    /// <summary>
    /// PropData → Assets/Prefabs/Props/*.prefab 생성, PropLayoutSettings.prefab 연결.
    /// 메뉴: Yoegoe/Bake Prop Prefabs
    /// </summary>
    public static class PropPrefabBaker
    {
        public const string PrefabFolder = "Assets/Prefabs/Props";
        const string LayoutPath = "Assets/Resources/PropLayoutSettings.asset";

        [MenuItem("Yoegoe/Bake Prop Prefabs")]
        public static void BakeFromMenu()
        {
            int n = BakeAll();
            Debug.Log($"[PropPrefabBaker] Prefab {n}개 준비, PropLayoutSettings 연결 완료.");
        }

        /// <summary>배치 모드: -executeMethod Yoegoe.Characters.EditorTools.PropPrefabBaker.BakeAllBatch</summary>
        public static void BakeAllBatch()
        {
            BakeAll();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        public static int BakeAll()
        {
            EnsureFolder("Assets/Prefabs");
            EnsureFolder(PrefabFolder);

            float propScale = ArtScaleSettings.GetOrDefault().propScale;
            var layout = AssetDatabase.LoadAssetAtPath<PropLayoutSettings>(LayoutPath);
            if (layout == null)
            {
                Debug.LogError($"[PropPrefabBaker] {LayoutPath} 없음.");
                return 0;
            }

            int count = 0;
            if (layout.placements == null) return 0;

            for (int i = 0; i < layout.placements.Length; i++)
            {
                var place = layout.placements[i];
                if (place?.data == null) continue;

                var slot = EnsurePrefab(place.data, propScale);
                if (slot == null) continue;
                place.prefab = slot;
                count++;
            }

            EditorUtility.SetDirty(layout);
            AssetDatabase.SaveAssets();
            return count;
        }

        static PropSlot EnsurePrefab(PropData data, float propScale)
        {
            string fileName = SanitizeFileName(data.name);
            string path = $"{PrefabFolder}/{fileName}.prefab";

            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
            {
                var existingSlot = existing.GetComponent<PropSlot>();
                if (existingSlot != null)
                {
                    // 이미 Prefab이 있으면 data 참조만 맞추고 비주얼은 건드리지 않음 (아트 수정 보존)
                    if (existingSlot.data != data)
                    {
                        existingSlot.data = data;
                        EditorUtility.SetDirty(existing);
                        PrefabUtility.SavePrefabAsset(existing);
                    }
                    return existingSlot;
                }
            }

            var go = new GameObject("Prop_" + (!string.IsNullOrEmpty(data.displayName) ? data.displayName : data.name));
            go.transform.localScale = Vector3.one * Mathf.Max(0.01f, propScale);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = data.icon;
            sr.color = Color.white;
            sr.sortingOrder = 0;

            var slot = go.AddComponent<PropSlot>();
            slot.data = data;

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);

            return prefab != null ? prefab.GetComponent<PropSlot>() : null;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        static string SanitizeFileName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Prop";
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }
    }
}
