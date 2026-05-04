using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;
using Orora.UI.Extensions;

namespace Orora.ImageObjectForge
{
    internal static class ForgePrefabFactory
    {
        public const string PrefabsDir = ForgeIO.ToolRoot + "/Prefabs";
        public const string DefaultFontAssetPath = "Assets/JC_Work/Assets_jc/Maplestory Light SDF.asset";
        public const string BasePrefabPath = "Assets/JC_Work/__ProtoType/Prefabs/UI/UIButtonBase.prefab";

        public enum PrefabKind { Button, Label, SpriteRenderer, Modal }

        public class Options
        {
            public PrefabKind Kind = PrefabKind.Button;
            public Sprite SourceSprite;           // 필수
            public string NameOverride;           // null이면 sprite texture 이름 사용
            // Button 전용
            public GameObject BasePrefab;         // null이면 BasePrefabPath의 UIButtonBase 사용
            public TMP_FontAsset Font;
            public string ButtonText = "Button";
            public float FontSize = 14f;
            public Color TextColor = Color.black;
            // 공통
            public Vector2? SizeOverride;         // null이면 sprite 픽셀 크기
        }

        // 여러 Sprite를 일괄 생성. opts의 SourceSprite는 무시됨.
        // useSpriteNameAsButtonText=true 면 각 버튼의 Text를 해당 스프라이트 이름으로 자동 설정
        public static List<GameObject> CreateBatch(IList<Sprite> sprites, Options opts, bool useSpriteNameAsButtonText, out int failCount)
        {
            var result = new List<GameObject>();
            failCount = 0;
            if (sprites == null || sprites.Count == 0) return result;

            foreach (var sp in sprites)
            {
                if (sp == null) { failCount++; continue; }
                var o = new Options
                {
                    Kind = opts.Kind,
                    SourceSprite = sp,
                    NameOverride = null,
                    BasePrefab = opts.BasePrefab,
                    Font = opts.Font,
                    ButtonText = useSpriteNameAsButtonText ? sp.texture.name : opts.ButtonText,
                    FontSize = opts.FontSize,
                    TextColor = opts.TextColor,
                    SizeOverride = opts.SizeOverride,
                };
                var go = Create(o, out _);
                if (go != null) result.Add(go);
                else failCount++;
            }
            return result;
        }

        public static GameObject Create(Options opts, out string errorMsg)
        {
            errorMsg = null;
            if (opts == null || opts.SourceSprite == null) { errorMsg = "Sprite가 지정되지 않았습니다."; return null; }

            EnsureDir(PrefabsDir);

            string stem = string.IsNullOrEmpty(opts.NameOverride)
                ? opts.SourceSprite.texture.name
                : opts.NameOverride;
            string assetPath = NextAvailablePrefabPath(stem);

            GameObject root;
            switch (opts.Kind)
            {
                case PrefabKind.Button:         root = BuildButton(opts); break;
                case PrefabKind.Label:          root = BuildLabel(opts); break;
                case PrefabKind.SpriteRenderer: root = BuildSpriteRenderer(opts); break;
                case PrefabKind.Modal:          root = BuildModal(opts); break;
                default:
                    errorMsg = "지원하지 않는 타입";
                    return null;
            }

            var savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, assetPath);
            Object.DestroyImmediate(root);
            if (savedPrefab == null) { errorMsg = "프리팹 저장 실패"; return null; }

            AssetDatabase.ImportAsset(assetPath);
            return savedPrefab;
        }

        // -------- Button --------
        // 베이스 프리팹(UIButtonBase)을 인스턴스화 → sprite/이름/크기/Label 텍스트 override.
        // 호출자(Create)가 SaveAsPrefabAsset에 넘기면 자동으로 Prefab Variant로 저장됨.
        static GameObject BuildButton(Options opts)
        {
            var basePrefab = opts.BasePrefab != null
                ? opts.BasePrefab
                : AssetDatabase.LoadAssetAtPath<GameObject>(BasePrefabPath);
            if (basePrefab == null)
            {
                Debug.LogError("[ImageObjectForge] BasePrefab을 찾지 못했습니다. 경로: " + BasePrefabPath);
                return null;
            }

            // 스프라이트 텍스처 Readable 보장 (alpha hit test에 필요)
            EnsureSpriteReadable(opts);

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
            if (instance == null)
            {
                Debug.LogError("[ImageObjectForge] BasePrefab 인스턴스화 실패");
                return null;
            }
            instance.name = opts.SourceSprite.texture.name;

            // RectTransform — 좌상단 표준 + 크기
            var rt = instance.GetComponent<RectTransform>();
            if (rt != null)
            {
                ApplyTopLeftAnchor(rt);
                rt.sizeDelta = ResolveSize(opts);
            }

            // Image — sprite override (color는 베이스 흰색 그대로)
            var img = instance.GetComponent<Image>();
            if (img != null)
            {
                img.sprite = opts.SourceSprite;
                img.raycastTarget = true;
            }

            // Label 자식 텍스트 override (베이스 자식 이름 = "Label")
            var labelTr = instance.transform.Find("Label");
            if (labelTr != null)
            {
                var tmp = labelTr.GetComponent<TextMeshProUGUI>();
                if (tmp != null)
                {
                    tmp.text = opts.ButtonText ?? "Button";
                    if (opts.FontSize > 0f) tmp.fontSize = opts.FontSize;
                    tmp.color = opts.TextColor;
                    var font = opts.Font != null ? opts.Font : LoadDefaultFont();
                    if (font != null) tmp.font = font;
                }
            }

            return instance;
        }

        static void EnsureSpriteReadable(Options opts)
        {
            if (opts.SourceSprite == null) return;
            var spritePath = AssetDatabase.GetAssetPath(opts.SourceSprite);
            if (string.IsNullOrEmpty(spritePath)) return;
            var importer = AssetImporter.GetAtPath(spritePath) as TextureImporter;
            if (importer != null && !importer.isReadable)
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
                var reloaded = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
                if (reloaded != null) opts.SourceSprite = reloaded;
            }
        }

        // -------- Label (Image 단독) --------
        static GameObject BuildLabel(Options opts)
        {
            var size = ResolveSize(opts);
            var go = new GameObject(opts.SourceSprite.texture.name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            ApplyTopLeftAnchor(rt);
            rt.sizeDelta = size;

            var img = go.AddComponent<Image>();
            img.sprite = opts.SourceSprite;
            img.raycastTarget = false;

            return go;
        }

        // -------- Modal (단일 sprite Panel + Backdrop + Modal 컴포넌트) --------
        // 양식: Root(full stretch) → Modal(시작 비활성, Modal 컴포넌트) → Backdrop + Panel(=sprite, 중앙)
        // Modal 컴포넌트는 reflection 부착 (Assembly-CSharp에 없으면 경고 후 그대로 진행)
        static GameObject BuildModal(Options opts)
        {
            var size = ResolveSize(opts);

            var root = new GameObject(opts.SourceSprite.texture.name, typeof(RectTransform));
            FullStretch(root.GetComponent<RectTransform>());

            var modal = new GameObject("Modal", typeof(RectTransform));
            modal.transform.SetParent(root.transform, false);
            FullStretch(modal.GetComponent<RectTransform>());

            // Modal 컴포넌트 reflection 부착 (실패는 경고 후 진행)
            var modalType = System.Type.GetType("Modal, Assembly-CSharp");
            if (modalType != null)
            {
                modal.AddComponent(modalType);
            }
            else
            {
                Debug.LogWarning("[ImageObjectForge] Modal 컴포넌트 타입을 찾지 못함 (Assembly-CSharp). Modal 컴포넌트 없이 prefab 생성 — 필요 시 수동 부착하세요.");
            }

            var backdrop = new GameObject("Backdrop", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            backdrop.transform.SetParent(modal.transform, false);
            FullStretch(backdrop.GetComponent<RectTransform>());
            var backdropImg = backdrop.GetComponent<Image>();
            backdropImg.color = new Color(0f, 0f, 0f, 0.5f);
            backdropImg.raycastTarget = true;

            var panel = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panel.transform.SetParent(modal.transform, false);
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin        = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax        = new Vector2(0.5f, 0.5f);
            panelRect.pivot            = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta        = size;
            var panelImg = panel.GetComponent<Image>();
            panelImg.sprite = opts.SourceSprite;
            panelImg.color = Color.white;
            panelImg.raycastTarget = true;

            modal.SetActive(false);
            return root;
        }

        static void FullStretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        // -------- SpriteRenderer (월드) --------
        static GameObject BuildSpriteRenderer(Options opts)
        {
            var go = new GameObject(opts.SourceSprite.texture.name);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = opts.SourceSprite;
            return go;
        }

        // -------- Helpers --------
        // Auto-Place 좌상단 표준과 일관: anchor·pivot 모두 (0, 1).
        public static void ApplyTopLeftAnchor(RectTransform rt)
        {
            var topLeft = new Vector2(0f, 1f);
            rt.anchorMin = topLeft;
            rt.anchorMax = topLeft;
            rt.pivot = topLeft;
        }

        static Vector2 ResolveSize(Options opts)
        {
            if (opts.SizeOverride.HasValue) return opts.SizeOverride.Value;
            var r = opts.SourceSprite.rect;
            return new Vector2(r.width, r.height);
        }

        public static TMP_FontAsset LoadDefaultFont()
        {
            return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(DefaultFontAssetPath);
        }

        static void EnsureDir(string assetDir)
        {
            string abs = Path.Combine(Directory.GetCurrentDirectory(), assetDir);
            if (!Directory.Exists(abs)) Directory.CreateDirectory(abs);
        }

        static string NextAvailablePrefabPath(string stem)
        {
            string absDir = Path.Combine(Directory.GetCurrentDirectory(), PrefabsDir);
            string candidate = stem + ".prefab";
            int i = 2;
            while (File.Exists(Path.Combine(absDir, candidate)))
            {
                candidate = stem + "_" + i + ".prefab";
                i++;
            }
            return PrefabsDir + "/" + candidate;
        }
    }
}
