using UnityEngine;
using Yoegoe.Data;

namespace Yoegoe.UI
{
    /// <summary>
    /// UI·월드 라벨 글자 크기. ArtScaleSettings.uiFontScale 배율 적용.
    /// </summary>
    public static class UiFonts
    {
        public static int Size(int baseSize)
        {
            float scale = ArtScaleSettings.GetOrDefault().uiFontScale;
            if (scale < 0.5f) scale = 0.5f;
            if (scale > 2.5f) scale = 2.5f;
            return Mathf.Max(1, Mathf.RoundToInt(baseSize * scale));
        }
    }
}
