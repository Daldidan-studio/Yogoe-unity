using UnityEngine;
using KSpirits.Data;

namespace KSpirits.Core
{
    /// <summary>
    /// 대사 JSON 로케일. Resources/Dialogue/{character}_tutorial.{locale}.json
    /// </summary>
    public static class GameLocale
    {
        public const string Fallback = "ko";

        static string _current = Fallback;

        public static string Current
        {
            get => _current;
            set
            {
                var next = string.IsNullOrWhiteSpace(value) ? Fallback : value.Trim().ToLowerInvariant();
                if (_current == next) return;
                _current = next;
                OktoDialogue.Invalidate();
            }
        }
    }
}
