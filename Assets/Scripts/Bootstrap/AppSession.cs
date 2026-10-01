using System;
using System.Runtime.InteropServices;
using UnityEngine;
using Yoegoe.Characters;
using Yoegoe.Core;
using Yoegoe.Economy;
using Yoegoe.Save;
using Yoegoe.UI;

namespace Yoegoe.Bootstrap
{
    /// <summary>
    /// 앱 세션: 벽시계 정산, pause/focus 세이브, 출석 팝업, WebGL 로딩 오버레이.
    /// Unity 콜백은 <see cref="Main"/>에 두고 여기로 위임한다.
    /// </summary>
    public sealed class AppSession
    {
        /// <summary>이보다 긴 벽시계 공백이면 캐릭터 정산을 돌린다 (WebGL 탭 숨김 등).</summary>
        const float WallClockCatchUpThresholdSeconds = 1f;

        DateTime lastActiveUtc;
        bool worldReady;
        readonly Font hudFont;

        public AppSession(Font hudFont)
        {
            this.hudFont = hudFont;
            lastActiveUtc = TrustedTime.UtcNow;
        }

        public void MarkReady()
        {
            lastActiveUtc = TrustedTime.UtcNow;
            worldReady = true;
            Greeting.Request(); // 앱을 켜면 놀던 요괴들이 인사 (출석 윷점 대사 뒤)
        }

        public void Tick()
        {
            if (!worldReady) return;

            var now = TrustedTime.UtcNow;
            double gap = (now - lastActiveUtc).TotalSeconds;
            lastActiveUtc = now;

            GameEconomy.Instance?.EnsureYutTokenFresh(now);

            if (gap >= WallClockCatchUpThresholdSeconds)
            {
                float seconds = (float)Math.Min(gap, OfflineSimulator.MaxOfflineSeconds);
                CharacterAgent.CatchUpAll(seconds);
            }
            if (gap >= Greeting.AwaySecondsForGreeting) Greeting.Request();

            Greeting.Tick(AttendanceScreen.IsOpen);
        }

        public void OnPause(bool pause)
        {
            if (pause)
            {
                if (worldReady) GameSaveBridge.SaveFromWorld();
                return;
            }

            ApplyWallClockCatchUpIfNeeded();
            if (worldReady) AttendanceScreen.TryOpenIfDue(hudFont);
        }

        public void OnFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                if (worldReady) GameSaveBridge.SaveFromWorld();
                return;
            }

            ApplyWallClockCatchUpIfNeeded();
            if (worldReady) AttendanceScreen.TryOpenIfDue(hudFont);
        }

        public void OnQuit()
        {
            GameSaveBridge.SaveFromWorld();
        }

        public void TryOpenAttendanceIfDue()
        {
            AttendanceScreen.TryOpenIfDue(hudFont);
        }

        public void HideWebGlLoadingOverlay()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            YogoeHideLoadingOverlay();
#endif
        }

        void ApplyWallClockCatchUpIfNeeded()
        {
            if (!worldReady) return;

            var now = TrustedTime.UtcNow;
            double gap = (now - lastActiveUtc).TotalSeconds;
            lastActiveUtc = now;

            GameEconomy.Instance?.EnsureYutTokenFresh(now);

            if (gap < WallClockCatchUpThresholdSeconds) return;

            float seconds = (float)Math.Min(gap, OfflineSimulator.MaxOfflineSeconds);
            CharacterAgent.CatchUpAll(seconds);
            if (gap >= Greeting.AwaySecondsForGreeting) Greeting.Request();
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        static extern void YogoeHideLoadingOverlay();
#endif
    }
}
