using System;
using UnityEngine;

namespace BehaviourTree.Editor
{
    /// <summary>
    /// Serialized data for a graph editor note.
    /// Stored on the BehaviourTreeAsset and restored on graph load.
    /// </summary>
    [Serializable]
    public class EditorNoteData
    {
        public Vector2 position;
        public Vector2 size = new Vector2(200, 150);
        public string title = "Note";
        public string contents = "";
        public Color noteColor = new Color(1f, 0.92f, 0.55f, 0.85f);
        public Color textColor = new Color(0f, 0f, 0f, 1f);
    }
}
