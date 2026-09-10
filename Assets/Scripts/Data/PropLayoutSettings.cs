using System;
using UnityEngine;

namespace Yoegoe.Data
{
    /// <summary>
    /// 맵 기물 배치 (좌표·폴백 색). 밸런스·스프라이트는 각 PropData 에셋.
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
