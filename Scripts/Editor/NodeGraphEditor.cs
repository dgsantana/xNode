using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace XNodeEditor
{
    /// <summary> Base class to derive custom Node Graph editors from. Use this to override how graphs are drawn in the editor. </summary>
    [CustomNodeGraphEditor(typeof(XNode.NodeGraph))]
    public class NodeGraphEditor : Internal.NodeEditorBase<NodeGraphEditor, NodeGraphEditor.CustomNodeGraphEditorAttribute, XNode.NodeGraph>
    {
        /// <summary> The position of the window in screen space. </summary>
        public Rect Position;
        /// <summary> Are we currently renaming a node? </summary>
        protected bool IsRenaming;

        public virtual void OnGUI() { }

        public virtual Texture2D GetGridTexture()
        {
            return NodeEditorPreferences.GetSettings().GridTexture;
        }

        public virtual Texture2D GetSecondaryGridTexture()
        {
            return NodeEditorPreferences.GetSettings().CrossTexture;
        }

        /// <summary> Return default settings for this graph type. This is the settings the user will load if no previous settings have been saved. </summary>
        public virtual NodeEditorPreferences.Settings GetDefaultPreferences()
        {
            return new NodeEditorPreferences.Settings();
        }

        /// <summary> Returns context menu path. Returns null if node is not available. </summary>
        public virtual string GetNodePath(Type type)
        {
            //Check if type has the CreateNodeMenuAttribute
            XNode.Node.CreateNodeMenuAttribute attrib;
            return NodeEditorUtilities.GetAttrib(type, out attrib) ? attrib.MenuName : ObjectNames.NicifyVariableName(type.ToString().Replace('.', '/'));
        }

        public virtual Color GetTypeColor(Type type)
        {
            return NodeEditorPreferences.GetTypeColor(type);
        }

        /// <summary> Creates a copy of the original node in the graph </summary>
        public XNode.Node CopyNode(XNode.Node original)
        {
            XNode.Node node = target.CopyNode(original);
            node.name = original.name;
            AssetDatabase.AddObjectToAsset(node, target);
            if (NodeEditorPreferences.GetSettings().AutoSave) AssetDatabase.SaveAssets();
            return node;
        }

        /// <summary> Safely remove a node and all its connections. </summary>
        public void RemoveNode(XNode.Node node)
        {
            UnityEngine.Object.DestroyImmediate(node, true);
            target.RemoveNode(node);
            if (NodeEditorPreferences.GetSettings().AutoSave) AssetDatabase.SaveAssets();
        }

        [AttributeUsage(AttributeTargets.Class)]
        public class CustomNodeGraphEditorAttribute : Attribute, INodeEditorAttrib
        {
            private Type _inspectedType;
            public string EditorPrefsKey;

            /// <summary> Tells a NodeGraphEditor which Graph type it is an editor for </summary>
            /// <param name="inspectedType">Type that this editor can edit</param>
            /// <param name="editorPrefsKey"></param>
            public CustomNodeGraphEditorAttribute(Type inspectedType, string editorPrefsKey = "xNode.Settings")
            {
                _inspectedType = inspectedType;
                EditorPrefsKey = editorPrefsKey;
            }

            public Type GetInspectedType()
            {
                return _inspectedType;
            }
        }
    }
}