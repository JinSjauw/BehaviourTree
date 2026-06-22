# Tomorrow Tasks

## UI Polish

- **SquadView & CommanderView UI make nicer** — Improve layout, spacing, and visual clarity of the squad and commander editor views.
- **Order Dictionary flow** — Add a button to open the Order Dictionary more easily. Polish the UI of the dictionary itself.

## Squad Size

- **Add Max Squad Size** — Add a `maxSquadSize` field to the squad definition and/or on the commander. Writing it to the squad definition should propagate so both stay in sync.
- **Leader selection** — Add a way to select a squad leader (e.g. by role). May need a new field on the squad definition.
- **Squad spawner role assignment** — Spawner assigns roles based on the amount defined per role in the squad definition. Also add a fallback role for excess agents.
- **Per-role prefab references** — When referencing a squad definition, the spawner should draw a prefab field for each unique role defined in the squad def.

## New Generic Nodes

- **Generic MoveTo node** — Create a new node that searches for a `NavMeshAgent` on the target component first, then falls back to searching in children.
- **Remove old Write BB nodes** — Delete the legacy write-blackboard nodes that have been replaced by the generic varianbles.

## Commander Behaviour

- **Formation code** — Write formation positioning logic for squad agents.
- **Flank & Suppress code** — Implement flanking and suppression behaviour nodes.

## Package

- **Make it a Unity Package** — Convert the BehaviourTree project into a proper Unity Package with `package.json`, proper assembly definitions, and package structure.

## Enemy Integration

- **Hook up the Enemy to the new BT systems** — Wire enemy behaviour to use the new behaviour tree architecture (commander, tracked bindings, generic nodes, etc.).
