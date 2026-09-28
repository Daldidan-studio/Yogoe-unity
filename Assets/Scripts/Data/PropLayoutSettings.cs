using System;
using UnityEngine;

namespace Yoegoe.Data
{
    /// <summary>
    /// 맵 기물 배치 (좌표·폴백 색). 밸런스·스프라이트는 각 PropData 에셋.
    /// 좌표는 mapScale=1(맵 로컬) 기준 — ArtScaleSettings.mapScale 이 월드로 곱해진다.
    /// Assets/Resources/PropLayoutSettings.asset 하나만 바꾸면 된다.
    /// </summary>
    [CreateAssetMenu(fileName = "PropLayoutSettings", menuName = "Yoegoe/Prop Layout Settings")]
    public class PropLayoutSettings : ScriptableObject
    {
        [Serializable]
        public class Placement
        {
            public PropData data;
            public Vector3 position;
            [Tooltip("스프라이트 없을 때 쓰는 큐브 색.")]
            public Color fallbackColor = new Color(0.5f, 0.5f, 0.5f, 1f);
        }

        public Placement[] placements;

        [Tooltip("공덕 버드나무 위치 (7-4). 공덕은 여기 모이고 탭해서 수거.")]
        public Vector3 willowPosition = new Vector3(0f, -1.35f, 0f);

        public static PropLayoutSettings Get()
        {
            var loaded = Resources.Load<PropLayoutSettings>("PropLayoutSettings");
            if (loaded != null) return loaded;
            return CreateInstance<PropLayoutSettings>();
        }

        public PropData FindByPropId(string propId)
        {
            if (string.IsNullOrEmpty(propId) || placements == null) return null;
            for (int i = 0; i < placements.Length; i++)
            {
                var p = placements[i];
                if (p?.data == null) continue;
                if (p.data.propId == propId || p.data.displayName == propId)
                    return p.data;
            }
            return null;
        }
    }
}
