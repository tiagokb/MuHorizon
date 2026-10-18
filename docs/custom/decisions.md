# Decisões Técnicas — Implementação

> Registra divergências do plano normativo (`09-implementation-plan.md`) tomadas durante a codificação.

---

## Fase 0

### D-F0-01 — WebSocketGameServerListener não cria RemotePlayer na Fase 0

**Plano:** Ao aceitar conexão WebSocket, instanciar `WebSocketConnection` e criar `RemotePlayer`.

**Decisão:** Phase 0 conecta o `PingHandlerPlugIn` diretamente ao evento `PacketReceived` da conexão; `RemotePlayer` não é criado.

**Por quê:** `RemotePlayer` usa `MainPacketHandlerPlugInContainer`, que despacha pelo primeiro byte do pacote (protocolo MU legado). Pacotes Protobuf têm primeiro byte diferente (field tag), causando warnings no dispatcher legado e overhead desnecessário para um simples ping/pong. A integração com `RemotePlayer` (ou um `ProtobufRemotePlayer` dedicado) será feita na Fase 1.

**Impacto:** O evento `PlayerConnected` não é disparado para conexões WebSocket na Fase 0; o GameServer não rastreia essas conexões no contador de players.

---

### D-F0-02 — Framing controlado pelo escritor, não pela conexão

**Plano:** Framing 4-byte big-endian, responsabilidade da camada de transporte.

**Decisão:** `WebSocketConnection` não injeta o prefixo de 4 bytes automaticamente na escrita. O handler (`PingHandlerPlugIn`) escreve `[4-byte BE length][payload]` explicitamente no `Output`. O loop de envio transmite todos os bytes em buffer como uma única mensagem WebSocket.

**Por quê:** Mantém a conexão agnóstica ao protocolo. Na leitura, `WebSocketConnection` parseia o framing para extrair payloads individuais — consistente com como `Connection.cs` funciona no protocolo legado.

---

### D-F0-03 — Porta WebSocket interna: 5000 (plano original dizia 55910)

**Decisão:** Porta **5000** conforme especificado no prompt de implementação da Fase 0.

**Por quê:** O prompt explicita porta 5000. A porta evita conflito com as portas legadas (5590x) e é convenção comum para serviços HTTP/WS internos em Docker.

---

### D-F0-04 — HttpListener em vez de Kestrel

**Decisão:** Usar `HttpListener` (BCL) para o `WebSocketGameServerListener`.

**Por quê:** Não exige adicionar dependências ASP.NET Core ao projeto `GameServer`. `HttpListener` com prefixo `http://+:5000/ws/` funciona perfeitamente em Linux (Docker) com nginx na frente para TLS. Migrar para Kestrel se futuramente precisarmos de middleware de autenticação HTTP.

---

## Fase 7

### D-F7-01 — Escopo da Fase 7 reduzido: AdminPanel mantido, remoções de features adiadas

**Plano original:** Fase 7 incluía CLI de gestão (7.0), remoção do AdminPanel (7.6), remoção de frutas/reset (7.7–7.8), remoção de handlers/RemoteView legados (7.10–7.11), anti-multi-client (7.12), atualização de todos os testes (7.17) e load test (7.18).

**Decisão:** Escopo reduzido a: remoção do código de rede legado (7.1–7.5, 7.9) e enforcement de camadas via Roslyn + auditorias CI (7.13–7.16). Tudo mais adiado indefinidamente.

**Por quê:**
- **AdminPanel mantido:** É o único editor de `GameConfiguration` disponível. Durante desenvolvimento ativo de novas features (skills, itens, mobs), remover o AdminPanel eliminaria a capacidade de configurar o jogo sem escrever código. O benefício de removê-lo é zero enquanto não existir uma alternativa equivalente.
- **Frutas, reset, handlers legados:** São código morto inofensivo. Removê-los exige atualizar dezenas de seeds de inicialização e testes de integração sem nenhum ganho de gameplay. Custo alto, benefício praticamente zero neste momento do projeto.
- **Anti-multi-client:** Feature nova que não é limpeza de legado. Faz sentido implementar quando houver jogadores reais, não durante a fase de prototipagem.
- **Atualização de testes + load test:** Alto esforço, praticamente zero recompensa de gameplay. Os testes existentes testam comportamentos do protocolo legado que não é mais usado; reescrevê-los é trabalho de polimento, não de desenvolvimento de produto.

**Impacto:** O `09-implementation-plan.md` foi atualizado para refletir este escopo. As tarefas adiadas permanecem documentadas na seção "Tarefas adiadas indefinidamente" da Fase 7 para referência futura.

---

### D-F7-02 — Dados só Season 6: versões 0.75 e 0.95d removidas (2026-10-17)

**Decisão:** Removidos os inicializadores de dados 0.75/0.95d (`DataInitialization`, itens, monstros, mapas, skills, classes) e todos os plugins de update específicos dessas versões (33 arquivos + 35 entradas do `UpdateVersion`). Season 6 é a única versão instalável.

**Por quê:** O cliente legado foi descartado (P6) e o jogo roda só em Season 6. Cada mudança de dados (ex.: movement speed aditivo) precisava ser escrita 3 vezes, para versões que nunca seriam instaladas.

**O que ficou:** ~26 classes em `Version075/`, `Version095d/` e `Version097d/` que o Season 6 ainda herda (mapas Lorencia/Devias/Noria/Atlans/Dungeon/LostTower/Exile/Arena/Icarus/Tarkan/Devil Square 1-4, `NpcInitialization` + `MerchantStores`, `InvasionMobsInitialization`, `DevilSquareInitializer`, `Jewels`/`Jewelery`). Fundir cada cadeia 075 → 095d → S6 numa classe só fica para uma etapa posterior. `SlotTypesInitializer` foi movido para `Initialization/Items` (era genérico). O update de joias do 0.75 virou `AddItemDropGroupForJewelsUpdateBase` (abstrato), base do update do Season 6.

**Impacto:** Bancos já instalados em Season 6 não mudam. Os números do `UpdateVersion` são explícitos, então os updates restantes mantêm a numeração. Um backup dos arquivos removidos não é necessário: está no histórico do git.

---

### D-F7-03 — Protocolo legado sai feature por feature ("migra e remove") (2026-10-17)

**Decisão:** Os handlers (`GameServer/MessageHandler/*`) e views (`GameServer/RemoteView/*`) do protocolo binário **não** são removidos em bloco. Cada um sai no mesmo commit em que a feature correspondente ganha a versão Protobuf (ex.: implementar quests via Protobuf → remover `MessageHandler/Quests` e `RemoteView/Quest`). `Network/Packets`, a `Connection` TCP e o ConnectServer saem quando não tiverem mais consumidor.

**Por quê:** Ainda faltam migrar várias features cuja lógica já existe na GameLogic (quests, upgrade de itens com joias, chaos machine, troca de mapa, eventos, party, trade, guild...). O handler legado mostra quais `PlayerAction` chamar e com quais parâmetros; o view mostra quais dados o cliente precisa. É o mapa de cada migração, e é mais prático no código do que no histórico do git. Apesar de ser código morto em runtime (só o `ProtobufRemotePlayer` é instanciado), o custo de mantê-lo é baixo.

**Removido já:** `ClientLauncher` e `ClientErrorLogDecryptor` (ferramentas do `main.exe` original, sem valor de referência para o cliente Godot).

---

### D-F7-04 — PvP por mapa e flag, substituindo PK/herói (2026-10-17)

**Decisão:** Quem pode bater em quem é decidido por `PvpRules`: safezone nunca, party nunca; o mapa define (`GameMapDefinition.PvpMode`): cidades PvE, mapas "flagged" (os dois com PvP ligado se batem; desligar exige 30 s sem combate PvP), mapas PvP (todo mundo). Matar player não gera status PK nem penalidade.

**Por quê:** Modelo de jogos modernos, previsível e sem PvP acidental; abre espaço para o loop de PvP planejado (facção, arena, castle siege — ver backlog).

---

### D-F7-05 — Controle de grupo vs. movimento (2026-10-18)

**Decisão:** Stun, sleep, root (freeze) e slow limitam o movimento **próprio** do personagem — andar, teleporte, dash até o alvo — e o slow tem prioridade sobre qualquer bônus de velocidade (é um fator multiplicativo). Movimento **forçado** por outra entidade (puxar, empurrar, knockback; de inimigo ou de aliado) acontece mesmo sob CC: `Player.MoveAsync` move a posição contínua sem checar CC, e as skills de movimento próprio checam CC onde são lançadas.

**Por quê:** Combinações de time são mecânica de combate: um aliado dá root, outro puxa o healer para fora da backline. Se root impedisse ser puxado, a combinação não existiria.

**Impacto:** As skills herdadas do MU ainda não foram desenhadas para o estilo MOBA (dashes, puxões, knockbacks com direção); quando forem, seguem esta regra.
