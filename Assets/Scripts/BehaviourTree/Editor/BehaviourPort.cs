using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace BehaviourTree.Editor
{
    public class BehaviourPort : Port
    {
        public BehaviourPort(Orientation orientation, Direction direction, Capacity capacity, System.Type type) 
        : base(orientation, direction, capacity, type)
        {
            portName = "";

            LoadTemplate();
            LoadStylesheet();
            SetupBaseClasses();
        }

        public override void Connect(Edge edge)
        {
            base.Connect(edge);
            UpdateConnectionStateClass();
        }

        public override void Disconnect(Edge edge)
        {
            base.Disconnect(edge);
            UpdateConnectionStateClass();
        }

        private void LoadTemplate()
        {
            VisualTreeAsset treeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(BehaviourTreeEditorPaths.BehaviourPortUxml);
            if (treeAsset != null)
            {
                treeAsset.CloneTree(this);
            }
        }

        private void LoadStylesheet()
        {
            StyleSheet styleSheet =
                AssetDatabase.LoadAssetAtPath<StyleSheet>(BehaviourTreeEditorPaths.BehaviourPortUss);
            if (styleSheet != null)
            {
                styleSheets.Add(styleSheet);
            }
        }

        private void SetupBaseClasses()
        {
            AddToClassList("behaviour-port");

            if (direction == Direction.Input)
            {
                AddToClassList("behaviour-port--input");
            }
            else
            {
                AddToClassList("behaviour-port--output");
            }

            UpdateConnectionStateClass();

            VisualElement connectorElement = this.Q<VisualElement>("connector");
            if (connectorElement != null)
            {
                connectorElement.pickingMode = PickingMode.Position;
            }
        }

        private void UpdateConnectionStateClass()
        {
            RemoveFromClassList("behaviour-port--connected");
            RemoveFromClassList("behaviour-port--disconnected");

            if (connected)
            {
                AddToClassList("behaviour-port--connected");
            }
            else
            {
                AddToClassList("behaviour-port--disconnected");
            }
        }
    }
}
