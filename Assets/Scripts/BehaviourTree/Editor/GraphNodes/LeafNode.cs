using System;
using Unity.GraphToolkit.Editor;
using UnityEngine;

namespace BehaviourTree.Editor
{
    [Serializable]
    public class LeafNode : BTNodeEditorBase
    {
        public override BehaviourNodeType NodeType => BehaviourNodeType.ACTION;

        protected override void OnDefineOptions(IOptionDefinitionContext context) 
        {
            context.AddOption("MethodID", typeof(MethodID)).Build();
            context.AddOption("ConfigID", typeof(NodeConfigID)).Build();
            context.AddOption("BlackBoardTypeID", typeof(BlackBoardType)).Build();
        }

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            context.AddInputPort<BehaviourNode>("Parent").Build();
            //context.AddOutputPort<BehaviourNode>("Children").Build();
        }
    }
}
