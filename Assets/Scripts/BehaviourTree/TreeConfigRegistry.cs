using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

public enum TreeConfigID 
{
    NONE = 0,
    HELLOWORLD = 1,
}

//Deserialization Template
[StructLayout(LayoutKind.Sequential)]
public struct TestParams 
{
    public float MoveSpeed;
    public float FireRange;
}

public static class TreeConfigRegistry
{
    public static List<TreeConfigAsset> configs;

    private static Dictionary<TreeConfigID, byte[]> treeConfigRegistry;

    //Populate the dictionary
    //Get all valid ConfigAssets
    //Get their byte[] and ID's
    //Put into the Dictionary

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize() 
    {
        treeConfigRegistry = new Dictionary<TreeConfigID, byte[]>();

        List<TreeConfigAsset> configs = new List<TreeConfigAsset>(Resources.LoadAll<TreeConfigAsset>(""));

        for(int i = 0; i < configs.Count; i++) 
        {

            TreeConfigAsset config = configs[i];
            treeConfigRegistry[config.TreeConfigID] = config.GetSerializedData();

            Debug.Log("Name: " + config.name + " ByteBlob: " + treeConfigRegistry[config.TreeConfigID]);
            ConvertTest(treeConfigRegistry[config.TreeConfigID]);
        }
    }

    static unsafe void ConvertTest(byte[] blob) 
    {
        fixed (byte* ptr = blob) 
        {
            TestParams* t = (TestParams*)ptr;

            Debug.Log("MoveSpeed: " + t->MoveSpeed);
            Debug.Log("FireRange: " + t->FireRange);
        }
    }
}
