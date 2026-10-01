using UnityEngine;

namespace Yoegoe.Debugging
{
    /// <summary>
    /// 맵 패닝·줌. 입력은 MapPointerRouter가 소유하고, 여기서는 카메라만 움직인다.
    /// </summary>
    public class MapCameraDrag : MonoBehaviour
    {
        private Camera cam;
        private Vector2 panCenter;
        private float contentHalfW;
        private float contentHalfH;
        private bool boundsSet;

        private Vector2 minCenter;
        private Vector2 maxCenter;

        [Tooltip("최대 확대(ortho 최소).")]
        public float minOrtho = 1.4f;

        [Tooltip("최대 축소(ortho 최대). CaptureHome / 맵 맞춤 시 갱신.")]
        public float maxOrtho = 8f;

        private void Awake()
        {
            cam = GetComponent<Camera>();
        }

        /// <summary>화면 픽셀 델타만큼 카메라를 이동(맵이 손가락을 따라오는 방향).</summary>
        public void ApplyScreenDelta(Vector2 screenDelta)
        {
            if (cam == null) cam = GetComponent<Camera>();
            if (cam == null) return;
            if (screenDelta.sqrMagnitude <= 0f) return;

            float worldPerPixel = (cam.orthographicSize * 2f) / Mathf.Max(1, Screen.height);
            Vector3 worldDelta = new Vector3(-screenDelta.x * worldPerPixel, -screenDelta.y * worldPerPixel, 0f);
            Vector3 newPos = cam.transform.position + worldDelta;
            cam.transform.position = ClampCenter(newPos);
        }

        /// <summary>
        /// 핀치/휠 줌. ratio&gt;1 이면 축소(멀리), &lt;1 이면 확대(가까이).
        /// screenPivot 아래 월드 좌표가 줌 전후에 유지된다.
        /// </summary>
        public void ZoomByRatio(Vector2 screenPivot, float ratio)
        {
            if (cam == null) cam = GetComponent<Camera>();
            if (cam == null || !cam.orthographic) return;
            if (ratio <= 0.0001f || Mathf.Approximately(ratio, 1f)) return;

            float oldOrtho = cam.orthographicSize;
            float newOrtho = Mathf.Clamp(oldOrtho * ratio, minOrtho, maxOrtho);
            if (Mathf.Approximately(newOrtho, oldOrtho)) return;

            float depth = Mathf.Abs(cam.transform.position.z);
            Vector3 before = cam.ScreenToWorldPoint(new Vector3(screenPivot.x, screenPivot.y, depth));
            before.z = 0f;

            cam.orthographicSize = newOrtho;
            RefreshPanLimits();

            Vector3 after = cam.ScreenToWorldPoint(new Vector3(screenPivot.x, screenPivot.y, depth));
            after.z = 0f;
            Vector3 pos = cam.transform.position + (before - after);
            cam.transform.position = ClampCenter(pos);
        }

        public bool BoundsReady => boundsSet;
        public Vector2 ContentCenter => panCenter;
        public Vector2 MinPanCenter => minCenter;
        public Vector2 MaxPanCenter => maxCenter;

        /// <summary>맵 콘텐츠 반폭·반높이(월드)와 중심. 줌에 따라 패닝 한도를 다시 계산한다.</summary>
        public void SetContentRect(Vector2 center, float halfWidth, float halfHeight)
        {
            panCenter = center;
            contentHalfW = Mathf.Max(0f, halfWidth);
            contentHalfH = Mathf.Max(0f, halfHeight);
            boundsSet = true;
            RefreshPanLimits();
            cam.transform.position = ClampCenter(cam.transform.position);
        }

        public void SetOrthoLimits(float min, float max)
        {
            minOrtho = Mathf.Max(0.5f, min);
            maxOrtho = Mathf.Max(minOrtho + 0.1f, max);
            if (cam == null) cam = GetComponent<Camera>();
            if (cam != null)
                cam.orthographicSize = Mathf.Clamp(cam.orthographicSize, minOrtho, maxOrtho);
            RefreshPanLimits();
        }

        /// <summary>ortho 변경 후 패닝 한도 재계산 (인트로 등).</summary>
        public void RefreshPanLimits()
        {
            if (!boundsSet || cam == null) return;
            float camH = cam.orthographicSize * 2f;
            float camW = camH * cam.aspect;
            float halfExtraW = Mathf.Max(0f, contentHalfW - camW * 0.5f);
            float halfExtraH = Mathf.Max(0f, contentHalfH - camH * 0.5f);
            minCenter = new Vector2(panCenter.x - halfExtraW, panCenter.y - halfExtraH);
            maxCenter = new Vector2(panCenter.x + halfExtraW, panCenter.y + halfExtraH);
        }

        public Vector3 ClampCenter(Vector3 pos)
        {
            if (!boundsSet) return pos;
            pos.x = Mathf.Clamp(pos.x, minCenter.x, maxCenter.x);
            pos.y = Mathf.Clamp(pos.y, minCenter.y, maxCenter.y);
            return pos;
        }
    }
}
