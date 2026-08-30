# Refinements Backlog

Known polish items that don't block feature completion but matter for feel and correctness.  
Each entry documents: what's wrong, root cause, and the implementation path.

---

## R-01 — Monster rotation / heading direction

**Status:** resolved (2026-08-29)  
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

**Status:** resolved (2026-08-29)  
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

**Status:** resolved (2026-08-29)  
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

**Status:** resolved (2026-08-29) — AnimState priority system in EntityAnimator already handles this.  
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

## R-08 — Scroll bleed-through ao atingir o fim da lista na loja

**Status:** pendente  
**Affects:** NpcShopWindow (Phase 13)

### Problem
Quando o `ScrollContainer` da loja chega ao limite de scroll (topo ou fundo da lista),
eventos de scroll adicionais não são consumidos pelo UI e vazam para o `WorldCamera`,
acionando o zoom da câmera.

### Root cause
Godot 4: `ScrollContainer` só marca o evento como handled enquanto consegue scrollar.
No limite, o evento cai em `_UnhandledInput` do `WorldCamera`.

### Implementation path
Em `NpcShopWindow`, sobrescrever `_UnhandledInput` e consumir eventos de scroll do mouse
enquanto a janela está visível e o cursor está sobre ela:

```csharp
public override void _UnhandledInput(InputEvent @event)
{
    if (!Visible) return;
    if (@event is not InputEventMouseButton mb) return;
    if (mb.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown)
        GetViewport().SetInputAsHandled();
}
```

Isso garante que qualquer scroll sobre a janela (mesmo no limite da lista) nunca
chega ao zoom da câmera.

---

## R-05 — Lerp state persistence across AoI re-entry

**Status:** deferred — pop is negligible at AoI boundary crossings  
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

**Status:** resolved (2026-08-28)

---

## R-07 — Shock animation blocked when defender is mid-attack

**Status:** resolved (2026-08-28)
