using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "BehaviourTreeAsset", menuName = "Scriptable Objects/BT")]
public class BehaviourTreeAsset : ScriptableObject
{
    public List<BehaviourNode> nodesList;
    public BehaviourNode rootCopy;
    [SerializeField] private BehaviourNode root;

    //Create unique runtime instances of the SO's
    public void Initialize() 
    {
        nodesList = new List<BehaviourNode>();

        rootCopy = Instantiate(root);

        nodesList.Add(rootCopy);

        for(int i = 0; i < rootCopy.children.Count; i++) 
        {
            BehaviourNode nodeCopy = Instantiate(rootCopy.children[i]);
            
            rootCopy.children[i] = nodeCopy;
            nodesList.Add(nodeCopy);

            for(int j = 0; j < nodeCopy.children.Count; j++)
            {
                BehaviourNode childCopy = Instantiate(nodeCopy.children[j]);
                nodeCopy.children[j] = childCopy;
                nodesList.Add(childCopy);
            }
        }
    }

    public void ClearNodes() 
    {
        for(int i = 0; i < nodesList.Count; i++) 
        {
            Destroy(nodesList[i]);
        }
    }
}
