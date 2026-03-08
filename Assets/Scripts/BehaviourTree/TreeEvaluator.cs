using System.Collections.Generic;

//This class is meant to evaluate the tree and also handle the per node type execution logic.
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
