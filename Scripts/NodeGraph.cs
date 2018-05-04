using System;
using System.Collections.Generic;
using UnityEngine;

namespace XNode
{
    /// <summary> Base class for all node graphs </summary>
    [Serializable]
    public abstract class NodeGraph : ScriptableObject
    {

        /// <summary> All nodes in the graph. <para/>
        /// See: <see cref="AddNode{T}"/> </summary>
        [SerializeField] public List<Node> Nodes = new List<Node>();

        /// <summary> Add a node to the graph by type </summary>
        public T AddNode<T>() where T : Node
        {
            return AddNode(typeof(T)) as T;
        }

        /// <summary> Add a node to the graph by type </summary>
        public virtual Node AddNode(Type type)
        {
            Node node = ScriptableObject.CreateInstance(type) as Node;
            Nodes.Add(node);
            node.Graph = this;
            return node;
        }

        /// <summary> Creates a copy of the original node in the graph </summary>
        public virtual Node CopyNode(Node original)
        {
            Node node = ScriptableObject.Instantiate(original);
            node.ClearConnections();
            Nodes.Add(node);
            node.Graph = this;
            return node;
        }

        /// <summary> Safely remove a node and all its connections </summary>
        /// <param name="node"></param>
        public void RemoveNode(Node node)
        {
            node.ClearConnections();
            Nodes.Remove(node);
            if (Application.isPlaying) Destroy(node);
        }

        /// <summary> Remove all nodes and connections from the graph </summary>
        public void Clear()
        {
            if (Application.isPlaying)
            {
                for (int i = 0; i < Nodes.Count; i++)
                {
                    Destroy(Nodes[i]);
                }
            }
            Nodes.Clear();
        }

        /// <summary> Create a new deep copy of this graph </summary>
        public NodeGraph Copy()
        {
            // Instantiate a new nodegraph instance
            NodeGraph graph = Instantiate(this);
            // Instantiate all nodes inside the graph
            for (int i = 0; i < Nodes.Count; i++)
            {
                if (Nodes[i] == null) continue;
                Node node = Instantiate(Nodes[i]) as Node;
                node.Graph = graph;
                graph.Nodes[i] = node;
            }

            // Redirect all connections
            for (int i = 0; i < graph.Nodes.Count; i++)
            {
                if (graph.Nodes[i] == null) continue;
                foreach (NodePort port in graph.Nodes[i].Ports)
                {
                    port.Redirect(Nodes, graph.Nodes);
                }
            }

            return graph;
        }

        private void OnDestroy()
        {
            // Remove all nodes prior to graph destruction
            Clear();
        }
    }
}