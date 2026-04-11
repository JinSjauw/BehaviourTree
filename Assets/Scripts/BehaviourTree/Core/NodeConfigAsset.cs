using BehaviourTree.Utility;
using UnityEngine;

namespace BehaviourTree
{
    //Asset to hold static tree wide data/fields for the nodes
    public abstract class NodeConfigAsset : ScriptableObject
    {
        public NodeConfigID TreeConfigID;

        public abstract byte[] GetSerializedData();
    }

    public class NodeConfigAsset<T> : NodeConfigAsset where T : struct
    {
        public T paramFields;
        public override byte[] GetSerializedData() => ByteHelper.StructToBytes(paramFields);
    }
}

