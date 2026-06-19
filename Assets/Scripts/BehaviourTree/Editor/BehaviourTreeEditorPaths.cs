namespace BehaviourTree.Editor
{
    public static class BehaviourTreeEditorPaths
    {
        private const string BasePath = "Assets/Scripts/BehaviourTree/Editor/UIDocuments/";

        // ── Main editor ──────────────────────────────────────
        public const string EditorUxml = BasePath + "BehaviourTreeEditor.uxml";
        public const string EditorUss = BasePath + "BehaviourTreeEditor.uss";

        // ── Graph views ──────────────────────────────────────
        public const string GraphNodeViewUxml = BasePath + "GraphNodeView.uxml";
        public const string GraphNoteUxml = BasePath + "GraphNote.uxml";
        public const string GraphNoteUss = BasePath + "GraphNote.uss";
        public const string BehaviourPortUxml = BasePath + "BehaviourPort.uxml";
        public const string BehaviourPortUss = BasePath + "BehaviourPort.uss";

        // ── Blackboard / variable editing ────────────────────
        public const string BlackboardVariableEntryUxml = BasePath + "BlackboardVariableEntry.uxml";
        public const string BlackboardVariableEntryUss = BasePath + "BlackboardVariableEntry.uss";
        public const string ArrayElementRowUxml = BasePath + "ArrayElementRow.uxml";
        public const string ArrayElementRowUss = BasePath + "ArrayElementRow.uss";
        public const string VariableTypeSearchPopupUxml = BasePath + "VariableTypeSearchPopup.uxml";
        public const string VariableTypeSearchPopupUss = BasePath + "VariableTypeSearchPopup.uss";

        // ── Tracked variables ────────────────────────────────
        public const string TrackedVariablesViewUxml = BasePath + "TrackedVariablesView.uxml";
        public const string TrackedVariablesViewUss = BasePath + "TrackedVariablesView.uss";
        public const string TrackedBindingRowUxml = BasePath + "TrackedBindingRow.uxml";
        public const string TrackedBindingRowUss = BasePath + "TrackedBindingRow.uss";
    }
}
