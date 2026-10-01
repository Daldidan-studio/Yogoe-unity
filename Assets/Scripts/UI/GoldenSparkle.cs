using UnityEngine;
using UnityEngine.UI;

namespace Yoegoe.UI
{
    /// <summary>
    /// 금빛 반짝임 (황금음식·황금 재료 표시). 붙인 Graphic의 색을 원래 색 ↔ 금색으로 천천히 오간다.
    /// 기물의 황금 반짝임(PropSlot)과 같은 박자.
    /// </summary>
    [RequireComponent(typeof(Graphic))]
    public class GoldenSparkle : MonoBehaviour
    {
        public static readonly Color Gold = new Color(1f, 0.86f, 0.35f, 1f);

        Graphic graphic;
        Color baseColor;

        /// <summary>a ↔ b 사이 지금 박자의 색.</summary>
        public static Color Pulse(Color a, Color b) =>
            Color.Lerp(a, b, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f));

        public static GoldenSparkle Attach(Graphic target)
        {
            if (target == null) return null;
            var s = target.GetComponent<GoldenSparkle>();
            if (s == null) s = target.gameObject.AddComponent<GoldenSparkle>();
            return s;
        }

        void Awake()
        {
            graphic = GetComponent<Graphic>();
            baseColor = graphic.color;
        }

        void Update()
        {
            if (graphic != null) graphic.color = Pulse(baseColor, Gold);
        }

        void OnDisable()
        {
            if (graphic != null) graphic.color = baseColor;
        }
    }
}
