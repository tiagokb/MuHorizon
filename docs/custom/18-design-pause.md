# 18 — Pausa para respirar: revisão e design antes de construir o novo

Decidido em 2026-10-23, depois do Mercado (item 7 do `16-game-design-plan.md`). As funcionalidades
base estão quase todas escritas; daqui pra frente cada sistema novo depende de decisões que ainda não
tomamos (itens, progressão, PvP × PvE, recompensas, monetização). Antes de construir, a gente para,
limpa, revisa e fecha o design — para ter um norte e não refazer coisa.

## Ordem

1. **Limpeza do legado já substituído** (item 8 do doc 16) — fatias pequenas, uma por vez: compila → validação rápida no jogo → commit.
   - [ ] Duel (substituído pelo Desafio)
   - [ ] Loja pessoal (substituída pelo Mercado)
   - [ ] MU Helper / offline leveling
   - [ ] Comandos de pet por skill (substituídos pelos modos de companheiro)
   - [ ] Fluxo de quest legado (substituído pelas Missions) — **as quests de classe ainda não foram migradas**: o que elas usam fica até migrar
   - [ ] Views legadas sem uso (varredura: o que nenhum plugin Protobuf implementa/usa)
2. **Inventário do legado** (frente 1) — junto com a limpeza.
3. **Sessões de design** (frente 2) — uma ou duas por dia, no ritmo do usuário; decidem o destino do legado sem destino.
4. **Revisão de código + base de performance** (frentes 3 e 4) — dá para alternar com o design.
5. **Tecnologia do cliente** (frente 5).
6. **Limpeza do restante do legado**, agora com destino definido ("migra e remove": nada sai antes de se saber o que entra no lugar).
7. **Construção do novo**: skins (se a monetização pedir), arena ranqueada, eventos novos, conteúdo de guild, auto-play, battlegrounds (ordem do §8 do doc 16).

## Frente 1 — Inventário do legado

Listar tudo o que o MU original tem e classificar **manter / remover / redesenhar**, com o motivo
(tabela como o §1 do doc 16). O que for "redesenhar" vira pauta de uma sessão de design.

Candidatos já citados: loja X / cash shop (monetização P2W do MU), sockets e itens 380 (sistemas
fracassados), harmony, opções excellent / ancient, bosses, invasões, Chaos Machine, Kalima, Gens,
master skill tree — e o que mais aparecer na varredura (server: plugins, actions, configuração;
client legado em `C:\dev\MuMain`).

## Frente 2 — Design do jogo (brainstorm)

Cada sessão fecha decisões num doc (ou seção). Pauta inicial e perguntas:

- **Loop do jogador** — o que faz alguém logar; rotina do dia (ex.: diárias → evento → arena), da semana, da temporada. Como os sistemas se ligam e o que cada um dá de recompensa.
- **Progressão e itens** — o que substitui sockets / 380 / exc; como um item se diferencia (nível, opções, conjunto, algo novo); de onde vêm os itens.
- **PvP × PvE** — como quem só gosta de PvP se diverte e constrói o personagem? Item de PvP só dropa no PvE? Ideia a discutir: o ranked normaliza atributos (vale habilidade, não equipamento) e dá moeda própria para equipamento PvP; o PvE dá drop; os dois caminhos chegam ao mesmo poder por rotas diferentes.
- **Economia** — de onde vem o zen, para onde vai (sinks: taxa do mercado, reparo...); o que tem valor de troca.
- **Monetização** — sem P2W: skins, passe de temporada, conveniência — o que é aceitável.
  - **Sistema de skins**: o MU não tem. Visual separado do item equipado (transmog); skins de arma, armadura, montaria, pet; skins como recompensa (ranked, eventos, temporada).
- **Fim de jogo** — papel de bosses, eventos (decisões em aberto do `15-events-rework.md`), guild (castle siege, facção) e ranked no loop.

## Frente 3 — Revisão de código

- Conformidade com as regras (`08-refactoring-rules.md`, P1–P5) em tudo o que foi escrito no refactor.
- Código morto, duplicação, constantes que deveriam estar no DB (P3).
- O que não escala — ex.: snapshot do mercado em memória (`MarketService`), estados estáticos, varreduras por todos os jogadores.
- Por camada: GameLogic → Persistence → GameServer/Network → cliente.

## Frente 4 — Performance

- **Server**: teste de carga com os bots (`DummyPlayer`) — N jogadores num mapa; medir tick (o log já mostra "Tick overshoot"), memória, rede, banco.
- **Orçamentos**: ex.: tick < X ms com Y jogadores por mapa; banda por jogador.
- **Cliente**: FPS, draw calls, mapas grandes (profiler do Godot).
- Pendências já conhecidas: inicialização de ~27 s (`12-backlog.md`, ambiente de dev).

## Frente 5 — Tecnologia do cliente

- **VFX**: partículas/shaders GPU do Godot × converter efeitos do MU × refazer.
- **UI moderna**: hoje as janelas são montadas em código — tema, layout, padrão de componentes, talvez ferramenta.
- Decidir antes de criar mais janelas: afeta todo o cliente daqui pra frente.

## Refinamentos anotados (não esquecer, entram quando couber)

- **Mercado**: no cliente o personagem ainda anda e dá para digitar em campos enquanto o mercado está ocupado (o server ignora, nada quebra) — travar de verdade; botões criados durante o "busy" não ficam desabilitados (§4 do doc 16).
- **Skills**: passada de validação (Electric Spike, skills evoluídas herdando o tipo de slot).
- **IA**: invocações e mobs; cooldown de teste de 15 s da invocação.
- **Warps** caindo em tile não andável (e movimento contínuo em tile não andável — `12-backlog.md`).
- **Guild**: emblema.
- **Companheiros**: visual (entrega D, `17-companions.md`).
- **Talentos**: quest de especialização, reset, nossas árvores.
- **Missions**: objetivo "usar item", migrar quests de classe, conteúdo real.
- Demais itens abertos em `12-backlog.md` e `11-refinements.md`.
