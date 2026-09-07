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

        // URP 프로젝트에서 GameObject.CreatePrimitive()가 기본으로 물려주는 머티리얼은
        // Built-in Standard 셰이더라 URP에서 인식을 못 해 분홍색(에러 셰이더)으로 보인다.
        // 프리미티브를 쓸 때는 항상 이 셰이더로 새 머티리얼을 만들어서 색을 입힌다.
        private static Shader UrpLitShader => Shader.Find("Universal Render Pipeline/Lit");

        private void Awake()
        {
            EnsureCamera();
            EnsureLight();

            var propManagerGO = new GameObject("PropManager");
            propManagerGO.AddComponent<PropManager>();

            CreateProp("돌탑", new Vector3(-3, -1.5f, 0), new Color(0.55f, 0.5f, 0.45f));
            CreateProp("우물", new Vector3(0, -1.5f, 0), new Color(0.4f, 0.45f, 0.55f));
            CreateProp("떡절구", new Vector3(3, -1.5f, 0), new Color(0.6f, 0.45f, 0.3f));

            CreateCharacter("옥토끼", new Vector3(-1, 2, 0), Color.white, oktoData);
            CreateCharacter("삼족오", new Vector3(0, 2, 0), Color.black, null);
            CreateCharacter("구미호", new Vector3(1, 2, 0), new Color(1f, 0.6f, 0.2f), null);
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

        private static void ApplyUrpColor(Renderer renderer, Color color)
        {
            if (renderer == null) return;
            var shader = UrpLitShader;
            var mat = shader != null ? new Material(shader) : new Material(Shader.Find("Standard"));
            mat.color = color;
            renderer.material = mat;
        }

        private void CreateProp(string name, Vector3 pos, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Prop_" + name;
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * 0.8f;
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);

            ApplyUrpColor(go.GetComponent<Renderer>(), color);

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
