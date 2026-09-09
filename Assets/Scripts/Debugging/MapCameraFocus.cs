using System.Collections;
using UnityEngine;

namespace Yoegoe.Debugging
{
    /// <summary>맵 카메라 줌인/복귀 (소환·진화·넋 탭 연출용).</summary>
    public class MapCameraFocus : MonoBehaviour
    {
        public static MapCameraFocus Instance { get; private set; }

        Camera cam;
        Vector3 homePos;
        float homeOrtho;
        bool hasHome;
        Coroutine moveRoutine;

        void Awake()
        {
            Instance = this;
            cam = GetComponent<Camera>();
            CaptureHome();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void CaptureHome()
        {
            if (cam == null) cam = GetComponent<Camera>();
            if (cam == null) return;
            homePos = cam.transform.position;
            homeOrtho = cam.orthographicSize;
            hasHome = true;
        }

        /// <summary>worldPos를 화면 중앙에 두고 ortho로 줌.</summary>
        public void Focus(Vector3 worldPos, float orthoSize, float duration = 0.55f)
        {
            if (cam == null) cam = GetComponent<Camera>();
            if (cam == null) return;
            if (!hasHome) CaptureHome();

            Vector3 target = cam.transform.position;
            target.x = worldPos.x;
            target.y = worldPos.y;
            float ortho = Mathf.Max(1.2f, orthoSize);

            if (moveRoutine != null) StopCoroutine(moveRoutine);
            moveRoutine = StartCoroutine(LerpCam(target, ortho, duration));
        }

        public void Restore(float duration = 0.45f)
        {
            if (cam == null || !hasHome) return;
            if (moveRoutine != null) StopCoroutine(moveRoutine);
            moveRoutine = StartCoroutine(LerpCam(homePos, homeOrtho, duration));
        }

        IEnumerator LerpCam(Vector3 toPos, float toOrtho, float duration)
        {
            Vector3 fromPos = cam.transform.position;
            float fromOrtho = cam.orthographicSize;
            float t = 0f;
            float dur = Mathf.Max(0.01f, duration);
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / dur));
                cam.transform.position = Vector3.Lerp(fromPos, toPos, u);
                cam.orthographicSize = Mathf.Lerp(fromOrtho, toOrtho, u);
                yield return null;
            }
            cam.transform.position = toPos;
            cam.orthographicSize = toOrtho;
            moveRoutine = null;
        }
    }
}
