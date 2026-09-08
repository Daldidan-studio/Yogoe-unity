using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using Yoegoe.Characters;
using Yoegoe.Data;
using Yoegoe.Debugging;
using Yoegoe.Economy;
using Yoegoe.UI;

namespace Yoegoe
{
    /// <summary>
    /// Main 씬 진입점. 카메라·맵·기물·캐릭터·HUD를 조립한다.
    /// 화면 크기: ArtScaleSettings.asset / 시작 재화·스탯: StartingStateSettings.asset
    /// </summary>
    public class Main : MonoBehaviour
    {
        [Header("실제 아트 연결 (없으면 캡슐로 대체 재생)")]
        [Tooltip("옥토끼 CharacterData (Walk Down/Left/Right/Up 스프라이트까지 채운 에셋)를 연결하면 " +
                 "캡슐 대신 실제 스프라이트로 만들고, CharacterAgent.Data도 이 실제 에셋을 그대로 사용한다.")]
        public CharacterData oktoData;
        [Tooltip("삼족오 CharacterData. 비워두면 삼족오는 검정 캡슐로 대체 재생된다.")]
        public CharacterData samjokOData;
        [Tooltip("구미호 CharacterData. 비워두면 구미호는 주황 캡슐로 대체 재생된다.")]
        public CharacterData gumihoData;

        [Header("화면 크기 (여기 말고 ArtScaleSettings.asset에서 조절)")]
        [Tooltip("비워두면 Resources/ArtScaleSettings 를 자동으로 찾는다. 맵·캐릭터·기물 배율은 그 에셋 하나에서 바꾼다.")]
        public ArtScaleSettings artScale;

        [Header("맵 배경 (없으면 카메라 단색 배경 그대로)")]
        [Tooltip("사용자가 준 배경 이미지(예: Background_GrassField) — 카메라 뷰 전체를 덮도록 자동 스케일하고, " +
                 "이 배경이 덮는 범위를 그대로 '맵 범위(MapBounds)'로 설정해서 캐릭터가 정처 없이 돌아다닐 때도 " +
                 "이 안에서만 돌아다니게 한다.")]
        public Sprite backgroundSprite;

        [Header("기물 그림 (없으면 그 기물만 색깔 큐브로 대체)")]
        public Sprite propSpriteGate;        // 솟대/문
        public Sprite propSpriteWell;        // 우물
        public Sprite propSpriteThatchedHut; // 초가집
        public Sprite propSpriteSwing;       // 그네
        public Sprite propSpriteStoneLion;   // 돌사자

        [Header("HUD (상단 재화 바 + 하단 슬롯바)")]
        [Tooltip("한글 표시용 폰트. 비워두면 유니티 기본 폰트로 나오는데 한글이 깨질 수 있음 " +
                 "(Assets/Fonts/DOSGothic.ttf 연결 권장 — 프로젝트에 이미 있는 한글 폰트).")]
        public Font hudFont;
        [Tooltip("정화수 재화 칩에 쓸 아이콘 (Assets/Art/Offerings/Offering_PurifiedWater 연결 권장).")]
        public Sprite purifiedWaterIcon;
        [Tooltip("상세화면 하단 급여 바에 나열할 공양물 전체 목록 (Assets/Data/Offerings/*.asset 전부 연결).")]
        public OfferingData[] offerings;

        private ArtScaleSettings _scale;
        private ArtScaleSettings Scale
        {
            get
            {
                if (_scale != null) return _scale;
                if (artScale != null) return _scale = artScale;
                _scale = Resources.Load<ArtScaleSettings>("ArtScaleSettings");
                if (_scale == null)
                {
                    // 에셋이 없어도 부트스트랩이 죽지 않게 런타임 기본값
                    _scale = ScriptableObject.CreateInstance<ArtScaleSettings>();
                }
                return _scale;
            }
        }

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
                Debug.LogError("[Main] 사용 가능한 셰이더를 하나도 찾지 못했습니다. " +
                                "머티리얼 없이 렌더러 기본값으로 진행합니다.");
                return null;
            }

            return new Material(shader);
        }

        private void Awake()
        {
            // [비활성 탭 대응] 브라우저 탭이 백그라운드로 가면 유니티 프레임이 뜸해지는데(스로틀링),
            // 유니티는 기본적으로 한 프레임의 deltaTime을 Time.maximumDeltaTime(기본 0.33초)로
            // 잘라버려서 탭이 비활성이던 동안 흐른 실제 시간이 통째로 무시되고 기력/생산이 멈춘
            // 것처럼 보인다. 이 값을 크게 잡아서, 탭이 다시 활성화됐을 때 그동안 지난 실제 시간을
            // 한 번에 반영(따라잡기)하게 한다 — "오프라인 동일 속도" 요구사항(4장)의 최소 버전.
            // (앱을 완전히 껐다 켜는 진짜 오프라인 정산은 세이브 시스템이 있어야 해서 별도 작업 필요.)
            Time.maximumDeltaTime = 3600f;

            GameEconomy.ApplyStartingState(StartingStateSettings.Get());

            EnsureCamera();
            EnsureLight();
            EnsureEventSystem();
            EnsureMapPointerRouter();
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

            CreateHud();
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        static extern void YogoeHideLoadingOverlay();
#endif

        private void Start()
        {
            // 빌드 씬은 Main — 로딩 오버레이를 아래로 내리며 게임을 드러낸다.
#if UNITY_WEBGL && !UNITY_EDITOR
            YogoeHideLoadingOverlay();
#endif
        }

        private void CreateHud()
        {
            var detailGO = new GameObject("DetailScreen");
            var detail = detailGO.AddComponent<DetailScreen>();
            detail.font = hudFont;
            detail.offerings = offerings;

            var hudGO = new GameObject("Hud");
            var hud = hudGO.AddComponent<GameHud>();
            hud.font = hudFont;
            hud.purifiedWaterIcon = purifiedWaterIcon;
            hud.detailScreen = detail;
        }

        /// <summary>
        /// uGUI 버튼(슬롯 탭 등)이 반응하려면 EventSystem이 씬에 있어야 한다. 이 프로젝트는 새
        /// Input System만 쓰도록 설정돼 있어서(Project Settings) 구식 StandaloneInputModule 대신
        /// InputSystemUIInputModule을 붙인다.
        /// </summary>
        private void EnsureEventSystem()
        {
            if (FindObjectOfType<EventSystem>() != null) return;
            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<EventSystem>();
            esGo.AddComponent<InputSystemUIInputModule>();
        }

        private void EnsureCamera()
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
            cam.orthographicSize = Scale.cameraOrthoSize;
        }

        /// <summary>
        /// 맵 탭/드래그·캐릭터 드래그 단일 라우터. CreateBackground보다 먼저 붙여 두고,
        /// MapCameraDrag는 배경 생성 시 같은 카메라에 추가된다 (라우터가 Awake 이후 GetComponent).
        /// </summary>
        private void EnsureMapPointerRouter()
        {
            var cam = Camera.main;
            if (cam == null) return;

            var router = cam.GetComponent<Yoegoe.Characters.MapPointerRouter>();
            if (router == null) router = cam.gameObject.AddComponent<Yoegoe.Characters.MapPointerRouter>();
            router.targetCamera = cam;
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
            sr.sortingOrder = Scale.backgroundSort;

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
            float scale = coverScale * Mathf.Max(1f, Scale.mapOverscan);
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

            var router = cam.GetComponent<Yoegoe.Characters.MapPointerRouter>();
            if (router != null) router.mapDrag = drag;
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
                go.transform.localScale = Vector3.one * Scale.propScale;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sortingOrder = Scale.SortOrderForProp(pos.y);
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
                sr.sortingOrder = Scale.SortOrderForCharacter(pos.y);
                go.transform.localScale = Vector3.one * Scale.characterScale;

                agent = go.AddComponent<CharacterAgent>();
                agent.Data = realData; // 런타임 스텁이 아니라 실제 에셋을 그대로 사용 (걷기 애니메이션 재생됨)
                agent.bubbleFont = hudFont; // 혼잣말 말풍선용 폰트 (한글 지원)
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
                agent.bubbleFont = hudFont;

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
