using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BehaviourTree.Core
{
    /// <summary>
    /// Single source of truth for all field types supported by the behaviour tree.
    /// Replaces both the old <c>NodeFieldType</c> enum and <c>BlackboardTypes</c> class.
    /// </summary>
    public enum FieldType
    {
        Int = 0,
        Float = 1,
        Bool = 2,
        Vector2 = 3,
        Vector3 = 4,
        GameObject = 5,
        Transform = 6,
    }

    
}