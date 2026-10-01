using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Yoegoe.Characters;
using Yoegoe.Data;
using Yoegoe.UI;

namespace Yoegoe.Minigames.Yut
{
    /// <summary>던지기 영역·윷가락 그림·던지기/착지 연출(플레이어·이무기). ThrowSticks는 출석 윷점과 공용. (YutMiniGame 분할 — 본체는 YutMiniGame.cs)</summary>
    public partial class YutMiniGame
    {
        static void EnsureStickSprites()
        {
            if (_stickFront == null)
                _stickFront = Resources.Load<Sprite>("UI/YutPieces/YutStick_Front");
            if (_stickFrontBaekdo == null)
                _stickFrontBaekdo = Resources.Load<Sprite>("UI/YutPieces/YutStick_FrontBaekdo");
            if (_stickBack == null)
                _stickBack = Resources.Load<Sprite>("UI/YutPieces/YutStick_Back");
        }

        static void ApplyStickFace(Image img, bool front, bool isBaekdoStick)
        {
            EnsureStickSprites();
            Sprite sprite = null;
            if (front)
                sprite = isBaekdoStick && _stickFrontBaekdo != null ? _stickFrontBaekdo : _stickFront;
            else
                sprite = _stickBack;

            if (sprite != null)
            {
                img.sprite = sprite;
                img.color = Color.white;
                img.preserveAspect = true;
            }
            else
            {
                img.sprite = null;
                img.color = front ? YutStickFront : YutStickBack;
            }

            // 자식 빽도 점(에셋 폴백용)은 앞면일 때만
            var mark = img.transform.Find("BaekdoMark");
            if (mark != null) mark.gameObject.SetActive(front);
        }

        void ForwardSwipeThrow(float power) => OnThrowPressed?.Invoke(power);

        /// <summary>탭 버튼 대신 아래→위 슬라이드로 던지는 입력 영역. 손 모양 힌트가 살짝 위아래로
        /// 통통 튀어서 "여기서 위로 밀어라"를 안내한다.</summary>
        void EnsureThrowSwipeZone()
        {
            if (_throwZone != null) return;

            // 보드(_boardRoot) 아래쪽 가장자리 근처에 붙인다. Prefab에 있으면 그 레이아웃을 유지하고,
            // 없을 때만 아래 기본 좌표/색으로 새로 만든다.
            // ※ Prefab에 이미 있으면 여기 앵커 숫자는 절대 적용 안 됨 — 크기 조절은 Prefab/씬의
            //   ThrowSwipeZone RectTransform을 직접 고쳐야 한다.
            var rt = FindOrCreatePanel(transform, "ThrowSwipeZone", 0.1f, 0.08f, 0.9f, 0.30f,
                new Color(0.12f, 0.22f, 0.18f, 0.92f), out bool created);
            _throwZone = rt.gameObject;
            // 보드·로스터보다 뒤에 그려지면(형제 순서가 앞이면) 영역을 키워도 클릭/슬라이드를
            // 보드가 가로챈다 — 놀이기록 패널 바로 앞에 두어 입력 우선권을 확보한다.
            BringThrowZoneAboveBoard(rt);

            if (!created)
            {
                _throwSwipe = rt.GetComponent<YutThrowSwipeZone>();
                if (_throwSwipe == null)
                    _throwSwipe = _throwZone.AddComponent<YutThrowSwipeZone>();
                _throwSwipe.OnSwipeThrow -= ForwardSwipeThrow;
                _throwSwipe.OnSwipeThrow += ForwardSwipeThrow;
                if (rt.Find("IdleStick0") == null)
                    BuildIdleThrowSticks(rt);
                if (Application.isPlaying)
                {
                    var existingLabelRt = rt.Find("Label") as RectTransform;
                    if (existingLabelRt != null)
                        StartCoroutine(BounceHint(existingLabelRt));
                }
                return;
            }

            BuildIdleThrowSticks(_throwZone.transform);

            var label = CreateText(_throwZone.transform, "Label", "↑ 위로 슬라이드해서 던지기", 30, TextAnchor.LowerCenter);
            SetAnchor(label.rectTransform, 0f, 0f, 1f, 0.34f, 0, 0, 0, 0);
            label.raycastTarget = false;

            _throwSwipe = _throwZone.AddComponent<YutThrowSwipeZone>();
            _throwSwipe.OnSwipeThrow += ForwardSwipeThrow;

            if (Application.isPlaying)
                StartCoroutine(BounceHint(label.rectTransform));
        }

        /// <summary>ThrowSwipeZone을 YutBoard/Roster 위, PlayLog 아래에 둔다 — 영역을 키워도 입력이 먹히게.</summary>
        void BringThrowZoneAboveBoard(RectTransform throwRt)
        {
            if (throwRt == null) return;
            var playLog = transform.Find("PlayLogPanel");
            if (playLog != null)
                throwRt.SetSiblingIndex(playLog.GetSiblingIndex());
            else
                throwRt.SetAsLastSibling();
        }

        /// <summary>
        /// 던지기 전 대기 상태의 윷가락 4개 — 실제로 던져질 때(PlayThrowAnim)와 같은 에셋을 써서
        /// "여기 놓인 진짜 윷을 집어 던진다"는 느낌을 준다. 던지는 순간엔 SetThrowVisible(false)로
        /// 이 존 전체가 꺼지고, PlayThrowAnim이 별도 스틱을 만들어 애니메이션하므로 서로 안 겹친다.
        /// </summary>
        void BuildIdleThrowSticks(Transform parent)
        {
            EnsureStickSprites();
            const float stickW = 20f, stickH = 86f, gap = 14f;
            float totalW = stickW * 4 + gap * 3;
            float startX = -totalW / 2f + stickW / 2f;
            for (int i = 0; i < 4; i++)
            {
                var go = new GameObject($"IdleStick{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(parent, false);
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.72f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(stickW, stickH);
                rt.anchoredPosition = new Vector2(startX + i * (stickW + gap), 0f);
                var img = go.GetComponent<Image>();
                ApplyStickFace(img, front: true, isBaekdoStick: i == 0);
                if (i == 0 && (_stickFrontBaekdo == null || img.sprite != _stickFrontBaekdo))
                {
                    var markGo = new GameObject("BaekdoMark", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    markGo.transform.SetParent(rt, false);
                    var markRt = markGo.GetComponent<RectTransform>();
                    markRt.anchorMin = new Vector2(0.5f, 0.85f);
                    markRt.anchorMax = new Vector2(0.5f, 0.85f);
                    markRt.sizeDelta = new Vector2(8f, 8f);
                    markGo.GetComponent<Image>().color = BaekdoMarkColor;
                }
                img.raycastTarget = false;
            }
        }

        IEnumerator BounceHint(RectTransform rt)
        {
            while (rt != null)
            {
                float bounce = Mathf.Sin(Time.unscaledTime * 2.4f) * 6f;
                rt.anchoredPosition = new Vector2(0, bounce);
                yield return null;
            }
        }

        /// <summary>
        /// 대화가 전부 말풍선(피스 위)으로 옮겨가면서 예전 LogBar(대화 스크롤창)는 없앴다 — 이무기
        /// 던지기 결과만 보여주던 미니 윷가락은 이 작은 자리 하나로 옮겨서 그대로 유지한다.
        /// 던질 때만 켜지고 평소엔 꺼져 있다.
        /// </summary>
        void EnsureOpponentMiniThrowPanel()
        {
            if (_miniThrowContainer != null) return;

            var rt = FindOrCreatePanel(transform, "OpponentMiniThrow", 0.06f, 0.8f, 0.26f, 0.9f,
                new Color(0.08f, 0.1f, 0.16f, 0.92f), out bool created);
            _miniThrowContainer = rt.gameObject;

            if (!created)
            {
                _miniThrowSticks = new Image[4];
                for (int i = 0; i < 4; i++)
                    _miniThrowSticks[i] = rt.Find($"Stick{i}")?.GetComponent<Image>();
                return;
            }

            _miniThrowSticks = new Image[4];
            for (int i = 0; i < 4; i++)
            {
                var go = new GameObject($"Stick{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(rt, false);
                var srt = go.GetComponent<RectTransform>();
                float slotW = 1f / 4;
                srt.anchorMin = new Vector2(i * slotW + slotW * 0.12f, 0.1f);
                srt.anchorMax = new Vector2((i + 1) * slotW - slotW * 0.12f, 0.9f);
                srt.offsetMin = Vector2.zero;
                srt.offsetMax = Vector2.zero;
                var img = go.GetComponent<Image>();
                img.preserveAspect = true;
                ApplyStickFace(img, front: true, isBaekdoStick: i == 0);
                _miniThrowSticks[i] = img;
            }
            _miniThrowContainer.SetActive(false);
        }

        /// <summary>
        /// 이무기가 던질 때, 보드 한가운데 큰 연출 대신 초상 밑 작은 자리에서 결과를 보여준다.
        /// 윷가락 4개가 잠깐 흔들리다 결과에 맞는 앞/뒷면을 드러낸다.
        /// </summary>
        public IEnumerator PlayOpponentMiniThrowAnim(YutThrowResult result)
        {
            EnsureBoard();
            if (_miniThrowContainer == null) yield break;

            _miniThrowContainer.SetActive(true);
            yield return ShakeRevealSticks(_miniThrowSticks, result);
            yield return new WaitForSecondsRealtime(0.5f);

            _miniThrowContainer.SetActive(false);
        }

        /// <summary>
        /// 이무기 던지기 연출 (윷놀이·출석 윷점 공용): 윷가락 4개가 잠깐 빠르게 흔들리다 결과에 맞는 앞/뒷면을 드러낸다.
        /// sticks = 0번이 빽도 가락인 4개.
        /// </summary>
        public static IEnumerator ShakeRevealSticks(Image[] sticks, YutThrowResult result, float shakeSeconds = 0.35f)
        {
            EnsureStickSprites();
            var frontStates = DetermineFrontStates(result);
            for (int i = 0; i < 4; i++)
                ApplyStickFace(sticks[i], front: true, isBaekdoStick: i == 0);

            float t = 0f;
            while (t < shakeSeconds)
            {
                t += Time.unscaledDeltaTime;
                for (int i = 0; i < 4; i++)
                {
                    var rt = sticks[i].rectTransform;
                    rt.localRotation = Quaternion.Euler(0, 0, Mathf.Sin((Time.unscaledTime + i) * 28f) * 12f);
                }
                yield return null;
            }

            for (int i = 0; i < 4; i++)
            {
                ApplyStickFace(sticks[i], frontStates[i], isBaekdoStick: i == 0);
                sticks[i].rectTransform.localRotation = Quaternion.identity;
            }
        }

        /// <summary>parent 가운데에 윷가락 4개를 가로로 세운다 (동적 콘텐츠). sticksOut에 RectTransform을 담는다.</summary>
        public static Image[] CreateStickRow(RectTransform parent, RectTransform[] sticksOut,
            float stickWidth = 22f, float stickHeight = 110f, float spacing = 52f)
        {
            EnsureStickSprites();
            var imgs = new Image[4];
            for (int i = 0; i < 4; i++)
            {
                var go = new GameObject($"YutStick{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(parent, false);
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(stickWidth, stickHeight);
                rt.anchoredPosition = new Vector2((i - 1.5f) * spacing, 0f);
                imgs[i] = go.GetComponent<Image>();
                imgs[i].raycastTarget = false;
                imgs[i].preserveAspect = true;
                ApplyStickFace(imgs[i], front: true, isBaekdoStick: i == 0);
                sticksOut[i] = rt;
            }
            return imgs;
        }

        /// <summary>
        /// 윷가락 4개를 던져서 흩뿌리는 연출. 결과(result)에 맞는 앞/뒤 패턴으로 착지한다 —
        /// 뒤집힌 가락 개수 = 0(모)/1(도·빽도)/2(개)/3(걸)/4(윷). 0번 가락은 빨간 점으로
        /// 표시된 "빽도 가락"이라, 1개만 뒤집혔을 때 그게 0번이면 빽도, 다른 가락이면 도로
        /// 갈린다(기획서 7-4 "빽도 가락만 엎어진 경우" 기준). 어느 가락이 뒤집힐지는 개/걸에서만
        /// 랜덤이고 개수는 항상 결과와 일치한다.
        /// power(0~1)는 슬라이드 던지기 속도 — 아치 높이·회전·착지 퍼짐만 키우고 줄인다.
        /// 시작=ThrowSwipeZone, 착지=YutBoard, 대기=North — Prefab/Scene 레이아웃을 따른다.
        /// </summary>
        public IEnumerator PlayThrowAnim(YutThrowResult result, float power = 1f)
        {
            EnsureBoard();
            EnsureThrowSwipeZone();
            ClearParkedSticks();

            var panel = (RectTransform)transform;
            Vector2 WorldToPanelLocal(Vector3 world) => panel.InverseTransformPoint(world);

            var throwRt = _throwZone != null ? (RectTransform)_throwZone.transform : null;
            var landZone = _boardRoot != null ? _boardRoot : panel;
            // 대기 윷(IdleStick)이 있는 Throw 존 위쪽 중앙에서 출발 — Scene 레이아웃 따름
            Vector2 originLocal = throwRt != null
                ? WorldToPanelLocal(ZoneNormToWorld(throwRt, new Vector2(0.5f, 0.72f)))
                : WorldToPanelLocal(ZoneNormToWorld(landZone, new Vector2(0.5f, 0.05f)));

            var sticks = new RectTransform[4];
            yield return ThrowSticks(this, panel, originLocal, landZone, result, power, sticks);

            yield return new WaitForSecondsRealtime(0.35f);

            // Prefab/Scene의 Quadrant_North 레이아웃을 따른다 — 고정 보드 좌표로 보내지 않음.
            var thrownZone = GetQuadrant(YutBoardQuadrant.North);
            yield return ParkSticksInto(sticks, thrownZone);
            _parkedSticks = sticks;
        }

        /// <summary>
        /// 윷가락 4개를 panel 위 originLocal에서 던져 landZone 안에 착지시킨다 — 윷놀이·출석 윷점 공용 연출.
        /// 결과(result)에 맞는 앞/뒷면으로 떨어지고, 만든 가락은 sticksOut(길이 4)에 담긴다(정리는 호출측).
        /// </summary>
        public static IEnumerator ThrowSticks(MonoBehaviour host, RectTransform panel, Vector2 originLocal,
            RectTransform landZone, YutThrowResult result, float power, RectTransform[] sticksOut)
        {
            EnsureStickSprites();
            System.Func<Vector3, Vector2> WorldToPanelLocal = world => panel.InverseTransformPoint(world);
            var frontStates = DetermineFrontStates(result);

            for (int i = 0; i < 4; i++)
            {
                var go = new GameObject($"YutStick{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(panel, false);
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(22f, 110f);
                rt.anchoredPosition = originLocal;
                var img = go.GetComponent<Image>();
                img.raycastTarget = false;
                ApplyStickFace(img, front: true, isBaekdoStick: i == 0);
                // 에셋에 빽도 점이 없으면 예전처럼 빨간 점 자식
                if (i == 0 && (_stickFrontBaekdo == null || img.sprite != _stickFrontBaekdo))
                {
                    var markGo = new GameObject("BaekdoMark", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    markGo.transform.SetParent(rt, false);
                    var markRt = markGo.GetComponent<RectTransform>();
                    markRt.anchorMin = new Vector2(0.5f, 0.85f);
                    markRt.anchorMax = new Vector2(0.5f, 0.85f);
                    markRt.sizeDelta = new Vector2(8f, 8f);
                    var markImg = markGo.GetComponent<Image>();
                    markImg.color = BaekdoMarkColor;
                    markImg.raycastTarget = false;
                }
                go.transform.SetAsLastSibling();
                sticksOut[i] = rt;
            }

            var routines = new Coroutine[4];
            for (int i = 0; i < 4; i++)
                routines[i] = host.StartCoroutine(ThrowOneStick(
                    sticksOut[i], originLocal, landZone, WorldToPanelLocal,
                    i * 0.05f, frontStates[i], isBaekdoStick: i == 0, power));
            for (int i = 0; i < 4; i++)
                yield return routines[i];
        }

        IEnumerator ParkSticksInto(RectTransform[] sticks, RectTransform zone)
        {
            if (zone == null) yield break;

            var starts = new Vector3[sticks.Length];
            var startRotations = new Quaternion[sticks.Length];
            var startSizes = new Vector2[sticks.Length];
            var targets = new Vector3[sticks.Length];
            for (int i = 0; i < sticks.Length; i++)
            {
                starts[i] = sticks[i].position;
                startRotations[i] = sticks[i].localRotation;
                startSizes[i] = sticks[i].sizeDelta;
                targets[i] = ZoneNormToWorld(zone, ParkSpotsInZone[i]);
            }

            const float duration = 0.3f;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / duration);
                for (int i = 0; i < sticks.Length; i++)
                {
                    sticks[i].position = Vector3.Lerp(starts[i], targets[i], u);
                    sticks[i].localRotation = Quaternion.Slerp(startRotations[i], Quaternion.identity, u);
                    sticks[i].sizeDelta = Vector2.Lerp(startSizes[i], ParkedStickSize, u);
                }
                yield return null;
            }

            for (int i = 0; i < sticks.Length; i++)
            {
                sticks[i].SetParent(zone, worldPositionStays: true);
                sticks[i].sizeDelta = ParkedStickSize;
                sticks[i].localRotation = Quaternion.identity;
                sticks[i].anchorMin = sticks[i].anchorMax = new Vector2(0.5f, 0.5f);
                sticks[i].pivot = new Vector2(0.5f, 0.5f);
                sticks[i].anchoredPosition = ZoneNormToLocal(zone, ParkSpotsInZone[i]);
            }
        }

        static Vector2 ZoneNormToLocal(RectTransform zone, Vector2 norm)
        {
            var r = zone.rect;
            return new Vector2((norm.x - 0.5f) * r.width, (norm.y - 0.5f) * r.height);
        }

        static Vector3 ZoneNormToWorld(RectTransform zone, Vector2 norm)
            => zone.TransformPoint(ZoneNormToLocal(zone, norm));

        void ClearParkedSticks()
        {
            if (_parkedSticks == null) return;
            foreach (var rt in _parkedSticks)
                if (rt != null) Destroy(rt.gameObject);
            _parkedSticks = null;
        }

        static bool[] DetermineFrontStates(YutThrowResult result)
        {
            var front = new[] { true, true, true, true }; // 기본: 4개 다 정상면(뒤집히지 않음)
            switch (result)
            {
                case YutThrowResult.Mo:
                    break; // 0개 뒤집힘
                case YutThrowResult.Baekdo:
                    front[0] = false; // 빽도 가락(0번)만 뒤집힘
                    break;
                case YutThrowResult.Do:
                    front[1 + UnityEngine.Random.Range(0, 3)] = false; // 빽도 가락 제외, 나머지 중 1개만
                    break;
                case YutThrowResult.Gae:
                    FlipRandom(front, 2);
                    break;
                case YutThrowResult.Geol:
                    FlipRandom(front, 3);
                    break;
                case YutThrowResult.Yut:
                    for (int i = 0; i < front.Length; i++) front[i] = false; // 4개 다 뒤집힘
                    break;
            }
            return front;
        }

        static void FlipRandom(bool[] front, int count)
        {
            var indices = new List<int> { 0, 1, 2, 3 };
            for (int i = 0; i < count; i++)
            {
                int pick = UnityEngine.Random.Range(0, indices.Count);
                front[indices[pick]] = false;
                indices.RemoveAt(pick);
            }
        }

        static IEnumerator ThrowOneStick(
            RectTransform rt,
            Vector2 startLocal,
            RectTransform landZone,
            System.Func<Vector3, Vector2> worldToPanelLocal,
            float delay,
            bool targetFront,
            bool isBaekdoStick,
            float power)
        {
            if (delay > 0f)
                yield return new WaitForSecondsRealtime(delay);

            // power(슬라이드 속도, 0~1)가 클수록 보드 안에서 더 멀리·높이·세게 — 확률과 무관, 연출 전용.
            // 착지 좌표는 YutBoard(landZone) 로컬 0~1 기준이라 Scene에서 보드를 옮겨도 따라간다.
            float spreadX = Mathf.Lerp(0.12f, 0.38f, power);
            float yMin = Mathf.Lerp(0.22f, 0.28f, power);
            float yMax = Mathf.Lerp(0.48f, 0.78f, power);
            var landNorm = new Vector2(
                Mathf.Clamp01(0.5f + UnityEngine.Random.Range(-spreadX, spreadX)),
                UnityEngine.Random.Range(yMin, yMax));
            Vector2 endLocal = landZone != null
                ? worldToPanelLocal(ZoneNormToWorld(landZone, landNorm))
                : startLocal + new Vector2(0f, 220f);

            float zoneH = landZone != null ? Mathf.Abs(landZone.rect.height) : 400f;
            float arcHeight = Mathf.Lerp(zoneH * 0.22f, zoneH * 0.55f, power)
                + UnityEngine.Random.Range(-15f, 15f);
            float spin = (Mathf.Lerp(480f, 1300f, power) + UnityEngine.Random.Range(-60f, 60f))
                * (UnityEngine.Random.value < 0.5f ? -1f : 1f);
            float duration = Mathf.Lerp(0.65f, 0.42f, power);

            Vector2 start = startLocal;
            Vector2 end = endLocal;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / duration);
                float eu = 1f - (1f - u) * (1f - u);
                var pos = Vector2.Lerp(start, end, eu);
                pos.y += arcHeight * 4f * u * (1f - u);
                rt.anchoredPosition = pos;
                rt.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(0, spin, u));
                yield return null;
            }
            rt.anchoredPosition = end;

            bool front = targetFront;
            var img = rt.GetComponent<Image>();
            const float flipDuration = 0.12f;
            float flipT = 0f;
            while (flipT < flipDuration)
            {
                flipT += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(flipT / flipDuration);
                rt.localScale = new Vector3(Mathf.Abs(Mathf.Cos(u * Mathf.PI)), 1f, 1f);
                if (u >= 0.5f)
                    ApplyStickFace(img, front, isBaekdoStick);
                yield return null;
            }
            rt.localScale = Vector3.one;
            ApplyStickFace(img, front, isBaekdoStick);

            const float settleDuration = 0.18f;
            float settleT = 0f;
            Vector2 settled = rt.anchoredPosition;
            while (settleT < settleDuration)
            {
                settleT += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(settleT / settleDuration);
                float bounce = Mathf.Sin(u * Mathf.PI) * 10f * (1f - u);
                rt.anchoredPosition = settled + new Vector2(0, bounce);
                yield return null;
            }
            rt.anchoredPosition = settled;
        }
    }
}
