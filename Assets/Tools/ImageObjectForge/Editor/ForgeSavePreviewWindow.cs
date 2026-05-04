using UnityEngine;
using UnityEditor;

namespace Orora.ImageObjectForge
{
    internal class ForgeSavePreviewWindow : EditorWindow
    {
        Texture2D _preview;
        RectInt _bounds;
        string _filename;
        System.Action<string> _onConfirm;

        public static void Show(Texture2D preview, RectInt bounds, string defaultFilename, System.Action<string> onConfirm)
        {
            var w = CreateInstance<ForgeSavePreviewWindow>();
            w.titleContent = new GUIContent("Save Preview");
            w._preview = preview;
            w._bounds = bounds;
            w._filename = defaultFilename;
            w._onConfirm = onConfirm;
            w.minSize = new Vector2(480, 560);
            w.ShowUtility();
        }

        void OnGUI()
        {
            if (_preview == null) { Close(); return; }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Crop Bounds", $"x={_bounds.x}  y={_bounds.y}   size={_bounds.width} × {_bounds.height}");
            EditorGUILayout.Space(6);

            var rect = GUILayoutUtility.GetRect(position.width - 20, 360);
            EditorGUI.DrawRect(rect, new Color(0.15f, 0.15f, 0.15f, 1f));
            float zx = rect.width / _preview.width;
            float zy = rect.height / _preview.height;
            float z = Mathf.Min(1f, Mathf.Min(zx, zy));
            float w = _preview.width * z;
            float h = _preview.height * z;
            var drawRect = new Rect(rect.x + (rect.width - w) * 0.5f, rect.y + (rect.height - h) * 0.5f, w, h);
            DrawCheckerboard(drawRect);
            GUI.DrawTexture(drawRect, _preview, ScaleMode.StretchToFill, true);

            EditorGUILayout.Space(6);
            _filename = EditorGUILayout.TextField("Filename", _filename);
            EditorGUILayout.LabelField("Target", ForgeIO.OutputDir + "/" + _filename);
            EditorGUILayout.Space(6);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Cancel", GUILayout.Height(30))) { Close(); }
                GUI.enabled = !string.IsNullOrWhiteSpace(_filename);
                if (GUILayout.Button("Save", GUILayout.Height(30)))
                {
                    string fn = _filename.Trim();
                    if (!fn.ToLowerInvariant().EndsWith(".png")) fn += ".png";
                    _onConfirm?.Invoke(fn);
                    _onConfirm = null;
                    Close();
                }
                GUI.enabled = true;
            }
        }

        static void DrawCheckerboard(Rect rect)
        {
            const int CellSize = 8;
            int cols = Mathf.CeilToInt(rect.width / CellSize);
            int rows = Mathf.CeilToInt(rect.height / CellSize);
            var c1 = new Color(0.75f, 0.75f, 0.75f);
            var c2 = new Color(0.55f, 0.55f, 0.55f);
            GUI.BeginClip(rect);
            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < cols; x++)
                {
                    var col = ((x + y) & 1) == 0 ? c1 : c2;
                    ForgeGfx.FilledRect(new Rect(x * CellSize, y * CellSize, CellSize, CellSize), col);
                }
            }
            GUI.EndClip();
        }

        void OnDestroy()
        {
            if (_preview != null) { DestroyImmediate(_preview); _preview = null; }
        }
    }
}
