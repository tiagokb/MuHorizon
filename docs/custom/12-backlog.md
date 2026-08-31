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

- [x] **R-01** — Rotação de monstros: client deriva `Rotation.Y` do delta de posição (Atan2) no lerp
- [x] **R-02** — Lerp speed por monstro: `move_delay_ms` em `EntityState`; client usa como `LerpDuration`
- [x] **R-03** — Terrain Y durante lerp: amostrar heightmap a cada frame no XZ interpolado
- [x] **R-04** — Animações cortadas: já resolvido pelo `AnimState` priority system no `EntityAnimator`
- [ ] **R-05** — Pop ao mob re-entrar no AoI: adiado (provavelmente resolvido pelo fog of war)
- [ ] **Fog of War** — shader de distância XZ centrado no player (~13 tiles AoI); fade-in/out nas bordas; decisões: cor, afeta terreno ou só entidades

---

## Gameplay

- [x] Character sheet (base stats + stat points — `CharacterSheet.cs`, tecla C)
- [x] Lista de skills do personagem (`S2CSkillList` → `GameState.LearnedSkills` → `SkillWindow` [U])
- [x] NPC interaction: loja (C2SInteractNpc → S2CShopItems → C2SBuyNpcItem)
- [x] Baú (Vault) — grid 8×15 com scroll, moves cross-janela inventory↔vault, `S2CVaultOpened` / `C2SCloseVault`
- [x] Shop grid — loja NPC como grid real (8 cols, tamanhos reais, ItemTooltip com preço de compra)
- [x] Venda a NPC — mover item do inventário pro shop vende; `S2CSellPriceList` mostra preço de venda no tooltip; confirmação server-side para itens valiosos (≥50K zen ou Exc/Ancient)

---

## Tools

- [ ] Pipeline inverso Blender → BMD: `BmdWriter` + `GltfBmdImporter` (ver seção I no `09-implementation-plan.md`)
