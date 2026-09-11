using System.Collections;
using UnityEngine;
using Yoegoe.Data;
using Yoegoe.UI;

namespace Yoegoe.Characters
{
    // 혼잣말 말풍선(6-4장), 임시 대사 시퀀스, 넋 탭 리액션.
    // 단일 탭: 표시·순환·10초 연장. 더블탭: 상세화면 (말풍선은 더블탭 판정 후에만).
    public partial class CharacterAgent
    {
        [Header("혼잣말 (6-4장)")]
        [Tooltip("혼잣말 말풍선에 쓸 한글 폰트. 비워두면 유니티 기본 폰트로 나와서 한글이 깨질 수 있음.")]
        public Font bubbleFont;

        private TextMesh bubbleTextMesh;
        private SpriteRenderer bubbleBg;
        private float monologueTimer;
        private bool monologueShowing;
        private int monologueIndex;
        private const float MonologueDisplaySeconds = 10f;
        private const float MonologueMinInterval = 30f;
        private const float MonologueMaxInterval = 60f;

        private static Sprite sharedBubbleSprite;
        Coroutine neokTapRoutine;
        Coroutine tempSpeechRoutine;
        private bool showingFaintedEllipsis;
        private const string FaintedBubbleText = "...";

        private bool CanShowMonologue =>
            Stats.State == ActionState.Walking
            || Stats.State == ActionState.Playing
            || Stats.State == ActionState.Staying;

        private bool CanTapMonologue =>
            CanShowMonologue || Stats.State == ActionState.Slumped;

        private void UpdateMonologue(float dt)
        {
            if (Stats.State == ActionState.Fainted)
            {
                if (!showingFaintedEllipsis) ShowFaintedEllipsis();
                else FollowBubblePosition();
                return;
            }

            if (showingFaintedEllipsis)
            {
                showingFaintedEllipsis = false;
                HideMonologue();
            }

            if (HasOfferingRequest || Requests.HasVisiblePropRequest) return;
            if (Data == null || Data.monologueLines == null || Data.monologueLines.Length == 0) return;

            FollowBubblePosition();

            if (monologueShowing)
            {
                monologueTimer -= dt;
                if (monologueTimer <= 0f) HideMonologue();
                return;
            }

            // 자동 팝업은 걷기/놀기/머물기에서만 (주저앉기는 탭만)
            if (!CanShowMonologue) return;

            monologueTimer -= dt;
            if (monologueTimer <= 0f) ShowMonologue();
        }

        void FollowBubblePosition()
        {
            if (!monologueShowing || bubbleTextMesh == null) return;
            float spriteTop = spriteRenderer != null ? spriteRenderer.bounds.extents.y : 0.3f;
            Vector3 bubblePos = transform.position + Vector3.up * (spriteTop + 0.55f);
            bubbleTextMesh.transform.position = bubblePos;
            if (bubbleBg != null) bubbleBg.transform.position = bubblePos;
        }

        /// <summary>
        /// 단일 탭 확정 시(더블탭이 아님) MapPointerRouter가 호출.
        /// 넋: 통통·깜빡 리액션(보상 없음). 기절: "..." 갱신. 주저앉기·혼: 혼잣말.
        /// </summary>
        public void OnTapped()
        {
            if (Stats.Stage == GrowthStage.Neok)
            {
                PlayNeokTapReact();
                return;
            }

            if (Stats.State == ActionState.Fainted)
            {
                ShowFaintedEllipsis();
                return;
            }

            if (!CanTapMonologue) return;
            if (Data == null || Data.monologueLines == null || Data.monologueLines.Length == 0) return;
            ShowMonologue();
        }

        /// <summary>기절 중 머리 위 말풍선. 탭하면 같은 문구로 갱신.</summary>
        void ShowFaintedEllipsis()
        {
            showingFaintedEllipsis = true;
            EnsureBubble();
            bubbleTextMesh.text = FaintedBubbleText;
            bubbleTextMesh.gameObject.SetActive(true);
            bubbleBg.gameObject.SetActive(true);

            var renderer = bubbleTextMesh.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sortingOrder = 1001;
                Bounds bounds = renderer.bounds;
                bubbleBg.transform.localScale = new Vector3(bounds.size.x + 0.3f, bounds.size.y + 0.18f, 1f);
            }

            monologueShowing = true;
            monologueTimer = float.PositiveInfinity;
            FollowBubblePosition();
        }

        /// <summary>9-2: 넋 탭 시 비언어 리액션 + 살짝 줌.</summary>
        void PlayNeokTapReact()
        {
            if (neokTapRoutine != null) StopCoroutine(neokTapRoutine);
            neokTapRoutine = StartCoroutine(NeokTapReactRoutine());
        }

        IEnumerator NeokTapReactRoutine()
        {
            var focus = Yoegoe.Debugging.MapCameraFocus.Instance;
            if (focus != null)
            {
                var cam = Camera.main;
                float ortho = cam != null ? cam.orthographicSize * 0.72f : 3.2f;
                focus.Focus(transform.position, ortho, 0.28f);
            }

            Vector3 baseScale = transform.localScale;
            var sr = GetComponentInChildren<SpriteRenderer>();
            var mr = GetComponent<MeshRenderer>();
            Color baseColor = Color.white;
            if (sr != null)
            {
                baseColor = sr.color;
            }
            else if (mr != null && mr.material != null)
            {
                if (mr.material.HasProperty("_BaseColor"))
                    baseColor = mr.material.GetColor("_BaseColor");
                else if (mr.material.HasProperty("_Color"))
                    baseColor = mr.material.color;
            }

            // 통통 2회 + 깜빡
            for (int i = 0; i < 2; i++)
            {
                float t = 0f;
                const float half = 0.12f;
                while (t < half)
                {
                    t += Time.deltaTime;
                    float u = Mathf.Sin(Mathf.Clamp01(t / half) * Mathf.PI);
                    transform.localScale = baseScale * (1f + 0.22f * u);
                    float a = 0.55f + 0.45f * (1f - u);
                    if (sr != null)
                    {
                        var c = baseColor;
                        c.a = a;
                        sr.color = c;
                    }
                    else
                        SetMeshAlpha(mr, a);
                    yield return null;
                }
            }

            transform.localScale = baseScale;
            if (sr != null)
            {
                sr.color = baseColor;
            }
            else if (mr != null && mr.material != null)
            {
                if (mr.material.HasProperty("_BaseColor")) mr.material.SetColor("_BaseColor", baseColor);
                if (mr.material.HasProperty("_Color")) mr.material.color = baseColor;
            }

            if (focus != null) focus.Restore(0.35f);
            neokTapRoutine = null;
        }

        private void ShowMonologue()
        {
            EnsureBubble();
            bubbleTextMesh.text = PickMonologueLine();
            bubbleTextMesh.gameObject.SetActive(true);
            bubbleBg.gameObject.SetActive(true);

            // 배경 판을 텍스트 실제 크기에 맞춰 다시 그림 (말풍선처럼 보이게).
            // 배경과 텍스트는 서로 형제 오브젝트라, 배경 스케일을 바꿔도 텍스트 크기엔 영향 없음.
            var renderer = bubbleTextMesh.GetComponent<MeshRenderer>();
            renderer.sortingOrder = 1001; // 상태 점(1000)보다 위
            Bounds bounds = renderer.bounds;
            bubbleBg.transform.localScale = new Vector3(bounds.size.x + 0.3f, bounds.size.y + 0.18f, 1f);

            monologueShowing = true;
            monologueTimer = MonologueDisplaySeconds;
        }

        /// <summary>혼잣말1 → 2 → 3 … 순서로 순환.</summary>
        private string PickMonologueLine()
        {
            var lines = Data.monologueLines;
            if (lines.Length == 0) return string.Empty;
            if (monologueIndex < 0 || monologueIndex >= lines.Length) monologueIndex = 0;
            string line = lines[monologueIndex];
            monologueIndex = (monologueIndex + 1) % lines.Length;
            return line;
        }

        private void HideMonologue()
        {
            if (bubbleTextMesh != null) bubbleTextMesh.gameObject.SetActive(false);
            if (bubbleBg != null) bubbleBg.gameObject.SetActive(false);
            monologueShowing = false;
            showingFaintedEllipsis = false;
            monologueTimer = Random.Range(MonologueMinInterval, MonologueMaxInterval);
        }

        public void HideMonologueForRequest() => HideMonologue();

        /// <summary>요구 완료 등 짧은 대사.</summary>
        public void ShowTempSpeech(string line)
        {
            if (string.IsNullOrEmpty(line)) return;
            ShowTempSpeechSequence(new[] { line }, null);
        }

        /// <summary>대사를 순서대로 표시한 뒤 onComplete 호출. 요구→꾸러미 연출용.</summary>
        public void ShowTempSpeechSequence(string[] lines, System.Action onComplete)
        {
            if (lines == null || lines.Length == 0)
            {
                onComplete?.Invoke();
                return;
            }
            if (tempSpeechRoutine != null) StopCoroutine(tempSpeechRoutine);
            tempSpeechRoutine = StartCoroutine(TempSpeechSequenceRoutine(lines, onComplete));
        }

        IEnumerator TempSpeechSequenceRoutine(string[] lines, System.Action onComplete)
        {
            const float secondsPerLine = 2.8f;
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (string.IsNullOrEmpty(line)) continue;
                yield return TempSpeechRoutine(line, secondsPerLine, hideAtEnd: true);
            }
            tempSpeechRoutine = null;
            onComplete?.Invoke();
        }

        IEnumerator TempSpeechRoutine(string line, float duration, bool hideAtEnd)
        {
            EnsureBubble();
            bubbleTextMesh.text = line;
            bubbleTextMesh.gameObject.SetActive(true);
            bubbleBg.gameObject.SetActive(true);
            var renderer = bubbleTextMesh.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sortingOrder = 1001;
                Bounds bounds = renderer.bounds;
                bubbleBg.transform.localScale = new Vector3(bounds.size.x + 0.3f, bounds.size.y + 0.18f, 1f);
            }
            monologueShowing = true;
            monologueTimer = duration;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                if (bubbleTextMesh != null)
                {
                    float spriteTop = spriteRenderer != null ? spriteRenderer.bounds.extents.y : 0.3f;
                    Vector3 bubblePos = transform.position + Vector3.up * (spriteTop + 0.55f);
                    bubbleTextMesh.transform.position = bubblePos;
                    if (bubbleBg != null) bubbleBg.transform.position = bubblePos;
                }
                yield return null;
            }
            if (hideAtEnd) HideMonologue();
        }

        /// <summary>말풍선 배경용 1색 스프라이트 (디버그 점 제거 후에도 말풍선이 씀).</summary>
        private static Sprite GetSharedDotSprite()
        {
            if (sharedBubbleSprite != null) return sharedBubbleSprite;
            var tex = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color[64];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
            tex.SetPixels(pixels);
            tex.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            sharedBubbleSprite = Sprite.Create(tex, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f), 8f);
            return sharedBubbleSprite;
        }

        private void EnsureBubble()
        {
            if (bubbleTextMesh != null) return;

            var bgGo = new GameObject(gameObject.name + "_BubbleBg");
            bubbleBg = bgGo.AddComponent<SpriteRenderer>();
            bubbleBg.sprite = GetSharedDotSprite();
            bubbleBg.color = new Color(1f, 1f, 0.96f, 0.92f);
            bubbleBg.sortingOrder = 1000;
            bgGo.SetActive(false);

            var textGo = new GameObject(gameObject.name + "_Bubble");
            bubbleTextMesh = textGo.AddComponent<TextMesh>();
            bubbleTextMesh.characterSize = 0.045f;
            bubbleTextMesh.fontSize = UiFonts.Size(48);
            bubbleTextMesh.anchor = TextAnchor.MiddleCenter;
            bubbleTextMesh.alignment = TextAlignment.Center;
            bubbleTextMesh.color = new Color(0.15f, 0.1f, 0.08f);
            if (bubbleFont != null)
            {
                bubbleTextMesh.font = bubbleFont;
                textGo.GetComponent<MeshRenderer>().material = bubbleFont.material;
            }
            textGo.GetComponent<MeshRenderer>().sortingOrder = 1001;
            textGo.SetActive(false);
        }
    }
}
