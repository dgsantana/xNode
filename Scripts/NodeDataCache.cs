using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace XNode
{
    /// <summary> Precaches reflection data in editor so we won't have to do it runtime </summary>
    public static class NodeDataCache
    {
        private static PortDataCache _portDataCache;
        private static bool Initialized => _portDataCache != null;

        /// <summary> Update static ports to reflect class fields. </summary>
        public static void UpdatePorts(Node node, Dictionary<string, NodePort> ports)
        {
            if (!Initialized) BuildCache();

            var staticPorts = new Dictionary<string, NodePort>();
            var nodeType = node.GetType();

            if (_portDataCache.ContainsKey(nodeType))
                for (var i = 0; i < _portDataCache[nodeType].Count; i++)
                    staticPorts.Add(_portDataCache[nodeType][i].FieldName, _portDataCache[nodeType][i]);

            // Cleanup port dict - Remove nonexisting static ports - update static port types
            // Loop through current node ports
            foreach (var port in ports.Values.ToList())
                // If port still exists, check it it has been changed
                if (staticPorts.ContainsKey(port.FieldName))
                {
                    var staticPort = staticPorts[port.FieldName];
                    // If port exists but with wrong settings, remove it. Re-add it later.
                    if (port.connectionType != staticPort.connectionType || port.IsDynamic ||
                        port.Direction != staticPort.Direction) ports.Remove(port.FieldName);
                    else port.ValueType = staticPort.ValueType;
                }
                // If port doesn't exist anymore, remove it
                else if (port.IsStatic)
                {
                    ports.Remove(port.FieldName);
                }

            // Add missing ports
            foreach (var staticPort in staticPorts.Values)
                if (!ports.ContainsKey(staticPort.FieldName))
                    ports.Add(staticPort.FieldName, new NodePort(staticPort, node));
        }

        private static void BuildCache()
        {
            _portDataCache = new PortDataCache();
            var baseType = typeof(Node);
            var nodeTypes = new List<Type>();
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            var selfAssembly = Assembly.GetAssembly(baseType);
            if (selfAssembly.FullName.StartsWith("Assembly-CSharp"))
                nodeTypes.AddRange(selfAssembly.GetTypes().Where(t => !t.IsAbstract && baseType.IsAssignableFrom(t)));
            else
                foreach (var assembly in assemblies)
                    nodeTypes.AddRange(assembly.GetTypes().Where(t => !t.IsAbstract && baseType.IsAssignableFrom(t))
                        .ToArray());
            for (var i = 0; i < nodeTypes.Count; i++) CachePorts(nodeTypes[i]);
        }

        private static void CachePorts(Type nodeType)
        {
            var fieldInfo = nodeType.GetFields();
            for (var i = 0; i < fieldInfo.Length; i++)
            {
                //Get InputAttribute and OutputAttribute
                var attribs = fieldInfo[i].GetCustomAttributes(false);
                var inputAttrib = attribs.FirstOrDefault(x => x is Node.InputAttribute) as Node.InputAttribute;
                var outputAttrib = attribs.FirstOrDefault(x => x is Node.OutputAttribute) as Node.OutputAttribute;

                if (inputAttrib == null && outputAttrib == null) continue;

                if (inputAttrib != null && outputAttrib != null)
                {
                    Debug.LogError("Field " + fieldInfo[i].Name + " of type " + nodeType.FullName +
                                   " cannot be both input and output.");
                }
                else
                {
                    if (!_portDataCache.ContainsKey(nodeType)) _portDataCache.Add(nodeType, new List<NodePort>());
                    _portDataCache[nodeType].Add(new NodePort(fieldInfo[i]));
                }
            }
        }

        [Serializable]
        private class PortDataCache : Dictionary<Type, List<NodePort>>, ISerializationCallbackReceiver
        {
            [SerializeField] private readonly List<Type> keys = new List<Type>();
            [SerializeField] private readonly List<List<NodePort>> values = new List<List<NodePort>>();

            // save the dictionary to lists
            public void OnBeforeSerialize()
            {
                keys.Clear();
                values.Clear();
                foreach (var pair in this)
                {
                    keys.Add(pair.Key);
                    values.Add(pair.Value);
                }
            }

            // load dictionary from lists
            public void OnAfterDeserialize()
            {
                Clear();

                if (keys.Count != values.Count)
                    throw new Exception(string.Format(
                        "there are {0} keys and {1} values after deserialization. Make sure that both key and value types are serializable."));

                for (var i = 0; i < keys.Count; i++)
                    Add(keys[i], values[i]);
            }
        }
    }
}