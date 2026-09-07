using UnityEngine;
using Yoegoe.Characters;
using Yoegoe.Data;

namespace Yoegoe.Debugging
{
    /// <summary>
    /// 에셋(스프라이트) 없이도 상태머신을 실제 빌드(WebGL 포함)에서 눈으로 확인하기 위한 임시 부트스트랩.
    /// 완전히 빈 씬에 이 스크립트 하나만 올려두고 재생하면:
    ///  - 기물 3개(돌탑/우물/떡절구)를 큐브로
    ///  - 캐릭터 3마리(옥토끼/삼족오/구미호)를 캡슐로
    /// 코드로 직접 생성해서 배치한다. 카메라/조명도 없으면 자동으로 만든다.
    ///
    /// 실제 아트/씬 세팅이 끝나면 이 스크립트와 테스트 씬은 지우면 된다.
    /// </summary>
    public class TestSceneBootstrap : MonoBehaviour
    {
        private void Awake()
        {
            EnsureCamera();
            EnsureLight();

            var propManagerGO = new GameObject("PropManager");
            propManagerGO.AddComponent<PropManager>();

            CreateProp("돌탑", new Vector3(-3, -1.5f, 0));
            CreateProp("우물", new Vector3(0, -1.5f, 0));
            CreateProp("떡절구", new Vector3(3, -1.5f, 0));

            CreateCharacter("옥토끼", new Vector3(-1, 2, 0), Color.white);
            CreateCharacter("삼족오", new Vector3(0, 2, 0), Color.black);
            CreateCharacter("구미호", new Vector3(1, 2, 0), new Color(1f, 0.6f, 0.2f));
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

        private void CreateProp(string name, Vector3 pos)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Prop_" + name;
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * 0.8f;
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var slot = go.AddComponent<PropSlot>();

            var data = ScriptableObject.CreateInstance<PropData>();
            data.propId = name;
            data.displayName = name;
            data.baseProductionPerMinute = 100;
            data.isPrebuilt = true;
            slot.data = data;
        }

        private void CreateCharacter(string name, Vector3 pos, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "Char_" + name;
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * 0.6f;
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var renderer = go.GetComponent<Renderer>();
            if (renderer != null) renderer.material.color = color;

            var agent = go.AddComponent<CharacterAgent>();

            var data = ScriptableObject.CreateInstance<CharacterData>();
            data.displayName = name;
            data.startingStage = GrowthStage.Hon;
            data.startingIntimacy = 50f;
            data.startingStamina = 100f;
            agent.Data = data;
        }
    }
}
