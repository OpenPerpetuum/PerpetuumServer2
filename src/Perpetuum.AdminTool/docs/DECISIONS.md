# Decisions

## 2026-05-06 — Robot-template editor refinements (post-G)

1. **Ammoable detection uses `attributeflags.ammo_required`** (bit 18), not the runtime-derived `options.ammoCategoryFlags > 0 && ammoCapacity > 0` rule. Reason: the attribute flag is the authoritative data-side declaration.
2. **Ammo target is `options.ammoType`** (Genxy long, e.g. `L3120a`), not `options.ammoCategoryFlags`. Verified against real DB rows (`p35.5/10_Black_Seth_fix.sql`) and `EntityDefaultOptions.cs`. The dictionary dump in server's `ActiveModule.ToDictionary` also surfaces `ammoCategoryFlags` at runtime, but the *stored* key in `entitydefaults.options` is `ammoType`.
3. **Filter shape is hierarchical** (`IsCategory` mask), not exact match. We re-implement the bit math locally; calling `Perpetuum.CategoryFlagsExtensions.IsCategory` would trigger its static ctor which binds to the `Db` facade we deliberately avoid.
4. **Empty target → empty picks (not "all").** When a module has `ammo_required` but its `options.ammoType` is 0, the mask collapses and every entity would pass. Early-return with empty picks; the editable ComboBox still allows manual id entry.
5. **Per-part category filters.** Robot/Head/Chassis/Leg/Container dropdowns each filter to a specific CategoryFlags subtree:
   - Robot → `cf_robots = 0x1`
   - Head → `cf_robot_head = 0x150`
   - Chassis → `cf_robot_chassis = 0x250`
   - Leg → `cf_robot_leg = 0x350`
   - Container → `cf_robot_inventory = 0x30915`
6. **Disabled filtering.** All editor selectors hide `enabled = 0` rows. The Entities tab itself shows them so they can be re-enabled. `EntityRepository`'s old hard `WHERE enabled = 1` was removed.

## 2026-05-06 — Task F descope (plain-text options)

The user opted out of a structured Genxy editor. `entitydefaults.options` becomes a plain editable TextBox; users edit the Genxy string directly. No round-trip parsing in the tool.

**Why:** simpler, smaller surface, fewer ways to corrupt data. The server side already validates Genxy on load.
**How to apply:** when extending the entities editor, do not invest in field-by-field options editing. Treat `options` as opaque text.

## 2026-05-06 — Task G scope (robot-template structured editor)

1. **Placement.** Modal opened from "Structured edit…" button on the Robot Templates view. Round-trips Genxy in/out of the row's `Description` text (the existing raw editor and Validate button stay).
2. **Source of truth for shape.** `ROBOT_TEMPLATE_SAMPLE.md` at the solution root, plus `Perpetuum/Items/Templates/RobotTemplate.cs` (server's `ToDictionary` / `CreateFromDictionary`). Top-level keys we own: `robot`, `head`, `chassis`, `leg`, `container`, `headModules`, `chassisModules`, `legModules`. Anything else (e.g. `items`) is preserved verbatim through the modal.
3. **Slots.** Slot count + order come from the chosen part's `entitydefaults.options.slotFlags` (int[]). Module candidates are filtered by the slot's flag using the server's rule from `RobotComponent.IsValidSlotTo`: `(module.moduleFlag & slotFlag) == module.moduleFlag` AND specialized-bit (1 &lt;&lt; 11) check.
4. **Ammo.** Ammo candidates are filtered by the chosen module's `options.ammoCategoryFlags` using the hierarchical category test from `CategoryFlagsExtensions.IsCategory` (mask up to highest non-zero byte, then equality). Ammo controls are only enabled when the module is "ammoable" (`ammoCategoryFlags > 0 && ammoCapacity > 0`).
5. **Cargo `items`.** Not editable. If the input Genxy carries `items`, we round-trip it untouched.

## 2026-05-06 — Skip Task E

Task E (bulk multi-row edits in entities grid) is dropped from this pass — not needed currently. Polish-backlog ordering becomes A → B → C → D → F → G.

## 2026-05-06 — Task C scope (auto-create translation key)

1. **Trigger.** Only when the user adds a brand-new `entitydefaults` row (`IsNew=true`). Renaming `definitionName` on an existing row does not create a new key.
2. **Initial values.** English (lang 0) = the `definitionName` itself; every other loaded language gets an empty string. User can edit afterwards in the Translations tab.
3. **Save coupling.** The translation key is created when the user commits (either Direct DB or SQL Script mode). Acceptable that a SQL-Script-mode commit creates the JSON key even though the row isn't in the DB yet — the user knows what they queued.

## 2026-05-06 — Polish-backlog kickoff

1. **`entitydefaults` filter for Loot/Relations dropdowns.** Leave as-is — both pickers show all rows, no `enabled = 1` filter.
2. **Definition edits on existing template-relations.** Keep current behavior — emit `UPDATE robottemplaterelation SET definition=new WHERE definition=old` (in-place row update), not delete+insert.
3. **Phase 4 + Phase 5 status.** Both treated as done. No further verification gate.
4. **Polish backlog ordering.** Skip `npcflockloot` editor entirely (not needed). Remaining order:
   1. Cache for `entitydefaults` + `robottemplates` (foundation for dropdowns).
   2. Template-relations `templateid` editable dropdown.
   3. Auto-create translation key on new `entitydefaults.definitionName`.
   4. Decompose `categoryflags` into hierarchy navigation.
   5. Bulk multi-row edits in entities grid.
   6. Genxy options-bag structured editor for `entitydefaults.options`.
   7. Robot template structured editor (head/chassis/leg + module slots + cargo).
