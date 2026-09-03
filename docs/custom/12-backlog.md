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
- [x] **Walkability client-side** — `walkable.png` (256×256) carregada no `WorldMap`; `IsWalkable(serverX,serverY)` filtra `C2SMove` antes de enviar; `MuAssetConverter` corrigido para preferir `Terrain.att` sobre `EncTerrain*.att`
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
- [x] Usar itens — right-click em item do inventário envia `C2SUseItem`; potions recuperam HP/MP; scrolls aprendem skills; durabilidade atualizada via `S2CItemAppeared`
- [x] Stack count — badge amarelo no canto de itens stackable (poções, joias); `ItemData.durability` + `ItemDefinitionData.is_stackable`
- [x] Buff da Elfa — interação com NPC #257 (Elf Soldier) aplica `ElfSoldierBuff` (def +50+lvl/5, dmg +45+lvl/3, 60min); `S2CBuffApplied`/`S2CBuffExpired` → `ActiveBuffIds`

---

## Combat Visual Feedback (targeting / range indicators)

- [x] **Range ring (A-click)** — ao pressionar A, exibir círculo no chão em torno do player com raio = `AttackRange` do personagem; some ao soltar A ou ao clicar (`RangeIndicatorManager.ShowAttackRing`)
- [x] **Skill range indicator** — ao selecionar skill na skill bar (Q-F), mostrar o indicador adequado ao tipo da skill:
  - **AoE ground-target** (`RangeWorld > 0`): anel no cursor com raio = `SkillInfo.RangeWorld`
  - **Destination ground-target** (Teleport — `RangeWorld == 0`): anel ao redor do player com raio = `CastRangeWorld`
  - **Targeted**: anel ao redor do player com raio = `CastRangeWorld` (padrão 48f = 6 tiles)
  - **Line / skillshot** (projétil, cone): retângulo rotacionado na direção cursor→player
  - **Self / instant**: sem indicador
- [x] **Snap de alvo para skills targeted**: se o ray miss o hitbox do mob, busca o mob mais próximo do ponto no chão em raio ≤ 8 world units (1 tile); skills GroundTarget não fazem snap (clique intencional no chão)
- [x] **Cooldown overlay no HUD**: slot escurece + exibe segundos restantes enquanto em cooldown
- [x] **Cancela indicador** ao pressionar ESC, soltar A, trocar de skill ou clicar (`CancelSkillCharging`/`GoIdle`)
- [x] **Cor do indicador**: azul fixo (`ColBlue` em `RangeIndicatorManager`)

---

## Segurança / Anti-cheat (dívida crítica — implementar antes de qualquer sessão multiplayer real)

- [ ] **CRÍTICO — Server-side attack speed**: o rate-limit em `ProtobufPacketDispatcher` usa timer hardcoded (100ms); deve usar `Stats.AttackSpeed` do player para rejeitar rafagas acima do permitido
- [ ] **CRÍTICO — Server-side skill range validation**: `C2SCastSkill` e `C2STeleport` não verificam distância no servidor; um cliente modificado pode castá-los de qualquer lugar do mapa
- [ ] **CRÍTICO — Server-side interaction range**: `C2SInteractNpc` não verifica proximidade no servidor; cliente calcula e enforça, mas o servidor aceita qualquer ID
- [ ] **Documentação**: todas as validações acima são atualmente client-authoritative por herança do protocolo legado do MU Online

## Tools

- [ ] Pipeline inverso Blender → BMD: `BmdWriter` + `GltfBmdImporter` (ver seção I no `09-implementation-plan.md`)
