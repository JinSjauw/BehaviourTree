using BehaviourTree;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

public class BehaviourNodeView : Node
{
    public BehaviourNode NodeSO { get; private set; }
    public MethodID LeafMethodID { get; set; }  //enum ID for delegate dispatch
    public string Guid { get; private set; }

    public Port input;
    public Port output;

    public BehaviourNodeView(BehaviourNode nodeObject)
    {
        NodeSO = nodeObject;
        Guid = NodeSO.guid;

        title = nodeObject.NodeType.ToString(); // Or use mainTitle/subTitle for styling

        style.left = NodeSO.graphPosition.x;
        style.top = NodeSO.graphPosition.y;

        style.borderTopWidth = 3;
        style.borderBottomWidth = 3;
        style.borderLeftWidth = 3;
        style.borderRightWidth = 3;
        style.borderTopColor = nodeObject.NodeType switch
        {
            BehaviourNodeType.ROOT => Color.green,
            BehaviourNodeType.SELECTOR => Color.blue,
            BehaviourNodeType.SEQUENCE => Color.purple,
            BehaviourNodeType.ACTION => Color.red,
            BehaviourNodeType.CONDITION => Color.yellow,
            _ => Color.gray
        };

        CreateInputPorts();
        CreateOutputPorts();
    }

    private void CreateInputPorts()
    {
        if (NodeSO.NodeType == BehaviourNodeType.ROOT) return;

        input = InstantiatePort(Orientation.Vertical, Direction.Input, Port.Capacity.Single, typeof(BehaviourNode));

        if(input != null) 
        {
            input.portName = "";
            inputContainer.Add(input);
        }
    }

    private void CreateOutputPorts()
    {
        if (NodeSO is ActionNode || NodeSO is ConditionNode)
        {
            return;
        }

        output = InstantiatePort(Orientation.Vertical, Direction.Output, Port.Capacity.Multi, typeof(BehaviourNode));
        if (output != null)
        {
            output.portName = "";
            outputContainer.Add(output);
        }
    }

    public int GetChildCount() =>
        outputContainer.Children().OfType<Port>().FirstOrDefault()?.connections.Count() ?? 0;

    public override void SetPosition(Rect newPos)
    {
        base.SetPosition(newPos);

        NodeSO.graphPosition.x = newPos.xMin;
        NodeSO.graphPosition.y = newPos.yMin;
    }
}
