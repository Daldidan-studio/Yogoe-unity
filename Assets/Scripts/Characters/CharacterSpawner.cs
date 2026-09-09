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
            Font bubbleFont,
            bool forceNeokPlaceholder = false)
        {
            if (data == null) return null;

            string name = string.IsNullOrEmpty(data.displayName) ? data.id.ToString() : data.displayName;
            bool hasArt = FirstSprite(data) != null;
            bool usePlaceholder = forceNeokPlaceholder || !hasArt;
            var scale = ArtScaleSettings.GetOrDefault();

            GameObject go;
            CharacterAgent agent;

            if (!usePlaceholder)
            {
                go = new GameObject("Char_" + name);
                go.transform.position = pos;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = FirstSprite(data);
                sr.sortingOrder = scale.SortOrderForCharacter(pos.y);
                go.transform.localScale = Vector3.one * scale.characterScale;
                agent = go.AddComponent<CharacterAgent>();
            }
            else
            {
                // 넋: 혼 아트가 있어도 소환 직후엔 도깨비불 플레이스홀더
                go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = "Char_" + name;
                go.transform.position = pos;
                go.transform.localScale = Vector3.one * 0.35f;
                var col = go.GetComponent<Collider>();
                if (col != null) Object.Destroy(col);
                ApplyUrpColor(go.GetComponent<Renderer>(), fallbackColor);
                agent = go.AddComponent<CharacterAgent>();
            }

            agent.Data = data;
            agent.bubbleFont = bubbleFont;
            return agent;
        }

        /// <summary>넋 구체 → 혼 스프라이트 렌더러로 교체.</summary>
        public static void EnsureHonVisual(CharacterAgent agent)
        {
            if (agent == null || agent.Data == null) return;
            var sprite = FirstSprite(agent.Data);
            if (sprite == null)
            {
                Debug.LogWarning("[CharacterSpawner] 혼 스프라이트가 없어 비주얼 교체를 건너뜁니다: " +
                                 agent.Data.displayName);
                return;
            }

            var go = agent.gameObject;
            if (go == null) return;
            var scale = ArtScaleSettings.GetOrDefault();

            // Destroy()는 프레임 끝에 지워져서 같은 프레임 AddComponent가 실패/null 될 수 있음
            var meshRenderer = go.GetComponent<MeshRenderer>();
            var meshFilter = go.GetComponent<MeshFilter>();
            if (meshRenderer != null) Object.DestroyImmediate(meshRenderer);
            if (meshFilter != null) Object.DestroyImmediate(meshFilter);

            var sr = go.GetComponent<SpriteRenderer>();
            if (sr == null) sr = go.AddComponent<SpriteRenderer>();
            if (sr == null)
            {
                var child = new GameObject("HonSprite");
                child.transform.SetParent(go.transform, false);
                sr = child.AddComponent<SpriteRenderer>();
            }
            if (sr == null) return;

            sr.sprite = sprite;
            sr.sortingOrder = scale.SortOrderForCharacter(go.transform.position.y);
            go.transform.localScale = Vector3.one * scale.characterScale;

            agent.BindSpriteRenderer(sr);
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
