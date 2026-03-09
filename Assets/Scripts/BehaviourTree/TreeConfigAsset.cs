using UnityEngine;

//Asset to hold static tree wide data/fields for the nodes
public abstract class TreeConfigAsset : ScriptableObject
{
    public TreeConfigID TreeConfigID;

    public abstract byte[] GetSerializedData();
}
