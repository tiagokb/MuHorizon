# Phase 8 — Entity 3D Model Rendering

## Context

GLBs already exported: 101 NPCs, 344 monsters, Object1/2 terrain objects (all from BMD via muconv).  
Client currently renders all entities as colored cylinder placeholders.  
Goal: replace placeholders with correct 3D models + animations, incrementally.

## Dual-path constraint

| Direction | Path | Status |
|---|---|---|
| Forward | BMD → GLB → Godot | Done (muconv) |
| Inverse | New art → BMD (for private servers) | Future tool — does not block this roadmap |

---

## Module 8.1 — Proto: EntityType enum  ✅ DONE

Added `MONSTER = 2` to the `EntityType` enum in `envelope.proto`.  
Server sets `EntityType.Monster` for `Monster` instances, `EntityType.Npc` for everything else.  
`class_number` carries `Definition.Number` (NPC/monster) or `CharacterClass.Number` (player) — no new field needed.

**Key discovery:** `Definition.Number` in openMU ≠ MU original TypeNumber for non-sequential monsters.  
`MONSTER_MODEL_*` enum in `_enum.h` is the authoritative source for model indices.

---

## Module 8.2 — Client: GLB spawn  ✅ DONE

Replace `CylinderMesh` placeholder with GLB instance per entity.

**Monster path:** `assets/models/Monster/Monster{modelIndex+1:D2}.glb`  
Model index comes from a static lookup table derived from `ZzzCharacter.cpp` switch cases —  
**not** from `ClassNumber + 1` (that formula is wrong for most monsters).

Known mapping (Lorencia):

| openMU ClassNumber | Name | GLB |
|---|---|---|
| 0 | Bull Fighter | Monster01 |
| 1 | Hound | Monster02 |
| 2 | Budge Dragon | Monster03 |
| 3 | Spider | Monster10 (MONSTER_MODEL_SPIDER = 9) |
| 4 | Elite Bull Fighter | Monster01 (same mesh as Bull Fighter) |
| 6 | Lich | Monster05 |
| 7 | Giant | Monster06 |
| 14 | Skeleton Warrior | cylinder fallback (uses player-skeleton system) |

**NPC path:** named by BMD stem — no numeric convention. Deferred to Module 8.6.  
**Player path:** Module 8.5.

Fallback to colored cylinder if GLB not found (silent — no log spam).

**Methodology note:** always read `ZzzCharacter.cpp` / `ZzzOpenData.cpp` before implementing  
any model/animation mapping. Numeric conventions in MU have hardcoded exceptions throughout.

---

## Module 8.3 — Animations: Idle + Walk  ✅ DONE

BMD actions export as `action_0`, `action_1`, `action_2` … (index only, from `BmdGltfExporter.cs`).  
Mapping from `_define.h`:

| Index | Constant | Use |
|---|---|---|
| 0 | MONSTER01_STOP1 | idle (looped) |
| 1 | MONSTER01_STOP2 | idle variant |
| 2 | MONSTER01_WALK | walk (looped) |
| 3 | MONSTER01_ATTACK1 | attack — Module 8.4 |
| 4 | MONSTER01_ATTACK2 | attack variant — Module 8.4 |
| 5 | MONSTER01_SHOCK | hit reaction — Module 8.4 |
| 6 | MONSTER01_DIE | death — Module 8.4 |

GLB animations have no loop flag — manually re-play via `AnimationFinished` signal.  
`PlaybackDefaultBlendTime = 0.15f` for cross-fade between states.

**Movement detection:** server sends tile positions (~400 ms per step via Walker).  
Client-side lerp (`LerpDuration = 0.35s`, SmoothStep) smooths discrete tile jumps.  
Walk → idle transition: 400 ms after last position change.

**Monster movement prerequisite:** `BroadcastSnapshotsAsync` in `GameTickLoop` now includes  
monsters whose tile `Position` changed since last tick (`_lastNpcPositions` dict).  
Walker runs outside the tick loop via `Task.Delay` — this is existing legacy behaviour.

### Known refinements (not blocking)

- **Rotation:** monsters don't face their movement direction. Requires `direction` field in  
  `EntitySnapshot` proto + server-side heading logic.
- **Per-monster lerp speed:** `LerpDuration` is fixed at 350 ms; each monster type has its own  
  `MoveDelay` (e.g. Spider = 1800 ms). Could send `move_delay_ms` in `EntityState` and cache  
  per entity to calibrate.
- **Terrain Y during lerp:** Y (terrain height) is sampled only at snapshot arrival. On slopes  
  the lerp interpolates Y linearly instead of following the heightmap — minor visual artifact.

---

## Module 8.4 — Combat Animations  ✅ DONE

- `S2CSkillAnimation` → play `action_3` (ATTACK1); return to idle after animation ends
- `S2CDamageResult` → play `action_5` (SHOCK / hit reaction) on target
- `S2CEntityDied` → play `action_6` (DIE), remove node after animation ends

Priority system (AnimState enum): Die > Shock > Attack > Walk > Idle.  
`AnimationFinished` never names animations — any non-idle/walk one-shot returns to idle.  
`FlashEntity` fixed to walk the GLB subtree for `MeshInstance3D` (no longer cylinder-only).

**Deliverable:** combat visually readable, death properly animated.

---

## Module 8.5 — Player Model  ✅ DONE

Same GLB spawn pipeline as 8.2, driven by `class_number`.  
Local player and remote players handled identically.  
No equipment rendering in this phase.

**Source:** `ZzzCharacter.cpp` — `IsFemale(class)` determines male/female model.

| openMU ClassNumber | GLB used |
|---|---|
| 0,2,3 (Dark Wizard tree) | `NPC/Man01.glb` |
| 4,6,7 (Dark Knight tree) | `NPC/Man01.glb` |
| 8,10,11 (Fairy Elf tree) | `NPC/Female01.glb` |
| 12,13 (Magic Gladiator tree) | `NPC/Man01.glb` |
| 16,17 (Dark Lord tree) | `NPC/Man01.glb` |
| 20,22,23 (Summoner tree) | `NPC/Female01.glb` |
| 24,25 (Rage Fighter tree) | `NPC/Man01.glb` |

**Animation indices for player GLBs** (Man01/Female01 have 2 actions):
- `action_0` = PLAYER_STOP_MALE / PLAYER_STOP_FEMALE → idle (looped)
- `action_1` = PLAYER_WALK_MALE / PLAYER_WALK_FEMALE → walk (looped)

Walk uses `action_1` for player models vs `action_2` for monster models.  
The animation system detects which is available via `HasAnimation()`.

**Own player:** loaded at `_Ready()` from `GameState.Characters` class number.  
`_ownPlayer` changed from `MeshInstance3D` to `Node3D`; GLB attached as "Mesh" child.

**Deliverable:** player has class-correct 3D model with idle/walk animations.

### Known limitations (refinements)

- **No equipment:** player appears as base NPC body (Man01/Female01), not class-appropriate armor.  
  Proper rendering requires loading equipment piece GLBs and attaching to skeleton bones (Phase 9).
- **Single male/female model per gender:** all wizard variations look identical to all knight variations.  
  True class differentiation comes from equipped items.
- **Flash on hit doesn't work for GLB player models** (same limitation as remote GLB entities).

---

## Module 8.6 — Player Attack Animations  ✅ DONE

Player attack animations require both a server fix and a client hook.

**Server:** `BasicAttackInput` was not calling `ShowAnimationAsync` on the attacker.  
Fix: call `await player.InvokeViewPlugInAsync<IShowAnimationPlugIn>(...)` inside  
`PlayerAttackAction` after the attack resolves.  
Also fixed: `Direction` was not being set on `C2SCastSkill`, causing `SkillInput` to  
receive `Direction = 0` — players always faced East regardless of click target.

**Client:** player attack animations use `action_0` through `action_N` from their GLB.  
The player model (Man01/Female01) has a dedicated attack track; class-number determines  
which offset to use (same `EntityConfig.PlayerAttackAnim(classNumber)` lookup).

**Deliverable:** player visibly attacks when hitting enemies; faces the correct direction.

---

## NPC Model Name (deferred)

NPC GLBs are named by BMD stem (e.g. `Breeder.glb`, `ElfWizard01.glb`) — no numeric convention.  
Current approach: client-side lookup table `Definition.Number → stem` in `EntityConfig.NpcAssets`.  
This is maintainable for the known NPC roster; extend as new NPCs are added.

---

## Phase 9 — Client Modularization  ✅ DONE (2026-08-28)

WorldScreen reduced from 1094 → 78 lines. Extracted modules:

| Module | Script | Phase |
|---|---|---|
| EntityAnimator | `scripts/EntityAnimator.cs` | 9.1 |
| EntityVisual + EntityHpBar | `scripts/EntityVisual.cs`, `EntityHpBar.cs` | 9.2 |
| EntityRegistry | `scripts/EntityRegistry.cs` | 9.3 |
| CombatSystem | `scripts/CombatSystem.cs` | 9.4 |
| WorldCamera + WorldMap + InputController + OwnPlayer | respective scripts | 9.5 |
| MessageRouter | `scripts/MessageRouter.cs` | 9.6 |
| EntityConfig SkillInfo/TargetingType + InteractionType routing | `scripts/EntityConfig.cs` | 9.7 |

See `MuHorizonClient/docs/architecture.md` for full architecture reference.

---

## Phase 10 — Character State Pipeline  ✅ DONE (2026-08-28)

Full character data on world entry and real-time updates. See `character-data-pipeline.md`.

---

## Future — Equipment Rendering

- Server adds visible item slots to `EntityState` (weapon, armor set, wing)
- Client attaches equipment GLBs to skeleton bone sockets per slot
- Requires bone socket mapping per character class
- Scope: significant — left for a dedicated phase

---

## Future — Performance & Polish

- LOD: beyond N tiles, swap GLB for billboard or low-poly mesh
- Particle effects: skill hit, death, spawn burst
- Shadow LOD: disable shadow casting for distant entities
- Occlusion culling for dense areas

---

## Inverse Path — Asset Export for Private Servers (future, separate track)

Goal: author new art (skins, monsters, effects) and export it back to `.bmd` so it works in the original MU client used by private servers.

- **`BmdWriter.cs`** in muconv: writes `.bmd` from an intermediate representation
- Input: GLB from Blender export (or any DCC tool via intermediate)
- Optional Blender addon for direct BMD authoring

This track is fully independent. Modules 8.1–8.5 don't need to wait for it.
