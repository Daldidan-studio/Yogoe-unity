using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using Yoegoe.Characters;
using Yoegoe.Data;
using Yoegoe.Debugging;
using Yoegoe.Economy;
using Yoegoe.Save;
using Yoegoe.UI;

namespace Yoegoe
{
    /// <summary>
    /// Main 씬 진입점. 카메라·맵·기물·캐릭터·HUD를 조립한다.
    /// 화면 크기: ArtScaleSettings.asset / 시작 재화·스탯: StartingStateSettings.asset
    /// </summary>
    public class Main : MonoBehaviour
    {
        /// <summary>이보다 긴 벽시계 공백이면 캐릭터 정산을 돌린다 (WebGL 탭 숨김 등).</summary>
        private const float WallClockCatchUpThresholdSeconds = 1f;

        private DateTime lastActiveUtc;
        private bool worldReady;
        [Header("실제 아트 연결 (없으면 캡슐로 대체 재생)")]
        [Tooltip("옥토끼 CharacterData (Walk Down/Left/Right/Up 스프라이트까지 채운 에셋)를 연결하면 " +
                 "캡슐 대신 실제 스프라이트로 만들고, CharacterAgent.Data도 이 실제 에셋을 그대로 사용한다.")]
        public CharacterData oktoData;
        [Tooltip("삼족오 CharacterData. 비워두면 삼족오는 검정 캡슐로 대체 재생된다.")]
        public CharacterData samjokOData;
        [Tooltip("구미호 CharacterData. 비워두면 구미호는 주황 캡슐로 대체 재생된다.")]
        public CharacterData gumihoData;
        [Tooltip("고라니 CharacterData (소환용). 비워두면 Resources/Characters/Gorani 를 찾는다.")]
        public CharacterData goraniData;

        [Header("화면 크기 (여기 말고 ArtScaleSettings.asset에서 조절)")]
        [Tooltip("비워두면 Resources/ArtScaleSettings 를 자동으로 찾는다. 맵·캐릭터·기물 배율은 그 에셋 하나에서 바꾼다.")]
        public ArtScaleSettings artScale;

        [Header("맵 배경")]
        [Tooltip("전체 맵(섬 전경). Background_IslandOverview")]
        public Sprite overviewBackgroundSprite;
        [Tooltip("걷기 가능 잔디 레이어. Background_GrassField — 전체맵 위에 올림. 잔디 중심이 월드 원점.")]
        public Sprite playfieldSprite;
        [Tooltip("전체맵 위치 보정(잔디=원점일 때 섬 잔디 정상과 맞추는 오프셋).")]
        public Vector2 overviewOffset = new Vector2(-0.05f, -0.32f);

        [Header("기물 그림 (없으면 그 기물만 색깔 큐브로 대체)")]
        public Sprite propSpriteGate;        // 솟대/문
        public Sprite propSpriteWell;        // 우물
        public Sprite propSpriteThatchedHut; // 초가집
        public Sprite propSpriteSwing;       // 그네
        public Sprite propSpriteStoneLion;   // 돌사자
        public Sprite propSpriteMortar;      // 떡절구 (빈 기물)
        public Sprite propSpriteMortarOccupiedRabbit; // 옥토끼 점유 연출

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
            // maximumDeltaTime은 이동 스파이크 방지용으로 남겨 두되, 실제 공백 정산은 벽시계로 한다.
            // (WebGL은 탭 복귀 시 deltaTime 스파이크를 안 주는 경우가 많다.)
            Time.maximumDeltaTime = 3600f;
            lastActiveUtc = DateTime.UtcNow;

            GameEconomy.ApplyStartingState(StartingStateSettings.Get());

            CharacterCatalog.EnsureLoaded();
            CharacterCatalog.SetOfferings(offerings);

            EnsureCamera();
            EnsureLight();
            EnsureEventSystem();
            EnsureMapPointerRouter();
            CreateBackground();

            var propManagerGO = new GameObject("PropManager");
            propManagerGO.AddComponent<PropManager>();

            // 기획 8장 시작: 우물·돌사자·떡절구 건립, 나머지 자물쇠
            CreateProp("돌사자", new Vector3(-2.1f, 1.2f, 0), new Color(0.5f, 0.5f, 0.5f),
                propSpriteStoneLion, prebuilt: true);
            CreateProp("초가집", new Vector3(-0.6f, -0.2f, 0), new Color(0.55f, 0.45f, 0.35f),
                propSpriteThatchedHut, prebuilt: false);
            CreateProp("그네", new Vector3(2.0f, 0.8f, 0), new Color(0.5f, 0.4f, 0.3f),
                propSpriteSwing, prebuilt: false);
            CreateProp("솟대문", new Vector3(-1.9f, -1.3f, 0), new Color(0.6f, 0.55f, 0.5f),
                propSpriteGate, prebuilt: false);
            CreateProp("우물", new Vector3(1.6f, -1.4f, 0), new Color(0.4f, 0.45f, 0.55f),
                propSpriteWell, prebuilt: true);
            CreateProp("떡절구", new Vector3(0.5f, 1.35f, 0), new Color(0.75f, 0.55f, 0.35f),
                propSpriteMortar, prebuilt: true, endingProp: true, endingOwner: CharacterId.Rabbit,
                occupiedByOwnerSprite: propSpriteMortarOccupiedRabbit);

            CreateCharacter("옥토끼", new Vector3(-1f, 0.5f, 0), Color.white, oktoData);
            CreateCharacter("삼족오", new Vector3(0f, 0.5f, 0), Color.black, samjokOData);
            CreateCharacter("구미호", new Vector3(1f, 0.5f, 0), new Color(1f, 0.6f, 0.2f), gumihoData);

            CreateHud();
        }

        private IEnumerator Start()
        {
            // CharacterAgent.Start(기본 스탯)가 끝난 뒤 세이브를 덮어써야 복원이 유지된다.
            yield return null;
            GameSaveBridge.TryLoadSimulateAndApply();
            lastActiveUtc = DateTime.UtcNow;
            worldReady = true;

#if UNITY_WEBGL && !UNITY_EDITOR
            YogoeHideLoadingOverlay();
#endif
        }

        private void Update()
        {
            if (!worldReady) return;

            var now = DateTime.UtcNow;
            double gap = (now - lastActiveUtc).TotalSeconds;
            lastActiveUtc = now;

            // Update가 멈췄다 재개되면(탭 숨김·잠금화면 등) gap이 커진다.
            if (gap >= WallClockCatchUpThresholdSeconds)
            {
                float seconds = (float)Math.Min(gap, OfflineSimulator.MaxOfflineSeconds);
                CharacterAgent.CatchUpAll(seconds);
            }
        }

        private void OnApplicationPause(bool pause)
        {
            if (pause)
            {
                if (worldReady) GameSaveBridge.SaveFromWorld();
                return;
            }

            // pause=false: Update 한 프레임이 오기 전에 포커스가 돌아올 수 있어 여기서도 정산.
            ApplyWallClockCatchUpIfNeeded();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                if (worldReady) GameSaveBridge.SaveFromWorld();
                return;
            }

            ApplyWallClockCatchUpIfNeeded();
        }

        private void ApplyWallClockCatchUpIfNeeded()
        {
            if (!worldReady) return;

            var now = DateTime.UtcNow;
            double gap = (now - lastActiveUtc).TotalSeconds;
            lastActiveUtc = now;
            if (gap < WallClockCatchUpThresholdSeconds) return;

            float seconds = (float)Math.Min(gap, OfflineSimulator.MaxOfflineSeconds);
            CharacterAgent.CatchUpAll(seconds);
        }

        private void OnApplicationQuit()
        {
            GameSaveBridge.SaveFromWorld();
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        static extern void YogoeHideLoadingOverlay();
#endif

        private void CreateHud()
        {
            var detailGO = new GameObject("DetailScreen");
            detailGO.SetActive(false);
            var detail = detailGO.AddComponent<DetailScreen>();
            detail.font = hudFont;
            detail.offerings = offerings;
            detail.purifiedWaterIcon = purifiedWaterIcon;
            detailGO.SetActive(true);

            var purchaseGO = new GameObject("PropPurchasePopup");
            purchaseGO.SetActive(false);
            var purchase = purchaseGO.AddComponent<PropPurchasePopup>();
            purchase.font = hudFont;
            purchaseGO.SetActive(true);

            var summonGO = new GameObject("SummonPopup");
            summonGO.SetActive(false);
            var summon = summonGO.AddComponent<SummonPopup>();
            summon.font = hudFont;
            summon.goraniData = goraniData;
            summonGO.SetActive(true);

            var hudGO = new GameObject("Hud");
            hudGO.SetActive(false);
            var hud = hudGO.AddComponent<GameHud>();
            hud.font = hudFont;
            hud.purifiedWaterIcon = purifiedWaterIcon;
            hud.detailScreen = detail;
            hudGO.SetActive(true);
        }

        /// <summary>
        /// uGUI 버튼(슬롯 탭 등)이 반응하려면 EventSystem이 씬에 있어야 한다. 이 프로젝트는 새
        /// Input System만 쓰도록 설정돼 있어서(Project Settings) 구식 StandaloneInputModule 대신
        /// InputSystemUIInputModule을 붙인다.
        /// </summary>
        private void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
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

            // 배경이 없어도 패닝 컴포넌트는 카메라에 있어야 라우터가 연결할 수 있다.
            if (cam.GetComponent<MapCameraDrag>() == null)
                cam.gameObject.AddComponent<MapCameraDrag>();

            var router = cam.GetComponent<Yoegoe.Characters.MapPointerRouter>();
            if (router == null) router = cam.gameObject.AddComponent<Yoegoe.Characters.MapPointerRouter>();
            router.targetCamera = cam;
            router.mapDrag = cam.GetComponent<MapCameraDrag>();
            // 기물 PNG(스프라이트 bounds) 안에서만 드롭 판정
            router.propDropRadius = 0f;
        }

        private void EnsureLight()
        {
            if (FindAnyObjectByType<Light>() != null) return;
            var lightGO = new GameObject("Directional Light");
            var light = lightGO.AddComponent<Light>();
            light.type = LightType.Directional;
            lightGO.transform.rotation = Quaternion.Euler(50, -30, 0);
        }

        /// <summary>
        /// 전체맵(섬) + 그 위 잔디 플레이필드.
        /// 걷기는 잔디 bounds만, 카메라 패닝은 전체맵 기준.
        /// </summary>
        private void CreateBackground()
        {
            float scale = Mathf.Max(0.01f, Scale.mapScale);
            var cam = Camera.main;

            // 1) 전체 맵 (뒤)
            Sprite overview = overviewBackgroundSprite;
            if (overview != null)
            {
                var go = new GameObject("Background_Overview");
                go.transform.position = new Vector3(overviewOffset.x, overviewOffset.y, 1f);
                go.transform.localScale = new Vector3(scale, scale, 1f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = overview;
                sr.sortingOrder = Scale.backgroundSort;
            }

            // 2) 잔디 플레이필드 (앞) — 월드 원점, 걷기/기물 좌표 기준
            Sprite playfield = playfieldSprite;
            if (playfield == null) return;

            var fieldGO = new GameObject("Background_Playfield");
            fieldGO.transform.position = new Vector3(0f, 0f, 0.9f);
            fieldGO.transform.localScale = new Vector3(scale, scale, 1f);
            var fieldSr = fieldGO.AddComponent<SpriteRenderer>();
            fieldSr.sprite = playfield;
            fieldSr.sortingOrder = Scale.backgroundSort + 1;

            float fieldW = playfield.bounds.size.x * scale;
            float fieldH = playfield.bounds.size.y * scale;
            if (fieldW <= 0f || fieldH <= 0f) return;

            const float margin = 0.35f;
            MapBounds.SetBounds(
                new Vector2(-fieldW / 2f + margin, -fieldH / 2f + margin),
                new Vector2(fieldW / 2f - margin, fieldH / 2f - margin));

            if (cam == null || !cam.orthographic) return;

            // 패닝 범위: 전체맵이 있으면 그 크기, 없으면 잔디
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

            // 전체맵이 overviewOffset만큼 밀렸으면 카메라 패닝 중심도 같이 이동
            Vector2 panCenter = overview != null ? overviewOffset : Vector2.zero;
            drag.SetBounds(
                new Vector2(panCenter.x - halfExtraW, panCenter.y - halfExtraH),
                new Vector2(panCenter.x + halfExtraW, panCenter.y + halfExtraH));

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

        private void CreateProp(string name, Vector3 pos, Color color, Sprite sprite = null,
            bool prebuilt = true, bool endingProp = false, CharacterId endingOwner = CharacterId.Rabbit,
            Sprite occupiedByOwnerSprite = null)
        {
            GameObject go;

            if (sprite != null)
            {
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
            data.icon = sprite;
            data.baseProductionPerMinute = 100;
            data.isPrebuilt = prebuilt;
            data.isEndingProp = endingProp;
            data.owner = endingOwner;
            data.hasUniqueEndingAnimation = endingProp && endingOwner == CharacterId.Rabbit
                && occupiedByOwnerSprite != null;
            data.occupiedByOwnerSprite = occupiedByOwnerSprite;
            slot.data = data;
            slot.SetBuiltAppearance(sprite, color, occupiedByOwnerSprite);
            slot.ConfigureBuiltState(prebuilt);
        }

        private void CreateCharacter(string name, Vector3 pos, Color color, CharacterData realData)
        {
            if (realData != null)
            {
                CharacterCatalog.ApplyTo(realData);
                CharacterSpawner.Spawn(realData, pos, color, hudFont);
                return;
            }

            // 에셋 미연결 시 런타임 스텁 — JSON 카탈로그로 이름·선호 등 채움
            var data = ScriptableObject.CreateInstance<CharacterData>();
            data.id = ResolveCharacterIdByName(name);
            data.displayName = name;
            data.startingStage = GrowthStage.Hon;
            data.startingIntimacy = 50f;
            data.startingStamina = 100f;
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
