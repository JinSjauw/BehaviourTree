using System;
using System.Collections.Generic;
using UnityEngine;

namespace BehaviourTree.Core 
{
    public enum BlackBoardType
    {
        SELF = 0,
        SQUAD = 1,
    }

    public class BlackBoard : MonoBehaviour, IBlackBoardAccess
    {        
        /// <summary>Serialized reference-values exclusively for GameObject / Transform slots.
        /// These are kept in sync with the runtime values[] array.
        /// Index maps 1:1 to the definition's sharedVariables list.
        /// Value types are stored as null. </summary>
        [SerializeField] private List<UnityEngine.Object> serializedReferences = new();

        /// <summary>Per-component overrides for value-type variables (int, float, bool, Vector2/3/4, Color, enum).
        /// Keyed by variable name + element index — survives definition reorders without remapping. </summary>
        [SerializeField] private List<BlackboardValueOverride> valueOverrides = new();

        /// <summary>Flat slot indices of explicitly-overridden reference-type variables.
        /// Toggled by the editor Override/X buttons — independent of serializedReferences contents
        /// so that stale data in serializedReferences doesn't pollute the override state on domain reload. </summary>
        [SerializeField] private List<int> overriddenReferenceSlots = new();

        private BlackboardDefinition definition;
        private IBlackboardStorage storage;

        /// <summary>Snapshot of variable names from last BuildSerializedReferences call.
        /// Used to detect when the definition layout changes so we can remap reference values by name.</summary>
        [NonSerialized] private string[] lastBuiltVarNames;
        [NonSerialized] private int[] lastBuiltVarStrides;

        public BlackboardDefinition Definition => definition;
        public IBlackboardStorage Storage => storage;

        /// <summary>Initialize index-based storage from a definition.</summary>
        public void Initialize(BlackboardDefinition blackboardDefinition)
        {
            if(definition == null || definition != blackboardDefinition)
            {
                definition = blackboardDefinition;   
            }

            if (definition == null) return;

            int count = GetTotalSlotCount(definition);
            if (storage == null) storage = new ManagedBlackboardStorage();
            storage.Initialize(definition);

#if UNITY_EDITOR
            Debug.Log($"[BB.Initialize] def='{definition.name}' totalSlots={count} serializedRefs.Count={serializedReferences.Count}");
            IReadOnlyList<BlackboardVariableBase> allVars = definition.GetAllVariables();
            for (int vi = 0; vi < allVars.Count; vi++)
            {
                BlackboardVariableBase bv = allVars[vi];
                int baseSlot = 0;
                for (int pi = 0; pi < vi; pi++)
                {
                    int ps = allVars[pi].Stride;
                    baseSlot += (ps > 1) ? ps : 1;
                }
                Debug.Log($"[BB.Initialize]   var[{vi}]='{bv.Name}' type={bv.TypeName} stride={bv.Stride} baseSlot={baseSlot}");
            }
#endif

            while (serializedReferences.Count < count)
            {
                serializedReferences.Add(null);
            }

            for (int i = 0; i < count; i++)
            {
                BlackboardSlotKind kind = storage.GetSlotKind(i);
                UnityEngine.Object refVal = serializedReferences[i];
#if UNITY_EDITOR
                Debug.Log($"[BB.Initialize]   slot[{i}] kind={kind} serializedRef='{(refVal != null ? refVal.name : "null")}'");
#endif
                if (kind == BlackboardSlotKind.Reference && refVal != null)
                {
                    storage.SetBoxed(i, refVal);
#if UNITY_EDITOR
                    object stored = storage.GetBoxed(i);
                    Debug.Log($"[BB.Initialize]   slot[{i}] SYNCED: '{refVal.name}' → storage='{(stored != null ? ((UnityEngine.Object)stored).name : "null")}'");
#endif
                }
            }

            // Sync value-type overrides into storage.
            if (valueOverrides != null && valueOverrides.Count > 0)
            {
                for (int overrideIndex = 0; overrideIndex < valueOverrides.Count; overrideIndex++)
                {
                    BlackboardValueOverride vo = valueOverrides[overrideIndex];
                    if (vo == null) continue;

                    // Compute base slot offset by scanning variables before the matched one.
                    int baseSlot = 0;
                    bool found = false;
                    for (int varIndex = 0; varIndex < allVars.Count; varIndex++)
                    {
                        if (allVars[varIndex].Name == vo.variableName)
                        {
                            found = true;
                            break;
                        }
                        int stride = allVars[varIndex].Stride;
                        baseSlot += (stride > 1) ? stride : 1;
                    }

                    if (!found) continue;

                    int slotIndex = baseSlot + vo.elementIndex;
                    object overrideValue = vo.GetBoxedValue();
                    if (overrideValue != null && slotIndex < count)
                    {
                        storage.SetBoxed(slotIndex, overrideValue);
#if UNITY_EDITOR
                        Debug.Log($"[BB.Initialize] Value override: '{vo.variableName}[{vo.elementIndex}]' slot={slotIndex} value={overrideValue}");
#endif
                    }
                }
            }
        }

        public void BuildSerializedReferences(BlackboardDefinition blackboardDefinition)
        {
            // 1. Detect whether the variable layout is changing.
            //    Compare the NEW definition's layout against the last-built snapshot.
            //    If different, save existing reference values using the SNAPSHOT layout
            //    (not the definition, which was already mutated by BlackBoardView).
            Dictionary<string, List<UnityEngine.Object>> savedByName = null;
            bool sameLayout = true;
            if (lastBuiltVarNames != null && blackboardDefinition != null)
            {
                IReadOnlyList<BlackboardVariableBase> newVars = blackboardDefinition.GetAllVariables();

                // Compare new definition's layout against the stored snapshot
                sameLayout = HasSameVariableLayout(newVars, lastBuiltVarNames, lastBuiltVarStrides);
                if (!sameLayout && definition != null && serializedReferences.Count > 0)
                {
#if UNITY_EDITOR
                    Debug.Log($"[BB.BuildSerializedRefs] Layout changed — remapping references by name. def='{(definition != null ? definition.name : "null")}' → '{(blackboardDefinition != null ? blackboardDefinition.name : "null")}'");
#endif
                    // Save from the SNAPSHOT (last-built layout), not from definition.GetAllVariables().
                    // The ScriptableObject was already mutated by BlackBoardView to the new layout,
                    // so reading its variables would compute wrong slot offsets and save refs under wrong names.
                    savedByName = SaveReferencesByNameFromSnapshot(lastBuiltVarNames, lastBuiltVarStrides);

                    // Remap saved references when variables were renamed.
                    // SaveReferencesByNameFromSnapshot keys by the OLD name (from snapshot),
                    // but RestoreReferencesByName looks up by the NEW name (from definition).
                    // Detect renames and remap the dictionary keys to match the new names.
                    RemapRenamedReferences(savedByName, newVars);
                }
            }

            definition = blackboardDefinition;

            if (definition == null)
            {
                serializedReferences.Clear();
                lastBuiltVarNames = null;
                lastBuiltVarStrides = null;
                return;
            }

            if (!sameLayout)
            {
                int count = GetTotalSlotCount(definition);
#if UNITY_EDITOR
                Debug.Log($"[BB.BuildSerializedRefs] def='{(definition != null ? definition.name : "null")}' totalSlots={count} serializedRefs.Count={serializedReferences.Count} → rebuilding to {count}");
#endif
                serializedReferences.Clear();
                for (int i = 0; i < count; i++)
                    serializedReferences.Add(null);

                if (savedByName != null)
                    RestoreReferencesByName(definition, savedByName);
            }
            else
            {
                int count = GetTotalSlotCount(definition);
                while (serializedReferences.Count < count)
                    serializedReferences.Add(null);
                while (serializedReferences.Count > count)
                    serializedReferences.RemoveAt(serializedReferences.Count - 1);
            }

            // 3. Store new layout snapshot
            IReadOnlyList<BlackboardVariableBase> allVars = definition.GetAllVariables();
            lastBuiltVarNames = new string[allVars.Count];
            lastBuiltVarStrides = new int[allVars.Count];
            for (int i = 0; i < allVars.Count; i++)
            {
                lastBuiltVarNames[i] = allVars[i].Name;
                lastBuiltVarStrides[i] = allVars[i].Stride;
            }
        }

        public void ClearSerializedReferences()
        {
            definition = null;
            storage = null;
            serializedReferences.Clear();
            lastBuiltVarNames = null;
            lastBuiltVarStrides = null;
        }

        /// <summary>
        /// Compares current variable layout against a stored snapshot (names + strides).
        /// Returns true if the layout is identical — same variables in same order with same strides.
        /// </summary>
        private static bool HasSameVariableLayout(IReadOnlyList<BlackboardVariableBase> vars, string[] names, int[] strides)
        {
            if (vars == null || names == null) return false;
            if (vars.Count != names.Length) return false;
            for (int i = 0; i < vars.Count; i++)
            {
                if (vars[i].Name != names[i] || vars[i].Stride != strides[i])
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Saves all non-null reference values from serializedReferences, keyed by variable name.
        /// Uses the given variable list to compute slot offsets (reflects the definition at save time).
        /// </summary>
        private Dictionary<string, List<UnityEngine.Object>> SaveReferencesByName(IReadOnlyList<BlackboardVariableBase> vars)
        {
            var result = new Dictionary<string, List<UnityEngine.Object>>();
            if (vars == null) return result;

            int slot = 0;
            for (int varIndex = 0; varIndex < vars.Count; varIndex++)
            {
                BlackboardVariableBase bv = vars[varIndex];
                int stride = bv.Stride;
                int effectiveStride = (stride > 1) ? stride : 1;
                Type type = bv.GetValueType();

                if (type != null && !type.IsValueType)
                {
                    var values = new List<UnityEngine.Object>(effectiveStride);
                    for (int elementIndex = 0; elementIndex < effectiveStride; elementIndex++)
                    {
                        int slotIndex = slot + elementIndex;
                        values.Add(slotIndex < serializedReferences.Count ? serializedReferences[slotIndex] : null);
                    }
                    if (!string.IsNullOrEmpty(bv.Name))
                        result[bv.Name] = values;
                }

                slot += effectiveStride;
            }
            return result;
        }

        /// <summary>
        /// Saves all non-null reference values from serializedReferences, keyed by variable name.
        /// Uses the snapshot (names + strides) to compute slot offsets — this reflects the layout
        /// at the time of the last BuildSerializedReferences call, before the ScriptableObject was mutated.
        /// </summary>
        private Dictionary<string, List<UnityEngine.Object>> SaveReferencesByNameFromSnapshot(string[] names, int[] strides)
        {
            var result = new Dictionary<string, List<UnityEngine.Object>>();
            if (names == null) return result;

            int slot = 0;
            for (int i = 0; i < names.Length; i++)
            {
                int effectiveStride = (strides[i] > 1) ? strides[i] : 1;
                var values = new List<UnityEngine.Object>(effectiveStride);
                for (int elementIndex = 0; elementIndex < effectiveStride; elementIndex++)
                {
                    int slotIndex = slot + elementIndex;
                    values.Add(slotIndex < serializedReferences.Count ? serializedReferences[slotIndex] : null);
                }

                if (!string.IsNullOrEmpty(names[i]))
                    result[names[i]] = values;

                slot += effectiveStride;
            }
            return result;
        }

        /// <summary>
        /// Detects variable renames between the saved snapshot names and the new definition,
        /// and remaps the dictionary keys in <paramref name="savedByName"/> from old names to new names.
        /// This ensures that per-component reference overrides survive variable renaming.
        /// </summary>
        private static void RemapRenamedReferences(
            Dictionary<string, List<UnityEngine.Object>> savedByName,
            IReadOnlyList<BlackboardVariableBase> newVars)
        {
            if (savedByName == null || savedByName.Count == 0) return;
            if (newVars == null || newVars.Count == 0) return;

            // Build sets of names with saved refs, and names in the new definition
            HashSet<string> savedNames = new HashSet<string>(savedByName.Keys);
            HashSet<string> newNames = new HashSet<string>();
            for (int i = 0; i < newVars.Count; i++)
            {
                if (!string.IsNullOrEmpty(newVars[i].Name))
                    newNames.Add(newVars[i].Name);
            }

            // Names that were saved but don't appear in the new definition → removed
            List<string> removed = new List<string>();
            foreach (string name in savedNames)
            {
                if (!newNames.Contains(name))
                    removed.Add(name);
            }

            // Names in the new definition that have no saved ref → added (renamed candidates)
            List<string> added = new List<string>();
            foreach (string name in newNames)
            {
                if (!savedNames.Contains(name))
                    added.Add(name);
            }

            // Pair up removals and additions as renames, remapping the dictionary keys.
            // This follows the same pattern as HandleRenames in BlackBoardView:
            // removals and additions are paired in list order.
            int pairCount = Math.Min(removed.Count, added.Count);
            for (int i = 0; i < pairCount; i++)
            {
                string oldName = removed[i];
                string newName = added[i];
                if (savedByName.TryGetValue(oldName, out List<UnityEngine.Object> savedList))
                {
                    savedByName.Remove(oldName);
                    savedByName[newName] = savedList;
#if UNITY_EDITOR
                    Debug.Log($"[BB.BuildSerializedRefs] Renamed reference mapping: '{oldName}' → '{newName}' ({savedList?.Count ?? 0} slots)");
#endif
                }
            }
        }

        /// <summary>
        /// Restores saved reference values into serializedReferences at new slot positions.
        /// Matches by variable name — values follow their variable even if the slot offset changed.
        /// </summary>
        private void RestoreReferencesByName(BlackboardDefinition def, Dictionary<string, List<UnityEngine.Object>> saved)
        {
            if (saved == null || saved.Count == 0) return;

            IReadOnlyList<BlackboardVariableBase> allVars = def.GetAllVariables();
            int slot = 0;
            for (int varIndex = 0; varIndex < allVars.Count; varIndex++)
            {
                BlackboardVariableBase bv = allVars[varIndex];
                int stride = bv.Stride;
                int effectiveStride = (stride > 1) ? stride : 1;

                if (!string.IsNullOrEmpty(bv.Name) && saved.TryGetValue(bv.Name, out var savedValues))
                {
                    int count = Math.Min(effectiveStride, savedValues.Count);
                    for (int elementIndex = 0; elementIndex < count; elementIndex++)
                    {
                        int slotIndex = slot + elementIndex;
                        if (slotIndex < serializedReferences.Count && savedValues[elementIndex] != null)
                            serializedReferences[slotIndex] = savedValues[elementIndex];
                    }
                }

                slot += effectiveStride;
            }
        }

        private static int GetTotalSlotCount(BlackboardDefinition definition)
        {
            if (definition == null)
                return 0;

            IReadOnlyList<BlackboardVariableBase> allVars = definition.GetAllVariables();
            int total = 0;
            for (int i = 0; i < allVars.Count; i++)
            {
                if (allVars[i] == null) continue;
                int stride = allVars[i].Stride;
                total += (stride > 1) ? stride : 1;
            }
            return total;
        }

        public int FindVariableIndex(string keyName)
        {
            if (definition == null) 
            {
#if UNITY_EDITOR
                Debug.LogWarning($"[Blackboard] Definition is NULL");
#endif
                return -1; 
            }

            IReadOnlyList<BlackboardVariableBase> allVars = definition.GetAllVariables();
            for (int i = 0; i < allVars.Count; i++)
            {
                if (allVars[i].Name == keyName)
                {
                    return i;
                }
            }
            return -1;
        }

        public void Set<T>(string keyName, T value)
        {
            int index = FindVariableIndex(keyName);
            Debug.Log($"[Blackboard] Setting {keyName} to {value} at {index}");
            if (index >= 0)
            {   
                Set(index, value);
            }
        }

        public T Get<T>(string keyName)
        {
            int index = FindVariableIndex(keyName);
            if (index >= 0)
            {
                return Get<T>(index);
            }
            return default;
        }

        /// <summary>Get a value by index in the blackboard array.</summary>
        public T Get<T>(int index)
        {
            if (storage == null)
            {
#if UNITY_EDITOR
                Debug.LogWarning($"[Blackboard] Storage is NULL, returning default");
#endif
                return default;
            }
            return storage.Get<T>(index);
        }

        /// <summary>Set a value by index in the blackboard array.</summary>
        public void Set<T>(int index, T value)
        {
            if (storage == null)
            {
#if UNITY_EDITOR
                Debug.LogWarning($"[Blackboard] Storage is NULL");
#endif
                return;
            }

            storage.Set(index, value);

            // Keep serialized reference in sync for reference types
            if (definition != null && index >= 0 && index < serializedReferences.Count)
            {
                if (storage.GetSlotKind(index) == BlackboardSlotKind.Reference)
                {
                    if (value == null)
                    {
                        serializedReferences[index] = null;
                    }
                    else if (value is UnityEngine.Object unityObject)
                    {
                        serializedReferences[index] = unityObject;
                    }
                }
            }
        }

        /// <summary>Get a boxed value by slot index. Used by the bridge for type-agnostic copying.</summary>
        public object GetBoxed(int index)
        {
            if (storage == null)
                return null;
            return storage.GetBoxed(index);
        }

        /// <summary>Set a boxed value by slot index. Used by the bridge for type-agnostic copying.</summary>
        public void SetBoxed(int index, object value)
        {
            if (storage == null)
                return;
            storage.SetBoxed(index, value);

            if (definition != null && index >= 0 && index < serializedReferences.Count)
            {
                if (storage.GetSlotKind(index) == BlackboardSlotKind.Reference)
                {
                    if (value == null)
                    {
                        serializedReferences[index] = null;
                    }
                    else if (value is UnityEngine.Object unityObject)
                    {
                        serializedReferences[index] = unityObject;
                    }
                }
            }
        }

        // ── IBlackBoardAccess explicit methods ──────────────────────────

        // ── Generic ──
        T IBlackBoardAccess.Get<T>(int slot) => Get<T>(slot);
        void IBlackBoardAccess.Set<T>(int slot, T value) => Set(slot, value);
        object IBlackBoardAccess.GetBoxed(int slot) => GetBoxed(slot);
        void IBlackBoardAccess.SetBoxed(int slot, object value) => SetBoxed(slot, value);

        // ── Value-Type Override Management ─────────────────────────────

        /// <summary>Finds a value-type override by variable name and element index.</summary>
        public BlackboardValueOverride GetValueOverride(string variableName, int elementIndex)
        {
            for (int i = 0; i < valueOverrides.Count; i++)
            {
                BlackboardValueOverride vo = valueOverrides[i];
                if (vo.variableName == variableName && vo.elementIndex == elementIndex)
                    return vo;
            }
            return null;
        }

        /// <summary>Creates or updates a value-type override for the given variable/element.</summary>
        public void SetValueOverride(string variableName, int elementIndex, object value)
        {
            BlackboardValueOverride existing = GetValueOverride(variableName, elementIndex);
            if (existing != null)
            {
                existing.SetBoxedValue(value);
            }
            else
            {
                BlackboardValueOverride newOverride = new BlackboardValueOverride
                {
                    variableName = variableName,
                    elementIndex = elementIndex
                };
                newOverride.SetBoxedValue(value);
                valueOverrides.Add(newOverride);
            }
        }

        /// <summary>Removes a value-type override, reverting to the definition value.</summary>
        public void ClearValueOverride(string variableName, int elementIndex)
        {
            for (int i = valueOverrides.Count - 1; i >= 0; i--)
            {
                if (valueOverrides[i].variableName == variableName && valueOverrides[i].elementIndex == elementIndex)
                {
                    valueOverrides.RemoveAt(i);
                    return;
                }
            }
        }

        /// <summary>Returns whether the given reference slot is explicitly overridden.</summary>
        public bool IsReferenceSlotOverridden(int slotIndex) => overriddenReferenceSlots.Contains(slotIndex);

        /// <summary>Marks a reference slot as overridden.</summary>
        public void SetReferenceSlotOverridden(int slotIndex)
        {
            if (!overriddenReferenceSlots.Contains(slotIndex))
                overriddenReferenceSlots.Add(slotIndex);
        }

        /// <summary>Clears the override flag for a reference slot.</summary>
        public void ClearReferenceSlotOverridden(int slotIndex)
        {
            overriddenReferenceSlots.Remove(slotIndex);
        }


    }
}
