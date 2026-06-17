using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace BehaviourTree.Editor
{
    /// <summary>
    /// Custom graph note element for the behaviour tree editor.
    /// Uses direct TextField and ColorField bindings — no fragile internal element queries.
    /// </summary>
    public class GraphNote : GraphElement
    {
        public EditorNoteData Data => userData as EditorNoteData;

        private VisualElement mainContainer;
        private VisualElement grabHeader;
        private TextField titleField;
        private TextField contentsField;
        private ColorField colorField;
        private ColorField textColorField;
        private VisualElement titleTextInput;
        private VisualElement contentsTextInput;
        private System.Action onChanged;

        public GraphNote()
        {
            capabilities = Capabilities.Movable
                           | Capabilities.Resizable
                           | Capabilities.Selectable
                           | Capabilities.Deletable;

            // Render behind nodes so they always overlap the note.
            layer = -1;

            focusable = true;

            VisualTreeAsset uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(BehaviourTreeEditorPaths.GraphNoteUxml);
            if (uxml != null) uxml.CloneTree(this);

            mainContainer = this.Q<VisualElement>("main-container");
            grabHeader = this.Q<VisualElement>("note-grab-header");
            titleField = this.Q<TextField>("note-title");
            contentsField = this.Q<TextField>("note-contents");
            colorField = this.Q<ColorField>("note-color");
            textColorField = this.Q<ColorField>("note-text-color");

            titleTextInput = titleField?.Q<VisualElement>("unity-text-input");
            contentsTextInput = contentsField?.Q<VisualElement>("unity-text-input");
        }

        /// <summary>
        /// Bind this note to a data model and wire up callbacks.
        /// Must be called after adding to the graph.
        /// </summary>
        public void Bind(EditorNoteData data, System.Action onChanged = null)
        {
            userData = data;
            this.onChanged = onChanged;

            titleField.SetValueWithoutNotify(data.title);
            contentsField.SetValueWithoutNotify(data.contents);
            colorField.SetValueWithoutNotify(data.noteColor);
            textColorField.SetValueWithoutNotify(data.textColor);

            SetPosition(new Rect(data.position, data.size));
            ApplyBackgroundColor(data.noteColor);
            ApplyTextColor(data.textColor);

            titleField.RegisterValueChangedCallback(evt =>
            {
                data.title = evt.newValue;
                onChanged?.Invoke();
            });

            contentsField.RegisterValueChangedCallback(evt =>
            {
                data.contents = evt.newValue;
                onChanged?.Invoke();
            });

            colorField.RegisterValueChangedCallback(evt =>
            {
                data.noteColor = evt.newValue;
                ApplyBackgroundColor(evt.newValue);
                onChanged?.Invoke();
            });

            textColorField.RegisterValueChangedCallback(evt =>
            {
                data.textColor = evt.newValue;
                ApplyTextColor(evt.newValue);
                onChanged?.Invoke();
            });
        }

        public void PersistLayout()
        {
            if (Data == null) return;
            Rect pos = GetPosition();
            Data.position = pos.position;
            Data.size = pos.size;
        }

        private void ApplyBackgroundColor(Color color)
        {
            mainContainer.style.backgroundColor = color;
            if (grabHeader != null)
            {
                Color.RGBToHSV(color, out float h, out float s, out float v);
                Color darkerColor = Color.HSVToRGB(h, s, v * 0.7f);
                darkerColor.a = color.a;
                grabHeader.style.backgroundColor = darkerColor;
            }
        }

        private void ApplyTextColor(Color color)
        {
            if (titleTextInput != null) titleTextInput.style.color = color;
            if (contentsTextInput != null) contentsTextInput.style.color = color;
        }

        /// <summary>
        /// Called by GraphView manipulators when a resize or move finishes.
        /// </summary>
        public override void UpdatePresenterPosition()
        {
            base.UpdatePresenterPosition();
            PersistLayout();
            onChanged?.Invoke();
        }
    }
}
