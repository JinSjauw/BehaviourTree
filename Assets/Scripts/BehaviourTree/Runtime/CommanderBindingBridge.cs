using System.Collections.Generic;
using BehaviourTree.Core;
using UnityEngine;

namespace BehaviourTree.Runtime
{
    /// <summary>
    /// Binding between a commander BB variable and the corresponding
    /// variable in the agent's merged self BB.
    /// </summary>
    [System.Serializable]
    public struct CommanderBinding
    {
        [Tooltip("Name of the variable in the commander BB definition.")]
        public string commanderVarName;

        [Tooltip("Name of the variable in the agent's merged self BB definition.")]
        public string selfVarName;
        
        [Tooltip("If true, only updates the commander BB. If false, updates both BBs.")]
        public bool onlyUpdateCommander;
    }

    /// <summary>
    /// Lives on each agent GameObject. Copies data between the commander
    /// BlackBoard and the agent's merged self BlackBoard each evaluation tick.
    ///
    /// Tick order:
    ///   1. PushDataProviders()        – components write to self BB
    ///   2. CopyCommanderToAgent()     – commander BB → self BB (role, target, etc.)
    ///   3. TreeRunner.Evaluate()      – agent tree reads self BB
    ///   4. CopyAgentToCommander()     – self BB → commander BB (health, position, etc.)
    /// </summary>
    public class CommanderBindingBridge : MonoBehaviour
    {
        [SerializeField] private BlackBoard selfBB;
        [SerializeField] private BlackBoard commanderBB;
        [SerializeField] private int agentID;
        [SerializeField] private List<CommanderBinding> bindings = new List<CommanderBinding>();

        private List<IBlackboardDataProvider> dataProviders;

        // Runtime-resolved indices
        private int[] commanderToSelfCommanderIndices;  // [selfIndex, commanderBaseIndex, stride, ...]
        private int[] selfToCommanderIndices;           // [selfIndex, commanderBaseIndex, stride, ...]

        private void Awake()
        {
            CollectDataProviders();
        }

        private void CollectDataProviders()
        {
            IBlackboardDataProvider[] providers = GetComponentsInChildren<IBlackboardDataProvider>();
            dataProviders = new List<IBlackboardDataProvider>(providers);
        }

        /// <summary>
        /// Resolves variable names to slot indices + strides from both BB definitions.
        /// Called by CommanderTreeRunner after all BBs are initialized.
        /// </summary>
        public void ResolveBindings()
        {
            if (selfBB == null || commanderBB == null || bindings == null)
                return;

            IBlackboardStorage selfStorage = selfBB.Storage;
            IBlackboardStorage commanderStorage = commanderBB.Storage;

            if (selfStorage is not ManagedBlackboardStorage selfManaged)
            {
                Debug.LogError($"[Bridge.ResolveBindings] Self BB storage is not ManagedBlackboardStorage (got {selfStorage?.GetType().Name ?? "null"}). Bindings cannot be resolved.");
                return;
            }
            if (commanderStorage is not ManagedBlackboardStorage commanderManaged)
            {
                Debug.LogError($"[Bridge.ResolveBindings] Commander BB storage is not ManagedBlackboardStorage (got {commanderStorage?.GetType().Name ?? "null"}). Bindings cannot be resolved.");
                return;
            }

            BlackboardDefinition selfDef = selfStorage.Definition;
            BlackboardDefinition commanderDef = commanderStorage.Definition;

            if (selfDef == null || commanderDef == null)
                return;

            // Debug.Log($"[Bridge.ResolveBindings] Resolving {bindings.Count} binding(s) for agent {agentID}. " +
            //           $"Commander def: '{commanderDef.name}' ({commanderDef.VariableCount} vars). " +
            //           $"Self def: '{selfDef.name}' ({selfDef.VariableCount} vars).");

            IReadOnlyList<BlackboardVariableBase> commanderVars = commanderDef.GetAllVariables();
            IReadOnlyList<BlackboardVariableBase> selfVars = selfDef.GetAllVariables();

            List<int> cToS = new List<int>();
            List<int> sToC = new List<int>();

            for (int bindingIndex = 0; bindingIndex < bindings.Count; bindingIndex++)
            {
                CommanderBinding binding = bindings[bindingIndex];

                // Find commander variable index
                int commanderVarIndex = -1;
                for (int ci = 0; ci < commanderVars.Count; ci++)
                {
                    if (commanderVars[ci].Name == binding.commanderVarName)
                    {
                        commanderVarIndex = ci;
                        break;
                    }
                }
                if (commanderVarIndex < 0)
                {
                    Debug.LogWarning($"[Bridge.ResolveBindings] Commander var '{binding.commanderVarName}' not found in commander def.");
                    continue;
                }

                // Find self variable index
                int selfVarIndex = -1;
                for (int si = 0; si < selfVars.Count; si++)
                {
                    if (selfVars[si].Name == binding.selfVarName)
                    {
                        selfVarIndex = si;
                        break;
                    }
                }
                if (selfVarIndex < 0)
                {
                    Debug.LogWarning($"[Bridge.ResolveBindings] Self var '{binding.selfVarName}' not found in self def.");
                    continue;
                }

                // Get commander slot range (accounts for stride)
                commanderManaged.GetVariableSlotRange(commanderVarIndex, out int commanderBaseSlot, out int stride);

                int selfSlot = selfVarIndex;
                selfManaged.GetVariableSlotRange(selfVarIndex, out int selfBaseSlot, out int selfStride);

                // Debug.Log($"[Bridge.ResolveBindings] Binding: '{binding.commanderVarName}' → '{binding.selfVarName}' | " +
                //           $"Commander: varIdx={commanderVarIndex} baseSlot={commanderBaseSlot} stride={stride} | " +
                //           $"Self: varIdx={selfVarIndex} baseSlot={selfBaseSlot} stride={selfStride}");
                
                if (!binding.onlyUpdateCommander)
                {
                     // Commander → Self
                    cToS.Add(selfBaseSlot);
                    cToS.Add(commanderBaseSlot);
                    cToS.Add(stride);
                }

                // Self → Commander
                sToC.Add(selfBaseSlot);
                sToC.Add(commanderBaseSlot);
                sToC.Add(stride);
            }

            commanderToSelfCommanderIndices = cToS.ToArray();
            selfToCommanderIndices = sToC.ToArray();
        }

        /// <summary>
        /// Pushes runtime component data into the agent's self BB.
        /// </summary>
        public void PushDataProviders()
        {
            if (dataProviders == null) return;

            for (int i = 0; i < dataProviders.Count; i++)
            {
                dataProviders[i].ProvideData(selfBB);
            }
        }

        /// <summary>
        /// Copies commander BB values into the agent's self BB.
        /// E.g. Commander.AgentRole[agentID] → Self.MyRole
        /// </summary>
        public void CopyCommanderToAgent()
        {
            if (commanderToSelfCommanderIndices == null || commanderBB == null || selfBB == null)
            {
                Debug.LogWarning($"[Bridge.CopyCommanderToAgent] Skipped — indices={commanderToSelfCommanderIndices != null}, selfBB={selfBB != null}, cmdrBB={commanderBB != null}");
                return;
            }

            for (int i = 0; i < commanderToSelfCommanderIndices.Length; i += 3)
            {
                int selfSlot = commanderToSelfCommanderIndices[i];
                int commanderBase = commanderToSelfCommanderIndices[i + 1];
                int stride = commanderToSelfCommanderIndices[i + 2];
                int commanderSlot = (stride > 1) ? commanderBase + agentID : commanderBase;

                object boxed = commanderBB.GetBoxed(commanderSlot);
                //Debug.Log($"[Bridge.CopyCommanderToAgent] agentID={agentID} selfSlot={selfSlot} ← cmdrSlot={commanderSlot}(base={commanderBase}+{(stride>1?agentID:0)}) stride={stride} value={boxed}");
                selfBB.SetBoxed(selfSlot, boxed);
            }
        }

        /// <summary>
        /// Copies agent self BB values back to the commander BB.
        /// E.g. Self.Health → Commander.AgentHealth[agentID]
        /// </summary>
        public void CopyAgentToCommander()
        {
            if (selfToCommanderIndices == null || commanderBB == null || selfBB == null)
                return;

            for (int i = 0; i < selfToCommanderIndices.Length; i += 3)
            {
                int selfSlot = selfToCommanderIndices[i];
                int commanderBase = selfToCommanderIndices[i + 1];
                int stride = selfToCommanderIndices[i + 2];
                int commanderSlot = (stride > 1) ? commanderBase + agentID : commanderBase;

                object boxed = selfBB.GetBoxed(selfSlot);
                commanderBB.SetBoxed(commanderSlot, boxed);
            }
        }
    }
}
