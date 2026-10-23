# 16 — Plano de design de jogo e implementação (pós-legado)

Status: **decidido em linhas gerais** (2026-10-19). Triagem do que falta do legado + direção de design dos sistemas novos.
Regra geral: o código legado só é aproveitado quando **não limita** mudar o design depois. Na dúvida,
modelo de dados novo e o legado vira referência ("migra e remove").

## 1. Triagem do legado

| Bloco | Decisão | Por quê |
|---|---|---|
| Guild (membros, cargos, aliança) | **Migrar básico** | Estrutura serve; o que muda é o conteúdo e a premiação (aditivo) |
| Amigos / cartas / chat privado | **Migrar básico** | Social básico, sem mudança de design |
| Duel (salas, espectadores) | **Remover** → *Desafio* (novo, §3) | Regras novas: party vs party, sem recompensa |
| Loja pessoal | **Remover** → *Mercado* (novo, §4) | Modelo diferente: anuncia e sai, não fica com a loja aberta |
| MU Helper / offline | **Remover** → auto-play novo (futuro) | O helper depende da mecânica antiga; refazer quando o combate novo estiver fechado |
| Master level | **Aproveitar o motor, trocar os dados** (§5) | O modelo já é uma árvore de talentos genérica |
| Quests | **Modelo novo** (§6) | O atual só conhece "matar N" / "ter item", crédito só pra quem matou, numeração legada |
| Pets / invocações / montarias | **Sistema novo** (§7) | Corvo é código fixo por pet e comanda por skill; invocações não têm comportamento |
| Diversos do mundo (clima, invasões, efeitos) | Migrar sob demanda | Pequenos, sem decisão de design |

## 2. Skills: tipos e loadout (núcleo do estilo MOBA)

- Cada skill ganha um **tipo de slot**: `Basic` (Q, W, E), `Ultimate` (R), `Utility` (D, F) e `Passive`.
- O personagem pode **conhecer N skills**, mas só **equipa** uma por slot: 3 básicas, 1 ultimate, 2 utility, mais as passivas.
- **Loadout travado em combate**: só troca fora de combate. Usa o `LastPvpCombatUtc` / estado de combate que já existe e estende pra PvE.
- Server valida o cast: só skills equipadas no loadout podem ser usadas.
- Dados: `Skill.SlotType` no DB (P3); loadout salvo por personagem; protocolo `C2SSetLoadout` / `S2CLoadout`.
- **Em aberto**: quantas passivas ativas por vez? Ultimate tem requisito de nível?

## 3. Desafio (substitui o duel)

- Um jogador desafia outro. Se os dois estão em party, é **party vs party** (1v1 até 5v5, limite = tamanho da party). Sem party, é 1v1.
- **Instância separada**: mapa pequeno só para a luta (menos carga no servidor e no cliente, sem interferência do mundo).
- **Morrer só tira da luta** (não é morte de verdade: sem perda, volta com a vida cheia).
- **Rounds**: quem morre sai do round; o round acaba quando um time inteiro morreu — vence o time que sobreviveu (ou o último a morrer). O formato ranqueado usa **melhor de 3**.
- **Sem recompensa e sem penalidade**: serve pra testar mecânica e build, ou por rixa.
- Reaproveita `PvpRules` (exceção de duelo/minigame), a party e o `MiniGameContext` como base de instância (mapa próprio, entrada, fim).
- **Arena ranqueada** (fila + ranking, 1v1/2v2/3v3, melhor de 3) usa o mesmo motor de rounds — vem depois do desafio.
- **Battleground** (5v5, 10v10, capturar bandeira, dominar área, estilo WoW) — **pós-MVP**.

## 4. Mercado (substitui a loja pessoal)

- O jogador **anuncia** o item com preço e pode sair do jogo. Quando vende, o dinheiro fica para **resgatar**. Pode cancelar e resgatar o item não vendido.
- Fluxo: anunciar → listado → vendido (zen a resgatar) | cancelado/expirado (item a resgatar).
- O item sai do inventário ao anunciar (fica em custódia, como um storage do mercado). Mesmo cuidado do trade: transação atômica no server.
- **Taxa para anunciar** (sink de zen, dá valor à moeda).
- **Acesso por NPC** no início. **Notificação** quando um item é vendido, para o jogador saber que tem zen para resgatar.
- Princípio de mundo: como o MU tem teleporte entre mapas, ter um sistema num mapa/NPC específico não pesa — dá liberdade de dar função a mapas (mercado, Chaos Machine etc.).
- **Em aberto**: prazo de expiração do anúncio; busca e filtros (tipo, nível, opções).

## 5. Especialização / árvore de talentos (motor do master level)

- O modelo atual (`MasterSkillDefinition`) já é genérico: árvores (`MasterSkillRoot`), rank, nível mín./máx., fórmula de valor, **atributo alvo + agregação** e "substitui skill X" (upgrade de skill).
- Isso cobre talento que dá stat e talento que melhora skill. **Manter o motor e trocar os dados** (árvores e valores novos).
- **Progressão**: ao chegar no nível máximo, o jogador faz a **quest de especialização** e passa a ganhar pontos de talento.
- Os pontos vêm de **qualquer conteúdo endgame**, não só de matar mob: arena, battleground, dungeons, mobs e o que mais existir. O "master exp" legado vira uma fonte entre várias.
- **Matchmaking por progressão** (quando houver jogadores suficientes): quem acabou de pegar a especialização não enfrenta/joga com o veterano. Sem jogadores suficientes, o caminho é ter **conteúdo de progressão solo** para não travar o novato nem atrapalhar o veterano.
- Ideia do legado que vale manter (o conceito, não o código): a 3ª classe ter **nível próprio** nos eventos (BC/DS/CC) — ver `15-events-rework.md`.
- Protocolo novo para a árvore (as views de master skill legadas não migram).
- **Em aberto**: reset de talentos (custo)? Quantas árvores por classe? Talento pode mudar o comportamento da skill (ex.: virar área), além de número?

## 6. Quests (modelo novo)

- **Crédito compartilhado na party**: um kill da party conta para todos os membros próximos que tenham a quest ativa, estejam ou não "fazendo junto". Recompensas individuais.
- Objetivos genéricos por etapa: matar monstro X, coletar item, falar com NPC, chegar num lugar, usar item… Lista extensível.
- Quests solo e em party usam o mesmo sistema; a party só muda quem recebe o crédito.
- Reaproveitar: estado por personagem (`CharacterQuestState`) como ideia, e o gancho de kill (`QuestMonsterKillCountPlugIn`) como ponto de entrada (estender para os membros da party).
- As quests de mudança de classe já funcionam; migram para o modelo novo quando ele existir.
- **Em aberto**: rastreador de quest no HUD; quests diárias/semanais; cadeias (uma libera a outra).

## 7. Companheiros: pets, invocações e montarias

Princípio: o código não pode amarrar o design — skill, passiva ou status de cada companheiro é **balanceamento**, decidido depois. O que importa agora é a estrutura permitir qualquer uma dessas formas.

- **Pets que atacam** (corvo do DL e similares): comportamento definido por **comando, não por skill** — modos (seguir, atacar o meu alvo, atacar livre, defender, parado) trocados por um controle próprio no HUD. Uma build de pet não gasta os slots de skill.
- Um **controlador único de companheiro** (IA com modos); dados (dano, alcance, velocidade) no DB.
- **Invocações da Elfa**: continuam sendo **skills**, provavelmente **ultimate** — vários tipos de invocação mudam o estilo de jogo; ou ela joga sem invocação, como arqueira solitária. A invocação usa o mesmo controlador de companheiro.
- **Dark Lord**: manter a ideia de invocação/companheiro. Objetivo de design: o DL ser **viável no PvP** (no MU original não é — sem shield e com dano pouco relevante). Como (skill, passiva, status) fica para o balanceamento.
- **Slots separados**: `Montaria` (velocidade + passivas) e `Pet` (buff/passiva, ex.: Imp, Guardian Angel). Dá para ter os dois ao mesmo tempo.
- **Cavalo do DL**: montaria exclusiva do DL que **tem skill própria** (build com cavalo vs. sem cavalo).
- Passivas e buffs de pets/montarias são dados (atributos + agregação, como os talentos), para adicionar novos sem código.
- **Em aberto**: limite de invocações ativas; pet de ataque tem HP/morre?

## 8. Ordem de implementação proposta

1. ✅ **Skills: tipos e loadout** (§2) — muda o núcleo do combate; tudo que vem depois (talentos, companheiros) se apoia nele.
2. ✅ **Guild básica** + **amigos/cartas** — migração direta, social pronto.
3. ✅ **Árvore de talentos** (§5) — 1ª versão: janela + investir fora de combate, árvores do MU como conteúdo de teste; faltam quest de especialização, reset e as nossas árvores — dados novos no motor existente.
4. ✅ **Companheiros** (§7) — slot de montaria, modos/HUD para invocações e corvo como entidade (`17-companions.md`); visual (entrega D) fica para o polimento — controlador de comportamento + slots de montaria/pet.
5. ✅ **Quests novas** (§6) — 1ª versão como *Missions* (etapas, objetivos kill/collect/talk/reach, crédito de party, diário L, rastreador, diálogo de NPC, diária/semanal); faltam objetivo "usar item", migrar as quests de classe e conteúdo real — com crédito de party.
6. **Desafio** (§3) — pequeno, reaproveita party e `PvpRules`.
7. **Mercado** (§4).
8. **Remover o legado** que sair da migração: duel, loja pessoal, MU Helper/offline, comandos de pet por skill, fluxo de quest legado, views legadas sem uso.

Depois: arena ranqueada (mesmo motor de rounds do desafio), conteúdo de guild (castle siege, facção), eventos novos (`15-events-rework.md`), auto-play novo. Pós-MVP: battlegrounds (5v5/10v10, objetivos).
