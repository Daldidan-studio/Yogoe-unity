using UnityEngine;
using Yoegoe.Data;

namespace Yoegoe.Characters
{
    /// <summary>캐릭터 GameObject + CharacterAgent 생성 (Main·소환·세이브 복원 공용).</summary>
    public static class CharacterSpawner
    {
        public static CharacterAgent Spawn(
            CharacterData data,
            Vector3 pos,
            Color fallbackColor,
            Font bubbleFont)
        {
            if (data == null) return null;

            string name = string.IsNullOrEmpty(data.displayName) ? data.id.ToString() : data.displayName;
            bool hasArt = FirstSprite(data) != null;
            var scale = ArtScaleSettings.GetOrDefault();

            GameObject go = new GameObject("Char_" + name);
            go.transform.position = pos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = scale.SortOrderForCharacter(pos.y);
            go.transform.localScale = Vector3.one * scale.characterScale;

            if (hasArt)
            {
                sr.sprite = FirstSprite(data);
                sr.color = Color.white;
            }
            else
            {
                // 아트 미연결 시 색 구체 폴백
                Object.DestroyImmediate(sr);
                Object.DestroyImmediate(go);
                go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = "Char_" + name;
                go.transform.position = pos;
                go.transform.localScale = Vector3.one * 0.35f;
                var col = go.GetComponent<Collider>();
                if (col != null) Object.Destroy(col);
                ApplyUrpColor(go.GetComponent<Renderer>(), fallbackColor);
            }

            var agent = go.AddComponent<CharacterAgent>();
            agent.Data = data;
            agent.bubbleFont = bubbleFont;
            var boundSr = go.GetComponent<SpriteRenderer>();
            if (boundSr != null)
                agent.BindSpriteRenderer(boundSr);
            return agent;
        }

        public static Sprite FirstSprite(CharacterData data)
        {
            if (data == null) return null;
            if (data.walkDown != null) foreach (var s in data.walkDown) if (s != null) return s;
            if (data.walkLeft != null) foreach (var s in data.walkLeft) if (s != null) return s;
            if (data.walkRight != null) foreach (var s in data.walkRight) if (s != null) return s;
            if (data.walkUp != null) foreach (var s in data.walkUp) if (s != null) return s;
            if (data.idle != null) foreach (var s in data.idle) if (s != null) return s;
            return null;
        }

        private static void ApplyUrpColor(Renderer renderer, Color color)
        {
            if (renderer == null) return;
            var mat = CreateBaseMaterial();
            if (mat == null) return;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            renderer.material = mat;
        }

        private static Material CreateBaseMaterial()
        {
            var rp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
            if (rp != null && rp.defaultMaterial != null)
                return new Material(rp.defaultMaterial);

            var shader = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Standard")
                         ?? Shader.Find("Sprites/Default")
                         ?? Shader.Find("Unlit/Color");
            return shader != null ? new Material(shader) : null;
        }
    }
}
