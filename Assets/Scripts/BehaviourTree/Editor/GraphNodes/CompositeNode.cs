using UnityEngine;
using System.Collections.Generic;
using Unity.GraphToolkit.Editor;
using System;

namespace BehaviourTree.Editor
{
    [Serializable]
    public class CompositeNode : ContextNode
    {
        //public override BehaviourNodeType NodeType => BehaviourNodeType.ROOT;

        [SerializeField] private List<Node> childNodes = new List<Node>();
        
        public IReadOnlyList<Node> ChildNodes => childNodes;

        public void SyncChildrenFromGraph()
        {
            var outPort = GetOutputPortByName("Children");
            if (outPort == null) return;

            var connections = new List<IPort>();
            outPort.GetConnectedPorts(connections);

            childNodes.Clear();
            foreach (var conn in connections)
            {
                Node child = conn.GetNode() as Node; // returns the node at the other end
                childNodes.Add(child);
            }
        }

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            context.AddInputPort<BehaviourNode>("Parent").Build();
            context.AddOutputPort<BehaviourNode>("Children").Build();
        }
    }
}
