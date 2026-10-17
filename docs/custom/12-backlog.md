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

- [x] **Server-side attack speed** (2026-10-17): ataque básico e projéteis agora passam pelo `ISpeedHackCheatCheckPlugIn` (intervalo mínimo derivado de `Stats.AttackSpeed`, como skills e áreas já faziam). O servidor envia o intervalo ao cliente (`S2CAttackInterval`) e o cliente pacing por ele — evita falso positivo/AutoBan em personagens rápidos. O rate-limit de 100ms no dispatcher ficou só como proteção da fila.
- [x] **Server-side skill range validation** (2026-10-17): `SkillCastRules` (GameLogic/Inputs) — só skills aprendidas (removido o fallback para a configuração global em `SkillInput` e `TargetedSkillDefaultPlugin`), alcance `Skill.Range + 2` para alvo, projétil, área (`AreaSkillInput`) e teleport (`TeleportInput`, agora na `InputQueue`, com custo de mana e sem teleport duplo). `S2CSkillList` envia o `range` e o cliente usa esse valor no indicador e no clamp.
- [x] **Server-side interaction range**: já estava validado (`IsWithinInteractionRange`, 4 tiles) para NPC e pickup.
- [x] **Cooldown de skills no servidor**: `Skill.CooldownMilliseconds` (migração `AddCooldownToSkill`, update *Add Skill Cooldowns*), aplicado em `Player.TryConsumeForSkillAsync` (todo cast passa por lá antes do efeito). `S2CSkillCooldown` informa o cliente no cast aceito e na rejeição por cooldown; o cliente só trava o slot de verdade quando o servidor confirma.
- [ ] **Cooldown persiste só em memória**: reconectar zera os cooldowns.
- [x] **Mecânica de cast** (`CastController`, tick loop): `Skill.CastType` Charge (segura/solta, anda com slow 0.6, dano 30%→100%, segura 1.5 s no máximo e solta sozinha) e CastTime (enraíza, andar cancela). Cancelar/CC = sem efeito + cooldown. Nova virou Charge (plug-ins legados removidos); Cometfall, Inferno e Hellfire têm cast time. Update *Add Skill Casting*.
- [ ] **Cast bar de outros jogadores**: o servidor já manda `S2CCastStarted/Ended` pros observers; o cliente só mostra a barra do próprio jogador.
- [ ] **Constantes do charge no banco**: fator mínimo, slow e tempo de segurar estão em `CastController` (P3 pede DB se precisar variar por skill).
- [ ] **Charge com mira**: hoje o release do Charge é sempre burst em volta do caster (Nova); skills carregadas com direção (estilo Varus Q) precisam do ponto no release.
- [ ] **Balancear Nova**: mana agora é consumida uma vez só (15, era por estágio) e o dano usa o bônus do último estágio legado × fator da carga.
- [ ] **Posição em tile fora de sincronia** (parte 2 resolvida: troca de mapa no fluxo Protobuf — `S2CMapChanged` → cliente recarrega a cena → `C2SMapLoaded` → `ClientReadyAfterMapChangeAsync`; falta a parte 1, bloquear movimento contínuo em tile não andável): o movimento contínuo (`GameTickLoop`) não checa `WalkMap`, então o player pode parar em tile não andável. No login seguinte `ClientReadyAfterMapChangeAsync` chama `WarpToSafezoneAsync`, que deixa `CurrentMap = null` esperando o ack de troca de mapa (F3 12) que o cliente Protobuf nunca manda — o player fica fora do mapa (não ataca, não é visto) com a `ContinuousPosition` antiga. Precisa: (1) bloquear movimento contínuo em tile não andável; (2) warp/troca de mapa no fluxo Protobuf.
- [x] **Troca de mapa / warp**: `ProtobufMapChangePlugIn` + `C2SMapLoaded`; gates detectados pelo servidor no tick (`EnterGateInput`, ao entrar no tile do gate); lista de warp do servidor (`M`, `C2SGetWarpList`/`C2SWarp` → `WarpAction`, checa nível e zen); `S2CSystemMessage` (`IShowMessagePlugIn`). O cliente abre no mapa do personagem (antes sempre World1).
- [x] **Noria (World4)** convertida (terreno, walkable, 9399 objetos, 43 modelos Object4). `Object44` não existe nos dados legados (provável efeito/objeto invisível) — fica pulado.
- [ ] **Assets dos outros mapas**: só World1 (Lorencia), World2 (Dungeon) e World4 (Noria) existem no cliente; warp pra outro mapa mostra Lorencia com aviso. Converter com `MuAssetConverter --input <Data> --output <dir> --map WorldN` + `--obj EncTerrainN.obj` + `--bmd-dir ObjectN`.

## Ambiente de dev rápido (prioridade — restart do servidor domina o tempo de desenvolvimento)

Medido em 2026-10-17: restart do `openmu-dev` (Docker) ≈ **4 min 50 s** = restore 9 s + **compilação 4 min 17 s** + host 25 s. Build nativo no Windows: 2 min 56 s com 1 arquivo da GameLogic alterado; 1 min 45 s sem analyzers; 1 min 06 s com `-p:ci=true -p:RunAnalyzers=false`; **build sem mudança nenhuma: 1 min 02 s** → o custo é overhead, não compilação.

Causas: repos dentro do OneDrive (filtro em todo acesso a arquivo; no Docker ainda passa pela montagem do WSL2); grafo do `Startup` com ~30 projetos incluindo Blazor (static web assets reprocessados todo build); fora de `ci=true` rodam em todo build `npx xslt3` (docs de pacotes legados, 4×), `dotnet run` do gerador de modelo da Persistence (2×, reescreve `.Generated.cs` e força recompilação) e `sass`; StyleCop (+70 s).

- [x] **1. Servidor nativo + `dotnet watch` (hot reload)** — `./dev-server.ps1`: só o Postgres no Docker; servidor no host com `dotnet watch`; `appsettings.Development.json` põe o WebSocket em `localhost` (`GameServer:WebSocketHost`, sem admin/urlacl). Medido: edição em corpo de método aplicada em **0,25–1,8 s** sem restart (antes ~5 min). Primeiro start ~2 min (build + 15 s carregando projetos + 27 s host).
- [x] **2. Repos fora do OneDrive** — movidos para `C:\dev\` (cópias verificadas por contagem de arquivos e `git fsck`; originais mantidos em `OneDrive\Documentos\repos` até confirmação). Build vazio caiu só de 62 s para 54 s: o OneDrive não era a causa principal do build nativo. Exclusão do Windows Defender para `C:\dev` ainda é recomendada (ação do usuário).
- [x] **3. Perfil de build de dev** — `dev-server.ps1` usa `ci=true` + `RunAnalyzers=false`; `./regen-proto.ps1` regenera `Envelope.cs` após editar `.proto` (o watch reinicia sozinho).
- [ ] **4. Build de dev sem o admin panel** — adiado: `ServerContainerBase`, `GameServerContainer` e `ConnectServerContainer` dependem de `Web.AdminPanel.Services`, e o admin panel é o único editor de configuração (inclusive updates). Exige mover esses serviços para outro projeto; ganho estimado ~20 s só em restarts.
- [ ] **5. Inicialização de 27 s** — medido: ~20 s carregando a `GameConfiguration` do Postgres (grafo inteiro via EF) e ~6 s inicializando os 3 game servers/mapas. Caminhos: modelo compilado do EF (`dotnet ef dbcontext optimize`), cache da configuração, ou subir só 1 game server em dev.
- [ ] **Restart do watch (edição estrutural)** ainda paga ~1 min de overhead do MSBuild (varredura transitiva de ~24 projetos + static web assets do Blazor) + 27 s de host.

## Tools

- [ ] Pipeline inverso Blender → BMD: `BmdWriter` + `GltfBmdImporter` (ver seção I no `09-implementation-plan.md`)
- [ ] **Git tags por fase** — criar tags retroativas nos dois repos (`openMU` + `MuHorizonClient`) para cada fase concluída, facilitando `git checkout fase-X` para gravar vídeos de progresso sem risco de perder estado atual
