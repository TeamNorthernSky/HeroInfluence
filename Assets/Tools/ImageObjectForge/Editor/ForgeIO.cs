using System.IO;
using UnityEngine;
using UnityEditor;

namespace Orora.ImageObjectForge
{
    internal static class ForgeIO
    {
        public const string ToolRoot = "Assets/Tools/ImageObjectForge";
        public const string SourcesDir = ToolRoot + "/Sources";
        public const string OutputDir = ToolRoot + "/Output";

        public static bool LoadImageFromDialog(out Texture2D tex, out string assetPath, out string errorMsg)
        {
            tex = null; assetPath = null; errorMsg = null;
            string path = EditorUtility.OpenFilePanel("Load Image", "", "png,jpg,jpeg");
            if (string.IsNullOrEmpty(path)) { errorMsg = "취소됨"; return false; }
            return LoadImage(path, out tex, out assetPath, out errorMsg);
        }

        public static bool LoadImage(string srcAbsPath, out Texture2D tex, out string assetPath, out string errorMsg)
        {
            tex = null; assetPath = null; errorMsg = null;
            if (!File.Exists(srcAbsPath))
            {
                errorMsg = "파일이 존재하지 않음: " + srcAbsPath;
                return false;
            }

            string normalizedSrc = srcAbsPath.Replace('\\', '/');
            string projectData = Application.dataPath.Replace('\\', '/');
            bool insideProject = normalizedSrc.StartsWith(projectData + "/", System.StringComparison.OrdinalIgnoreCase);

            string destAbs;
            if (insideProject)
            {
                // 프로젝트 내부 파일: 복사 생략, 기존 에셋을 그대로 사용.
                assetPath = "Assets" + normalizedSrc.Substring(projectData.Length);
                destAbs = normalizedSrc;
            }
            else
            {
                EnsureDir(SourcesDir);

                string stem = Path.GetFileNameWithoutExtension(srcAbsPath);
                string ext = Path.GetExtension(srcAbsPath).ToLowerInvariant();
                if (string.IsNullOrEmpty(ext)) ext = ".png";

                string destName = stem + ext;
                string absSourcesDir = AbsPath(SourcesDir);
                destAbs = Path.Combine(absSourcesDir, destName);

                int i = 2;
                while (File.Exists(destAbs))
                {
                    destName = stem + "_" + i + ext;
                    destAbs = Path.Combine(absSourcesDir, destName);
                    i++;
                }

                try { File.Copy(srcAbsPath, destAbs); }
                catch (System.Exception ex) { errorMsg = "복사 실패: " + ex.Message; return false; }

                assetPath = SourcesDir + "/" + destName;
                AssetDatabase.ImportAsset(assetPath);
            }

            byte[] data;
            try { data = File.ReadAllBytes(destAbs); }
            catch (System.Exception ex) { errorMsg = "읽기 실패: " + ex.Message; return false; }

            tex = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            if (!tex.LoadImage(data, false))
            {
                Object.DestroyImmediate(tex);
                tex = null;
                errorMsg = "디코딩 실패 (지원되는 png/jpg인지 확인)";
                return false;
            }
            return true;
        }

        public static RectInt ComputeMaskBounds(byte[] mask, int imgW, int imgH)
        {
            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            for (int y = 0; y < imgH; y++)
            {
                int row = y * imgW;
                for (int x = 0; x < imgW; x++)
                {
                    if (mask[row + x] != 0)
                    {
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }
            }
            if (minX > maxX) return new RectInt(0, 0, 0, 0);
            return new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }

        // 마스크=0 픽셀은 투명. 마스크≠0 픽셀은 원본 RGB + A=255.
        // bounds는 이미지 좌표(y-up). 결과 텍스처도 y-up(텍스처 표준) 그대로.
        public static Texture2D BuildCropped(Texture2D source, byte[] mask, RectInt bounds)
        {
            int W = source.width;
            var srcPix = source.GetPixels32();
            var dst = new Color32[bounds.width * bounds.height];
            var transparent = new Color32(0, 0, 0, 0);

            for (int dy = 0; dy < bounds.height; dy++)
            {
                int srcRow = (bounds.y + dy) * W + bounds.x;
                int dstRow = dy * bounds.width;
                for (int dx = 0; dx < bounds.width; dx++)
                {
                    if (mask[srcRow + dx] != 0)
                    {
                        var c = srcPix[srcRow + dx];
                        c.a = 255;
                        dst[dstRow + dx] = c;
                    }
                    else
                    {
                        dst[dstRow + dx] = transparent;
                    }
                }
            }
            var t = new Texture2D(bounds.width, bounds.height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            t.SetPixels32(dst);
            t.Apply(false, false);
            return t;
        }

        public static string SavePng(Texture2D tex, string filename, out string errorMsg)
        {
            errorMsg = null;
            EnsureDir(OutputDir);
            string assetPath = OutputDir + "/" + filename;
            string abs = AbsPath(assetPath);
            try
            {
                byte[] bytes = tex.EncodeToPNG();
                File.WriteAllBytes(abs, bytes);
                AssetDatabase.ImportAsset(assetPath);
                ApplySpriteImportSettings(assetPath);
            }
            catch (System.Exception ex) { errorMsg = ex.Message; return null; }
            return assetPath;
        }

        // 저장된 PNG를 UI/Sprite 용도 기본값으로 임포트 설정
        public static void ApplySpriteImportSettings(string assetPath,
            float pixelsPerUnit = 100f,
            SpriteAlignment alignment = SpriteAlignment.Center,
            Vector2? customPivot = null,
            FilterMode filterMode = FilterMode.Bilinear,
            TextureImporterCompression compression = TextureImporterCompression.Uncompressed,
            bool generateMipMaps = false,
            int maxSize = 2048)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = generateMipMaps;
            importer.filterMode = filterMode;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = maxSize;
            importer.textureCompression = compression;
            importer.alphaIsTransparency = true;
            importer.isReadable = true;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)alignment;
            if (customPivot.HasValue) settings.spritePivot = customPivot.Value;
            settings.spritePixelsPerUnit = pixelsPerUnit;
            settings.spriteMeshType = SpriteMeshType.Tight;
            settings.spriteExtrude = 1;
            settings.spriteGenerateFallbackPhysicsShape = false;
            importer.SetTextureSettings(settings);

            importer.SaveAndReimport();
        }

        // Output/ 폴더 스캔하여 Sprite 일괄 변환. (processed, skipped) 반환.
        public static (int processed, int skipped) BatchApplyOutputSprites(bool forceReapply,
            float pixelsPerUnit = 100f,
            SpriteAlignment alignment = SpriteAlignment.Center,
            Vector2? customPivot = null,
            FilterMode filterMode = FilterMode.Bilinear,
            TextureImporterCompression compression = TextureImporterCompression.Uncompressed,
            bool generateMipMaps = false,
            int maxSize = 2048)
        {
            EnsureDir(OutputDir);
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { OutputDir });
            int processed = 0, skipped = 0;
            try
            {
                for (int i = 0; i < guids.Length; i++)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    if (EditorUtility.DisplayCancelableProgressBar("Batch Convert Sprites", path, (float)i / Mathf.Max(1, guids.Length)))
                        break;
                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer == null) { skipped++; continue; }
                    if (!forceReapply && importer.textureType == TextureImporterType.Sprite
                        && importer.spriteImportMode == SpriteImportMode.Single)
                    {
                        skipped++;
                        continue;
                    }
                    ApplySpriteImportSettings(path, pixelsPerUnit, alignment, customPivot, filterMode, compression, generateMipMaps, maxSize);
                    processed++;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                AssetDatabase.Refresh();
            }
            return (processed, skipped);
        }

        // Output/ 내 Sprite 목록 로드 (이름 오름차순). 호환용: 내부에서 EnumerateSpritesIn(OutputDir) 호출.
        public static System.Collections.Generic.List<Sprite> EnumerateOutputSprites()
        {
            EnsureDir(OutputDir);
            return EnumerateSpritesIn(OutputDir);
        }

        // 임의 폴더(Assets 이하) 내 Sprite 목록 로드 (이름 오름차순). 폴더가 유효하지 않으면 빈 리스트.
        public static System.Collections.Generic.List<Sprite> EnumerateSpritesIn(string folderAssetPath)
        {
            var list = new System.Collections.Generic.List<Sprite>();
            if (string.IsNullOrEmpty(folderAssetPath) || !AssetDatabase.IsValidFolder(folderAssetPath))
                return list;
            var guids = AssetDatabase.FindAssets("t:Sprite", new[] { folderAssetPath });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (s != null) list.Add(s);
            }
            list.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.OrdinalIgnoreCase));
            return list;
        }

        // 임의 폴더(Assets 이하) 내 Prefab 목록 로드 (이름 오름차순). 폴더가 유효하지 않으면 빈 리스트.
        public static System.Collections.Generic.List<GameObject> EnumeratePrefabsIn(string folderAssetPath)
        {
            var list = new System.Collections.Generic.List<GameObject>();
            if (string.IsNullOrEmpty(folderAssetPath) || !AssetDatabase.IsValidFolder(folderAssetPath))
                return list;
            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { folderAssetPath });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var p = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (p != null) list.Add(p);
            }
            list.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.OrdinalIgnoreCase));
            return list;
        }

        // 폴더 검증: Assets/ 이하의 유효한 폴더인지.
        public static bool IsValidAssetsFolder(string folderAssetPath)
        {
            if (string.IsNullOrEmpty(folderAssetPath)) return false;
            if (!folderAssetPath.StartsWith("Assets/", System.StringComparison.Ordinal)
                && folderAssetPath != "Assets") return false;
            return AssetDatabase.IsValidFolder(folderAssetPath);
        }

        // ---------- Sidecar (.crop.json) ----------
        public static string GetSidecarPathFor(string pngAssetPath)
        {
            if (string.IsNullOrEmpty(pngAssetPath)) return null;
            string dir = Path.GetDirectoryName(pngAssetPath).Replace('\\', '/');
            string stem = Path.GetFileNameWithoutExtension(pngAssetPath);
            return dir + "/" + stem + ".crop.json";
        }

        public static bool WriteCropSidecar(string pngAssetPath, ForgeCropMeta meta, out string errorMsg)
        {
            errorMsg = null;
            try
            {
                string sidecar = GetSidecarPathFor(pngAssetPath);
                if (string.IsNullOrEmpty(sidecar)) { errorMsg = "pngAssetPath 비어있음"; return false; }
                string json = JsonUtility.ToJson(meta, true);
                File.WriteAllText(AbsPath(sidecar), json);
                AssetDatabase.ImportAsset(sidecar);
                return true;
            }
            catch (System.Exception ex)
            {
                errorMsg = ex.Message;
                return false;
            }
        }

        public static ForgeCropMeta LoadCropSidecar(string sidecarAssetPath)
        {
            if (string.IsNullOrEmpty(sidecarAssetPath)) return null;
            string abs = AbsPath(sidecarAssetPath);
            if (!File.Exists(abs)) return null;
            try
            {
                string json = File.ReadAllText(abs);
                var meta = JsonUtility.FromJson<ForgeCropMeta>(json);
                if (meta == null) return null;
                // v2 자동 호환: canvasSize가 비어 있고 sourceSize가 채워져 있으면 매핑.
                if (meta.canvasSize.width == 0 && meta.canvasSize.height == 0
                    && (meta.sourceSize.width != 0 || meta.sourceSize.height != 0))
                {
                    meta.canvasSize = meta.sourceSize;
                }
                return meta;
            }
            catch
            {
                return null;
            }
        }

        public static string NextAvailableOutputName(string stem)
        {
            EnsureDir(OutputDir);
            string absDir = AbsPath(OutputDir);
            string baseName = string.IsNullOrEmpty(stem) ? "cut" : stem + "_cut";
            string candidate = baseName + ".png";
            int i = 2;
            while (File.Exists(Path.Combine(absDir, candidate)))
            {
                candidate = baseName + "_" + i + ".png";
                i++;
            }
            return candidate;
        }

        public static void EnsureDir(string assetDir)
        {
            string abs = AbsPath(assetDir);
            if (!Directory.Exists(abs)) Directory.CreateDirectory(abs);
        }

        public static string AbsPath(string assetPath)
        {
            return Path.Combine(Directory.GetCurrentDirectory(), assetPath);
        }
    }
}
