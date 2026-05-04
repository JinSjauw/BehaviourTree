using BehaviourTree;
using BehaviourTree.Core;
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

public class BehaviourNodeView : Node
{
    public BehaviourNode NodeSO { get; private set; }
    public MethodID LeafMethodID { get; set; }  //enum ID for delegate dispatch
    public string Guid { get; private set; }

    public Port input;
    public Port output;

    public Action<BehaviourNodeView> OnNodeSelected;

    public BehaviourNodeView(BehaviourNode nodeObject) : base("Assets/Scripts/BehaviourTree/Editor/GraphNodeView.uxml")
    {
        NodeSO = nodeObject;
        Guid = NodeSO.guid;

        title = nodeObject.NodeType.ToString(); // Or use mainTitle/subTitle for styling

        //Set position
        style.left = NodeSO.graphPosition.x;
        style.top = NodeSO.graphPosition.y;

        inputContainer.style.flexDirection  = FlexDirection.Row;
        inputContainer.style.justifyContent = Justify.Center;
        inputContainer.style.alignItems     = Align.Center;

        outputContainer.style.flexDirection  = FlexDirection.Row;
        outputContainer.style.justifyContent = Justify.Center;
        outputContainer.style.alignItems     = Align.Center;

        SetNodeColor();
        CreateInputPorts();
        CreateOutputPorts();
    }

    private void SetNodeColor()
    {
        VisualElement nodeColorElement = this.Q<VisualElement>("input");
        if(nodeColorElement == null) return;

        nodeColorElement.style.backgroundColor =
        NodeSO.NodeType switch
        {
            BehaviourNodeType.ROOT => Color.green,
            BehaviourNodeType.SELECTOR => Color.blue,
            BehaviourNodeType.SEQUENCE => Color.purple,
            BehaviourNodeType.ACTION => Color.red,
            BehaviourNodeType.CONDITION => Color.yellow,
            _ => Color.gray
        };
    }

    private void CreateInputPorts()
    {
        if (NodeSO.NodeType == BehaviourNodeType.ROOT) return;

        input = InstantiatePort(Orientation.Vertical, Direction.Input, Port.Capacity.Single, typeof(BehaviourNode));

        if(input != null) 
        {
            input.portName = "";
            input.style.flexDirection = FlexDirection.Column;

            VisualElement connectorElement = input.Q<VisualElement>("connector");
            if(connectorElement != null)
            {
                connectorElement.pickingMode = PickingMode.Position;
            }

            inputContainer.Add(input);
        }
    }

    private void CreateOutputPorts()
    {
        if (NodeSO.NodeType == BehaviourNodeType.ACTION || NodeSO.NodeType == BehaviourNodeType.CONDITION)
        {
            return;
        }

        Port.Capacity portCapacity = Port.Capacity.Multi;

        if(NodeSO.NodeType == BehaviourNodeType.ROOT) 
        {
            portCapacity = Port.Capacity.Single;
        }

        output = InstantiatePort(Orientation.Vertical, Direction.Output, portCapacity, typeof(BehaviourNode));

        if (output != null)
        {
            output.portName = "";
            output.style.flexDirection = FlexDirection.ColumnReverse;

            VisualElement connectorElement = output.Q<VisualElement>("connector");
            if(connectorElement != null)
            {
                connectorElement.pickingMode = PickingMode.Position;
            }

            outputContainer.Add(output);
        }
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
        base.SetPosition(newPos);
        Undo.RecordObject(NodeSO, "(BTree) Set Position");
        NodeSO.graphPosition.x = newPos.xMin;
        NodeSO.graphPosition.y = newPos.yMin;
        EditorUtility.SetDirty(NodeSO);
    }

    public void SortChildren()
    {
        if(NodeSO.NodeType == BehaviourNodeType.SELECTOR || NodeSO.NodeType == BehaviourNodeType.SEQUENCE)
        {
            NodeSO.children.Sort(SortByHorizontalPosition);
        }
    }

    private int SortByHorizontalPosition(BehaviourNode left, BehaviourNode right)
    {
        return left.graphPosition.x < right.graphPosition.x ? -1 : 1;
    }
}
