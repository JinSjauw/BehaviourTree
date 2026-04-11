using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace BehaviourTree 
{
    public enum NodeConfigID
    {
        NONE = 0,
        TEST_CONFIG = 1,
        MOVETO_CONFIG = 2,
    }

    //Deserialization Template
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct TestParams
    {
        public float MoveSpeed;
        public float FireRange;
    }

    public static class NodeConfigRegistry
    {
        public static List<NodeConfigAsset> configs;

        private static Dictionary<NodeConfigID, byte[]> treeConfigRegistry;

        //Save a link from treeConfigID to deserializationTemplate(Struct)
        //private static Dictionary<TreeConfigID, > 

        //Populate the dictionary
        //Get all valid ConfigAssets
        //Get their byte[] and ID's
        //Put into the Dictionary

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            treeConfigRegistry = new Dictionary<NodeConfigID, byte[]>();

            List<NodeConfigAsset> configs = new List<NodeConfigAsset>(Resources.LoadAll<NodeConfigAsset>(""));

            for (int i = 0; i < configs.Count; i++)
            {

                NodeConfigAsset config = configs[i];
                treeConfigRegistry[config.TreeConfigID] = config.GetSerializedData();

                //Debug.Log("Name: " + config.name + " ByteBlob: " + treeConfigRegistry[config.TreeConfigID]);
                //ConvertTest(treeConfigRegistry[config.TreeConfigID]);
            }
        }

        public static byte[] GetStaticConfig(NodeConfigID configID)
        {
            if (configID == NodeConfigID.NONE) return Array.Empty<byte>();

            if (!treeConfigRegistry.TryGetValue(configID, out byte[] blob))
            {
                #if UNITY_EDITOR
                throw new KeyNotFoundException($"Missing static config: {configID}");
                #else
                Debug.LogError($"Missing static config: {configID}");
                return Array.Empty<byte>(); // or a pre-allocated static empty array
                #endif
            }
            return blob;
        }

        public static unsafe void ConvertTest(byte[] blob)
        {
            fixed (byte* ptr = blob)
            {
                TestParams* t = (TestParams*)ptr;

                Debug.Log("MoveSpeed: " + t->MoveSpeed);
                Debug.Log("FireRange: " + t->FireRange);
            }
        }
    }
}
