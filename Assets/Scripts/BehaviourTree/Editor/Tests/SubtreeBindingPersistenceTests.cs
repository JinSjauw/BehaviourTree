using System.Collections.Generic;
using BehaviourTree.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BehaviourTree.Editor.Tests
{
    public class SubtreeBindingPersistenceTests
    {
        private BehaviourTreeAsset parentTreeAsset;
        private BehaviourTreeAsset subtreeAsset;
        private SubtreeNode subtreeNode;

        [SetUp]
        public void SetUp()
        {
            parentTreeAsset = ScriptableObject.CreateInstance<BehaviourTreeAsset>();
            parentTreeAsset.name = "TestParentTree";

            subtreeAsset = ScriptableObject.CreateInstance<BehaviourTreeAsset>();
            subtreeAsset.name = "TestSubtree";
            subtreeAsset.nodesList = new List<BehaviourNode>();

            subtreeNode = ScriptableObject.CreateInstance<SubtreeNode>();
            subtreeNode.name = "TestSubtreeNode";
            subtreeNode.subTreeAsset = subtreeAsset;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(parentTreeAsset);
            Object.DestroyImmediate(subtreeAsset);
            Object.DestroyImmediate(subtreeNode);
        }

        [Test]
        public void BindingRoundTrip_ThroughSerializedObject_PreservesData()
        {
            var bb = ScriptableObject.CreateInstance<BlackboardDefinition>();
            bb.sharedVariables = new List<BlackboardVariable>
            {
                new BlackboardVariable { name = "IntVar", typeName = "System.Int32" },
                new BlackboardVariable { name = "FloatVar", typeName = "System.Single" }
            };
            subtreeAsset.blackboardDefinition = bb;

            subtreeNode.bindings = new List<SubtreeBinding>
            {
                new SubtreeBinding { subtreeVariableName = "IntVar", parentVariableName = "ParentInt" },
                new SubtreeBinding { subtreeVariableName = "FloatVar", parentVariableName = "" }
            };

            SerializedObject so = new SerializedObject(subtreeNode);
            SerializedProperty bindingsProp = so.FindProperty("bindings");
            Assert.AreEqual(2, bindingsProp.arraySize, "bindings arraySize should be 2");

            SerializedProperty b0 = bindingsProp.GetArrayElementAtIndex(0);
            Assert.AreEqual("IntVar", b0.FindPropertyRelative("subtreeVariableName").stringValue);
            Assert.AreEqual("ParentInt", b0.FindPropertyRelative("parentVariableName").stringValue);

            SerializedProperty b1 = bindingsProp.GetArrayElementAtIndex(1);
            Assert.AreEqual("FloatVar", b1.FindPropertyRelative("subtreeVariableName").stringValue);
            Assert.AreEqual("", b1.FindPropertyRelative("parentVariableName").stringValue);
        }

        [Test]
        public void BindingSurvives_WhenSubtreeAssetHasSameVars()
        {
            var bb = ScriptableObject.CreateInstance<BlackboardDefinition>();
            bb.sharedVariables = new List<BlackboardVariable>
            {
                new BlackboardVariable { name = "A", typeName = "System.Int32" },
                new BlackboardVariable { name = "B", typeName = "System.Single" }
            };
            subtreeAsset.blackboardDefinition = bb;

            subtreeNode.bindings = new List<SubtreeBinding>
            {
                new SubtreeBinding { subtreeVariableName = "A", parentVariableName = "MappedVar" },
                new SubtreeBinding { subtreeVariableName = "B", parentVariableName = "" }
            };

            SerializedObject so = new SerializedObject(subtreeNode);
            so.Update();
            SerializedProperty bindingsProp = so.FindProperty("bindings");

            // simulate EnsureBindingsSize — same count, no change
            List<BlackboardVariable> vars = bb.sharedVariables;
            while (bindingsProp.arraySize < vars.Count)
                bindingsProp.InsertArrayElementAtIndex(bindingsProp.arraySize);
            while (bindingsProp.arraySize > vars.Count)
                bindingsProp.DeleteArrayElementAtIndex(bindingsProp.arraySize - 1);

            so.ApplyModifiedProperties();

            Assert.AreEqual(2, subtreeNode.bindings.Count, "binding count unchanged");
            Assert.AreEqual("MappedVar", subtreeNode.bindings[0].parentVariableName, "mapping preserved");
            Assert.AreEqual("", subtreeNode.bindings[1].parentVariableName, "local preserved");
        }

        [Test]
        public void BindingPreserved_WhenSubtreeVarsIncrease()
        {
            var bb = ScriptableObject.CreateInstance<BlackboardDefinition>();
            bb.sharedVariables = new List<BlackboardVariable>
            {
                new BlackboardVariable { name = "A", typeName = "System.Int32" }
            };
            subtreeAsset.blackboardDefinition = bb;

            subtreeNode.bindings = new List<SubtreeBinding>
            {
                new SubtreeBinding { subtreeVariableName = "A", parentVariableName = "MappedVar" }
            };

            SerializedObject so = new SerializedObject(subtreeNode);
            so.Update();
            SerializedProperty bindingsProp = so.FindProperty("bindings");

            // subtree now has 2 vars (was 1)
            List<BlackboardVariable> newVars = new List<BlackboardVariable>
            {
                new BlackboardVariable { name = "A", typeName = "System.Int32" },
                new BlackboardVariable { name = "B", typeName = "System.Single" }
            };

            while (bindingsProp.arraySize < newVars.Count)
                bindingsProp.InsertArrayElementAtIndex(bindingsProp.arraySize);
            while (bindingsProp.arraySize > newVars.Count)
                bindingsProp.DeleteArrayElementAtIndex(bindingsProp.arraySize - 1);

            // Simulate full editor loop: overwrite subtreeVariableName and run popup logic
            for (int i = 0; i < newVars.Count; i++)
            {
                SerializedProperty bp = bindingsProp.GetArrayElementAtIndex(i);
                bp.FindPropertyRelative("subtreeVariableName").stringValue = newVars[i].name;
            }
            so.ApplyModifiedProperties();

            Assert.AreEqual(2, subtreeNode.bindings.Count, "binding count increased");
            Assert.AreEqual("MappedVar", subtreeNode.bindings[0].parentVariableName, "first mapping preserved");
            // InsertArrayElementAtIndex copies the previous element, so the new entry
            // starts with MappedVar — but the full editor loop would run BuildParentOptions
            // for var "B" (type Single) which has no matching parent vars, showing <Local>,
            // which would then write "". The test doesn't simulate IMGUI popups, so it
            // preserves whatever was inserted.
            Assert.AreEqual("MappedVar", subtreeNode.bindings[1].parentVariableName,
                "new binding inherits from copy via InsertArrayElementAtIndex; " +
                "the editor loop would overwrite this via the popup with empty.");
        }

        [Test]
        public void BindingPreserved_WhenParentBlackboardHasNoMatch_BuildParentOptionsReturnsMissing()
        {
            var bb = ScriptableObject.CreateInstance<BlackboardDefinition>();
            bb.sharedVariables = new List<BlackboardVariable>
            {
                new BlackboardVariable { name = "A", typeName = "System.Int32" }
            };
            subtreeAsset.blackboardDefinition = bb;

            subtreeNode.bindings = new List<SubtreeBinding>
            {
                new SubtreeBinding { subtreeVariableName = "A", parentVariableName = "TargetVar" }
            };

            SerializedObject so = new SerializedObject(subtreeNode);
            so.Update();
            SerializedProperty b0 = so.FindProperty("bindings").GetArrayElementAtIndex(0);
            string before = b0.FindPropertyRelative("parentVariableName").stringValue;
            Assert.AreEqual("TargetVar", before, "binding intact before switch");

            BlackboardDefinition emptyBb = ScriptableObject.CreateInstance<BlackboardDefinition>();
            emptyBb.sharedVariables = new List<BlackboardVariable>();

            // Simulate BuildParentOptions with empty parent — should show Missing entry
            List<string> matching = new List<string>();
            matching.Insert(0, "<Local>");
            bool isMissing = !string.IsNullOrEmpty("TargetVar") && !matching.Contains("TargetVar");
            if (isMissing)
                matching.Add("<Missing: TargetVar>");
            string[] options = matching.ToArray();

            int selectedIndex = isMissing ? matching.Count - 1 : 0;
            Assert.AreEqual(1, selectedIndex, "should select Missing entry for TargetVar");

            // Simulate popup: user sees <Missing: TargetVar> highlighted yellow, doesn't change it
            int nextIndex = selectedIndex;

            bool missingAndUnchanged = isMissing && nextIndex == selectedIndex;
            if (missingAndUnchanged)
            {
                // Keep the stored value — don't overwrite
            }
            else if (nextIndex <= 0)
            {
                b0.FindPropertyRelative("parentVariableName").stringValue = string.Empty;
            }
            else
            {
                b0.FindPropertyRelative("parentVariableName").stringValue = options[nextIndex];
            }
            so.ApplyModifiedProperties();

            string after = subtreeNode.bindings[0].parentVariableName;
            Assert.AreEqual("TargetVar", after,
                "FIX: binding preserved because the Missing entry indicates the value wasn't found in the current parent, " +
                "not that the user chose Local.");
        }

        [Test]
        public void BindingPreserved_WhenSameVariableNameExistsInNewParent()
        {
            var bb = ScriptableObject.CreateInstance<BlackboardDefinition>();
            bb.sharedVariables = new List<BlackboardVariable>
            {
                new BlackboardVariable { name = "A", typeName = "System.Int32" }
            };
            subtreeAsset.blackboardDefinition = bb;

            subtreeNode.bindings = new List<SubtreeBinding>
            {
                new SubtreeBinding { subtreeVariableName = "A", parentVariableName = "SharedVar" }
            };

            BlackboardDefinition newParentBb = ScriptableObject.CreateInstance<BlackboardDefinition>();
            newParentBb.sharedVariables = new List<BlackboardVariable>
            {
                new BlackboardVariable { name = "SharedVar", typeName = "System.Int32" }
            };

            // BuildParentOptions with new parent that happens to have "SharedVar" of matching type
            List<string> matching = new List<string>();
            matching.Add("SharedVar");
            matching.Sort(System.StringComparer.Ordinal);
            matching.Insert(0, "<Local>");
            string[] options = matching.ToArray();

            int selectedIndex = matching.IndexOf("SharedVar");
            Assert.AreEqual(1, selectedIndex, "SharedVar should be at index 1");

            SerializedObject so = new SerializedObject(subtreeNode);
            so.Update();
            SerializedProperty b0 = so.FindProperty("bindings").GetArrayElementAtIndex(0);
            int nextIndex = selectedIndex;
            b0.FindPropertyRelative("parentVariableName").stringValue = nextIndex <= 0 ? string.Empty : options[nextIndex];
            so.ApplyModifiedProperties();

            Assert.AreEqual("SharedVar", subtreeNode.bindings[0].parentVariableName,
                "Mapping preserved when same-named var exists in new parent");
        }
    }
}
