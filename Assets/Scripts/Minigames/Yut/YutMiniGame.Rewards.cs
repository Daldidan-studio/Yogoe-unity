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
    /// <summary>특수 칸 아이콘·재배치 연출·보상 비행·획득 물건·완주 말 표시. (YutMiniGame 분할 — 본체는 YutMiniGame.cs)</summary>
    public partial class YutMiniGame
    {
        /// <summary>매 윷판(매치)마다 새로 뽑히는 특수 칸 구성을 반영한다 — 색부터 다시 칠하고
        /// (EnsureBoard는 세션당 한 번만 돌아서 두 번째 매치부터는 갱신 안 됨), 칸마다 어떤
        /// 보상인지 보여줄 아이콘(엽전/그 칸에 배정된 공양물/보물상자)을 붙인다. 보물상자는
        /// 안이 뭔지 숨기고 상자 아이콘만 보여준다(호출부가 그렇게 넘겨준다).</summary>
        public void RefreshSpecialSquareVisuals(IReadOnlyDictionary<int, Sprite> iconsByNode)
        {
            EnsureBoard();
            if (_pads == null) return;
            for (int i = 0; i < _pads.Length; i++)
            {
                RecolorPad(_pads[i], i);
                Sprite icon = iconsByNode != null && iconsByNode.TryGetValue(i, out var s) ? s : null;
                SetSpecialSquareIcon(i, icon);
            }
        }

        /// <summary>보드를 미세하게 흔든 뒤, 기존 보상 아이콘이 새 칸으로 슝 날아간다.
        /// 연출 중에는 from 칸 아이콘을 숨기고, 끝나면 호출부가 RefreshSpecialSquareVisuals로
        /// 최종 배치를 입힌다.</summary>
        public IEnumerator PlaySpecialSquaresReshuffleAnim(IReadOnlyList<SpecialSquareFlight> flights)
        {
            EnsureBoard();
            if (_boardRoot == null) yield break;

            yield return ShakeBoardRoutine(0.32f);

            if (flights == null || flights.Count == 0) yield break;

            var flyRoot = new GameObject("SpecialSquareFlights", typeof(RectTransform));
            flyRoot.transform.SetParent(transform, false);
            var flyRootRt = (RectTransform)flyRoot.transform;
            flyRootRt.anchorMin = Vector2.zero;
            flyRootRt.anchorMax = Vector2.one;
            flyRootRt.offsetMin = Vector2.zero;
            flyRootRt.offsetMax = Vector2.zero;
            flyRoot.transform.SetAsLastSibling();

            var flyers = new List<(RectTransform rt, Vector3 from, Vector3 to, Image img)>(flights.Count);
            for (int i = 0; i < flights.Count; i++)
            {
                var f = flights[i];
                SetSpecialSquareIcon(f.FromNode, null);
                if (_pads != null && f.FromNode >= 0 && f.FromNode < _pads.Length)
                    RecolorPadAsNormal(_pads[f.FromNode], f.FromNode);

                if (f.Icon == null) continue;
                Vector3 fromPos = PadWorldCenter(f.FromNode);
                Vector3 toPos = PadWorldCenter(f.ToNode);
                float size = PadPixelSize(f.FromNode);

                var go = new GameObject($"Fly_{f.FromNode}_{f.ToNode}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(flyRoot.transform, false);
                var rt = (RectTransform)go.transform;
                rt.sizeDelta = new Vector2(size, size);
                rt.position = fromPos;
                var img = go.GetComponent<Image>();
                img.sprite = f.Icon;
                img.preserveAspect = true;
                img.raycastTarget = false;
                img.color = Color.white;
                flyers.Add((rt, fromPos, toPos, img));
            }

            const float flyDuration = 0.48f;
            float t = 0f;
            while (t < flyDuration)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / flyDuration);
                // 초반에 튕기듯 가속했다가 착지 — 슝 느낌.
                float eased = u * u * (3f - 2f * u);
                float lift = Mathf.Sin(u * Mathf.PI) * 36f;
                for (int i = 0; i < flyers.Count; i++)
                {
                    var f = flyers[i];
                    if (f.rt == null) continue;
                    Vector3 p = Vector3.Lerp(f.from, f.to, eased);
                    p.y += lift;
                    f.rt.position = p;
                    // 살짝 축소했다가 착지 때 복귀.
                    float scale = 1f + 0.18f * Mathf.Sin(u * Mathf.PI);
                    f.rt.localScale = new Vector3(scale, scale, 1f);
                    if (f.img != null)
                    {
                        var c = f.img.color;
                        c.a = u < 0.85f ? 1f : Mathf.Lerp(1f, 0.35f, (u - 0.85f) / 0.15f);
                        f.img.color = c;
                    }
                }
                yield return null;
            }

            Destroy(flyRoot);
        }

        /// <summary>윷판 스트레치 앵커에서는 px 고정 흔들림이 거의 안 보이므로,
        /// 보드 크기 비율 + 미세 회전으로 짧은 진동감을 낸다.</summary>
        IEnumerator ShakeBoardRoutine(float duration)
        {
            if (_boardRoot == null) yield break;

            Vector2 restPos = _boardRoot.anchoredPosition;
            Quaternion restRot = _boardRoot.localRotation;
            float size = Mathf.Min(_boardRoot.rect.width, _boardRoot.rect.height);
            if (size < 1f) size = 400f;
            float amp = size * 0.012f; // 약 1.2% — 진동 느낌
            const float rotAmp = 0.9f; // 도
            const float freq = 28f; // Hz에 가까운 빠른 떨림

            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / duration);
                float damp = 1f - u;
                damp = Mathf.Sqrt(damp);
                // 랜덤 점프 대신 sin 기반 진동 + 아주 약한 노이즈.
                float wave = Mathf.Sin(t * freq * Mathf.PI * 2f);
                float noiseX = (UnityEngine.Random.value * 2f - 1f) * 0.25f;
                float noiseY = (UnityEngine.Random.value * 2f - 1f) * 0.25f;
                float ax = (wave + noiseX) * amp * damp;
                float ay = (Mathf.Cos(t * freq * Mathf.PI * 2f) + noiseY) * amp * damp * 0.85f;
                float rz = wave * rotAmp * damp;
                _boardRoot.anchoredPosition = restPos + new Vector2(ax, ay);
                _boardRoot.localRotation = Quaternion.Euler(0f, 0f, rz);
                yield return null;
            }

            _boardRoot.anchoredPosition = restPos;
            _boardRoot.localRotation = restRot;
        }

        Vector3 PadWorldCenter(int nodeId)
        {
            if (_pads == null || nodeId < 0 || nodeId >= _pads.Length || _pads[nodeId] == null)
                return Vector3.zero;
            return _pads[nodeId].rectTransform.position;
        }

        float PadPixelSize(int nodeId)
        {
            if (_pads == null || nodeId < 0 || nodeId >= _pads.Length || _pads[nodeId] == null)
                return 36f;
            var rt = _pads[nodeId].rectTransform;
            float w = rt.rect.width * Mathf.Abs(rt.lossyScale.x);
            float h = rt.rect.height * Mathf.Abs(rt.lossyScale.y);
            float m = Mathf.Min(w, h);
            return m > 1f ? m * 0.78f : 36f;
        }

        static void RecolorPadAsNormal(Image pad, int nodeId)
        {
            if (pad == null) return;
            pad.color = IsWaypoint(nodeId)
                ? new Color(0.7f, 0.55f, 0.3f, 0.85f)
                : new Color(0.35f, 0.32f, 0.28f, 0.9f);
        }

        void SetSpecialSquareIcon(int nodeId, Sprite icon)
        {
            if (_pads == null || nodeId < 0 || nodeId >= _pads.Length || _pads[nodeId] == null) return;
            var pad = _pads[nodeId];

            var iconT = pad.transform.Find("SpecialIcon");
            Image iconImg;
            if (iconT == null)
            {
                var go = new GameObject("SpecialIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(pad.transform, false);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = new Vector2(0.12f, 0.12f);
                rt.anchorMax = new Vector2(0.88f, 0.88f);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                iconImg = go.GetComponent<Image>();
                iconImg.preserveAspect = true;
                iconImg.raycastTarget = false;
            }
            else
            {
                iconImg = iconT.GetComponent<Image>();
            }

            iconImg.sprite = icon;
            iconImg.color = Color.white;
            iconImg.enabled = icon != null;
        }

        /// <summary>엽전 아이콘. Resources/UI/Currency/Yeopjeon.</summary>
        public static Sprite YeopjeonIcon()
        {
            if (_yeopjeonIcon == null)
                _yeopjeonIcon = Resources.Load<Sprite>("UI/Currency/Yeopjeon");
            return _yeopjeonIcon;
        }

        /// <summary>물 아이콘. 카탈로그 OfferingData, 없으면 Resources 폴백.</summary>
        public static Sprite WaterIcon()
        {
            if (_waterIcon != null) return _waterIcon;
            var offering = CharacterCatalog.FindOffering("water");
            if (offering != null && offering.icon != null)
                _waterIcon = offering.icon;
            if (_waterIcon == null)
                _waterIcon = Resources.Load<Sprite>("UI/Currency/Water");
            return _waterIcon;
        }

        /// <summary>보물상자를 연 상태 아이콘. 보상 팝업에서 "상자에서 뭐가 나왔는지" 보여줄 때 쓴다.</summary>
        public static Sprite TreasureChestOpenIcon()
        {
            if (_treasureChestOpenIcon == null)
                _treasureChestOpenIcon = Resources.Load<Sprite>("UI/GiftChest_Open");
            return _treasureChestOpenIcon;
        }

        /// <summary>재료보따리 칸 아이콘. 전용 아트 없으면 닫힌 상자 폴백.</summary>
        public static Sprite IngredientBagIcon()
        {
            if (_ingredientBagIcon == null)
            {
                _ingredientBagIcon = Resources.Load<Sprite>("UI/IngredientBag");
                if (_ingredientBagIcon == null)
                    _ingredientBagIcon = Resources.Load<Sprite>("UI/GiftChest_Closed");
            }
            return _ingredientBagIcon;
        }

        /// <summary>특수 칸에서 얻은 아이콘이 동(東) 보상란으로 슝 날아간다.</summary>
        public IEnumerator PlayCollectRewardFly(int fromNodeId, Sprite icon)
        {
            EnsureBoard();
            var east = GetQuadrant(YutBoardQuadrant.East);
            EnsureCollectedItemsRoot(east);
            if (_collectedItemsRoot == null) yield break;

            Vector3 fromPos = PadWorldCenter(fromNodeId);
            Vector3 toPos = ((RectTransform)_collectedItemsRoot).position;
            float size = PadPixelSize(fromNodeId);

            var flyRoot = new GameObject("CollectRewardFly", typeof(RectTransform));
            flyRoot.transform.SetParent(transform, false);
            var flyRootRt = (RectTransform)flyRoot.transform;
            flyRootRt.anchorMin = Vector2.zero;
            flyRootRt.anchorMax = Vector2.one;
            flyRootRt.offsetMin = Vector2.zero;
            flyRootRt.offsetMax = Vector2.zero;
            flyRoot.transform.SetAsLastSibling();

            var go = new GameObject("FlyIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(flyRoot.transform, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(size, size);
            rt.position = fromPos;
            var img = go.GetComponent<Image>();
            img.raycastTarget = false;
            img.preserveAspect = true;
            if (icon != null)
            {
                img.sprite = icon;
                img.color = Color.white;
            }
            else
            {
                img.sprite = null;
                img.color = new Color(0.85f, 0.75f, 0.45f, 0.95f);
            }

            const float duration = 0.42f;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / duration);
                float eased = u * u * (3f - 2f * u);
                float lift = Mathf.Sin(u * Mathf.PI) * 48f;
                Vector3 p = Vector3.Lerp(fromPos, toPos, eased);
                p.y += lift;
                rt.position = p;
                float scale = Mathf.Lerp(1.05f, 0.72f, eased);
                rt.localScale = new Vector3(scale, scale, 1f);
                var c = img.color;
                c.a = u < 0.8f ? 1f : Mathf.Lerp(1f, 0.2f, (u - 0.8f) / 0.2f);
                img.color = c;
                yield return null;
            }

            Destroy(flyRoot);
        }

        /// <summary>동(東) 구역 — 이번 매치에서 특수 칸으로 모은 것들(공양물·물·엽전)을
        /// 아이콘+개수로 보여준다. YutScreen이 재화를 지급할 때마다 최신 목록을 넘겨준다.</summary>
        public void ShowCollectedItems(IReadOnlyList<CollectedItemView> items)
        {
            var east = GetQuadrant(YutBoardQuadrant.East);
            EnsureCollectedItemsRoot(east);

            for (int i = _collectedItemsRoot.childCount - 1; i >= 0; i--)
                Destroy(_collectedItemsRoot.GetChild(i).gameObject);

            if (items == null || items.Count == 0) return;

            for (int i = 0; i < items.Count; i++)
                CreateCollectedItemChip(_collectedItemsRoot, items[i]);
        }

        void EnsureCollectedItemsRoot(Transform east)
        {
            if (_collectedItemsRoot != null)
            {
                ApplyEastHalfAnchors((RectTransform)_collectedItemsRoot, upperHalf: true);
                return;
            }

            // 예전에 텍스트만 쓰던 Items 노드는 치운다
            var legacy = east.Find("Items");
            if (legacy != null)
                Destroy(legacy.gameObject);

            var existing = east.Find("ItemIcons");
            if (existing != null)
            {
                _collectedItemsRoot = existing;
                ApplyEastHalfAnchors((RectTransform)_collectedItemsRoot, upperHalf: true);
                return;
            }

            var go = new GameObject("ItemIcons", typeof(RectTransform));
            go.transform.SetParent(east, false);
            var rt = go.GetComponent<RectTransform>();
            // 동 구역 상단 절반 — 공양물·물·엽전. 하단 절반은 완주 말.
            ApplyEastHalfAnchors(rt, upperHalf: true);

            var grid = go.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(42f, 54f);
            grid.spacing = new Vector2(4f, 2f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
            grid.childAlignment = TextAnchor.UpperCenter;
            grid.padding = new RectOffset(2, 2, 2, 2);
            _collectedItemsRoot = go.transform;
        }

        void EnsureFinishedPiecesRoot(Transform east)
        {
            if (_finishedPiecesRoot != null)
            {
                ApplyEastHalfAnchors((RectTransform)_finishedPiecesRoot, upperHalf: false);
                EnsureFinishedHighlightImage((RectTransform)_finishedPiecesRoot);
                return;
            }

            var existing = east.Find("FinishedPieces");
            if (existing != null)
            {
                _finishedPiecesRoot = existing;
                ApplyEastHalfAnchors((RectTransform)_finishedPiecesRoot, upperHalf: false);
                EnsureFinishedHighlightImage((RectTransform)_finishedPiecesRoot);
                return;
            }

            var go = new GameObject("FinishedPieces", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(east, false);
            var rt = go.GetComponent<RectTransform>();
            ApplyEastHalfAnchors(rt, upperHalf: false);
            EnsureFinishedHighlightImage(rt);

            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 3f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.padding = new RectOffset(2, 2, 2, 2);
            _finishedPiecesRoot = go.transform;
        }

        /// <summary>동 구역을 상·하 절반으로 나눈다. 위=공양물, 아래=완주 말.</summary>
        static void ApplyEastHalfAnchors(RectTransform rt, bool upperHalf)
        {
            if (rt == null) return;
            rt.anchorMin = new Vector2(0f, upperHalf ? 0.5f : 0f);
            rt.anchorMax = new Vector2(1f, upperHalf ? 1f : 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        void EnsureFinishedHighlightImage(RectTransform finishedRoot)
        {
            if (finishedRoot == null) return;
            var img = finishedRoot.GetComponent<Image>();
            if (img == null)
                img = finishedRoot.gameObject.AddComponent<Image>();
            // 평소엔 투명 — 완주 후보 하이라이트 때만 펄스.
            if (!_eastFinishHighlightActive)
            {
                img.color = new Color(0f, 0f, 0f, 0f);
                img.raycastTarget = false;
            }
        }

        /// <summary>완주(골인)한 말들 — 참(시작점)에 그냥 멈춰 있는 것과 헷갈리지 않게, 보드에서
        /// 빠진 대신 동(東) 구역 하단에 작은 초상으로 한 줄 보여준다.</summary>
        public void ShowFinishedPieces(List<string> ids)
        {
            var east = GetQuadrant(YutBoardQuadrant.East);
            EnsureFinishedPiecesRoot(east);

            _finishedPieceAnchors.Clear();
            for (int i = _finishedPiecesRoot.childCount - 1; i >= 0; i--)
                Destroy(_finishedPiecesRoot.GetChild(i).gameObject);
            if (ids == null) return;

            for (int i = 0; i < ids.Count; i++)
            {
                string pieceId = ids[i];
                var go = new GameObject($"Finished_{pieceId}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(_finishedPiecesRoot, false);
                var rt = go.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(30f, 30f);
                var img = go.GetComponent<Image>();
                var sprite = PieceSpriteFor(pieceId);
                if (sprite != null)
                {
                    img.sprite = sprite;
                    img.color = Color.white;
                    img.preserveAspect = true;
                }
                else
                {
                    img.color = ColorForYokai(pieceId);
                }
                img.raycastTarget = false;
                _finishedPieceAnchors[pieceId] = rt;
            }
        }

        void CreateCollectedItemChip(Transform parent, CollectedItemView item)
        {
            var go = new GameObject("Item", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconGo.transform.SetParent(go.transform, false);
            var iconRt = iconGo.GetComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0.1f, 0.28f);
            iconRt.anchorMax = new Vector2(0.9f, 1f);
            iconRt.offsetMin = Vector2.zero;
            iconRt.offsetMax = Vector2.zero;
            var img = iconGo.GetComponent<Image>();
            img.raycastTarget = false;
            img.preserveAspect = true;
            if (item.Icon != null)
            {
                img.sprite = item.Icon;
                img.color = item.Tint ?? Color.white;
            }
            else
            {
                img.sprite = null;
                img.color = item.Tint ?? new Color(0.85f, 0.75f, 0.45f, 0.85f);
            }

            var count = CreateText(go.transform, "Count", "x" + item.Count, 19, TextAnchor.MiddleCenter);
            var countRt = count.rectTransform;
            countRt.anchorMin = new Vector2(0f, 0f);
            countRt.anchorMax = new Vector2(1f, 0.3f);
            countRt.offsetMin = Vector2.zero;
            countRt.offsetMax = Vector2.zero;
            count.color = new Color(0.95f, 0.9f, 0.7f);
            count.raycastTarget = false;
        }
    }
}
