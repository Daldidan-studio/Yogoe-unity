using UnityEngine;
using Yoegoe.Characters;
using Yoegoe.Data;

namespace Yoegoe.Debugging
{
    /// <summary>
    /// 에셋(스프라이트) 없이도 상태머신을 실제 빌드(WebGL 포함)에서 눈으로 확인하기 위한 임시 부트스트랩.
    /// 완전히 빈 씬에 이 스크립트 하나만 올려두고 재생하면:
    ///  - 기물 3개(돌탑/우물/떡절구)를 큐브로
    ///  - 캐릭터 3마리(옥토끼/삼족오/구미호)를 캡슐로(또는 Okto Data가 연결되어 있으면 실제 스프라이트로)
    /// 코드로 직접 생성해서 배치한다. 카메라/조명도 없으면 자동으로 만든다.
    ///
    /// 실제 아트/씬 세팅이 끝나면 이 스크립트와 테스트 씬은 지우면 된다.
    /// </summary>
    public class TestSceneBootstrap : MonoBehaviour
    {
        [Header("실제 아트 연결 (없으면 캡슐로 대체 재생)")]
        [Tooltip("옥토끼 CharacterData (Walk Down/Left/Right/Up 스프라이트까지 채운 에셋)를 연결하면 " +
                 "캡슐 대신 실제 스프라이트로 만들고, CharacterAgent.Data도 이 실제 에셋을 그대로 사용한다.")]
        public CharacterData oktoData;
        [Tooltip("삼족오 CharacterData. 비워두면 삼족오는 검정 캡슐로 대체 재생된다.")]
        public CharacterData samjokOData;
        [Tooltip("구미호 CharacterData. 비워두면 구미호는 주황 캡슐로 대체 재생된다.")]
        public CharacterData gumihoData;

        [Header("맵 배경 (없으면 카메라 단색 배경 그대로)")]
        [Tooltip("사용자가 준 배경 이미지(예: Background_GrassField) — 카메라 뷰 전체를 덮도록 자동 스케일하고, " +
                 "이 배경이 덮는 범위를 그대로 '맵 범위(MapBounds)'로 설정해서 캐릭터가 정처 없이 돌아다닐 때도 " +
                 "이 안에서만 돌아다니게 한다.")]
        public Sprite backgroundSprite;

        [Tooltip("맵을 화면(카메라 뷰)보다 이 배수만큼 더 크게 만든다. 기본 1 = 배경 원본 크기 그대로 " +
                 "(카메라를 덮는 최소 배율만 적용, 인위적으로 더 키우지 않음). 1보다 크게 주면 그만큼 더 " +
                 "크게 만들어서 드래그로 둘러볼 여지를 늘릴 수 있음.")]
        public float mapOverscan = 1f;

        [Tooltip("실제 스프라이트가 있는 캐릭터(옥토끼/삼족오/구미호)의 렌더 크기 배율. " +
                 "새로 받은 그림이 원래 픽셀 크기 그대로면 화면에 비해 너무 크게 나와서 기본값을 작게 잡아둠.")]
        public float characterScale = 0.35f;

        [Header("기물 그림 (없으면 그 기물만 색깔 큐브로 대체)")]
        [Tooltip("기물 렌더 크기 배율.")]
        public float propScale = 0.6f;
        public Sprite propSpriteGate;        // 솟대/문
        public Sprite propSpriteWell;        // 우물
        public Sprite propSpriteThatchedHut; // 초가집
        public Sprite propSpriteSwing;       // 그네
        public Sprite propSpriteStoneLion;   // 돌사자

        // URP 프로젝트에서 GameObject.CreatePrimitive()가 기본으로 물려주는 머티리얼은
        // Built-in Standard 셰이더라 URP에서 인식을 못 해 분홍색(에러 셰이더)으로 보인다.
        // [버그 수정] Shader.Find("Universal Render Pipeline/Lit")나 Shader.Find("Standard")는
        // 에디터에서는 항상 찾아지지만, WebGL 등 실제 빌드에서는 그 셰이더를 참조하는 에셋이
        // 하나도 없으면 빌드 과정에서 통째로 스트리핑되어 null을 반환한다 → new Material(null)이
        // "Value cannot be null. Parameter name: shader" 예외를 던지고 부트스트랩 전체가 죽는다.
        // 대신 현재 렌더 파이프라인(URP)이 자체적으로 들고 있는 기본 머티리얼을 복제해서 쓴다.
        // 이건 파이프라인 에셋 자신이 참조하고 있어서 빌드에서 절대 스트리핑되지 않는다.
        private static Material CreateBaseMaterial()
        {
            var rp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
            if (rp != null && rp.defaultMaterial != null)
            {
                return new Material(rp.defaultMaterial);
            }

            // 혹시 파이프라인이 아예 안 잡혀있는 극단적인 경우를 위한 최후의 폴백들.
            var shader = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Standard")
                         ?? Shader.Find("Sprites/Default")
                         ?? Shader.Find("Unlit/Color");

            if (shader == null)
            {
                Debug.LogError("[TestSceneBootstrap] 사용 가능한 셰이더를 하나도 찾지 못했습니다. " +
                                "머티리얼 없이 렌더러 기본값으로 진행합니다.");
                return null;
            }

            return new Material(shader);
        }

        private void Awake()
        {
            EnsureCamera();
            EnsureLight();
            CreateBackground();

            var propManagerGO = new GameObject("PropManager");
            propManagerGO.AddComponent<PropManager>();

            // [기물 아트 연결] 한 줄로 나란히 두지 말고 맵(세로 카메라 기준 x는 대략 ±2.3, y는 ±4.5
            // 안쪽) 여기저기에 자연스럽게 흩어서 배치. 실제 그림(propSprite*)이 비어있으면 CreateProp이
            // 알아서 색깔 큐브로 대체함.
            CreateProp("돌사자", new Vector3(-2.1f, 2.6f, 0), new Color(0.5f, 0.5f, 0.5f), propSpriteStoneLion);
            CreateProp("초가집", new Vector3(-0.6f, -0.6f, 0), new Color(0.55f, 0.45f, 0.35f), propSpriteThatchedHut);
            CreateProp("그네", new Vector3(2.0f, 1.0f, 0), new Color(0.5f, 0.4f, 0.3f), propSpriteSwing);
            CreateProp("솟대문", new Vector3(-1.9f, -3.2f, 0), new Color(0.6f, 0.55f, 0.5f), propSpriteGate);
            CreateProp("우물", new Vector3(1.6f, -3.6f, 0), new Color(0.4f, 0.45f, 0.55f), propSpriteWell);

            CreateCharacter("옥토끼", new Vector3(-1, 2, 0), Color.white, oktoData);
            CreateCharacter("삼족오", new Vector3(0, 2, 0), Color.black, samjokOData);
            CreateCharacter("구미호", new Vector3(1, 2, 0), new Color(1f, 0.6f, 0.2f), gumihoData);
        }

        private void EnsureCamera()
        {
            if (Camera.main != null) return;
            var camGO = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGO.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.transform.position = new Vector3(0, 0, -10);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.1f, 0.1f, 0.15f);
        }

        private void EnsureLight()
        {
            if (FindObjectOfType<Light>() != null) return;
            var lightGO = new GameObject("Directional Light");
            var light = lightGO.AddComponent<Light>();
            light.type = LightType.Directional;
            lightGO.transform.rotation = Quaternion.Euler(50, -30, 0);
        }

        /// <summary>
        /// 배경 스프라이트를 카메라 뷰 전체를 덮도록(CSS의 background-size: cover와 동일한 방식) 스케일해서
        /// 맨 뒤(sortingOrder 최하)에 깐다. 그리고 그 배경이 실제로 덮는 가로/세로 범위를 그대로
        /// MapBounds로 설정해서, "정처 없이 돌아다니는" 캐릭터가 배경(맵) 밖으로 나가지 않게 한다.
        /// backgroundSprite가 비어있으면 조용히 스킵 (기존처럼 카메라 단색 배경 그대로 동작).
        /// </summary>
        private void CreateBackground()
        {
            if (backgroundSprite == null) return;

            var go = new GameObject("Background");
            go.transform.position = new Vector3(0f, 0f, 1f); // 카메라(z=-10)에서 봤을 때 항상 맨 뒤
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = backgroundSprite;
            sr.sortingOrder = -100;

            var cam = Camera.main;
            if (cam == null || !cam.orthographic) return;

            float camHeight = cam.orthographicSize * 2f;
            float camWidth = camHeight * cam.aspect;
            float spriteWidth = backgroundSprite.bounds.size.x;
            float spriteHeight = backgroundSprite.bounds.size.y;
            if (spriteWidth <= 0f || spriteHeight <= 0f) return;

            // 카메라 뷰를 최소한으로 덮는 배율에 mapOverscan을 곱해서, 맵을 화면보다 일부러 더 크게 만든다
            // (그래야 드래그로 이동할 여지가 생긴다. mapOverscan=1이면 예전처럼 화면 딱 맞는 크기).
            float coverScale = Mathf.Max(camWidth / spriteWidth, camHeight / spriteHeight);
            float scale = coverScale * Mathf.Max(1f, mapOverscan);
            go.transform.localScale = new Vector3(scale, scale, 1f);

            float mapWidth = spriteWidth * scale;
            float mapHeight = spriteHeight * scale;

            // 약간의 여백(0.5유닛)을 두어 캐릭터가 맵 가장자리에 완전히 붙지 않게 한다.
            const float margin = 0.5f;
            MapBounds.SetBounds(
                new Vector2(-mapWidth / 2f + margin, -mapHeight / 2f + margin),
                new Vector2(mapWidth / 2f - margin, mapHeight / 2f - margin));

            // 맵이 화면보다 큰 만큼(overscan) 카메라를 드래그로 움직일 수 있게 하고, 배경 밖으로는
            // 못 나가도록 카메라 중심 이동 범위를 "맵 절반 - 카메라 뷰 절반"으로 제한한다.
            var drag = cam.GetComponent<MapCameraDrag>();
            if (drag == null) drag = cam.gameObject.AddComponent<MapCameraDrag>();

            float halfExtraW = Mathf.Max(0f, mapWidth / 2f - camWidth / 2f);
            float halfExtraH = Mathf.Max(0f, mapHeight / 2f - camHeight / 2f);
            drag.SetBounds(new Vector2(-halfExtraW, -halfExtraH), new Vector2(halfExtraW, halfExtraH));
        }

        private static void ApplyUrpColor(Renderer renderer, Color color)
        {
            if (renderer == null) return;
            var mat = CreateBaseMaterial();
            if (mat == null) return; // 렌더러 기본 머티리얼(핑크)로라도 일단 화면엔 나온다
            mat.color = color;
            renderer.material = mat;
        }

        private void CreateProp(string name, Vector3 pos, Color color, Sprite sprite = null)
        {
            GameObject go;

            if (sprite != null)
            {
                // 실제 기물 그림이 있으면 큐브 대신 SpriteRenderer로 생성.
                go = new GameObject("Prop_" + name);
                go.transform.position = pos;
                go.transform.localScale = Vector3.one * propScale;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "Prop_" + name;
                go.transform.position = pos;
                go.transform.localScale = Vector3.one * 0.8f;
                var col = go.GetComponent<Collider>();
                if (col != null) Destroy(col);

                ApplyUrpColor(go.GetComponent<Renderer>(), color);
            }

            var slot = go.AddComponent<PropSlot>();

            var data = ScriptableObject.CreateInstance<PropData>();
            data.propId = name;
            data.displayName = name;
            data.baseProductionPerMinute = 100;
            data.isPrebuilt = true;
            slot.data = data;
        }

        private void CreateCharacter(string name, Vector3 pos, Color color, CharacterData realData)
        {
            bool hasRealArt = realData != null && HasAnySprite(realData);

            GameObject go;
            CharacterAgent agent;

            if (hasRealArt)
            {
                // 실제 스프라이트가 있으면 캡슐 대신 SpriteRenderer로 생성 — CharacterAgent.Awake()가
                // 자식/자기 자신의 SpriteRenderer를 자동으로 찾아 쓰므로 별도 연결 코드 불필요.
                go = new GameObject("Char_" + name);
                go.transform.position = pos;

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = FirstSprite(realData);
                go.transform.localScale = Vector3.one * characterScale;

                agent = go.AddComponent<CharacterAgent>();
                agent.Data = realData; // 런타임 스텁이 아니라 실제 에셋을 그대로 사용 (걷기 애니메이션 재생됨)
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                go.name = "Char_" + name;
                go.transform.position = pos;
                go.transform.localScale = Vector3.one * 0.6f;
                var col = go.GetComponent<Collider>();
                if (col != null) Destroy(col);

                ApplyUrpColor(go.GetComponent<Renderer>(), color);

                agent = go.AddComponent<CharacterAgent>();

                var data = ScriptableObject.CreateInstance<CharacterData>();
                data.displayName = name;
                data.startingStage = GrowthStage.Hon;
                data.startingIntimacy = 50f;
                data.startingStamina = 100f;
                agent.Data = data;
            }
        }

        private static bool HasAnySprite(CharacterData data)
        {
            return FirstSprite(data) != null;
        }

        private static Sprite FirstSprite(CharacterData data)
        {
            if (data.walkDown != null) foreach (var s in data.walkDown) if (s != null) return s;
            if (data.walkLeft != null) foreach (var s in data.walkLeft) if (s != null) return s;
            if (data.walkRight != null) foreach (var s in data.walkRight) if (s != null) return s;
            if (data.walkUp != null) foreach (var s in data.walkUp) if (s != null) return s;
            return null;
        }
    }
}
