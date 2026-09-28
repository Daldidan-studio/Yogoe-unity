using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using Yoegoe.Characters;
using Yoegoe.Data;
using Yoegoe.Debugging;

namespace Yoegoe.Bootstrap
{
    /// <summary>
    /// Main 씬 월드 조립: 카메라·라이트·맵·기물·캐릭터.
    /// Inspector 필드는 <see cref="Main"/>에 두고 여기로 넘긴다.
    /// </summary>
    public static class WorldAssembler
    {
        public struct Config
        {
            public ArtScaleSettings scale;
            public PropLayoutSettings propLayout;
            public Sprite overviewBackgroundSprite;
            public Sprite playfieldSprite;
            public Vector2 overviewOffset;
            public CharacterData oktoData;
            public CharacterData samjokOData;
            public Font hudFont;
        }

        public static void Build(Config cfg)
        {
            EnsureCamera(cfg.scale);
            EnsureLight();
            EnsureEventSystem();
            EnsureMapPointerRouter();
            CreateBackground(cfg);

            var propManagerGO = new GameObject("PropManager");
            propManagerGO.AddComponent<PropManager>();

            float mapScale = Mathf.Max(0.01f, cfg.scale.mapScale);
            SpawnPropsFromLayout(cfg, mapScale);

            // 캐릭터 좌표도 PropLayout처럼 mapScale=1 기준 → 월드로 변환
            CreateCharacter("옥토끼", MapToWorld(new Vector3(-1f, 0.5f, 0), mapScale),
                Color.white, cfg.oktoData, cfg.hudFont);
            CreateCharacter("삼족오", MapToWorld(new Vector3(0f, 0.5f, 0), mapScale),
                Color.black, cfg.samjokOData, cfg.hudFont);
            // 구미호: 잠금 슬롯(엽전 99) 해금 후 소환 — 시작 스폰 없음
        }

        /// <summary>
        /// PropLayoutSettings·시작 캐릭터 좌표는 mapScale=1(맵 로컬) 기준.
        /// 배경 Transform 배율과 같이 곱해 월드 좌표로 만든다.
        /// </summary>
        static Vector3 MapToWorld(Vector3 mapLocal, float mapScale) =>
            new Vector3(mapLocal.x * mapScale, mapLocal.y * mapScale, mapLocal.z);

        /// <summary>
        /// uGUI 버튼(슬롯 탭 등)이 반응하려면 EventSystem이 씬에 있어야 한다. 이 프로젝트는 새
        /// Input System만 쓰도록 설정돼 있어서(Project Settings) 구식 StandaloneInputModule 대신
        /// InputSystemUIInputModule을 붙인다.
        /// </summary>
        static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null) return;
            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<EventSystem>();
            esGo.AddComponent<InputSystemUIInputModule>();
        }

        static void EnsureCamera(ArtScaleSettings scale)
        {
            Camera cam;
            if (Camera.main != null)
            {
                cam = Camera.main;
            }
            else
            {
                var camGO = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = camGO.AddComponent<Camera>();
                cam.orthographic = true;
                cam.transform.position = new Vector3(0, 0, -10);
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.1f, 0.1f, 0.15f);
            }

            cam.orthographic = true;
            cam.orthographicSize = scale.cameraOrthoSize;
        }

        /// <summary>
        /// 맵 탭/드래그·캐릭터 드래그 단일 라우터. CreateBackground보다 먼저 붙여 두고,
        /// MapCameraDrag는 배경 생성 시 같은 카메라에 추가된다 (라우터가 Awake 이후 GetComponent).
        /// </summary>
        static void EnsureMapPointerRouter()
        {
            var cam = Camera.main;
            if (cam == null) return;

            if (cam.GetComponent<MapCameraDrag>() == null)
                cam.gameObject.AddComponent<MapCameraDrag>();

            var router = cam.GetComponent<MapPointerRouter>();
            if (router == null) router = cam.gameObject.AddComponent<MapPointerRouter>();
            router.targetCamera = cam;
            router.mapDrag = cam.GetComponent<MapCameraDrag>();
            router.propDropRadius = 0.15f;
            router.lockTapRadius = 0.28f;
        }

        static void EnsureLight()
        {
            if (Object.FindAnyObjectByType<Light>() != null) return;
            var lightGO = new GameObject("Directional Light");
            var light = lightGO.AddComponent<Light>();
            light.type = LightType.Directional;
            lightGO.transform.rotation = Quaternion.Euler(50, -30, 0);
        }

        /// <summary>
        /// 전체맵(섬) + 그 위 잔디 플레이필드.
        /// 걷기는 잔디 bounds만, 카메라 패닝은 전체맵 기준.
        /// </summary>
        static void CreateBackground(Config cfg)
        {
            float scale = Mathf.Max(0.01f, cfg.scale.mapScale);
            var cam = Camera.main;

            // overviewOffset도 mapScale=1 기준 보정값 → 월드로 스케일
            Vector2 overviewOffsetWorld = cfg.overviewOffset * scale;

            Sprite overview = cfg.overviewBackgroundSprite;
            if (overview != null)
            {
                var go = new GameObject("Background_Overview");
                go.transform.position = new Vector3(overviewOffsetWorld.x, overviewOffsetWorld.y, 1f);
                go.transform.localScale = new Vector3(scale, scale, 1f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = overview;
                sr.sortingOrder = cfg.scale.backgroundSort;
            }

            Sprite playfield = cfg.playfieldSprite;
            if (playfield == null) return;

            var fieldGO = new GameObject("Background_Playfield");
            fieldGO.transform.position = new Vector3(0f, 0f, 0.9f);
            fieldGO.transform.localScale = new Vector3(scale, scale, 1f);
            var fieldSr = fieldGO.AddComponent<SpriteRenderer>();
            fieldSr.sprite = playfield;
            fieldSr.sortingOrder = cfg.scale.backgroundSort + 1;

            var walkCol = BuildPlayfieldWalkCollider(fieldGO, playfield);
            MapBounds.SetWalkArea(walkCol);
            if (walkCol == null)
            {
                if (!TryGetSpriteWorldAabb(playfield, scale, out Vector2 walkMin, out Vector2 walkMax))
                    return;
                const float margin = 0.35f;
                MapBounds.SetBounds(
                    new Vector2(walkMin.x + margin, walkMin.y + margin),
                    new Vector2(walkMax.x - margin, walkMax.y - margin));
            }

            float fieldW = playfield.bounds.size.x * scale;
            float fieldH = playfield.bounds.size.y * scale;

            if (cam == null || !cam.orthographic) return;

            float panW = fieldW;
            float panH = fieldH;
            if (overview != null)
            {
                panW = overview.bounds.size.x * scale;
                panH = overview.bounds.size.y * scale;
            }

            float camHeight = cam.orthographicSize * 2f;
            float camWidth = camHeight * cam.aspect;
            var drag = cam.GetComponent<MapCameraDrag>();
            if (drag == null) drag = cam.gameObject.AddComponent<MapCameraDrag>();

            float halfExtraW = Mathf.Max(0f, panW / 2f - camWidth / 2f);
            float halfExtraH = Mathf.Max(0f, panH / 2f - camHeight / 2f);

            if (halfExtraW <= 0.01f && halfExtraH <= 0.01f)
            {
                float orthoByW = (panW / 1.2f) / (2f * Mathf.Max(0.01f, cam.aspect));
                float orthoByH = (panH / 1.2f) / 2f;
                float newOrtho = Mathf.Min(orthoByW, orthoByH);
                if (newOrtho > 0.1f && newOrtho < cam.orthographicSize)
                {
                    cam.orthographicSize = newOrtho;
                    camHeight = cam.orthographicSize * 2f;
                    camWidth = camHeight * cam.aspect;
                    halfExtraW = Mathf.Max(0f, panW / 2f - camWidth / 2f);
                    halfExtraH = Mathf.Max(0f, panH / 2f - camHeight / 2f);
                }
            }

            Vector2 panCenter = overview != null ? overviewOffsetWorld : Vector2.zero;
            drag.SetContentRect(panCenter, panW * 0.5f, panH * 0.5f);
            drag.SetOrthoLimits(1.4f, Mathf.Max(cam.orthographicSize * 1.05f, cam.orthographicSize));

            var router = cam.GetComponent<MapPointerRouter>();
            if (router != null) router.mapDrag = drag;
        }

        static Collider2D BuildPlayfieldWalkCollider(GameObject fieldGO, Sprite sprite)
        {
            if (fieldGO == null || sprite == null) return null;

            int shapeCount = sprite.GetPhysicsShapeCount();
            if (shapeCount <= 0) return null;

            var col = fieldGO.AddComponent<PolygonCollider2D>();
            col.isTrigger = true;
            col.pathCount = shapeCount;

            var path = new List<Vector2>(64);
            for (int i = 0; i < shapeCount; i++)
            {
                path.Clear();
                sprite.GetPhysicsShape(i, path);
                col.SetPath(i, path);
            }
            return col;
        }

        static bool TryGetSpriteWorldAabb(Sprite sprite, float scale, out Vector2 min, out Vector2 max)
        {
            min = default;
            max = default;
            if (sprite == null || scale <= 0f) return false;

            var verts = sprite.vertices;
            if (verts != null && verts.Length > 0)
            {
                float minX = float.PositiveInfinity, minY = float.PositiveInfinity;
                float maxX = float.NegativeInfinity, maxY = float.NegativeInfinity;
                for (int i = 0; i < verts.Length; i++)
                {
                    Vector2 v = verts[i] * scale;
                    if (v.x < minX) minX = v.x;
                    if (v.y < minY) minY = v.y;
                    if (v.x > maxX) maxX = v.x;
                    if (v.y > maxY) maxY = v.y;
                }
                if (minX < maxX && minY < maxY)
                {
                    min = new Vector2(minX, minY);
                    max = new Vector2(maxX, maxY);
                    return true;
                }
            }

            Bounds b = sprite.bounds;
            float hx = b.extents.x * scale;
            float hy = b.extents.y * scale;
            if (hx <= 0f || hy <= 0f) return false;
            min = new Vector2(-hx, -hy);
            max = new Vector2(hx, hy);
            return true;
        }

        // URP: CreatePrimitive 기본 머티리얼은 Built-in이라 핑크. 파이프라인 defaultMaterial 복제.
        static Material CreateBaseMaterial()
        {
            var rp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
            if (rp != null && rp.defaultMaterial != null)
                return new Material(rp.defaultMaterial);

            var shader = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Standard")
                         ?? Shader.Find("Sprites/Default")
                         ?? Shader.Find("Unlit/Color");

            if (shader == null)
            {
                Debug.LogError("[WorldAssembler] 사용 가능한 셰이더를 하나도 찾지 못했습니다. " +
                                "머티리얼 없이 렌더러 기본값으로 진행합니다.");
                return null;
            }

            return new Material(shader);
        }

        static void ApplyUrpColor(Renderer renderer, Color color)
        {
            if (renderer == null) return;
            var mat = CreateBaseMaterial();
            if (mat == null) return;
            mat.color = color;
            renderer.material = mat;
        }

        static void SpawnPropsFromLayout(Config cfg, float mapScale)
        {
            var layout = cfg.propLayout != null ? cfg.propLayout : PropLayoutSettings.Get();
            if (layout?.placements == null || layout.placements.Length == 0)
            {
                Debug.LogError("[WorldAssembler] PropLayoutSettings 배치가 비어 있습니다. " +
                               "Assets/Resources/PropLayoutSettings.asset 을 확인하세요.");
                return;
            }

            for (int i = 0; i < layout.placements.Length; i++)
            {
                var place = layout.placements[i];
                if (place?.data == null)
                {
                    Debug.LogWarning($"[WorldAssembler] PropLayoutSettings.placements[{i}] 에 PropData 가 없습니다.");
                    continue;
                }
                CreateProp(place.data, MapToWorld(place.position, mapScale), place.fallbackColor, cfg.scale);
            }

            if (MeritWillow.Instance == null)
            {
                var willowPos = MapToWorld(layout.willowPosition, mapScale);
                MeritWillow.Create(willowPos, cfg.scale.SortOrderForProp(willowPos.y));
            }
        }

        static void CreateProp(PropData data, Vector3 pos, Color color, ArtScaleSettings scale)
        {
            PropCatalog.ApplyTo(data);
            string name = !string.IsNullOrEmpty(data.displayName) ? data.displayName
                : (!string.IsNullOrEmpty(data.propId) ? data.propId : "Prop");
            Sprite sprite = data.icon;
            GameObject go;

            if (sprite != null)
            {
                go = new GameObject("Prop_" + name);
                go.transform.position = pos;
                go.transform.localScale = Vector3.one * scale.propScale;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sortingOrder = scale.SortOrderForProp(pos.y);
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "Prop_" + name;
                go.transform.position = pos;
                go.transform.localScale = Vector3.one * 0.8f;
                var col = go.GetComponent<Collider>();
                if (col != null) Object.Destroy(col);

                ApplyUrpColor(go.GetComponent<Renderer>(), color);
            }

            var slot = go.AddComponent<PropSlot>();
            slot.data = data;
            slot.SetBuiltAppearance(sprite, color, data.occupiedByOwnerSprite);
            slot.ConfigureBuiltState(data.isPrebuilt);
        }

        static void CreateCharacter(string name, Vector3 pos, Color color, CharacterData realData, Font hudFont)
        {
            if (realData != null)
            {
                CharacterCatalog.ApplyTo(realData);
                CharacterSpawner.Spawn(realData, pos, color, hudFont);
                return;
            }

            var data = ScriptableObject.CreateInstance<CharacterData>();
            data.id = ResolveCharacterIdByName(name);
            data.displayName = name;
            data.startingIntimacy = 50f;
            data.startingStamina = 70f;
            CharacterCatalog.ApplyTo(data);
            CharacterSpawner.Spawn(data, pos, color, hudFont);
        }

        static CharacterId ResolveCharacterIdByName(string name)
        {
            if (name == "옥토끼") return CharacterId.Rabbit;
            if (name == "삼족오") return CharacterId.SamjokO;
            if (name == "구미호") return CharacterId.Gumiho;
            if (name == "고라니") return CharacterId.Gorani;
            return CharacterId.Rabbit;
        }
    }
}
