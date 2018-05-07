using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace XNodeEditor
{
    /// <summary> xNode-specific version of <see cref="EditorGUILayout"/> </summary>
    public static class NodeEditorGUILayout
    {

        /// <summary> Make a field for a serialized property. Automatically displays relevant node port. </summary>
        public static void PropertyField(SerializedProperty property, bool includeChildren = true, params GUILayoutOption[] options)
        {
            PropertyField(property, (GUIContent)null, includeChildren, options);
        }

        /// <summary> Make a field for a serialized property. Automatically displays relevant node port. </summary>
        public static void PropertyField(SerializedProperty property, GUIContent label, bool includeChildren = true, params GUILayoutOption[] options)
        {
            if (property == null) throw new NullReferenceException();
            var node = property.serializedObject.targetObject as XNode.Node;
            var port = node.GetPort(property.name);
            PropertyField(property, label, port, includeChildren);
        }

        /// <summary> Make a field for a serialized property. Manual node port override. </summary>
        public static void PropertyField(SerializedProperty property, XNode.NodePort port, bool includeChildren = true, params GUILayoutOption[] options)
        {
            PropertyField(property, null, port, includeChildren, options);
        }

        /// <summary> Make a field for a serialized property. Manual node port override. </summary>
        public static void PropertyField(SerializedProperty property, GUIContent label, XNode.NodePort port, bool includeChildren = true, params GUILayoutOption[] options)
        {
            if (property == null) throw new NullReferenceException();

            // If property is not a port, display a regular property field
            if (port == null) EditorGUILayout.PropertyField(property, label, includeChildren, GUILayout.MinWidth(30));
            else
            {
                var rect = new Rect();

                // If property is an input, display a regular property field and put a port handle on the left side
                if (port.Direction == XNode.NodePort.IO.Input)
                {
                    // Get data from [Input] attribute
                    var showBacking = XNode.Node.ShowBackingValue.Unconnected;
                    XNode.Node.InputAttribute inputAttribute;
                    if (NodeEditorUtilities.GetAttrib(port.Node.GetType(), property.name, out inputAttribute)) showBacking = inputAttribute.BackingValue;

                    switch (showBacking)
                    {
                        case XNode.Node.ShowBackingValue.Unconnected:
                            // Display a label if port is connected
                            if (port.IsConnected) EditorGUILayout.LabelField(label != null ? label : new GUIContent(property.displayName));
                            // Display an editable property field if port is not connected
                            else EditorGUILayout.PropertyField(property, label, includeChildren, GUILayout.MinWidth(30));
                            break;
                        case XNode.Node.ShowBackingValue.Never:
                            // Display a label
                            EditorGUILayout.LabelField(label != null ? label : new GUIContent(property.displayName));
                            break;
                        case XNode.Node.ShowBackingValue.Always:
                            // Display an editable property field
                            EditorGUILayout.PropertyField(property, label, includeChildren, GUILayout.MinWidth(30));
                            break;
                    }

                    rect = GUILayoutUtility.GetLastRect();
                    rect.position = rect.position - new Vector2(16, 0);
                    // If property is an output, display a text label and put a port handle on the right side
                }
                else if (port.Direction == XNode.NodePort.IO.Output)
                {
                    // Get data from [Output] attribute
                    var showBacking = XNode.Node.ShowBackingValue.Unconnected;
                    XNode.Node.OutputAttribute outputAttribute;
                    if (NodeEditorUtilities.GetAttrib(port.Node.GetType(), property.name, out outputAttribute)) showBacking = outputAttribute.BackingValue;

                    switch (showBacking)
                    {
                        case XNode.Node.ShowBackingValue.Unconnected:
                            // Display a label if port is connected
                            if (port.IsConnected) EditorGUILayout.LabelField(label != null ? label : new GUIContent(property.displayName), NodeEditorResources.OutputPort, GUILayout.MinWidth(30));
                            // Display an editable property field if port is not connected
                            else EditorGUILayout.PropertyField(property, label, includeChildren, GUILayout.MinWidth(30));
                            break;
                        case XNode.Node.ShowBackingValue.Never:
                            // Display a label
                            EditorGUILayout.LabelField(label != null ? label : new GUIContent(property.displayName), NodeEditorResources.OutputPort, GUILayout.MinWidth(30));
                            break;
                        case XNode.Node.ShowBackingValue.Always:
                            // Display an editable property field
                            EditorGUILayout.PropertyField(property, label, includeChildren, GUILayout.MinWidth(30));
                            break;
                    }

                    rect = GUILayoutUtility.GetLastRect();
                    rect.position = rect.position + new Vector2(rect.width, 0);
                }

                rect.size = new Vector2(16, 16);

                Color backgroundColor = new Color32(90, 97, 105, 255);
                if (NodeEditorWindow.nodeTint.ContainsKey(port.Node.GetType())) backgroundColor *= NodeEditorWindow.nodeTint[port.Node.GetType()];
                var col = NodeEditorWindow.Current.GraphEditor.GetTypeColor(port.ValueType);
                DrawPortHandle(rect, backgroundColor, col);

                // Register the handle position
                var portPos = rect.center;
                if (NodeEditor.portPositions.ContainsKey(port)) NodeEditor.portPositions[port] = portPos;
                else NodeEditor.portPositions.Add(port, portPos);
            }
        }

        /// <summary> Make a simple port field. </summary>
        public static void PortField(XNode.NodePort port, params GUILayoutOption[] options)
        {
            PortField(null, port, options);
        }

        /// <summary> Make a simple port field. </summary>
        public static void PortField(GUIContent label, XNode.NodePort port, params GUILayoutOption[] options)
        {
            if (port == null) return;
            if (label == null) EditorGUILayout.LabelField(ObjectNames.NicifyVariableName(port.FieldName), options);
            else EditorGUILayout.LabelField(label, options);
            var rect = GUILayoutUtility.GetLastRect();
            if (port.Direction == XNode.NodePort.IO.Input) rect.position = rect.position - new Vector2(16, 0);
            else if (port.Direction == XNode.NodePort.IO.Output) rect.position = rect.position + new Vector2(rect.width, 0);
            rect.size = new Vector2(16, 16);

            Color backgroundColor = new Color32(90, 97, 105, 255);
            if (NodeEditorWindow.nodeTint.ContainsKey(port.Node.GetType())) backgroundColor *= NodeEditorWindow.nodeTint[port.Node.GetType()];
            var col = NodeEditorWindow.Current.GraphEditor.GetTypeColor(port.ValueType);
            DrawPortHandle(rect, backgroundColor, col);

            // Register the handle position
            var portPos = rect.center;
            if (NodeEditor.portPositions.ContainsKey(port)) NodeEditor.portPositions[port] = portPos;
            else NodeEditor.portPositions.Add(port, portPos);
        }

        public static void DrawPortHandle(Rect rect, Color backgroundColor, Color typeColor)
        {
            var col = GUI.color;
            GUI.color = backgroundColor;
            GUI.DrawTexture(rect, NodeEditorResources.DotOuter);
            GUI.color = typeColor;
            GUI.DrawTexture(rect, NodeEditorResources.Dot);
            GUI.color = col;
        }
    }
}