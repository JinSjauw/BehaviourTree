using System;
using BehaviourTree.Core;

namespace BehaviourTree.Editor.Propagation
{
    public interface IVariableChangeHandler
    {
        void HandleRename(PropagationContext ctx, string oldName, string newName);
        void HandleDelete(PropagationContext ctx, string variableName, string variableTypeName);
        void HandleTypeChange(PropagationContext ctx, string variableName, string oldTypeName, string newTypeName);
    }
}
