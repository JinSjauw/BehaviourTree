using System.ComponentModel;
using UnityEngine;

[CreateAssetMenu(fileName = "TreeTestConfig", menuName = "Scriptable Objects/TreeTestConfig")]
public class TreeTestConfig : TreeConfigAsset
{
    public float MoveSpeed;
    public float FireRange;

    public override byte[] GetSerializedData()
    {
        TestParams test = new TestParams();
        test.MoveSpeed = MoveSpeed;
        test.FireRange = FireRange;

        return ByteHelper.StructToBytes(test);
    }
}
