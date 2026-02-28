using System.Collections.Generic;

public class TreeEvaluator
{
    //Root is needs to turn into a list/array the flattened behaviourNodes in the tree.
    private BehaviourNode root;
    private Stack<int> nodeStack = new Stack<int>();

    public TreeEvaluator(BehaviourNode root) 
    {
        this.root = root;
    }

    public void Evaluate(BlackBoard blackBoard) 
    {
        //Traverse Tree
    }
}
