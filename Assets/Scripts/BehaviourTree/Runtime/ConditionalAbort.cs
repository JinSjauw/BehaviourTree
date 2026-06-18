using BehaviourTree.Core;

namespace BehaviourTree.Runtime
{
    /// <summary>
    /// Conditional abort helpers for composite nodes.
    /// Implements Behaviour Designer-style Self, LowerPriority, and Both abort types.
    /// </summary>
    internal static partial class TickFunctions
    {
        /// <summary>
        /// Re-evaluates child conditions for conditional abort.
        /// Called at the start of each composite's Execute().
        /// Returns the child index to resume from (may change if abort occurred).
        /// </summary>
        internal static int CheckConditionalAbort(int compositeIndex, ref TickContext ctx)
        {
            ref NodeData composite = ref ctx.nodeDatas[compositeIndex];
            int first = composite.firstChildIndex;
            int last = composite.lastChildIndex;
            if (first < 0) return ctx.activeChildIndex[compositeIndex];

            AbortType parentAbortType = composite.abortType;
            int childCount = last - first + 1;

            // Find the currently RUNNING child of THIS composite
            int runningChildLocal = -1;
            for (int c = 0; c < childCount; c++)
            {
                if (ctx.nodeStates[first + c] == NodeState.RUNNING)
                {
                    runningChildLocal = c;
                    break;
                }
            }

            // ── SELF abort pass ──
            // Re-evaluates this composite's own children's conditions every tick.
            // If any condition transitions (true→false or false→true) while a later
            // sibling is RUNNING, abort the running sibling and restart from here.
            if (parentAbortType == AbortType.Self || parentAbortType == AbortType.Both)
            {
                for (int c = 0; c < childCount; c++)
                {
                    int childIndex = first + c;
                    ref NodeData childNode = ref ctx.nodeDatas[childIndex];

                    bool conditionMet;
                    if (ctx.methodInstances[childIndex] != null)
                    {
                        // Direct condition leaf — evaluate it
                        conditionMet = EvaluateLeafCondition(childIndex, ref ctx);
                    }
                    else if (childNode.nodeType == BehaviourNodeType.SUBTREE
                        && childNode.firstChildIndex >= 0)
                    {
                        // Subtree is transparent — walk through to find its first condition
                        conditionMet = EvaluateCompositeCondition(childIndex, AbortType.Self, ref ctx);
                    }
                    else
                    {
                        continue;
                    }

                    bool wasMet = ctx.lastConditionResult[childIndex];
                    ctx.lastConditionResult[childIndex] = conditionMet;

                    // Any status change triggers abort (both directions matter:
                    // true→false for Sequence — condition broke while action runs;
                    // false→true for Selector — higher-priority condition just became met)
                    if (wasMet != conditionMet && runningChildLocal >= 0 && runningChildLocal > c)
                    {
                        AbortSubtree(first + runningChildLocal, ref ctx);
                        return c;
                    }
                }
            }

            // ── LOWER PRIORITY abort pass ──
            // This composite checks children that are composites with LowerPriority/Both
            // abort type. If that child composite's condition transitions false→true,
            // and a sibling to the right is RUNNING, abort the sibling.
            for (int c = 0; c < childCount; c++)
            {
                int childIndex = first + c;
                ref NodeData childNode = ref ctx.nodeDatas[childIndex];

                // Subtree children → recursively find composites with LP/Both abort
                if (childNode.nodeType == BehaviourNodeType.SUBTREE
                    && childNode.firstChildIndex >= 0)
                {
                    int abortIndex = EvaluateLpConditionsRecursive(
                        childNode.firstChildIndex, childNode.lastChildIndex,
                        first, runningChildLocal, c, ref ctx);
                    if (abortIndex >= 0) return abortIndex;
                    continue;
                }

                // Only composites can have abort types
                if (childNode.nodeType != BehaviourNodeType.COMPOSITE) continue;
                AbortType childAbort = childNode.abortType;
                if (childAbort != AbortType.LowerPriority && childAbort != AbortType.Both) continue;

                // Evaluate this child composite's condition — pass its own abort type
                // for the recursion constraint
                bool conditionMet = EvaluateCompositeCondition(childIndex, childAbort, ref ctx);
                bool wasMet = ctx.lastConditionResult[childIndex];
                ctx.lastConditionResult[childIndex] = conditionMet;

                // Condition transitioned false→true, and a sibling to the right is RUNNING
                if (!wasMet && conditionMet && runningChildLocal >= 0 && runningChildLocal > c)
                {
                    // Abort the running sibling
                    AbortSubtree(first + runningChildLocal, ref ctx);
                    return c; // restart from this newly-active child composite
                }
            }

            return ctx.activeChildIndex[compositeIndex];
        }

        /// <summary>
        /// Recursively resets a node and its subtree to INACTIVE state.
        /// Calls OnAbort on each method-bearing node before resetting.
        /// </summary>
        internal static void AbortSubtree(int nodeIndex, ref TickContext ctx)
        {
            if (nodeIndex < 0 || nodeIndex >= ctx.nodeDatas.Length) return;

            ref NodeData node = ref ctx.nodeDatas[nodeIndex];

            // Call OnAbort before resetting state (method can read current state to clean up)
            NodeMethod method = ctx.methodInstances[nodeIndex];
            method?.OnAbort(ctx.blackBoard);

            // Reset this node
            ctx.nodeStates[nodeIndex] = NodeState.INACTIVE;
            ctx.activeChildIndex[nodeIndex] = 0;

            // Recursively reset children
            if (node.firstChildIndex >= 0)
            {
                for (int i = node.firstChildIndex; i <= node.lastChildIndex; i++)
                    AbortSubtree(i, ref ctx);
            }
        }

        /// <summary>
        /// Evaluates a single node's method as a standalone condition.
        /// Only ConditionMethod is supported as condition-bearing for abort.
        /// Other method types return true (condition "always met") to avoid
        /// executing actions or polling decorator child state during abort checks.
        /// </summary>
        private static bool EvaluateLeafCondition(int nodeIndex, ref TickContext ctx)
        {
            NodeMethod method = ctx.methodInstances[nodeIndex];
            if (method == null) return true;

            method.ResolveInputsGeneric(ctx.blackBoard);

            NodeState result = NodeState.SUCCESS;
            if (method is ConditionMethod condition)
                result = condition.Execute();
            // ActionMethod: don't execute (side effects)
            // DecoratorMethod: not supported yet (would need child evaluation)

            method.WriteOutputsGeneric(ctx.blackBoard);
            return result == NodeState.SUCCESS;
        }

        /// <summary>
        /// Finds and evaluates the first condition-bearing node inside a child
        /// composite subtree. Used for LowerPriority abort where the parent needs
        /// to check a child composite's condition.
        ///
        /// Recursion rule: only recurses into nested child composites that share
        /// the same abort type (LowerPriority or Both). Nested composites with
        /// None or Self are NOT descended into.
        ///
        /// Returns true if no qualifying condition was found (treated as "always met").
        /// </summary>
        private static bool EvaluateCompositeCondition(int compositeIndex, AbortType requiredType, ref TickContext ctx)
        {
            ref NodeData composite = ref ctx.nodeDatas[compositeIndex];
            return FindFirstCondition(composite.firstChildIndex, composite.lastChildIndex, requiredType, ref ctx);
        }

        private static bool FindFirstCondition(int first, int last, AbortType requiredType, ref TickContext ctx)
        {
            if (first < 0) return true;

            // Single pass: for each child, either evaluate its method as a condition
            // (if it's a leaf) or recurse into it (if it's a composite with compatible
            // abort type). Method-bearing composites (e.g. SEQUENCE) are never evaluated
            // as leaf conditions — their method is CompositeMethod, not ConditionMethod.
            for (int i = first; i <= last; i++)
            {
                ref NodeData child = ref ctx.nodeDatas[i];

                // Method-bearing non-composite leaf → evaluate as condition
                if (ctx.methodInstances[i] != null && child.nodeType != BehaviourNodeType.COMPOSITE)
                    return EvaluateLeafCondition(i, ref ctx);

                // Composite with children → recurse if abort type is compatible
                if (child.firstChildIndex >= 0 && child.nodeType == BehaviourNodeType.COMPOSITE)
                {
                    bool hasRequired = child.abortType == requiredType ||
                                       child.abortType == AbortType.Both;
                    if (!hasRequired) continue;

                    bool found = FindFirstCondition(child.firstChildIndex,
                        child.lastChildIndex, requiredType, ref ctx);
                    if (!found) return false; // condition found and FAILED
                    continue;
                }

                // Subtree with children → always recurse (subtrees are transparent)
                if (child.firstChildIndex >= 0 && child.nodeType == BehaviourNodeType.SUBTREE)
                {
                    bool found = FindFirstCondition(child.firstChildIndex,
                        child.lastChildIndex, requiredType, ref ctx);
                    if (!found) return false;
                }
            }

            return true; // no qualifying condition-bearing children found
        }

        /// <summary>
        /// Recursively evaluates LowerPriority/Both composites within [first, last],
        /// walking transparently through SUBTREE nodes.
        /// parentFirst: the original parent composite's firstChildIndex (for AbortSubtree target).
        /// parentPosition: the original subtree's child position in the parent composite.
        /// runningChildLocal: position of the currently RUNNING child in the parent composite.
        /// Returns the child position to resume from if an abort occurred, or -1.
        /// </summary>
        private static int EvaluateLpConditionsRecursive(
            int first, int last,
            int parentFirst,
            int runningChildLocal,
            int parentPosition,
            ref TickContext ctx)
        {
            int childCount = last - first + 1;
            for (int i = 0; i < childCount; i++)
            {
                int childIndex = first + i;
                ref NodeData childNode = ref ctx.nodeDatas[childIndex];

                if (childNode.nodeType == BehaviourNodeType.COMPOSITE)
                {
                    AbortType childAbort = childNode.abortType;
                    if (childAbort != AbortType.LowerPriority
                        && childAbort != AbortType.Both) continue;

                    bool conditionMet = EvaluateCompositeCondition(childIndex, childAbort, ref ctx);
                    bool wasMet = ctx.lastConditionResult[childIndex];
                    ctx.lastConditionResult[childIndex] = conditionMet;

                    if (!wasMet && conditionMet
                        && runningChildLocal >= 0
                        && runningChildLocal > parentPosition)
                    {
                        AbortSubtree(parentFirst + runningChildLocal, ref ctx);
                        return parentPosition;
                    }
                }
                else if (childNode.nodeType == BehaviourNodeType.SUBTREE
                    && childNode.firstChildIndex >= 0)
                {
                    int abortIndex = EvaluateLpConditionsRecursive(
                        childNode.firstChildIndex, childNode.lastChildIndex,
                        parentFirst, runningChildLocal, parentPosition, ref ctx);
                    if (abortIndex >= 0) return abortIndex;
                }
            }
            return -1;
        }
    }
}
