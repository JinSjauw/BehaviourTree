using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Scripting;

public enum MethodID 
{
    NONE = 0,
    HELLOWORLD = 1,
    BYEWORLD = 2,
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class BTreeMethodAttribute : Attribute 
{
    public MethodID methodID;
    public BTreeMethodAttribute(MethodID methodID) => this.methodID = methodID;
}

public delegate NodeState BehaviorMethod(ParamSetID parameters, BlackBoard blackBoard);

public static class MethodRegistry
{
    private static Dictionary<MethodID, BehaviorMethod> methodRegister = new Dictionary<MethodID, BehaviorMethod>();

    //Populate methodRegistry with all marked methods
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize() 
    {
        Debug.Log("Registering!: ");
        methodRegister.Clear();

        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        foreach (var assembly in assemblies)
        {
            // Get all types in the assembly
            foreach (var type in assembly.GetTypes())
            {
                // Get all methods of the type
                foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
                {
                    // Check for our custom attribute
                    var attr = method.GetCustomAttribute<BTreeMethodAttribute>();
                    if (attr != null)
                    {
                        // Create a delegate from the method
                        var del = (BehaviorMethod)Delegate.CreateDelegate(typeof(BehaviorMethod), method);
                        methodRegister.Add(attr.methodID, del);
                        Debug.Log("Registered: " + attr.methodID);
                    }
                }
            }
        }
    }

    public static BehaviorMethod GetMethod(MethodID methodID) => methodRegister[methodID]; 
}

//TEST CLASS -> REMOVE LATER
public class TestMethods 
{
    [Preserve] //FOR ILC2PP
    [BTreeMethod(MethodID.HELLOWORLD)]
    public static NodeState HelloWorld(ParamSetID input, BlackBoard blackBoard)
    {
        Debug.Log("HELLO WORLD!");

        return NodeState.SUCCESS;
    }

    [BTreeMethod(MethodID.BYEWORLD)]
    public static NodeState ByeWorld(ParamSetID input, BlackBoard blackBoard)
    {
        Debug.Log("Bye world... :(");

        return NodeState.FAILURE;
    }
}
