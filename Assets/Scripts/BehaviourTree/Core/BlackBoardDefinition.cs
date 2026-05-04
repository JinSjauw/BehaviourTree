using System;
using System.Collections.Generic;
using UnityEngine;


namespace BehaviourTree.Core
{
    [CreateAssetMenu(menuName = "BehaviourTree/Blackboard Definition")]
    public class BlackboardDefinition : ScriptableObject
    {
        public List<BlackboardVariable> variables = new();
    }
}
