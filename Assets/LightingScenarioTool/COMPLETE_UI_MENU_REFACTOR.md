# Complete UI Menu Refactor

## Change

`Tools > Lighting Scenario` now contains exactly one Lighting Scenario Tool authoring command:

`Build Complete UI In Current Scene`

The previous `Create Editable Demo Scene`, `Create Demo Scene`, `Build Editable UI In Current Scene`, and `Rebuild Editable UI In Current Scene` menu commands were removed.

## Behavior

The single command is intentionally deterministic and complete. In Edit Mode it:

- creates `LightingScenarioApp` when missing;
- attaches/ensures the required root components;
- creates or repairs the scene `EventSystem` input module;
- removes stale missing-script entries below the app root;
- regenerates all generated children under `LightingScenarioApp`;
- creates Timeline and Preview panels;
- creates Timeline/Preview templates and required view scripts;
- creates Timeline interaction helper components;
- assigns the serialized UI references used at runtime;
- marks the current scene dirty for saving.

Running the command again replaces the generated UI beneath `LightingScenarioApp` with the default generated hierarchy. This replaces the old create/build/repair/rebuild decision tree with one setup operation.

## Runtime guidance

All runtime error/warning text that previously referenced removed menu commands now points to:

`Tools > Lighting Scenario > Build Complete UI In Current Scene`

## Validation performed outside Unity

- verified exactly one `[MenuItem]` exists under the Lighting Scenario editor code;
- verified no runtime/editor C# source references the removed menu commands;
- checked C# brace and preprocessor directive balance;
- checked ZIP integrity after packaging.

Unity Editor compilation and Play Mode execution cannot be run in this environment.
