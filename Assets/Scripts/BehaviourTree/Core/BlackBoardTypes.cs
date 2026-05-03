    
namespace BehaviourTree.Core
{
    public static class BlackboardTypes
    {
        public static readonly (string typeName, string displayName)[] AllowedTypeNames =
        {
            ("System.Int32", "Integer"),
            ("System.Single", "Float"),
            ("System.Boolean", "Bool"),
            ("UnityEngine.Vector2", "Vector2"),
            ("UnityEngine.Vector3", "Vector3"),
            ("UnityEngine.GameObject", "GameObject"),
            ("UnityEngine.Transform", "Transform"),
        };
    }
}
