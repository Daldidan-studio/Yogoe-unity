using UnityEngine;

namespace Yoegoe.Debugging
{
    /// <summary>
    /// 맵 패닝. 입력은 MapPointerRouter가 소유하고, 여기서는 화면 델타만 카메라에 적용한다.
    /// </summary>
    public class MapCameraDrag : MonoBehaviour
    {
        private Camera cam;
        private Vector2 minCenter;
        private Vector2 maxCenter;
        private bool boundsSet;

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

            if (boundsSet)
            {
                newPos.x = Mathf.Clamp(newPos.x, minCenter.x, maxCenter.x);
                newPos.y = Mathf.Clamp(newPos.y, minCenter.y, maxCenter.y);
            }

            cam.transform.position = newPos;
        }

        /// <summary>카메라 중심이 이동할 수 있는 범위(= 맵 절반 - 카메라 뷰 절반).</summary>
        public void SetBounds(Vector2 min, Vector2 max)
        {
            minCenter = min;
            maxCenter = max;
            boundsSet = true;
        }
    }
}
