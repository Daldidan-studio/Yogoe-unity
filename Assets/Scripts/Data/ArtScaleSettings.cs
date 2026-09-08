using UnityEngine;

namespace Yoegoe.Data
{
    /// <summary>
    /// 맵·캐릭터·기물 화면 크기 조절을 한곳에서 하는 설정.
    /// Assets/Resources/ArtScaleSettings.asset 하나만 바꾸면 된다.
    /// (임포트 PPU는 각 PNG .meta — 캐릭터 16 / 맵·기물 100 가정)
    /// </summary>
    [CreateAssetMenu(fileName = "ArtScaleSettings", menuName = "Yoegoe/Art Scale Settings")]
    public class ArtScaleSettings : ScriptableObject
    {
        [Header("카메라")]
        [Tooltip("Orthographic Size. 화면 세로 절반(월드 유닛). 기본 5 → 세로 10유닛.")]
        public float cameraOrthoSize = 5f;

        [Header("맵")]
        [Tooltip("1 = 카메라 뷰를 덮는 최소 크기. 키우면 맵이 더 커져서 드래그 여유 증가.")]
        public float mapOverscan = 1f;

        [Header("캐릭터")]
        [Tooltip("스프라이트 캐릭터 Transform 배율. 키우면 캐릭터가 맵 대비 크게 보임.")]
        public float characterScale = 0.35f;

        [Header("기물")]
        [Tooltip("기물 스프라이트 Transform 배율.")]
        public float propScale = 0.6f;
    }
}
