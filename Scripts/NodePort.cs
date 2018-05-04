using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace XNode
{
    [Serializable]
    public class NodePort
    {
        public enum IO { Input, Output }

        public int ConnectionCount { get { return connections.Count; } }
        /// <summary> Return the first non-null connection </summary>
        public NodePort Connection
        {
            get
            {
                for (int i = 0; i < connections.Count; i++)
                {
                    if (connections[i] != null) return connections[i].Port;
                }
                return null;
            }
        }

        public IO Direction { get { return _direction; } }
        public Node.ConnectionType connectionType { get { return _connectionType; } }

        /// <summary> Is this port connected to anytihng? </summary>
        public bool IsConnected { get { return connections.Count != 0; } }
        public bool IsInput { get { return Direction == IO.Input; } }
        public bool IsOutput { get { return Direction == IO.Output; } }

        public string FieldName { get { return _fieldName; } }
        public Node Node { get { return _node; } }
        public bool IsDynamic { get { return _dynamic; } }
        public bool IsStatic { get { return !_dynamic; } }
        public Type ValueType
        {
            get
            {
                if (_valueType == null && !string.IsNullOrEmpty(_typeQualifiedName)) _valueType = Type.GetType(_typeQualifiedName, false);
                return _valueType;
            }
            set
            {
                _valueType = value;
                if (value != null) _typeQualifiedName = value.AssemblyQualifiedName;
            }
        }
        private Type _valueType;

        [SerializeField] private string _fieldName;
        [SerializeField] private Node _node;
        [SerializeField] private string _typeQualifiedName;
        [SerializeField] private List<PortConnection> connections = new List<PortConnection>();
        [SerializeField] private IO _direction;
        [SerializeField] private Node.ConnectionType _connectionType;
        [SerializeField] private bool _dynamic;

        /// <summary> Construct a static targetless nodeport. Used as a template. </summary>
        public NodePort(FieldInfo fieldInfo)
        {
            _fieldName = fieldInfo.Name;
            ValueType = fieldInfo.FieldType;
            _dynamic = false;
            var attribs = fieldInfo.GetCustomAttributes(false);
            for (int i = 0; i < attribs.Length; i++)
            {
                if (attribs[i] is Node.InputAttribute)
                {
                    _direction = IO.Input;
                    _connectionType = (attribs[i] as Node.InputAttribute).ConnectionType;
                }
                else if (attribs[i] is Node.OutputAttribute)
                {
                    _direction = IO.Output;
                    _connectionType = (attribs[i] as Node.OutputAttribute).ConnectionType;
                }
            }
        }

        /// <summary> Copy a nodePort but assign it to another node. </summary>
        public NodePort(NodePort nodePort, Node node)
        {
            _fieldName = nodePort._fieldName;
            ValueType = nodePort._valueType;
            _direction = nodePort.Direction;
            _dynamic = nodePort._dynamic;
            _connectionType = nodePort._connectionType;
            _node = node;
        }

        /// <summary> Construct a dynamic port. Dynamic ports are not forgotten on reimport, and is ideal for runtime-created ports. </summary>
        public NodePort(string fieldName, Type type, IO direction, Node.ConnectionType connectionType, Node node)
        {
            _fieldName = fieldName;
            this.ValueType = type;
            _direction = direction;
            _node = node;
            _dynamic = true;
            _connectionType = connectionType;
        }

        /// <summary> Checks all connections for invalid references, and removes them. </summary>
        public void VerifyConnections()
        {
            for (int i = connections.Count - 1; i >= 0; i--)
            {
                if (connections[i].Node != null &&
                    !string.IsNullOrEmpty(connections[i].FieldName) &&
                    connections[i].Node.GetPort(connections[i].FieldName) != null)
                    continue;
                connections.RemoveAt(i);
            }
        }

        /// <summary> Return the output value of this node through its parent nodes GetValue override method. </summary>
        /// <returns> <see cref="XNode.Node.GetValue(NodePort)"/> </returns>
        public object GetOutputValue()
        {
            if (Direction == IO.Input) return null;
            return Node.GetValue(this);
        }

        /// <summary> Return the output value of the first connected port. Returns null if none found or invalid.</summary>
        /// <returns> <see cref="NodePort.GetOutputValue"/> </returns>
        public object GetInputValue()
        {
            NodePort connectedPort = Connection;
            if (connectedPort == null) return null;
            return connectedPort.GetOutputValue();
        }

        /// <summary> Return the output values of all connected ports. </summary>
        /// <returns> <see cref="NodePort.GetOutputValue"/> </returns>
        public object[] GetInputValues()
        {
            object[] objs = new object[ConnectionCount];
            for (int i = 0; i < ConnectionCount; i++)
            {
                NodePort connectedPort = connections[i].Port;
                if (connectedPort == null)
                { // if we happen to find a null port, remove it and look again
                    connections.RemoveAt(i);
                    i--;
                    continue;
                }
                objs[i] = connectedPort.GetOutputValue();
            }
            return objs;
        }

        /// <summary> Return the output value of the first connected port. Returns null if none found or invalid. </summary>
        /// <returns> <see cref="NodePort.GetOutputValue"/> </returns>
        public T GetInputValue<T>()
        {
            object obj = GetInputValue();
            return obj is T ? (T)obj : default(T);
        }

        /// <summary> Return the output values of all connected ports. </summary>
        /// <returns> <see cref="NodePort.GetOutputValue"/> </returns>
        public T[] GetInputValues<T>()
        {
            object[] objs = GetInputValues();
            T[] ts = new T[objs.Length];
            for (int i = 0; i < objs.Length; i++)
            {
                if (objs[i] is T) ts[i] = (T)objs[i];
            }
            return ts;
        }

        /// <summary> Return true if port is connected and has a valid input. </summary>
        /// <returns> <see cref="NodePort.GetOutputValue"/> </returns>
        public bool TryGetInputValue<T>(out T value)
        {
            object obj = GetInputValue();
            if (obj is T)
            {
                value = (T)obj;
                return true;
            }
            else
            {
                value = default(T);
                return false;
            }
        }

        /// <summary> Return the sum of all inputs. </summary>
        /// <returns> <see cref="NodePort.GetOutputValue"/> </returns>
        public float GetInputSum(float fallback)
        {
            object[] objs = GetInputValues();
            if (objs.Length == 0) return fallback;
            float result = 0;
            for (int i = 0; i < objs.Length; i++)
            {
                if (objs[i] is float) result += (float)objs[i];
            }
            return result;
        }

        /// <summary> Return the sum of all inputs. </summary>
        /// <returns> <see cref="NodePort.GetOutputValue"/> </returns>
        public int GetInputSum(int fallback)
        {
            object[] objs = GetInputValues();
            if (objs.Length == 0) return fallback;
            int result = 0;
            for (int i = 0; i < objs.Length; i++)
            {
                if (objs[i] is int) result += (int)objs[i];
            }
            return result;
        }

        /// <summary> Connect this <see cref="NodePort"/> to another </summary>
        /// <param name="port">The <see cref="NodePort"/> to connect to</param>
        public void Connect(NodePort port)
        {
            if (connections == null) connections = new List<PortConnection>();
            if (port == null) { Debug.LogWarning("Cannot connect to null port"); return; }
            if (port == this) { Debug.LogWarning("Attempting to connect port to self."); return; }
            if (IsConnectedTo(port)) { Debug.LogWarning("Port already connected. "); return; }
            if (Direction == port.Direction) { Debug.LogWarning("Cannot connect two " + (Direction == IO.Input ? "input" : "output") + " connections"); return; }
            if (port.connectionType == Node.ConnectionType.Override && port.ConnectionCount != 0) { port.ClearConnections(); }
            if (connectionType == Node.ConnectionType.Override && ConnectionCount != 0) { ClearConnections(); }
            connections.Add(new PortConnection(port));
            if (port.connections == null) port.connections = new List<PortConnection>();
            if (!port.IsConnectedTo(this)) port.connections.Add(new PortConnection(this));
            Node.OnCreateConnection(this, port);
            port.Node.OnCreateConnection(this, port);
        }

        public NodePort GetConnection(int i)
        {
            //If the connection is broken for some reason, remove it.
            if (connections[i].Node == null || string.IsNullOrEmpty(connections[i].FieldName))
            {
                connections.RemoveAt(i);
                return null;
            }
            NodePort port = connections[i].Node.GetPort(connections[i].FieldName);
            if (port == null)
            {
                connections.RemoveAt(i);
                return null;
            }
            return port;
        }

        /// <summary> Get index of the connection connecting this and specified ports </summary>
        public int GetConnectionIndex(NodePort port)
        {
            for (int i = 0; i < ConnectionCount; i++)
            {
                if (connections[i].Port == port) return i;
            }
            return -1;
        }

        public bool IsConnectedTo(NodePort port)
        {
            for (int i = 0; i < connections.Count; i++)
            {
                if (connections[i].Port == port) return true;
            }
            return false;
        }

        /// <summary> Disconnect this port from another port </summary>
        public void Disconnect(NodePort port)
        {
            // Remove this ports connection to the other
            for (int i = connections.Count - 1; i >= 0; i--)
            {
                if (connections[i].Port == port)
                {
                    connections.RemoveAt(i);
                }
            }
            if (port != null)
            {
                // Remove the other ports connection to this port
                for (int i = 0; i < port.connections.Count; i++)
                {
                    if (port.connections[i].Port == this)
                    {
                        port.connections.RemoveAt(i);
                    }
                }
            }
            // Trigger OnRemoveConnection
            Node.OnRemoveConnection(this);
            if (port != null) port.Node.OnRemoveConnection(port);
        }

        public void ClearConnections()
        {
            while (connections.Count > 0)
            {
                Disconnect(connections[0].Port);
            }
        }

        /// <summary> Get reroute points for a given connection. This is used for organization </summary>
        public List<Vector2> GetReroutePoints(int index)
        {
            return connections[index].ReroutePoints;
        }

        /// <summary> Swap connected nodes from the old list with nodes from the new list </summary>
        public void Redirect(List<Node> oldNodes, List<Node> newNodes)
        {
            foreach (PortConnection connection in connections)
            {
                int index = oldNodes.IndexOf(connection.Node);
                if (index >= 0) connection.Node = newNodes[index];
            }
        }

        [Serializable]
        private class PortConnection
        {
            [SerializeField] public string FieldName;
            [SerializeField] public Node Node;
            public NodePort Port { get { return port != null ? port : port = GetPort(); } }

            [NonSerialized] private NodePort port;
            /// <summary> Extra connection path points for organization </summary>
            [SerializeField] public List<Vector2> ReroutePoints = new List<Vector2>();

            public PortConnection(NodePort port)
            {
                this.port = port;
                Node = port.Node;
                FieldName = port.FieldName;
            }

            /// <summary> Returns the port that this <see cref="PortConnection"/> points to </summary>
            private NodePort GetPort()
            {
                if (Node == null || string.IsNullOrEmpty(FieldName)) return null;
                return Node.GetPort(FieldName);
            }
        }
    }
}