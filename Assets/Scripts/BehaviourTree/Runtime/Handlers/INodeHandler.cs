using BehaviourTree.Core;

namespace BehaviourTree.Runtime
{
    /// <summary>
    /// Encapsulates evaluation logic for a single node type.
    /// </summary>
    public interface INodeHandler
    {
        /// <summary>
        /// Process the current node on top of the stack.
        /// Returns true if a new child frame was pushed (evaluation continues deeper).
        /// Returns false if the node popped itself (success, failure, or leaf completed).
        /// </summary>
        bool Process(EvaluatorContext ctx);
    }
}