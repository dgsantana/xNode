using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace XNodeEditor
{
    public partial class NodeEditorWindow
    {
        public enum NodeActivity { Idle, HoldNode, DragNode, HoldGrid, DragGrid }
        public static NodeActivity CurrentActivity = NodeActivity.Idle;
        public static bool IsPanning { get; private set; }
        public static Vector2[] DragOffset;

        private bool IsDraggingPort { get { return _draggedOutput != null; } }
        private bool IsHoveringPort { get { return _hoveredPort != null; } }
        private bool IsHoveringNode { get { return _hoveredNode != null; } }
        private bool IsHoveringReroute { get { return _hoveredReroute.Port != null; } }
        private XNode.Node _hoveredNode = null;
        [NonSerialized] private XNode.NodePort _hoveredPort = null;
        [NonSerialized] private XNode.NodePort _draggedOutput = null;
        [NonSerialized] private XNode.NodePort _draggedOutputTarget = null;
        [NonSerialized] private List<Vector2> _draggedOutputReroutes = new List<Vector2>();
        private RerouteReference _hoveredReroute = new RerouteReference();
        private List<RerouteReference> _selectedReroutes = new List<RerouteReference>();
        private Rect _nodeRects;
        private Vector2 _dragBoxStart;
        private UnityEngine.Object[] _preBoxSelection;
        private RerouteReference[] _preBoxSelectionReroute;
        private Rect _selectionBox;

        private struct RerouteReference
        {
            public XNode.NodePort Port;
            public int ConnectionIndex;
            public int PointIndex;

            public RerouteReference(XNode.NodePort port, int connectionIndex, int pointIndex)
            {
                this.Port = port;
                this.ConnectionIndex = connectionIndex;
                this.PointIndex = pointIndex;
            }

            public void InsertPoint(Vector2 pos) { Port.GetReroutePoints(ConnectionIndex).Insert(PointIndex, pos); }
            public void SetPoint(Vector2 pos) { Port.GetReroutePoints(ConnectionIndex)[PointIndex] = pos; }
            public void RemovePoint() { Port.GetReroutePoints(ConnectionIndex).RemoveAt(PointIndex); }
            public Vector2 GetPoint() { return Port.GetReroutePoints(ConnectionIndex)[PointIndex]; }
        }

        public void Controls()
        {
            wantsMouseMove = true;
            Event e = Event.current;
            switch (e.type)
            {
                case EventType.MouseMove:
                    break;
                case EventType.ScrollWheel:
                    if (e.delta.y > 0) Zoom += 0.1f * Zoom;
                    else Zoom -= 0.1f * Zoom;
                    break;
                case EventType.MouseDrag:
                    if (e.button == 0)
                    {
                        if (IsDraggingPort)
                        {
                            if (IsHoveringPort && _hoveredPort.IsInput)
                            {
                                if (!_draggedOutput.IsConnectedTo(_hoveredPort))
                                {
                                    _draggedOutputTarget = _hoveredPort;
                                }
                            }
                            else
                            {
                                _draggedOutputTarget = null;
                            }
                            Repaint();
                        }
                        else if (CurrentActivity == NodeActivity.HoldNode)
                        {
                            RecalculateDragOffsets(e);
                            CurrentActivity = NodeActivity.DragNode;
                            Repaint();
                        }
                        if (CurrentActivity == NodeActivity.DragNode)
                        {
                            // Holding ctrl inverts grid snap
                            bool gridSnap = NodeEditorPreferences.GetSettings().GridSnap;
                            if (e.control) gridSnap = !gridSnap;

                            Vector2 mousePos = WindowToGridPosition(e.mousePosition);
                            // Move selected nodes with offset
                            for (int i = 0; i < Selection.objects.Length; i++)
                            {
                                if (Selection.objects[i] is XNode.Node)
                                {
                                    XNode.Node node = Selection.objects[i] as XNode.Node;
                                    node.Position = mousePos + DragOffset[i];
                                    if (gridSnap)
                                    {
                                        node.Position.x = (Mathf.Round((node.Position.x + 8) / 16) * 16) - 8;
                                        node.Position.y = (Mathf.Round((node.Position.y + 8) / 16) * 16) - 8;
                                    }
                                }
                            }
                            // Move selected reroutes with offset
                            for (int i = 0; i < _selectedReroutes.Count; i++)
                            {
                                Vector2 pos = mousePos + DragOffset[Selection.objects.Length + i];
                                pos.x -= 8;
                                pos.y -= 8;
                                if (gridSnap)
                                {
                                    pos.x = (Mathf.Round((pos.x + 8) / 16) * 16);
                                    pos.y = (Mathf.Round((pos.y + 8) / 16) * 16);
                                }
                                _selectedReroutes[i].SetPoint(pos);
                            }
                            Repaint();
                        }
                        else if (CurrentActivity == NodeActivity.HoldGrid)
                        {
                            CurrentActivity = NodeActivity.DragGrid;
                            _preBoxSelection = Selection.objects;
                            _preBoxSelectionReroute = _selectedReroutes.ToArray();
                            _dragBoxStart = WindowToGridPosition(e.mousePosition);
                            Repaint();
                        }
                        else if (CurrentActivity == NodeActivity.DragGrid)
                        {
                            Vector2 boxStartPos = GridToWindowPosition(_dragBoxStart);
                            Vector2 boxSize = e.mousePosition - boxStartPos;
                            if (boxSize.x < 0) { boxStartPos.x += boxSize.x; boxSize.x = Mathf.Abs(boxSize.x); }
                            if (boxSize.y < 0) { boxStartPos.y += boxSize.y; boxSize.y = Mathf.Abs(boxSize.y); }
                            _selectionBox = new Rect(boxStartPos, boxSize);
                            Repaint();
                        }
                    }
                    else if (e.button == 1 || e.button == 2)
                    {
                        Vector2 tempOffset = PanOffset;
                        tempOffset += e.delta * Zoom;
                        // Round value to increase crispyness of UI text
                        tempOffset.x = Mathf.Round(tempOffset.x);
                        tempOffset.y = Mathf.Round(tempOffset.y);
                        PanOffset = tempOffset;
                        IsPanning = true;
                    }
                    break;
                case EventType.MouseDown:
                    Repaint();
                    if (e.button == 0)
                    {
                        _draggedOutputReroutes.Clear();

                        if (IsHoveringPort)
                        {
                            if (_hoveredPort.IsOutput)
                            {
                                _draggedOutput = _hoveredPort;
                            }
                            else
                            {
                                _hoveredPort.VerifyConnections();
                                if (_hoveredPort.IsConnected)
                                {
                                    XNode.Node node = _hoveredPort.Node;
                                    XNode.NodePort output = _hoveredPort.Connection;
                                    int outputConnectionIndex = output.GetConnectionIndex(_hoveredPort);
                                    _draggedOutputReroutes = output.GetReroutePoints(outputConnectionIndex);
                                    _hoveredPort.Disconnect(output);
                                    _draggedOutput = output;
                                    _draggedOutputTarget = _hoveredPort;
                                    if (NodeEditor.onUpdateNode != null) NodeEditor.onUpdateNode(node);
                                }
                            }
                        }
                        else if (IsHoveringNode && IsHoveringTitle(_hoveredNode))
                        {
                            // If mousedown on node header, select or deselect
                            if (!Selection.Contains(_hoveredNode))
                            {
                                SelectNode(_hoveredNode, e.control || e.shift);
                                if (!e.control && !e.shift) _selectedReroutes.Clear();
                            }
                            else if (e.control || e.shift) DeselectNode(_hoveredNode);
                            e.Use();
                            CurrentActivity = NodeActivity.HoldNode;
                        }
                        else if (IsHoveringReroute)
                        {
                            // If reroute isn't selected
                            if (!_selectedReroutes.Contains(_hoveredReroute))
                            {
                                // Add it
                                if (e.control || e.shift) _selectedReroutes.Add(_hoveredReroute);
                                // Select it
                                else
                                {
                                    _selectedReroutes = new List<RerouteReference>() { _hoveredReroute };
                                    Selection.activeObject = null;
                                }

                            }
                            // Deselect
                            else if (e.control || e.shift) _selectedReroutes.Remove(_hoveredReroute);
                            e.Use();
                            CurrentActivity = NodeActivity.HoldNode;
                        }
                        // If mousedown on grid background, deselect all
                        else if (!IsHoveringNode)
                        {
                            CurrentActivity = NodeActivity.HoldGrid;
                            if (!e.control && !e.shift)
                            {
                                _selectedReroutes.Clear();
                                Selection.activeObject = null;
                            }
                        }
                    }
                    break;
                case EventType.MouseUp:
                    if (e.button == 0)
                    {
                        //Port drag release
                        if (IsDraggingPort)
                        {
                            //If connection is valid, save it
                            if (_draggedOutputTarget != null)
                            {
                                XNode.Node node = _draggedOutputTarget.Node;
                                if (graph.Nodes.Count != 0) _draggedOutput.Connect(_draggedOutputTarget);

                                // ConnectionIndex can be -1 if the connection is removed instantly after creation
                                int connectionIndex = _draggedOutput.GetConnectionIndex(_draggedOutputTarget);
                                if (connectionIndex != -1)
                                {
                                    _draggedOutput.GetReroutePoints(connectionIndex).AddRange(_draggedOutputReroutes);
                                    if (NodeEditor.onUpdateNode != null) NodeEditor.onUpdateNode(node);
                                    EditorUtility.SetDirty(graph);
                                }
                            }
                            //Release dragged connection
                            _draggedOutput = null;
                            _draggedOutputTarget = null;
                            EditorUtility.SetDirty(graph);
                            if (NodeEditorPreferences.GetSettings().AutoSave) AssetDatabase.SaveAssets();
                        }
                        else if (CurrentActivity == NodeActivity.DragNode)
                        {
                            if (NodeEditorPreferences.GetSettings().AutoSave) AssetDatabase.SaveAssets();
                        }
                        else if (!IsHoveringNode)
                        {
                            // If click outside node, release field focus
                            if (!IsPanning)
                            {
                                // I've got no idea which of these do what, so we'll just reset all of it.
                                GUIUtility.hotControl = 0;
                                GUIUtility.keyboardControl = 0;
                                EditorGUIUtility.editingTextField = false;
                                EditorGUIUtility.keyboardControl = 0;
                                EditorGUIUtility.hotControl = 0;
                            }
                            if (NodeEditorPreferences.GetSettings().AutoSave) AssetDatabase.SaveAssets();
                        }

                        // If click node header, select it.
                        if (CurrentActivity == NodeActivity.HoldNode && !(e.control || e.shift))
                        {
                            _selectedReroutes.Clear();
                            SelectNode(_hoveredNode, false);
                        }

                        // If click reroute, select it.
                        if (IsHoveringReroute && !(e.control || e.shift))
                        {
                            _selectedReroutes = new List<RerouteReference>() { _hoveredReroute };
                            Selection.activeObject = null;
                        }

                        Repaint();
                        CurrentActivity = NodeActivity.Idle;
                    }
                    else if (e.button == 1)
                    {
                        if (!IsPanning)
                        {
                            if (IsDraggingPort)
                            {
                                _draggedOutputReroutes.Add(WindowToGridPosition(e.mousePosition));
                            }
                            else if (CurrentActivity == NodeActivity.DragNode && Selection.activeObject == null && _selectedReroutes.Count == 1)
                            {
                                _selectedReroutes[0].InsertPoint(_selectedReroutes[0].GetPoint());
                                _selectedReroutes[0] = new RerouteReference(_selectedReroutes[0].Port, _selectedReroutes[0].ConnectionIndex, _selectedReroutes[0].PointIndex + 1);
                            }
                            else if (IsHoveringReroute)
                            {
                                ShowRerouteContextMenu(_hoveredReroute);
                            }
                            else if (IsHoveringPort)
                            {
                                ShowPortContextMenu(_hoveredPort);
                            }
                            else if (IsHoveringNode && IsHoveringTitle(_hoveredNode))
                            {
                                if (!Selection.Contains(_hoveredNode)) SelectNode(_hoveredNode, false);
                                ShowNodeContextMenu();
                            }
                            else if (!IsHoveringNode)
                            {
                                ShowGraphContextMenu();
                            }
                        }
                        IsPanning = false;
                    }
                    break;
                case EventType.KeyDown:
                    if (EditorGUIUtility.editingTextField) break;
                    else if (e.keyCode == KeyCode.F) Home();
                    if (SystemInfo.operatingSystemFamily == OperatingSystemFamily.MacOSX)
                    {
                        if (e.keyCode == KeyCode.Return) RenameSelectedNode();
                    }
                    else
                    {
                        if (e.keyCode == KeyCode.F2) RenameSelectedNode();
                    }
                    break;
                case EventType.ValidateCommand:
                    if (e.commandName == "SoftDelete") RemoveSelectedNodes();
                    else if (e.commandName == "Duplicate") DublicateSelectedNodes();
                    Repaint();
                    break;
                case EventType.Ignore:
                    // If release mouse outside window
                    if (e.rawType == EventType.MouseUp && CurrentActivity == NodeActivity.DragGrid)
                    {
                        Repaint();
                        CurrentActivity = NodeActivity.Idle;
                    }
                    break;
            }
        }

        private void RecalculateDragOffsets(Event current)
        {
            DragOffset = new Vector2[Selection.objects.Length + _selectedReroutes.Count];
            // Selected nodes
            for (int i = 0; i < Selection.objects.Length; i++)
            {
                if (Selection.objects[i] is XNode.Node)
                {
                    XNode.Node node = Selection.objects[i] as XNode.Node;
                    DragOffset[i] = node.Position - WindowToGridPosition(current.mousePosition);
                }
            }

            // Selected reroutes
            for (int i = 0; i < _selectedReroutes.Count; i++)
            {
                DragOffset[Selection.objects.Length + i] = _selectedReroutes[i].GetPoint() - WindowToGridPosition(current.mousePosition);
            }
        }

        /// <summary> Puts all nodes in focus. If no nodes are present, resets view to  </summary>
        public void Home()
        {
            Zoom = 2;
            PanOffset = Vector2.zero;
        }

        public void CreateNode(Type type, Vector2 position)
        {
            XNode.Node node = graph.AddNode(type);
            node.Position = position;
            node.name = UnityEditor.ObjectNames.NicifyVariableName(type.ToString());
            AssetDatabase.AddObjectToAsset(node, graph);
            if (NodeEditorPreferences.GetSettings().AutoSave) AssetDatabase.SaveAssets();
            Repaint();
        }

        /// <summary> Remove nodes in the graph in Selection.objects</summary>
        public void RemoveSelectedNodes()
        {
            // We need to delete reroutes starting at the highest point index to avoid shifting indices
            _selectedReroutes = _selectedReroutes.OrderByDescending(x => x.PointIndex).ToList();
            for (int i = 0; i < _selectedReroutes.Count; i++)
            {
                _selectedReroutes[i].RemovePoint();
            }
            _selectedReroutes.Clear();
            foreach (UnityEngine.Object item in Selection.objects)
            {
                if (item is XNode.Node)
                {
                    XNode.Node node = item as XNode.Node;
                    GraphEditor.RemoveNode(node);
                }
            }
        }

        /// <summary> Initiate a rename on the currently selected node </summary>
        public void RenameSelectedNode()
        {
            if (Selection.objects.Length == 1 && Selection.activeObject is XNode.Node)
            {
                XNode.Node node = Selection.activeObject as XNode.Node;
                NodeEditor.GetEditor(node).InitiateRename();
            }
        }

        /// <summary> Draw this node on top of other nodes by placing it last in the graph.nodes list </summary>
        public void MoveNodeToTop(XNode.Node node)
        {
            int index;
            while ((index = graph.Nodes.IndexOf(node)) != graph.Nodes.Count - 1)
            {
                graph.Nodes[index] = graph.Nodes[index + 1];
                graph.Nodes[index + 1] = node;
            }
        }

        /// <summary> Dublicate selected nodes and select the dublicates </summary>
        public void DublicateSelectedNodes()
        {
            UnityEngine.Object[] newNodes = new UnityEngine.Object[Selection.objects.Length];
            Dictionary<XNode.Node, XNode.Node> substitutes = new Dictionary<XNode.Node, XNode.Node>();
            for (int i = 0; i < Selection.objects.Length; i++)
            {
                if (Selection.objects[i] is XNode.Node)
                {
                    XNode.Node srcNode = Selection.objects[i] as XNode.Node;
                    if (srcNode.Graph != graph) continue; // ignore nodes selected in another graph
                    XNode.Node newNode = GraphEditor.CopyNode(srcNode);
                    substitutes.Add(srcNode, newNode);
                    newNode.Position = srcNode.Position + new Vector2(30, 30);
                    newNodes[i] = newNode;
                }
            }

            // Walk through the selected nodes again, recreate connections, using the new nodes
            for (int i = 0; i < Selection.objects.Length; i++)
            {
                if (Selection.objects[i] is XNode.Node)
                {
                    XNode.Node srcNode = Selection.objects[i] as XNode.Node;
                    if (srcNode.Graph != graph) continue; // ignore nodes selected in another graph
                    foreach (XNode.NodePort port in srcNode.Ports)
                    {
                        for (int c = 0; c < port.ConnectionCount; c++)
                        {
                            XNode.NodePort inputPort = port.Direction == XNode.NodePort.IO.Input ? port : port.GetConnection(c);
                            XNode.NodePort outputPort = port.Direction == XNode.NodePort.IO.Output ? port : port.GetConnection(c);

                            if (substitutes.ContainsKey(inputPort.Node) && substitutes.ContainsKey(outputPort.Node))
                            {
                                XNode.Node newNodeIn = substitutes[inputPort.Node];
                                XNode.Node newNodeOut = substitutes[outputPort.Node];
                                newNodeIn.UpdateStaticPorts();
                                newNodeOut.UpdateStaticPorts();
                                inputPort = newNodeIn.GetInputPort(inputPort.FieldName);
                                outputPort = newNodeOut.GetOutputPort(outputPort.FieldName);
                            }
                            if (!inputPort.IsConnectedTo(outputPort)) inputPort.Connect(outputPort);
                        }
                    }
                }
            }
            Selection.objects = newNodes;
        }

        /// <summary> Draw a connection as we are dragging it </summary>
        public void DrawDraggedConnection()
        {
            if (IsDraggingPort)
            {
                Color col = NodeEditorPreferences.GetTypeColor(_draggedOutput.ValueType);

                if (!_portConnectionPoints.ContainsKey(_draggedOutput)) return;
                col.a = 0.6f;
                Vector2 from = _portConnectionPoints[_draggedOutput].center;
                Vector2 to = Vector2.zero;
                for (int i = 0; i < _draggedOutputReroutes.Count; i++)
                {
                    to = _draggedOutputReroutes[i];
                    DrawConnection(from, to, col);
                    from = to;
                }
                to = _draggedOutputTarget != null ? PortConnectionPoints[_draggedOutputTarget].center : WindowToGridPosition(Event.current.mousePosition);
                DrawConnection(from, to, col);

                Color bgcol = Color.black;
                Color frcol = col;
                bgcol.a = 0.6f;
                frcol.a = 0.6f;

                // Loop through reroute points again and draw the points
                for (int i = 0; i < _draggedOutputReroutes.Count; i++)
                {
                    // Draw reroute point at position
                    Rect rect = new Rect(_draggedOutputReroutes[i], new Vector2(16, 16));
                    rect.position = new Vector2(rect.position.x - 8, rect.position.y - 8);
                    rect = GridToWindowRect(rect);

                    NodeEditorGUILayout.DrawPortHandle(rect, bgcol, frcol);
                }
            }
        }

        bool IsHoveringTitle(XNode.Node node)
        {
            Vector2 mousePos = Event.current.mousePosition;
            //Get node position
            Vector2 nodePos = GridToWindowPosition(node.Position);
            float width = 200;
            if (NodeWidths.ContainsKey(node)) width = NodeWidths[node];
            Rect windowRect = new Rect(nodePos, new Vector2(width / Zoom, 30 / Zoom));
            return windowRect.Contains(mousePos);
        }
    }
}