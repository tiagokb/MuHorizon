# Refinements Backlog

Known polish items that don't block feature completion but matter for feel and correctness.  
Each entry documents: what's wrong, root cause, and the implementation path.

---

## R-01 — Monster rotation / heading direction

**Status:** not started  
**Affects:** Module 8.3 (walk animation)

### Problem
Monsters slide toward their target without rotating to face the direction of movement.

### Root cause
`EntitySnapshot` only carries `(id, x, y)`. No heading field exists.  
Server-side: `Monster.Position` is a tile `Point` — direction is implicit in the walker step but never exposed.

### Implementation path

**Server** — add `direction` to `EntitySnapshot` in `envelope.proto`:
```protobuf
message EntitySnapshot {
    uint32 id        = 1;
    float  x         = 2;
    float  y         = 3;
    uint32 direction = 4; // 0–7, same as MU direction enum (0=W, 1=SW, …)
}
```

Populate in `BroadcastSnapshotsAsync` for monsters: derive from `(lastPos → currentPos)` delta,  
or expose `Monster._walker` current step direction.

**Client** — on snapshot receive, lerp `node.Rotation.Y` toward the new heading over the same `LerpDuration`.

---

## R-02 — Per-monster lerp speed calibration

**Status:** not started  
**Affects:** Module 8.3 (walk animation)

### Problem
Client uses a fixed `LerpDuration = 0.35s` for all monsters.  
Slow monsters (e.g. Spider: `MoveDelay = 1800ms`) look like they're sprinting.  
Fast monsters overshoot and snap back.

### Root cause
`MoveDelay` is defined per `MonsterDefinition` in the DB but never sent to the client.

### Implementation path

**Option A — send in `EntityState`:**
```protobuf
message EntityState {
    // … existing fields …
    uint32 move_delay_ms = 10; // from MonsterDefinition.MoveDelay
}
```
Client caches `move_delay_ms` per entity ID and uses it as `LerpDuration` in `AdvanceEntityLerps`.

**Option B — derive from observed velocity:**
On each snapshot, compute `distance / timeSinceLastSnapshot`. Use a rolling average as the lerp speed.  
No proto change, but takes 2–3 snapshots to converge and jitters on direction changes.

Recommendation: Option A — clean and exact.

---

## R-03 — Terrain Y interpolation during entity lerp

**Status:** not started  
**Affects:** Module 8.3 (walk animation)

### Problem
On slopes, entities lerp their Y position linearly between the two terrain-sampled heights  
instead of following the heightmap surface. Looks like they float or sink mid-step.

### Root cause
`GetTerrainY` is called only at snapshot arrival. During the lerp, Y is interpolated between  
`From.Y` and `To.Y` linearly (`From.Lerp(To, t)`), not sampled from the heightmap each frame.

### Implementation path

In `AdvanceEntityLerps`, sample the terrain Y at the current interpolated XZ each frame:

```csharp
var xz   = new Vector3(s.From.X, 0f, s.From.Z).Lerp(new Vector3(s.To.X, 0f, s.To.Z), t);
var y    = GetTerrainY(xz.X, xz.Z);
node.Position = new Vector3(xz.X, y, xz.Z);
```

**Cost:** one physics raycast per moving entity per frame (same as player movement already does).  
Acceptable for the entity counts expected in a single map instance.

---

## R-04 — Animation timing mismatch on fast actions

**Status:** not started  
**Affects:** Module 8.4 (combat animations, future)

### Problem
Attack and hit animations have fixed durations in the BMD. If the server fires another  
`S2CSkillAnimation` before the previous attack animation finishes, the client drops it or cuts in abruptly.

### Root cause
`AnimationPlayer.Play()` with `PlaybackDefaultBlendTime` will cross-fade mid-animation.  
No queuing or priority system exists yet.

### Implementation path
Track per-entity "animation locked until" timestamp. Ignore lower-priority anim requests  
(idle, walk) while a non-interruptible anim (attack, die) is playing.  
Death (`action_6`) is highest priority — once started, nothing can interrupt it.

Simple state enum per entity:
```
Idle → Walk → Idle   (interruptible anytime)
Any  → Attack        (plays once, returns to Idle/Walk)
Any  → Hit           (short, plays once, returns)
Any  → Die           (terminal — no return, node removed after anim)
```

---

## R-05 — Lerp state persistence across AoI re-entry

**Status:** not started  
**Affects:** Module 8.3 (walk animation)

### Problem
If a monster leaves and re-enters a player's AoI mid-walk, it spawns at its current tile position  
(from `S2CEntitiesInScope`) with no lerp — appears at the correct place but with a visible pop  
if it was mid-step when it re-entered.

### Root cause
`HandleEntitiesInScope` sets the entity's initial position directly (`worldPos` from the scope message).  
No lerp is started for the initial placement.

### Implementation path
Minor: on spawn, start a lerp from a position slightly behind the entity (extrapolated from  
`S2CEntitiesInScope` data alone — not reliable without direction).  
Or: accept the pop as negligible — it only occurs at AoI boundary crossings which are uncommon.

Low priority.

---

## R-06 — Walk animation wrong for player model placeholder

**Status:** attempted fix (2026-08-23) — still not fully resolved  
**Affects:** Module 8.5 (player model)

### Problem
Wizard01 (used as male player placeholder) has 3 animations. The generic walk-detection heuristic
`HasAnimation("action_2") ? "action_2" : "action_1"` picks action_2 for Wizard01, which is a casting
animation, not walk. The player plays the casting animation while walking.

### Root cause
Heuristic assumes any model with action_2 uses it for walk (true for monsters: idle1/idle2/walk).
Wizard01 and other NPC models follow a different convention.

### Attempted fix (2026-08-23)
Added `WalkAnim string` field to `EntityAnimState`. Set to `"action_2"` for monsters and `"action_1"`
for players/NPCs at spawn time. All walk-detection sites now use `state.WalkAnim`.
Requires verifying what action index Wizard01 actually uses for walk — check BMD animation names
in muconv or inspect GLB in Godot animation tree.

### Proper fix
1. Open `NPC/Wizard01.glb` in Godot editor and inspect the AnimationPlayer actions to identify
   the walk animation index.
2. If needed, rename in the GLB or adjust the `WalkAnim` value used for NPCs.
3. Long-term: when proper player GLBs are composed (Phase 9), they will use standard action indices.

---

## R-07 — Shock animation blocked when defender is mid-attack

**Status:** attempted fix (2026-08-23) — still not fully resolved  
**Affects:** Module 8.4 (combat animations)

### Problem
When a monster attacks the player, the monster plays action_3 (attack) with `AnimPriority.Attack`.
If the player then hits the monster during that window, `PlayCombatAnim` tries to play action_5
(shock) with `AnimPriority.Shock`. Because Shock was previously lower priority than Attack, the
shock was silently discarded. The monster shows no hit reaction while its own attack was playing.

### Root cause
Original `AnimPriority` order: `Idle=0, Walk=1, Shock=2, Attack=3, Die=4`.  
In MU Online, taking damage interrupts the attacker's swing. The priority ordering was inverted
for the defender case.

### Attempted fix (2026-08-23)
Reordered to `Idle=0, Walk=1, Attack=2, Shock=3, Die=4` so Shock outranks Attack.  
User reports the issue persists — the shock might not be triggered at all (S2CSkillAnimation
may arrive with `TargetId=0` for certain skill types, or the TargetId doesn't match the entity
ID stored in `_entityAnims`).

### Investigation path
Add a debug log in `HandleSkillAnimation` printing `AttackerId` and `TargetId` to verify the
message is arriving with the correct target. Cross-check that the monster's entity ID in
`_entityAnims` matches `msg.TargetId`.
