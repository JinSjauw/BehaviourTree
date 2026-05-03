using System;
using System.Collections.Generic;
using UnityEngine;


namespace BehaviourTree.Core
{
    [Serializable]
    public struct BlackboardVariable
    {
        public string name;
        public string typeName;  
    }

    [CreateAssetMenu(menuName = "BehaviourTree/Blackboard Definition")]
    public class BlackboardDefinition : ScriptableObject
    {
        public List<BlackboardVariable> variables = new();
    }
}
