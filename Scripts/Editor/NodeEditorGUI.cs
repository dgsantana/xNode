using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace XNodeEditor
{
    /// <summary> Contains GUI methods </summary>
    public partial class NodeEditorWindow
    {
        public NodeGraphEditor GraphEditor;
        private List<UnityEngine.Object> _selectionCache;

        protected virtual void OnGUI()
        {
            var e = Event.current;
            var m = GUI.matrix;
            if (graph == null) return;
            GraphEditor = NodeGraphEditor.GetEditor(graph);
            GraphEditor.Position = position;

            Controls();

            DrawGrid(position, Zoom, PanOffset);
            DrawConnections();
            DrawDraggedConnection();
            DrawNodes();
            DrawSelectionBox();
            DrawTooltip();
            GraphEditor.OnGUI();

            GUI.matrix = m;
        }

        public static void BeginZoomed(Rect rect, float zoom)
        {
            GUI.EndClip();

            GUIUtility.ScaleAroundPivot(Vector2.one / zoom, rect.size * 0.5f);
            var padding = new Vector4(0, 22, 0, 0);
            padding *= zoom;
            GUI.BeginClip(new Rect(-((rect.width * zoom) - rect.width) * 0.5f, -(((rect.height * zoom) - rect.height) * 0.5f) + (22 * zoom),
                rect.width * zoom,
                rect.height * zoom));
        }

        public static void EndZoomed(Rect rect, float zoom)
        {
            GUIUtility.ScaleAroundPivot(Vector2.one * zoom, rect.size * 0.5f);
            var offset = new Vector3(
                (((rect.width * zoom) - rect.width) * 0.5f),
                (((rect.height * zoom) - rect.height) * 0.5f) + (-22 * zoom) + 22,
                0);
            GUI.matrix = Matrix4x4.TRS(offset, Quaternion.identity, Vector3.one);
        }

        public void DrawGrid(Rect rect, float zoom, Vector2 panOffset)
        {

            rect.position = Vector2.zero;

            var center = rect.size / 2f;
            var gridTex = GraphEditor.GetGridTexture();
            var crossTex = GraphEditor.GetSecondaryGridTexture();

            // Offset from origin in tile units
            var xOffset = -(center.x * zoom + panOffset.x) / gridTex.width;
            var yOffset = ((center.y - rect.size.y) * zoom + panOffset.y) / gridTex.height;

            var tileOffset = new Vector2(xOffset, yOffset);

            // Amount of tiles
            var tileAmountX = Mathf.Round(rect.size.x * zoom) / gridTex.width;
            var tileAmountY = Mathf.Round(rect.size.y * zoom) / gridTex.height;

            var tileAmount = new Vector2(tileAmountX, tileAmountY);

            // Draw tiled background
            GUI.DrawTextureWithTexCoords(rect, gridTex, new Rect(tileOffset, tileAmount));
            GUI.DrawTextureWithTexCoords(rect, crossTex, new Rect(tileOffset + new Vector2(0.5f, 0.5f), tileAmount));
        }

        public void DrawSelectionBox()
        {
            if (CurrentActivity == NodeActivity.DragGrid)
            {
                var curPos = WindowToGridPosition(Event.current.mousePosition);
                var size = curPos - _dragBoxStart;
                var r = new Rect(_dragBoxStart, size);
                r.position = GridToWindowPosition(r.position);
                r.size /= Zoom;
                Handles.DrawSolidRectangleWithOutline(r, new Color(0, 0, 0, 0.1f), new Color(1, 1, 1, 0.6f));
            }
        }

        public static bool DropdownButton(string name, float width)
        {
            return GUILayout.Button(name, EditorStyles.toolbarDropDown, GUILayout.Width(width));
        }

        /// <summary> Show right-click context menu for hovered reroute </summary>
        private void ShowRerouteContextMenu(RerouteReference reroute)
        {
            var contextMenu = new GenericMenu();
            contextMenu.AddItem(new GUIContent("Remove"), false, reroute.RemovePoint);
            contextMenu.DropDown(new Rect(Event.current.mousePosition, Vector2.zero));
            if (NodeEditorPreferences.GetSettings().AutoSave) AssetDatabase.SaveAssets();
        }

        /// <summary> Show right-click context menu for hovered port </summary>
        private void ShowPortContextMenu(XNode.NodePort hoveredPort)
        {
            var contextMenu = new GenericMenu();
            contextMenu.AddItem(new GUIContent("Clear Connections"), false, hoveredPort.ClearConnections);
            contextMenu.DropDown(new Rect(Event.current.mousePosition, Vector2.zero));
            if (NodeEditorPreferences.GetSettings().AutoSave) AssetDatabase.SaveAssets();
        }

        /// <summary> Show right-click context menu for selected nodes </summary>
        public void ShowNodeContextMenu()
        {
            var contextMenu = new GenericMenu();
            // If only one node is selected
            if (Selection.objects.Length == 1 && Selection.activeObject is XNode.Node)
            {
                var node = Selection.activeObject as XNode.Node;
                contextMenu.AddItem(new GUIContent("Move To Top"), false, () => MoveNodeToTop(node));
                contextMenu.AddItem(new GUIContent("Rename"), false, RenameSelectedNode);
            }

            contextMenu.AddItem(new GUIContent("Duplicate"), false, DublicateSelectedNodes);
            contextMenu.AddItem(new GUIContent("Remove"), false, RemoveSelectedNodes);

            // If only one node is selected
            if (Selection.objects.Length == 1 && Selection.activeObject is XNode.Node)
            {
                var node = Selection.activeObject as XNode.Node;
                AddCustomContextMenuItems(contextMenu, node);
            }

            contextMenu.DropDown(new Rect(Event.current.mousePosition, Vector2.zero));
        }

        /// <summary> Show right-click context menu for current graph </summary>
        private void ShowGraphContextMenu()
        {
            var contextMenu = new GenericMenu();
            var pos = WindowToGridPosition(Event.current.mousePosition);
            for (var i = 0; i < nodeTypes.Length; i++)
            {
                var type = nodeTypes[i];

                //Get node context menu path
                var path = GraphEditor.GetNodePath(type);
                if (path == null) continue;

                contextMenu.AddItem(new GUIContent(path), false, () =>
                {
                    CreateNode(type, pos);
                });
            }
            contextMenu.AddSeparator("");
            contextMenu.AddItem(new GUIContent("Preferences"), false, OpenPreferences);
            AddCustomContextMenuItems(contextMenu, graph);
            contextMenu.DropDown(new Rect(Event.current.mousePosition, Vector2.zero));
        }

        private void AddCustomContextMenuItems(GenericMenu contextMenu, object obj)
        {
            var items = GetContextMenuMethods(obj);
            if (items.Length == 0) return;

            contextMenu.AddSeparator("");
            foreach (var kvp in items)
            {
                var kvp1 = kvp;
                contextMenu.AddItem(new GUIContent(kvp.Key.menuItem), false, () => kvp1.Value.Invoke(obj, null));
            }
        }

        /// <summary> Draw a bezier from startpoint to endpoint, both in grid coordinates </summary>
        public void DrawConnection(Vector2 startPoint, Vector2 endPoint, Color col)
        {
            startPoint = GridToWindowPosition(startPoint);
            endPoint = GridToWindowPosition(endPoint);

            switch (NodeEditorPreferences.GetSettings().NoodleType)
            {
                case NodeEditorPreferences.NoodleType.Curve:
                    var startTangent = startPoint;
                    if (startPoint.x < endPoint.x) startTangent.x = Mathf.LerpUnclamped(startPoint.x, endPoint.x, 0.7f);
                    else startTangent.x = Mathf.LerpUnclamped(startPoint.x, endPoint.x, -0.7f);

                    var endTangent = endPoint;
                    if (startPoint.x > endPoint.x) endTangent.x = Mathf.LerpUnclamped(endPoint.x, startPoint.x, -0.7f);
                    else endTangent.x = Mathf.LerpUnclamped(endPoint.x, startPoint.x, 0.7f);
                    Handles.DrawBezier(startPoint, endPoint, startTangent, endTangent, col, null, 4);
                    break;
                case NodeEditorPreferences.NoodleType.Line:
                    Handles.color = col;
                    Handles.DrawAAPolyLine(5, startPoint, endPoint);
                    break;
                case NodeEditorPreferences.NoodleType.Angled:
                    Handles.color = col;
                    if (startPoint.x <= endPoint.x - (50 / Zoom))
                    {
                        var midpoint = (startPoint.x + endPoint.x) * 0.5f;
                        var start1 = startPoint;
                        var end1 = endPoint;
                        start1.x = midpoint;
                        end1.x = midpoint;
                        Handles.DrawAAPolyLine(5, startPoint, start1);
                        Handles.DrawAAPolyLine(5, start1, end1);
                        Handles.DrawAAPolyLine(5, end1, endPoint);
                    }
                    else
                    {
                        var midpoint = (startPoint.y + endPoint.y) * 0.5f;
                        var start1 = startPoint;
                        var end1 = endPoint;
                        start1.x += 25 / Zoom;
                        end1.x -= 25 / Zoom;
                        var start2 = start1;
                        var end2 = end1;
                        start2.y = midpoint;
                        end2.y = midpoint;
                        Handles.DrawAAPolyLine(5, startPoint, start1);
                        Handles.DrawAAPolyLine(5, start1, start2);
                        Handles.DrawAAPolyLine(5, start2, end2);
                        Handles.DrawAAPolyLine(5, end2, end1);
                        Handles.DrawAAPolyLine(5, end1, endPoint);
                    }
                    break;
            }
        }

        /// <summary> Draws all connections </summary>
        public void DrawConnections()
        {
            var mousePos = Event.current.mousePosition;
            var selection = _preBoxSelectionReroute != null ? new List<RerouteReference>(_preBoxSelectionReroute) : new List<RerouteReference>();
            _hoveredReroute = new RerouteReference();

            var col = GUI.color;
            foreach (var node in graph.Nodes)
            {
                //If a null node is found, return. This can happen if the nodes associated script is deleted. It is currently not possible in Unity to delete a null asset.
                if (node == null) continue;

                // Draw full connections and output > reroute
                foreach (var output in node.Outputs)
                {
                    //Needs cleanup. Null checks are ugly
                    if (!PortConnectionPoints.ContainsKey(output)) continue;

                    var connectionColor = GraphEditor.GetTypeColor(output.ValueType);

                    for (var k = 0; k < output.ConnectionCount; k++)
                    {
                        var input = output.GetConnection(k);

                        // Error handling
                        if (input == null) continue; //If a script has been updated and the port doesn't exist, it is removed and null is returned. If this happens, return.
                        if (!input.IsConnectedTo(output)) input.Connect(output);
                        if (!_portConnectionPoints.ContainsKey(input)) continue;

                        var from = _portConnectionPoints[output].center;
                        var reroutePoints = output.GetReroutePoints(k);
                        // Loop through reroute points and draw the path
                        foreach (var t in reroutePoints)
                        {
                            DrawConnection(from, t, connectionColor);
                            from = t;
                        }
                        var to = _portConnectionPoints[input].center;
                        DrawConnection(from, to, connectionColor);

                        // Loop through reroute points again and draw the points
                        for (var i = 0; i < reroutePoints.Count; i++)
                        {
                            var rerouteRef = new RerouteReference(output, k, i);
                            // Draw reroute point at position
                            var rect = new Rect(reroutePoints[i], new Vector2(12, 12));
                            rect.position = new Vector2(rect.position.x - 6, rect.position.y - 6);
                            rect = GridToWindowRect(rect);

                            // Draw selected reroute points with an outline
                            if (_selectedReroutes.Contains(rerouteRef))
                            {
                                GUI.color = NodeEditorPreferences.GetSettings().HighlightColor;
                                GUI.DrawTexture(rect, NodeEditorResources.DotOuter);
                            }

                            GUI.color = connectionColor;
                            GUI.DrawTexture(rect, NodeEditorResources.Dot);
                            if (rect.Overlaps(_selectionBox)) selection.Add(rerouteRef);
                            if (rect.Contains(mousePos)) _hoveredReroute = rerouteRef;

                        }
                    }
                }
            }
            GUI.color = col;
            if (Event.current.type != EventType.Layout && CurrentActivity == NodeActivity.DragGrid) _selectedReroutes = selection;
        }

        private void DrawNodes()
        {
            var e = Event.current;
            if (e.type == EventType.Layout)
            {
                _selectionCache = new List<UnityEngine.Object>(Selection.objects);
            }
            if (e.type == EventType.Repaint)
            {
                PortConnectionPoints.Clear();
                NodeWidths.Clear();
            }

            //Active node is hashed before and after node GUI to detect changes
            var nodeHash = 0;
            MethodInfo onValidate = null;
            if (Selection.activeObject != null && Selection.activeObject is XNode.Node)
            {
                onValidate = Selection.activeObject.GetType().GetMethod("OnValidate");
                if (onValidate != null) nodeHash = Selection.activeObject.GetHashCode();
            }

            BeginZoomed(position, Zoom);

            var mousePos = Event.current.mousePosition;

            if (e.type != EventType.Layout)
            {
                _hoveredNode = null;
                _hoveredPort = null;
            }

            var preSelection = _preBoxSelection != null ? new List<UnityEngine.Object>(_preBoxSelection) : new List<UnityEngine.Object>();

            // Selection box stuff
            var boxStartPos = GridToWindowPositionNoClipped(_dragBoxStart);
            var boxSize = mousePos - boxStartPos;
            if (boxSize.x < 0) { boxStartPos.x += boxSize.x; boxSize.x = Mathf.Abs(boxSize.x); }
            if (boxSize.y < 0) { boxStartPos.y += boxSize.y; boxSize.y = Mathf.Abs(boxSize.y); }
            var selectionBox = new Rect(boxStartPos, boxSize);

            //Save guiColor so we can revert it
            var guiColor = GUI.color;
            for (var n = 0; n < graph.Nodes.Count; n++)
            {
                // Skip null nodes. The user could be in the process of renaming scripts, so removing them at this point is not advisable.
                if (graph.Nodes[n] == null) continue;
                if (n >= graph.Nodes.Count) return;
                var node = graph.Nodes[n];

                var nodeEditor = NodeEditor.GetEditor(node);
                NodeEditor.portPositions = new Dictionary<XNode.NodePort, Vector2>();

                //Get node position
                var nodePos = GridToWindowPositionNoClipped(node.Position);

                GUILayout.BeginArea(new Rect(nodePos, new Vector2(nodeEditor.GetWidth(), 4000)));

                var selected = _selectionCache.Contains(graph.Nodes[n]);

                if (selected)
                {
                    var style = new GUIStyle(NodeEditorResources.Styles.NodeBody);
                    var highlightStyle = new GUIStyle(NodeEditorResources.Styles.NodeHighlight);
                    highlightStyle.padding = style.padding;
                    style.padding = new RectOffset();
                    GUI.color = nodeEditor.GetTint();
                    GUILayout.BeginVertical(new GUIStyle(style));
                    GUI.color = NodeEditorPreferences.GetSettings().HighlightColor;
                    GUILayout.BeginVertical(new GUIStyle(highlightStyle));
                }
                else
                {
                    var style = NodeEditorResources.Styles.NodeBody;
                    GUI.color = nodeEditor.GetTint();
                    GUILayout.BeginVertical(new GUIStyle(style));
                }

                GUI.color = guiColor;
                EditorGUI.BeginChangeCheck();

                //Draw node contents
                nodeEditor.OnNodeGUI();

                //Apply
                nodeEditor.serializedObject.ApplyModifiedProperties();

                //If user changed a value, notify other scripts through onUpdateNode
                if (EditorGUI.EndChangeCheck())
                {
                    NodeEditor.onUpdateNode?.Invoke(node);
                }

                if (e.type == EventType.Repaint)
                {
                    NodeWidths.Add(node, nodeEditor.GetWidth());

                    foreach (var kvp in NodeEditor.portPositions)
                    {
                        var portHandlePos = kvp.Value;
                        portHandlePos += node.Position;
                        var rect = new Rect(portHandlePos.x - 8, portHandlePos.y - 8, 16, 16);
                        PortConnectionPoints.Add(kvp.Key, rect);
                    }
                }

                GUILayout.EndVertical();
                if (selected) GUILayout.EndVertical();

                if (e.type != EventType.Layout)
                {
                    //Check if we are hovering this node
                    var nodeSize = GUILayoutUtility.GetLastRect().size;
                    var windowRect = new Rect(nodePos, nodeSize);
                    if (windowRect.Contains(mousePos)) _hoveredNode = node;

                    //If dragging a selection box, add nodes inside to selection
                    if (CurrentActivity == NodeActivity.DragGrid)
                    {
                        if (windowRect.Overlaps(selectionBox)) preSelection.Add(node);
                    }

                    //Check if we are hovering any of this nodes ports
                    //Check input ports
                    foreach (var input in node.Inputs)
                    {
                        //Check if port rect is available
                        if (!PortConnectionPoints.ContainsKey(input)) continue;
                        var r = GridToWindowRectNoClipped(PortConnectionPoints[input]);
                        if (r.Contains(mousePos)) _hoveredPort = input;
                    }
                    //Check all output ports
                    foreach (var output in node.Outputs)
                    {
                        //Check if port rect is available
                        if (!PortConnectionPoints.ContainsKey(output)) continue;
                        var r = GridToWindowRectNoClipped(PortConnectionPoints[output]);
                        if (r.Contains(mousePos)) _hoveredPort = output;
                    }
                }

                GUILayout.EndArea();
            }

            if (e.type != EventType.Layout && CurrentActivity == NodeActivity.DragGrid) Selection.objects = preSelection.ToArray();
            EndZoomed(position, Zoom);

            //If a change in hash is detected in the selected node, call OnValidate method. 
            //This is done through reflection because OnValidate is only relevant in editor, 
            //and thus, the code should not be included in build.
            if (nodeHash != 0)
            {
                if (onValidate != null && nodeHash != Selection.activeObject.GetHashCode()) onValidate.Invoke(Selection.activeObject, null);
            }
        }

        private void DrawTooltip()
        {
            if (_hoveredPort != null)
            {
                var type = _hoveredPort.ValueType;
                var content = new GUIContent();
                content.text = type.PrettyName();
                if (_hoveredPort.IsStatic && _hoveredPort.IsOutput)
                {
                    var obj = _hoveredPort.Node.GetValue(_hoveredPort);
                    content.text += " = " + (obj != null ? obj.ToString() : "null");
                }
                var size = NodeEditorResources.Styles.Tooltip.CalcSize(content);
                var rect = new Rect(Event.current.mousePosition - (size), size);
                EditorGUI.LabelField(rect, content, NodeEditorResources.Styles.Tooltip);
                Repaint();
            }
        }
    }
}