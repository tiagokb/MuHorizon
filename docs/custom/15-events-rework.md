# Eventos — rework

Status: **planejamento** (2026-10-19). Os eventos legados (Devil Square, Blood Castle, Chaos Castle) ficam como estão; o design novo ainda vai ser definido.

## O que já existe e pode ser reaproveitado

- **Server**: `MiniGameContext` (ciclo de vida: abertura → entrada → jogo → fim/score), `MiniGameDefinition` no DB (níveis, faixas de level, ticket, duração, recompensas), change events (kills → terreno/spawn), spawn waves, ranking (`SaveRankingAsync`).
- **Protocolo**: `C2SEnterMiniGame`, `C2SMiniGameOpeningRequest`, `S2CMiniGameEntrance`, `S2CMiniGameOpening`, `S2CMiniGameState`, `S2CMiniGameScore`, `S2CTerrainChanged` (plugin `ProtobufMiniGamePlugIn`).
- **Client**: `EventWindow` (J, lista de eventos com countdown e Enter), `EventHud` (status no topo + popup de score), `WorldMap.ApplyTerrainChange`, mapeamento de pastas dos mapas de evento.
- **Ferramentas**: `/startds`, `/startbc`, `/startcc` (GM), `/god`, `GameMapDefinition.MonsterStrengthPercent`.

## O que é legado (candidato a ser substituído)

- Regras de entrada: ticket por nível de evento (Devil's Invitation / Invisibility Cloak / Armor of Guardsman), faixas de level, taxa em zen, bloqueio de PK.
- Gameplay interno: sequência fixa do BC (ponte → portão → estátua → arma do Arcanjo), ondas do DS, last-man-standing do CC.
- Recompensas: exp/zen fixos por rank.

## Decisões em aberto (definir antes de implementar)

- [ ] Quais eventos existem no jogo novo (manter DS/BC/CC com regras novas, ou eventos novos)?
- [ ] Entrada: ticket? fila/matchmaking? solo/party? faixa de level ou escala por level?
- [ ] Agenda: horário fixo, sob demanda, rotativo?
- [ ] Objetivo e loop interno de cada evento; PvE, PvP ou misto (ligar com o loop de PvP do backlog: arena ranqueada, castle siege)?
- [ ] Recompensas e o que alimenta (itens, moeda, ranking, skins de season?).
- [ ] Escala de dificuldade (por nível do evento, nº de jogadores, `MonsterStrengthPercent`).
