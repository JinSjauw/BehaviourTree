using BehaviourTree;
using BehaviourTree.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;


namespace BehaviourTree.Editor
{
    public class BehaviourNodeView : Node
    {
        public BehaviourNode NodeSO { get; private set; }
        public string LeafMethodName { get; set; }
        public string Guid { get; private set; }
        public bool IsReadOnlyProxy { get; set; }
        public Dictionary<string, string> VariableMappings { get; set; }

        public Port input;
        public Port output;

        public Action<BehaviourNodeView> OnNodeSelected;

        private VisualElement statusborder;
        private TextField titleField;
        private Label subTitleLabel;

        private VisualElement abortTypeIcon;
        private VisualElement warningIcon;
        private Label abortLabel;

        public BehaviourTreeEditorGraphView GraphView { get; set; }

        public BehaviourNodeView(BehaviourNode nodeObject) : base(BehaviourTreeEditorPaths.GraphNodeViewUxml)
        {
            if (nodeObject == null) throw new ArgumentNullException(nameof(nodeObject));

            NodeSO = nodeObject;
            Guid = NodeSO.guid;

            //title = nodeObject.nodeName;
            style.left = NodeSO.graphPosition.x;
            style.top = NodeSO.graphPosition.y;

            SetTooltip();
            SetPortStyles();

            // Cache debug visuals
            statusborder = this.Q<VisualElement>("status-border");

            // Cache icon references
            abortTypeIcon = this.Q<VisualElement>("abort-type-icon");
            warningIcon = this.Q<VisualElement>("warning-icon");

            // Create abort text label as child of abort icon
            abortLabel = this.Q<Label>("abort-label");

            SetNodeColor();
            CreateInputPorts();
            CreateOutputPorts();

            SetupTitleField();

            if (NodeSO.NodeType == BehaviourNodeType.ROOT)
                capabilities &= ~(Capabilities.Movable | Capabilities.Selectable |Capabilities.Deletable | Capabilities.Copiable);

            RegisterCallback<MouseDownEvent>(OnNodeClicked);

            RefreshNodeIcons();
        }

        private void OnNodeClicked(MouseDownEvent evt)
        {
            if (evt.clickCount == 1)
            {
                OnNodeSelected?.Invoke(this);
            }
            else if (evt.clickCount == 2 && NodeSO is SubtreeNode subtreeNode && subtreeNode.subTreeAsset != null)
            {
                Selection.activeObject = subtreeNode.subTreeAsset;
                evt.StopPropagation();
            }
        }

        private void SetNodeColor()
        {
            VisualElement nodeColorElement = this.Q<VisualElement>("input");
            if (nodeColorElement == null) return;

            nodeColorElement.style.backgroundColor =
            NodeSO.NodeType switch
            {
                BehaviourNodeType.ROOT => Color.green,
                BehaviourNodeType.COMPOSITE => GetCompositeColor(NodeSO),
                BehaviourNodeType.ACTION => Color.red,
                BehaviourNodeType.CONDITION => Color.yellow,
                BehaviourNodeType.DECORATOR => Color.chocolate,
                BehaviourNodeType.SUBTREE => Color.yellowGreen,
                _ => Color.gray
            };
        }

        private static Color GetCompositeColor(BehaviourNode node)
        {
            if (node is CompositeNode composite && !string.IsNullOrEmpty(composite.methodName))
            {
                return composite.methodName switch
                {
                    "SELECTOR" => Color.blue,
                    "SEQUENCE" => Color.purple,
                    "PARALLEL" => Color.magenta,
                    "PRIORITY" => Color.cyan,
                    _ => Color.gray
                };
            }
            return Color.gray;
        }

        private void SetPortStyles()
        {
            inputContainer.style.flexDirection  = FlexDirection.Row;
            inputContainer.style.justifyContent = Justify.Center;
            inputContainer.style.alignItems     = Align.Center;


            outputContainer.style.flexDirection = FlexDirection.Row;
            outputContainer.style.justifyContent = Justify.Center;
            outputContainer.style.alignItems = Align.Center;
        }

        private void SetTooltip()
        {
            NodeTooltipData tooltipData = TooltipRegistry.GetTooltip(NodeSO);

            string description = tooltipData.nodeName;

            if (!string.IsNullOrEmpty(tooltipData.description))
            {
                description += "\n\n" + tooltipData.description;
            }
            if (!string.IsNullOrEmpty(tooltipData.returnValues))
            {
                description += "\n\nReturn Values:\n" + tooltipData.returnValues;
            }
            if (tooltipData.fieldDescriptions != null && tooltipData.fieldDescriptions.Length > 0)
            {
                bool hasAnyDesc = false;
                for (int i = 0; i < tooltipData.fieldDescriptions.Length; i++)
                {
                    if (!string.IsNullOrEmpty(tooltipData.fieldDescriptions[i].description))
                    {
                        hasAnyDesc = true;
                        break;
                    }
                }
                if (hasAnyDesc)
                {
                    description += "\n\nFields:";
                    for (int i = 0; i < tooltipData.fieldDescriptions.Length; i++)
                    {
                        var fd = tooltipData.fieldDescriptions[i];
                        if (!string.IsNullOrEmpty(fd.description))
                        {
                            description += "\n  " + fd.fieldName + " — " + fd.description;
                        }
                    }
                }
            }

            tooltip = description;
        }

        public override Port InstantiatePort(Orientation orientation, Direction direction, Port.Capacity capacity, Type type)
        {
            return new BehaviourPort(orientation, direction, capacity, type);
        }

        private void CreateInputPorts()
        {
            if (NodeSO.NodeType == BehaviourNodeType.ROOT) return;

            input = InstantiatePort(Orientation.Vertical, Direction.Input, Port.Capacity.Single, typeof(BehaviourNode));

            if (input != null)
            {
                inputContainer.Add(input);
            }
        }

        private void CreateOutputPorts()
        {
            if (NodeSO.NodeType == BehaviourNodeType.ACTION || NodeSO.NodeType == BehaviourNodeType.CONDITION || NodeSO.NodeType == BehaviourNodeType.SUBTREE)
            {
                return;
            }

            Port.Capacity portCapacity = Port.Capacity.Multi;

            if (NodeSO.NodeType == BehaviourNodeType.ROOT || NodeSO.NodeType == BehaviourNodeType.DECORATOR)
            {
                portCapacity = Port.Capacity.Single;
            }

            output = InstantiatePort(Orientation.Vertical, Direction.Output, portCapacity, typeof(BehaviourNode));

            if (output != null)
            {
                outputContainer.Add(output);
            }
        }

        private void SetupTitleField()
        {
            titleField = this.Q<TextField>("node-title-field");
            subTitleLabel = this.Q<Label>("sub-title-label");

            if (titleField != null)
            {
                titleField.SetValueWithoutNotify(GetDisplayName());
                titleField.RegisterCallback<KeyDownEvent>(OnTitleKeyDown, TrickleDown.TrickleDown);
                titleField.RegisterCallback<BlurEvent>(OnTitleBlur);
            }

            RefreshSubTitle();
        }

        private void OnTitleKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
            {
                CommitTitle();
                evt.StopPropagation();
            }
        }

        private void OnTitleBlur(BlurEvent evt)
        {
            CommitTitle();
        }

        private void CommitTitle()
        {
            if (titleField == null || NodeSO == null) return;

            string newValue = titleField.value;

            if (string.IsNullOrEmpty(newValue))
            {
                titleField.SetValueWithoutNotify(GetDisplayName());
                if (NodeSO.nodeName != "")
                {
                    Undo.RecordObject(NodeSO, "(BTree) Rename Node");
                    NodeSO.nodeName = "";
                    EditorUtility.SetDirty(NodeSO);
                }
            }
            else if (newValue != NodeSO.nodeName)
            {
                Undo.RecordObject(NodeSO, "(BTree) Rename Node");
                NodeSO.nodeName = newValue;
                EditorUtility.SetDirty(NodeSO);
            }

            RefreshSubTitle();
        }

        private void RefreshSubTitle()
        {
            if (subTitleLabel == null) return;

            string methodName = GetMethodName();
            if (!string.IsNullOrEmpty(NodeSO.nodeName)
                && !string.IsNullOrEmpty(methodName)
                && NodeSO.nodeName != methodName)
            {
                subTitleLabel.text = methodName;
                subTitleLabel.style.display = DisplayStyle.Flex;
            }
            else
            {
                subTitleLabel.text = "";
                subTitleLabel.style.display = DisplayStyle.None;
            }
        }

        private string GetDisplayName()
        {
            if (!string.IsNullOrEmpty(NodeSO.nodeName))
                return NodeSO.nodeName;
            return GetMethodName() ?? NodeSO.name;
        }

        public void RefreshTitle()
        {
            if (titleField != null)
                titleField.SetValueWithoutNotify(GetDisplayName());
            RefreshSubTitle();
        }

        private string GetMethodName()
        {
            return NodeSO switch
            {
                LeafNode leaf => leaf.methodName,
                CompositeNode composite => composite.methodName,
                DecoratorNode decorator => decorator.methodName,
                _ => null
            };
        }

        public override void OnSelected()
        {
            base.OnSelected();
            OnNodeSelected?.Invoke(this);
        }

        public int GetChildCount() =>
            outputContainer.Children().OfType<Port>().FirstOrDefault()?.connections.Count() ?? 0;

        public override void SetPosition(Rect newPos)
        {
            // Magnetic snap to grid — only snaps when within tolerance of a grid line
            const float gridSpacing = 35f;
            const float snapThreshold = 10f;

            float snappedX = Mathf.Round(newPos.x / gridSpacing) * gridSpacing;
            float snappedY = Mathf.Round(newPos.y / gridSpacing) * gridSpacing;

            if (Mathf.Abs(newPos.x - snappedX) <= snapThreshold)
                newPos.x = snappedX;
            if (Mathf.Abs(newPos.y - snappedY) <= snapThreshold)
                newPos.y = snappedY;

            base.SetPosition(newPos);
            Undo.RecordObject(NodeSO, "(BTree) Set Position");
            NodeSO.graphPosition.x = newPos.xMin;
            NodeSO.graphPosition.y = newPos.yMin;
            EditorUtility.SetDirty(NodeSO);
        }

        public void SortChildren()
        {
            if (NodeSO.NodeType == BehaviourNodeType.COMPOSITE)
            {
                NodeSO.children.Sort(SortByHorizontalPosition);
                EditorUtility.SetDirty(NodeSO);
            }
        }

        private int SortByHorizontalPosition(BehaviourNode left, BehaviourNode right)
        {
            return left.graphPosition.x < right.graphPosition.x ? -1 : left.graphPosition.x > right.graphPosition.x ? 1 : 0;
        }

        public void SetDebugState(NodeState state, bool isActive)
        {
            if (statusborder != null)
            {

                string nodeStatusClass;

                statusborder.ClearClassList();

                nodeStatusClass = state switch
                {
                    NodeState.RUNNING => "node-running",
                    NodeState.SUCCESS => "node-success",
                    NodeState.FAILURE => "node-failure",
                    _ => "node-none"
                };

                statusborder.AddToClassList(nodeStatusClass);
            }
        }

        // ── Node Icons ────────────────────────────────────────────────

        public void RefreshNodeIcons()
        {
            RefreshAbortIcon();
            RefreshWarningIcon();
        }

        private void RefreshAbortIcon()
        {
            if (abortTypeIcon == null) return;

            // Case 1: Composite with non-None abort type
            if (NodeSO is CompositeNode composite && composite.abortType != AbortType.None)
            {
                SetAbortIconActive(composite.abortType, GetAbortTypeTooltip(composite.abortType));
                return;
            }

            // Case 2: Condition leaf participating in parent composite's abort
            if (TryGetParentAbortType(out AbortType parentAbort))
            {
                SetAbortIconActive(parentAbort,
                    "Conditional Abort: " + parentAbort + "\n" +
                    "This condition is evaluated by the parent composite's abort logic.");
                return;
            }

            // Case 3: Nothing to show
            abortTypeIcon.style.display = DisplayStyle.None;
            if (abortLabel != null) abortLabel.text = "";
        }

        private void SetAbortIconActive(AbortType type, string tooltip)
        {
            abortTypeIcon.style.display = DisplayStyle.Flex;

            abortTypeIcon.style.backgroundColor = type switch
            {
                AbortType.Self          => new Color(0.38f, 0f, 1f),
                AbortType.LowerPriority => new Color(0f, 0.78f, 1f),
                AbortType.Both          => new Color(1f, 0f, 0.78f),
                _                       => Color.gray,
            };

            if (abortLabel != null)
            {
                abortLabel.text = type switch
                {
                    AbortType.Self          => "S",
                    AbortType.LowerPriority => "LP",
                    AbortType.Both          => "B",
                    _                       => "",
                };
            }

            abortTypeIcon.tooltip = tooltip;
        }

        private static string GetAbortTypeTooltip(AbortType type)
        {
            return type switch
            {
                AbortType.Self =>
                    "Conditional Abort: Self\nRe-evaluates own conditions each tick.",
                AbortType.LowerPriority =>
                    "Conditional Abort: Lower Priority\nChild composites may abort right-side siblings.",
                AbortType.Both =>
                    "Conditional Abort: Both\nSelf + Lower Priority combined.",
                _ => "Conditional Abort: " + type,
            };
        }

        private bool TryGetParentAbortType(out AbortType parentAbort)
        {
            parentAbort = AbortType.None;

            if (NodeSO is not LeafNode leaf || leaf.NodeType != BehaviourNodeType.CONDITION)
                return false;

            BehaviourNodeView parentView = FindParentNodeView();
            if (parentView == null || parentView.NodeSO is not CompositeNode parentComposite)
                return false;

            if (parentComposite.abortType == AbortType.None)
                return false;

            LeafNode firstCondition = NodeWarningEvaluator.GetFirstReachableCondition(parentComposite, parentComposite.abortType);
            if (firstCondition != leaf) return false;

            parentAbort = parentComposite.abortType;
            return true;
        }

        private BehaviourNodeView FindParentNodeView()
        {
            if (GraphView == null) return null;
            foreach (Edge edge in GraphView.edges.ToList())
            {
                if (edge.input?.node == this)
                    return edge.output?.node as BehaviourNodeView;
            }
            return null;
        }

        private void RefreshWarningIcon()
        {
            if (warningIcon == null) return;

            List<NodeWarning> warnings = NodeWarningEvaluator.Evaluate(NodeSO);
            if (warnings.Count > 0)
            {
                warningIcon.style.display = DisplayStyle.Flex;
                warningIcon.tooltip = NodeWarningEvaluator.BuildWarningTooltip(NodeSO);
            }
            else
            {
                warningIcon.style.display = DisplayStyle.None;
            }
        }
    }
}




