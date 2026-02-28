public class ActionNode : BehaviourNode
{
    public override BehaviourNodeType NodeType => BehaviourNodeType.ACTION;

    public string evaluatorID;

    //Maybe also a string/enum to find the corresponding param collection.
    //Maybe also entityID as KEY

    //paramSetType -> Find correct registry. 
    //entityID -> Find correct entry in correct paramSetType registry.

    public string paramSetType;
    public string entityID;
}
