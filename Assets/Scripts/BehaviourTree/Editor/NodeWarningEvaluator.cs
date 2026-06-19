using System.Collections.Generic;
using System.Text;
using BehaviourTree.Core;

namespace BehaviourTree.Editor
{
    public enum NodeWarningType
    {
        NoReachableConditionForAbort,
        VariableNotAssigned,
        VariableNotFound,
        MissingBlackboardDefinition,
        NoBlackboardVariables,
    }

    public struct NodeWarning
    {
        public NodeWarningType Type;
        public string Message;
    }

    /// <summary>
    /// Static utility that evaluates warnings for any BehaviourNode.
    /// Consumed by both BehaviourNodeView (icon / tooltip) and the inspector.
    /// </summary>
    public static class NodeWarningEvaluator
    {
        public static List<NodeWarning> Evaluate(BehaviourNode node)
        {
            List<NodeWarning> warnings = new List<NodeWarning>();
            EvaluateAbortWarnings(node, warnings);
            EvaluateVariableWarnings(node, warnings);
            return warnings;
        }

        private static void EvaluateAbortWarnings(BehaviourNode node, List<NodeWarning> warnings)
        {
            if (node is not CompositeNode composite) return;
            if (composite.abortType == AbortType.None) return;

            if (!HasValidConditionForAbort(composite, composite.abortType))
            {
                warnings.Add(new NodeWarning
                {
                    Type = NodeWarningType.NoReachableConditionForAbort,
                    Message = "No reachable Condition node found. Add a Condition node as a descendant.",
                });
            }
        }

        private static void EvaluateVariableWarnings(BehaviourNode node, List<NodeWarning> warnings)
        {
            List<NodeFieldEntry> entries = GetFieldEntries(node);
            if (entries == null || entries.Count == 0) return;

            BlackboardDefinition blackBoardDef = BehaviourTreeEditor.currentBlackboardDef;

            // Check if any field uses a variable
            bool anyVariableField = false;
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].isVariable)
                {
                    anyVariableField = true;
                    break;
                }
            }
            if (!anyVariableField) return;

            // Skip variable validation for nodes from subtrees — their blackboard
            // is a separate asset potentially not loaded in the current editor context.
            BaseEditorTreeAsset currentTree = BehaviourTreeEditor.currentTree;
            if (currentTree != null && currentTree.nodesList != null && !currentTree.nodesList.Contains(node))
                return;

            if (blackBoardDef == null)
            {
                warnings.Add(new NodeWarning
                {
                    Type = NodeWarningType.MissingBlackboardDefinition,
                    Message = "No Blackboard Definition assigned.",
                });
                return;
            }

            IReadOnlyList<BlackboardVariableBase> allVars = blackBoardDef.GetAllVariables();
            if (allVars == null || allVars.Count == 0)
            {
                warnings.Add(new NodeWarning
                {
                    Type = NodeWarningType.NoBlackboardVariables,
                    Message = "No Blackboard variables added.",
                });
                return;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                NodeFieldEntry entry = entries[i];
                if (!entry.isVariable) continue;

                string displayName = Capitalize(entry.fieldName);

                if (string.IsNullOrEmpty(entry.variableName))
                {
                    warnings.Add(new NodeWarning
                    {
                        Type = NodeWarningType.VariableNotAssigned,
                        Message = "Variable not assigned for '" + displayName + "'.",
                    });
                    continue;
                }

                // Search the tree's own blackboard and, if present, the commander blackboard.
                // Shared/commander variables live in the commander BB, not the tree's own BB.
                bool found = IsVariableInBlackboard(entry.variableName, blackBoardDef);

                if (!found)
                {
                    BlackboardDefinition commanderDef = BehaviourTreeEditor.currentTree?.CommanderBlackboardDefinition;
                    if (commanderDef != null && commanderDef != blackBoardDef)
                        found = IsVariableInBlackboard(entry.variableName, commanderDef);
                }

                if (!found)
                {
                    warnings.Add(new NodeWarning
                    {
                        Type = NodeWarningType.VariableNotFound,
                        Message = "Variable '" + entry.variableName + "' not found in Blackboard.",
                    });
                }
            }
        }

        private static bool IsVariableInBlackboard(string variableName, BlackboardDefinition blackboardDef)
        {
            if (blackboardDef == null) return false;
            IReadOnlyList<BlackboardVariableBase> allVars = blackboardDef.GetAllVariables();
            if (allVars == null) return false;

            for (int j = 0; j < allVars.Count; j++)
            {
                if (allVars[j].Name == variableName)
                    return true;
            }
            return false;
        }

        private static List<NodeFieldEntry> GetFieldEntries(BehaviourNode node)
        {
            return node switch
            {
                LeafNode leaf         => leaf.fieldEntries,
                CompositeNode composite => composite.fieldEntries,
                DecoratorNode decorator => decorator.fieldEntries,
                _ => null,
            };
        }

        private static string Capitalize(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            return char.ToUpper(text[0]) + text.Substring(1);
        }

        // ── Conditional Abort Validation ──

        public static bool HasValidConditionForAbort(CompositeNode composite, AbortType abortType)
        {
            return GetFirstReachableCondition(composite, abortType) != null;
        }

        /// <summary>
        /// Returns the first (leftmost) condition leaf reachable by the given abort type,
        /// respecting abort-type compatibility rules for nested composites.
        /// Returns null if no qualifying condition is found.
        /// </summary>
        public static LeafNode GetFirstReachableCondition(CompositeNode composite, AbortType abortType)
        {
            LeafNode result = null;
            FindFirstConditionRecursive(composite.children, abortType, ref result);
            return result;
        }

        private static void FindFirstConditionRecursive(List<BehaviourNode> children,
            AbortType requiredType, ref LeafNode result)
        {
            if (children == null || result != null) return;

            // Pass 1: check direct children for method-bearing condition nodes
            for (int i = 0; i < children.Count; i++)
            {
                if (children[i] is LeafNode leaf &&
                    leaf.NodeType == BehaviourNodeType.CONDITION &&
                    !string.IsNullOrEmpty(leaf.methodName))
                {
                    result = leaf;
                    return;
                }
            }

            // Pass 2: recurse into child composites with compatible abort type
            for (int i = 0; i < children.Count; i++)
            {
                if (children[i] is CompositeNode childComposite)
                {
                    AbortType childAbort = childComposite.abortType;
                    bool compatible = childAbort == requiredType || childAbort == AbortType.Both;
                    if (compatible)
                        FindFirstConditionRecursive(childComposite.children, requiredType, ref result);
                }
            }
        }

        public static string BuildWarningTooltip(BehaviourNode node)
        {
            List<NodeWarning> list = Evaluate(node);
            if (list.Count == 0) return "No warnings";

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<b>Warnings:</b>");
            for (int i = 0; i < list.Count; i++)
                sb.AppendLine("  \u2022 " + list[i].Message);
            return sb.ToString().TrimEnd();
        }
    }
}
