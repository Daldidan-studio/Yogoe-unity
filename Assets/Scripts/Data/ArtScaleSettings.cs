using UnityEngine;

namespace Yoegoe.Data
{
    /// <summary>
    /// 맵·캐릭터·기물 화면 크기·그리기 순서를 한곳에서 조절.
    /// Assets/Resources/ArtScaleSettings.asset 하나만 바꾸면 된다.
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
        [Tooltip("스프라이트 캐릭터 Transform 배율.")]
        public float characterScale = 0.35f;

        [Header("기물")]
        [Tooltip("기물 스프라이트 Transform 배율.")]
        public float propScale = 0.6f;

        [Header("그리기 순서 (sortingOrder)")]
        [Tooltip("배경 고정 order.")]
        public int backgroundSort = -100;
        [Tooltip("기물 기준 order. 최종 = propSortBase + (-y * ySortMultiplier)")]
        public int propSortBase = 0;
        [Tooltip("캐릭터 기준 order. 기물보다 크게 두면 앉았을 때 캐릭터가 앞에 옴.")]
        public int characterSortBase = 100;
        [Tooltip("Y가 작을수록(화면 아래) 앞에 그리도록 곱하는 값.")]
        public int ySortMultiplier = 10;

        public int SortOrderForProp(float worldY) =>
            propSortBase + Mathf.RoundToInt(-worldY * ySortMultiplier);

        public int SortOrderForCharacter(float worldY) =>
            characterSortBase + Mathf.RoundToInt(-worldY * ySortMultiplier);
    }
}
