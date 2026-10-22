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


## Eventos (DS / BC / CC) — legado congelado (2026-10-19)

Os eventos vão ser refeitos do zero (regras de entrada, gameplay interno etc.) — ver `15-events-rework.md`.
Validado e mantido: entrada (janela J / NPCs, ticket no inventário) e os timers de abertura. O resto não recebe mais investimento.

- [x] Quit Game dentro do evento (validado 2026-10-19): o logout no evento faz warp pra cidade antes (S2CMapChanged); o client recarregava a cena e perdia o S2CLogout, e o timer de 2 s do `GameMenu` chamava `GetTree()` num nó já liberado. Agora o Logout entra no buffer da cena e o timer usa a árvore capturada.
- [x] Summoner: Drain Life / Chain Lightning sem dano (validado 2026-10-19): são `AreaSkillExplicitTarget` e iam como área no chão sem alvo. Agora não têm área no `S2CSkillList` (client mira na entidade) e o `SkillInput` manda pro `AreaSkillAttackAction` com o alvo explícito. Vale também para Dragon Roar / Phoenix Shot (RF).
- [~] BC: kill conta para o primeiro change event pendente válido (fix da estátua) — não retestado; descartável no rework.
- [~] `MonsterStrengthPercent` por mapa (eventos em 25%) — fica como ferramenta geral de balanceamento.

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
- [ ] **Assets dos outros mapas**: World1 (Lorencia), World2 (Dungeon), World3 (Devias), World4 (Noria), World5 (Lost Tower), World8 (Atlans) e World9 (Tarkan) existem no cliente; o resto ainda não — warp pra outro mapa mostra Lorencia com aviso. Converter com `MuAssetConverter --input <Data> --output <dir> --map WorldN` + `--obj EncTerrainN.obj` + `--bmd-dir ObjectN`.

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
- [x] **Joias de upgrade**: selecionar a joia e clicar no item (bolsa ou equipado) → `C2SUseItem` com `has_target_slot` (slot 0 = mão esquerda, então "sem alvo" virou flag); `ProtobufItemUpgradedPlugIn` reenvia o inventário. Bless, Soul, Life, Harmony e refine stones.
- [x] **Chaos Machine** (Chaos Goblin, Noria): `S2CChaosMachine` (itens + receita detectada pelo servidor com chance e preço — `ItemCraftAction.GetMixPreview`), `C2SChaosMix`, `C2SCloseNpcDialog` (devolve os itens ao inventário; recusa fechar sem espaço). Resultado via `S2CSystemMessage`. Cliente: `StorageWindow` base compartilhada com o baú. Corrigido: o save ao fechar o goblin nunca rodava (comparava `npc.Id` com o número 238).
- [ ] **Chaos Machine — polish**: animação/efeito do mix, abrir o inventário junto, outras NPCs de crafting (pet trainer, refinaria etc.) usam o mesmo caminho mas não foram testadas.
- [ ] **"Motor" de UI do cliente**: as janelas são montadas em código com posições fixas (offsets em pixels), então várias ficam fora da tela dependendo da resolução. Criar uma base de layout (âncoras/containers, escala por resolução, janelas arrastáveis que ficam dentro da tela) e suportar janela, borderless e fullscreen. Corrigidos pontualmente até agora: menu ESC, mensagem de morte, menu M e mensagens do sistema.
- [x] **Quests (fluxo legado, Sebina/Marlon/Devin)**: `S2CQuestDialog` (missões do NPC para a classe, estado, requisitos com progresso, recompensas) e `C2SQuestAction` (start/complete/cancel → `QuestStartAction`/`QuestCompletionAction`/`QuestCancelAction`). Recompensas avisam por mensagem e reenviam `S2CCharacterState`, que agora traz `class_number`/`class_name` (troca de classe: DK → BK). O conversor pula objetos com coordenadas inválidas (Devias tinha 2).
- [ ] **Quests — pendências**: as quests "novas" (grupo != 0, `QuestSelectAction`/`IShowAvailableQuestsPlugIn`) não têm fluxo Protobuf; sem rastreador de quest no HUD; outras classes ao redor não veem a troca de classe (aparência igual por enquanto). Drop dos itens de quest é 0,1% (padrão legado). 4 modelos de Devias (Object25/27/33/90) falham no export glTF ("joints") — não usados no mapa.
- [ ] **Valores de teste no banco (decidir os definitivos)**: mix de asa lvl 2 fixo em 80% (`SimpleCraftingSettings` da crafting 7: SuccessPercent=MaximumSuccessPercent=80, original 0/90 por valor dos itens) e drop dos itens de quest de classe em 5% (`DropItemGroup` 'Quest Item%', original 0,1%). Alterados direto no banco, não por update plugin.
- [ ] **Quest de 3ª classe** (Marlon/Devin, lvl 380/400) — fluxo é o mesmo já migrado, falta testar.
- [x] **Test dummies** (`/dummy punchbag|stun|clear`, GM): `DummyPlayer : OfflinePlayer` com comportamento roteirizado em vez da IA, contas test1..test9, ações pela fila de inputs (`ActionInput`). Chat Protobuf (`C2SChatMessage`/`S2CChatMessage`) com os comandos de chat do servidor. CC (stun/sleep/freeze) agora bloqueia o movimento contínuo.
- [x] **PvP novo** (substitui PK/herói): `GameMapDefinition.PvpMode` (Disabled/Flagged/Enabled; cidades PvE via update *Add Map PvP Modes*), flag por jogador (`C2SSetPvp`, desligar exige 30 s sem combate PvP), regra central `PvpRules.CanAttack` (safezone, party, duelo, minigame, mapa) aplicada no dano e nos alvos de área. Barras HP+SD de outros jogadores (`S2CEntityVitals` a cada ~200 ms se mudou), nome vermelho com PvP ligado. Teleporte em tile bloqueado pousa no tile livre mais próximo (até 3).
- [ ] **PvP — pendências**: efeitos sem dano (debuffs de skills) aplicados a jogadores não passam por `PvpRules`; `Player.AfterKilledPlayerAsync` (PK legado) ficou sem uso — remover junto com o resto do sistema PK/herói; recompensa/penalidade de morte PvP a definir; mapas 100% PvP a escolher.
- [x] **Buffs/debuffs visíveis + estado de controle**: efeitos de qualquer entidade (`S2CBuffApplied/Expired` + `EntityState.effects` ao entrar em vista); cliente mostra tags acima da cabeça e deriva o estado próprio (Slowed/Rooted/Stunned) que bloqueia input. Dummies `slow`/`root`/`stun` usam os efeitos reais (Iced/Freeze/Stunned). Root bloqueia teleporte e skills que levam até o alvo.
- [x] **Criação/exclusão de personagem** no cliente (servidor já tinha). Exclusão confirma com a senha da conta quando não há código de segurança.
- [x] **Party Protobuf**: convite (`C2SPartyInvite`, tecla P no player selecionado), resposta, kick/sair, lista com HP (`S2CPartyList`, reenviada quando um membro entra num mapa porque o id muda). Dummies aceitam sempre. Skills `Ally` (buffs da elf, Soul Barrier: clicar no player ou no chão = si mesmo) e `Self` (castam na hora — antes não castavam).
- [ ] **Passada de validação de skills por classe** (depois do loadout): testar todas as skills de DK, DW, Elf, MG, DL, Summoner e RF — dano, alvo (`SkillTargeting` do server), área, buffs/debuffs, cast/charge. Achados até agora: Summon e Increase Crit do DL **continuam sem funcionar** mesmo com o targeting do server (`SkillTargeting.Self`) — investigar o caminho do cast (client manda? `SkillInput` com alvo = o próprio player? plugin da skill). Electric Spike (DL, 65): o primeiro uso dá dano, os seguintes não — no MU original ela tem cooldown; verificar cooldown/consumo e o caminho de área.
- [ ] **Validar**: XP dividida na party; Innovation (redução de defesa) contra alvo com defesa alta (punchbag); Chaos Machine com inventário cheio.
- [ ] **Debuffs de skill em players** (Weakness/Innovation/Sleep da summoner) — confirmar se passam por `PvpRules` (efeitos sem dano podem não passar por `AttackByAsync`).
- [ ] **Visual da Summoner**: usa modelo/animação genérico feminino.
- [x] **Constantes do charge no banco**: `Skill.ChargeMinimumDamagePercent` (30), `ChargeHoldMilliseconds` (1500), `ChargeMovementSpeedPercent` (60) — migração com esses padrões; editáveis no admin panel.
- [x] **Movimento forçado em players**: `Player.MoveAsync` (puxar/empurrar/dash ao alvo) não atualizava a posição contínua — o movimento se perdia. Agora atualiza, encerra a caminhada e ignora CC (D-F7-05).
- [ ] **Loop de PvP (futuro, depende de facção/guild/skins)**:
  - *Open world*: rank por facção — matar alguém de outra facção dá pontos; depois loja da facção, ranking board e skins da temporada ao atingir requisitos.
  - *Arena instanciada*: matchmaking 1v1, 2v2, 3v3…; time vencedor ganha pontos, perdedor perde; ranking e as mesmas skins de temporada.
  - *Castle siege*: evento semanal de guilda; a guilda que estiver no castelo no fim vira dona (libera mapa de farm/baú de drops); 2º e 3º por pontos da guilda (kill na sala do castelo vale mais que fora); baús menores para 2º e 3º.
- [ ] **Skills no estilo MOBA**: dashes (RF até o inimigo), puxões (DK), knockback com direção, skills carregadas com direção — as do MU não foram desenhadas assim; seguir D-F7-05.
- [ ] **Cooldown persiste só em memória** (impacto mínimo): se virar abuso, persistir cooldowns ou bloquear logout em combate.
- [x] **Trade Protobuf**: pedido (`C2STradeRequest`, tecla T no player selecionado), resposta, zen, botão de confirmação, cancelar; itens vão à mesa por `C2SMoveItem` (storage 1). O servidor manda a mesa inteira (`S2CTradeState`) a cada mudança em vez dos deltas legados. `/dummy trade` aceita, põe 1.000.000 de zen e confirma quando o parceiro confirma (views próprias do dummy em `DummyViewPlugInContainer`).
