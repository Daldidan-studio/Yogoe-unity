using UnityEngine;
using UnityEngine.UI;

namespace KSpirits.UI
{
    /// <summary>
    /// Scene/Prefab Text에 역할별 폰트를 붙인다.
    /// Inspector에서 Role만 고르면 UIFontSettings 와 연동된다.
    /// </summary>
    [RequireComponent(typeof(Text))]
    [DisallowMultipleComponent]
    public class UIFontApplier : MonoBehaviour
    {
        [SerializeField] UIFontRole _role = UIFontRole.Default;

        void Awake() => Apply();

#if UNITY_EDITOR
        void OnValidate()
        {
            if (!Application.isPlaying && TryGetComponent<Text>(out var text))
                UIFont.Apply(text, _role);
        }
#endif

        public void Apply()
        {
            if (TryGetComponent<Text>(out var text))
                UIFont.Apply(text, _role);
        }

        public UIFontRole Role => _role;
    }
}
