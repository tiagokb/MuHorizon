# Backlog

Itens pendentes em ordem de prioridade prática. Marcar como `[x]` ao concluir.

---

## HUD

- [x] HUD estilo MU Online: `MuHud.tscn` + `MuHud.cs` (orbs HP/MP, skill bar, stats, zen, XP bar)
- [x] Barras de SD / AG no StatsRow (dados já em `GameState.OwnCurrentSd/Ag`)
- [x] Zen em tempo real: `ProtobufUpdateMoneyPlugIn` no backend → `S2CMoneyUpdate` (field 28) → `MessageRouter`
- [x] Float de EXP ao matar mob (label dourada sobe e some, posição do mob ou fallback OwnPlayer)
- [x] Efeito visual de level-up (flash dourado + "LEVEL UP! Lv.X" centralizado, 2.8s)

---

## Inventário

- [x] Grid inventory estilo MU/POE: itens com width×height reais, posicionados no canvas 8×8
- [x] Nomes reais de itens: `S2CItemDefinitions` enviado no world entry → `ItemDatabase` singleton no client
- [x] Equipar / desequipar itens (clique no slot de equip / bag)
- [x] Tooltips de item com stats (nome, req. stats, classes, flags, tamanho)

---

## Entity Rendering (refinements)

- [ ] **R-01** — Rotação de monstros: adicionar `direction` ao `EntitySnapshot` no proto; client lerpa `Rotation.Y`
- [ ] **R-02** — Lerp speed por monstro: adicionar `move_delay_ms` ao `EntityState`; client usa como `LerpDuration`
- [ ] **R-03** — Terrain Y durante lerp: amostrar heightmap a cada frame no XZ interpolado
- [ ] **R-04** — Animações cortadas: ignorar animação de prioridade menor se a atual ainda não terminou
- [ ] **R-05** — Pop ao mob re-entrar no AoI: iniciar lerp suave no spawn (baixa prioridade)

---

## Gameplay

- [ ] Character sheet (base stats — dados já em `GameState`)
- [ ] Lista de skills do personagem (pipeline S2C não implementado)
- [ ] NPC interaction: diálogo + loja

---

## Tools

- [ ] Pipeline inverso Blender → BMD: `BmdWriter` + `GltfBmdImporter` (ver seção I no `09-implementation-plan.md`)
