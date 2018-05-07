using UnityEditor;
using UnityEngine;

namespace XNodeEditor
{
    public static class NodeEditorResources
    {
        private static Texture2D _dot;
        private static Texture2D _dotOuter;
        private static Texture2D _nodeBody;
        private static Texture2D _nodeHighlight;

        private static NodeStyles _styles;

        // Textures
        public static Texture2D Dot => _dot != null ? _dot : _dot = Resources.Load<Texture2D>("xnode_dot");

        public static Texture2D DotOuter =>
            _dotOuter != null ? _dotOuter : _dotOuter = Resources.Load<Texture2D>("xnode_dot_outer");

        public static Texture2D NodeBody =>
            _nodeBody != null ? _nodeBody : _nodeBody = Resources.Load<Texture2D>("xnode_node");

        public static Texture2D NodeHighlight => _nodeHighlight != null
            ? _nodeHighlight
            : _nodeHighlight = Resources.Load<Texture2D>("xnode_node_highlight");

        // Styles
        public static NodeStyles Styles => _styles != null ? _styles : _styles = new NodeStyles();
        public static GUIStyle OutputPort => new GUIStyle(EditorStyles.label) {alignment = TextAnchor.UpperRight};

        public static Texture2D GenerateGridTexture(Color line, Color bg)
        {
            var tex = new Texture2D(64, 64);
            var cols = new Color[64 * 64];
            for (var y = 0; y < 64; y++)
            for (var x = 0; x < 64; x++)
            {
                var col = bg;
                if (y % 16 == 0 || x % 16 == 0) col = Color.Lerp(line, bg, 0.65f);
                if (y == 63 || x == 63) col = Color.Lerp(line, bg, 0.35f);
                cols[y * 64 + x] = col;
            }

            tex.SetPixels(cols);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;
            tex.name = "Grid";
            tex.Apply();
            return tex;
        }

        public static Texture2D GenerateCrossTexture(Color line)
        {
            var tex = new Texture2D(64, 64);
            var cols = new Color[64 * 64];
            for (var y = 0; y < 64; y++)
            for (var x = 0; x < 64; x++)
            {
                var col = line;
                if (y != 31 && x != 31) col.a = 0;
                cols[y * 64 + x] = col;
            }

            tex.SetPixels(cols);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            tex.name = "Grid";
            tex.Apply();
            return tex;
        }

        public class NodeStyles
        {
            public GUIStyle InputPort, NodeHeader, NodeBody, Tooltip, NodeHighlight;

            public NodeStyles()
            {
                var baseStyle = new GUIStyle("Label") {fixedHeight = 18};

                InputPort = new GUIStyle(baseStyle)
                {
                    alignment = TextAnchor.UpperLeft,
                    padding = {left = 10}
                };

                NodeHeader = new GUIStyle
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold,
                    normal = {textColor = Color.white}
                };

                NodeBody = new GUIStyle
                {
                    normal = {background = NodeEditorResources.NodeBody},
                    border = new RectOffset(32, 32, 32, 32),
                    padding = new RectOffset(16, 16, 4, 16)
                };

                NodeHighlight = new GUIStyle
                {
                    normal = {background = NodeEditorResources.NodeHighlight},
                    border = new RectOffset(32, 32, 32, 32)
                };

                Tooltip = new GUIStyle("helpBox") {alignment = TextAnchor.MiddleCenter};
            }
        }
    }
}