# Visual parts workspace validation

Windows host, 2 November 2026. Accepted job `rpg_visual_parts_accept_20261102T162700_c18431`, exit 0. Logs: `gen/rpg_visual_parts/acceptance/` and `jobs/done/rpg_visual_parts_accept_20261102T162700_c18431.log`.

- Visual workspace GUI: 22 passed.
- Existing object-editor units: 16 passed.
- Existing object-editor GUI: 64 passed, including anchor and footprint editing, foreign patches and import.
- Resource and locale atlases: zero errors. Existing project translation warnings remain.

The workspace test uses the actual hall and desk images: automatic layer alignment, alpha-aware picking, live move/resize, undo/redo, sort placement in cell coordinates, independent character positioning, JSON roundtrip, projection restrictions, drag cancellation, pointer-centred zoom, pan, no geometry changes from camera movement, detached editing, Apply and atomic parent undo. It never saves the production room file. Its initial part count is captured rather than assuming that the author's room has only one image.

Earlier launches exposed three compilation mistakes (Control casting and a missing button-style namespace), an invalid test-suite version requirement, a private test API call, and an initial single-part fixture assumption. Integration diagnostics revealed the original compile errors that unit loading had obscured. One launch ended without a test result and emitted native shutdown warnings; it was not accepted. The first running GUI iteration also caught an incomplete inverse-coordinate update in the old anchor/footprint path. Those runtime conversions were fixed and all 64 original assertions passed unchanged. The initial narrow workspace layout was rejected after inspecting its screenshot and changed to fill the tool window.

Raw captures, intermediate logs, patch staging and review notes are outside repositories in `/workspace/art_sources/cthangover/rpg_visual_parts/20261102/`. No commits were created.


## Map-editor layout, reference scale and depth follow-up

Accepted final job: `rpg_parts_v2_final_20261102T173923_2ac156` (exit 0). Visual GUI: 33 passed; object-editor units: 19 passed; original editor GUI: 64 passed. The preceding acceptance also passed all 80 RPG units. Resource atlas: zero errors/warnings; locale atlas: zero errors. New labels are localized through the normal seven-language properties files.

The GUI uses the real parts list and mode buttons, checks automatic opaque-region depth detection, blue-foot-handle dragging, both depth presets, and saves only an isolated fixture to prove that a mounted world reads new part order without re-entering its scene. Player position is retained. A separate SubViewport check samples actual overlapping green-desk/red-character pixels behind and in front of the sorting point; the expected dominant channel switches. Source-sheet tests cover the actual JRP and RPG Plus figures and a scaled synthetic sheet, including visible-body height and foot anchoring. The final reference uses a fixed idle frame so source-facing padding cannot alter its visible reference height.

One verification failed because a tool-owned save event was referenced from the RPG engine; the subscription was moved to a tool-owned bridge and the engine exposes only a refresh request. Another failed when test navigation reused a disposed workspace while switching an unsaved document; the live-save test now opens a separate editor. Assertions were retained. The JRP artwork definition is never saved by these tests; only the virtual suite's fixture is saved and restored.

Accepted behind/front captures and raw intermediate evidence are archived in `/workspace/art_sources/cthangover/rpg_visual_parts/20261102_v2/`, outside repositories. Production docs use actual game captures, visually reviewed for the list, inspector, foot marker, depth guide, character scale and both occlusion outcomes. No commits were created.
