# 08 — Regras de Refatoração do Projeto

> Documento normativo. Toda decisão de arquitetura, PR e revisão deve ser validada contra este documento antes de ser aceita.

**Pré-requisitos:** 01-network-pipeline · 02-movement-system · 03-skills · 04-login-auth · 05-character-creation · 06-world-sync · 07-item-system

---

## Sumário

1. [Princípios Gerais](#1-princípios-gerais)
2. [Camadas e Contratos](#2-camadas-e-contratos)
3. [Regras do Protocolo Protobuf](#3-regras-do-protocolo-protobuf)
4. [Regras de Tick Loop e Sincronização](#4-regras-de-tick-loop-e-sincronização)
5. [Regras de Coordenadas](#5-regras-de-coordenadas)
6. [Regras de Segurança](#6-regras-de-segurança)
7. [Critérios de Validação](#7-critérios-de-validação)
8. [Anti-patterns](#8-anti-patterns)
9. [Inventário de Mudanças](#9-inventário-de-mudanças)

---

## 1. Princípios Gerais

Estas são as leis do projeto. Qualquer regra de seção posterior é uma especialização de um destes princípios.

### P1 — GameLogic nunca conhece o protocolo

A `GameLogic` (`src/GameLogic/`) **não importa nenhum tipo de `MUnique.OpenMU.Network`**, nenhum tipo gerado de pacote e nenhuma referência a `IConnection`. A única forma de a GameLogic se comunicar com o mundo externo é chamando interfaces de `IViewPlugIn` definidas em `src/GameLogic/Views/`.

Violação: qualquer `using MUnique.OpenMU.Network` dentro de `src/GameLogic/` é recusa automática de PR.

### P2 — Toda lógica de gameplay é server-side e autoritativa

O servidor não confia no cliente para nenhum dado de gameplay:
- Posição final de um movimento é computada e validada pelo servidor.
- Cooldowns são verificados e aplicados exclusivamente pelo servidor.
- Dano, crit, opções excellent, interrupção de cast — tudo calculado no servidor.
- O cliente recebe o resultado. Nunca o cliente declara o resultado.

### P3 — O banco de dados é a única fonte de verdade de configuração

Nenhuma constante de gameplay (dano base, duração de buff, drop rate, opções excellent) é hardcoded em runtime. Toda configuração é lida do banco via `GameConfiguration`. Mudanças de balanceamento são feitas via `IUpdatePlugIn` que modifica registros no banco — não por deploys de código.

Exceção permitida: constantes de protocolo de rede (tamanho de buffer, timeouts de conexão) podem ser configuradas via `appsettings.json`.

### P4 — O sistema de plugins é o único mecanismo de extensão de protocolo

Para adicionar suporte a um novo evento de jogo no protocolo:
1. Criar a interface `IXxxPlugIn` em `src/GameLogic/Views/`.
2. Criar a implementação Protobuf em `src/GameServer/RemoteView/`.
3. Registrar com `[PlugIn]`.

Nenhum código fora dessas duas etapas precisa ser modificado.

### P5 — Um único tick loop por instância de mapa

Toda lógica de gameplay com dimensão de tempo (movimento, cooldown, AI de monstro, expiração de buff, broadcast de snapshot) roda dentro do tick do `GameTickLoop` da instância de mapa correspondente. Timers individuais por entidade são proibidos em código novo.

### P6 — Compatibilidade com o protocolo legado é irrelevante

O cliente original do MU Online foi descartado. Não existe obrigação de manter, testar ou sequer compilar código que só existe para suportar o protocolo binário legado. Esses arquivos serão removidos, não mantidos em paralelo.

### P7 — Nenhuma funcionalidade descrita como removida será reintroduzida

Sistema de frutas, reset de personagem, multi-client, P2W — qualquer PR que introduza código relacionado é recusado independentemente do argumento apresentado. A lista de remoções em §9.2 é exaustiva e final para esta fase.

### P8 — Soft-delete é obrigatório para entidades de jogador

Personagens, contas e itens de jogador nunca são deletados fisicamente do banco. Toda exclusão usa soft-delete (campo `DeletedAt` ou flag `IsDeleted`). Hard-delete só é permitido em scripts de manutenção com aprovação explícita.

---

## 2. Camadas e Contratos

### 2.1 Mapa de camadas

```
┌─────────────────────────────────────────────────────────────────────┐
│  TRANSPORTE  (src/Network/)                                         │
│  WebSocketConnection : IConnection                                  │
│  TLS 1.3, framing Protobuf, OutputLock, BeginReceiveAsync           │
├─────────────────────────────────────────────────────────────────────┤
│  PROTOCOLO  (src/GameServer/MessageHandler/ + RemoteView/)          │
│  PacketHandlerPlugIn → desserializa Protobuf → chama PlayerAction   │
│  ViewPlugIn ← serializa Protobuf ← chamado pela GameLogic           │
├─────────────────────────────────────────────────────────────────────┤
│  CONTRATO  (src/GameLogic/Views/)                                   │
│  IViewPlugIn e suas especializações — interfaces puras, sem rede    │
├─────────────────────────────────────────────────────────────────────┤
│  LÓGICA DE JOGO  (src/GameLogic/)                                   │
│  PlayerActions, GameTickLoop, ContinuousWalker, Skills, AoI         │
│  Sem nenhuma referência a rede ou protocolo                         │
├─────────────────────────────────────────────────────────────────────┤
│  DADOS  (src/DataModel/ + src/Persistence/)                         │
│  EF Core, PostgreSQL, repositórios, seed via Initializers C#        │
└─────────────────────────────────────────────────────────────────────┘
```

### 2.2 Interfaces sagradas (não podem ser quebradas)

Estas interfaces definem o contrato entre camadas. Remover ou modificar sua assinatura exige aprovação explícita e atualização de todas as implementações existentes.

| Interface | Localização | Contrato |
|---|---|---|
| `IConnection` | `src/Network/IConnection.cs` | `PacketReceived`, `Output` (PipeWriter), `OutputLock`, `BeginReceiveAsync`, `Disconnect` |
| `IViewPlugIn` | `src/GameLogic/Views/IViewPlugIn.cs` | Interface marcadora base |
| `IObjectMovedPlugIn` | `src/GameLogic/Views/World/IObjectMovedPlugIn.cs` | `ObjectMovedAsync(ILocateable, MoveType)` |
| `IPacketHandlerPlugIn` | `src/GameServer/MessageHandler/IPacketHandlerPlugIn.cs` | `Key`, `HandlePacketAsync(Player, Memory<byte>)` |
| `ILocateable` | `src/GameLogic/ILocateable.cs` | `CurrentMap`, `Position` (Vector2F), `TilePosition` (Point) |
| `IDropGenerator` | `src/GameLogic/IDropGenerator.cs` | `GenerateItemDropsAsync`, `GenerateItemDrop` |
| `ILoginServer` | `src/Interfaces/ILoginServer.cs` | Único ponto de acesso ao estado de sessão |
| `IPlayerInputHandler` | *(novo)* `src/GameLogic/IPlayerInputHandler.cs` | `HandleAsync(IPlayerInput)` — processa inputs do buffer |
| `IGameTickable` | *(novo)* `src/GameLogic/IGameTickable.cs` | `TickAsync(TimeSpan deltaTime)` |

### 2.3 Interfaces que podem ser substituídas (implementação trocável)

| Interface | Implementação atual | Implementação alvo |
|---|---|---|
| `IConnection` | `Connection` (TCP + PipelinedDecryptor) | `WebSocketConnection` (WS + TLS 1.3) |
| `INetworkEncryptionFactoryPlugIn` | `SimpleModulusEncryptionFactoryPlugIn` | Removida; TLS cuida da criptografia |
| `ISupportWalk` / `Walker` | `Walker` (Task.Delay por passo) | `ContinuousWalker` (integração com delta time) |
| `BasicMonsterIntelligence` | Timer individual por monstro | Implementação de `IGameTickable` |
| `ILoginServer` | `LoginServer` (in-memory Dictionary) | Mantida in-memory agora; interface preparada para migração futura |

### 2.4 Regras de dependência entre camadas

- `src/DataModel/` não depende de nenhuma outra camada do projeto.
- `src/GameLogic/` depende apenas de `src/DataModel/` e `src/Pathfinding/`.
- `src/GameServer/` depende de `src/GameLogic/` e `src/Network/`.
- `src/Network/` não depende de `src/GameLogic/`.
- Violações detectadas pelo analisador de dependência (`.editorconfig` + Roslyn analyzer) são erros de build, não warnings.

### 2.5 `ILoginServer` — regra de acesso único

Todo acesso ao estado de sessão (login, logout, verificação de conta online) passa exclusivamente por `ILoginServer`. Nenhum código deve verificar ou modificar estado de sessão por outro meio (ex: consultar tabela diretamente, verificar campo no `Player`). Isso garante que uma futura migração para Garnet ou PostgreSQL exige apenas trocar a implementação de `ILoginServer`.

---

## 3. Regras do Protocolo Protobuf

### 3.1 Definição de mensagens

- Todos os arquivos `.proto` vivem em `src/Network/Protobuf/`.
- Organização: um arquivo por domínio (`movement.proto`, `skills.proto`, `items.proto`, `auth.proto`, `world.proto`, `chat.proto`).
- Versão do pacote: campo `uint32 protocol_version = 1` presente em **todas** as mensagens C→S.
- O servidor rejeita mensagens com `protocol_version` incompatível com um erro `ERR_PROTOCOL_VERSION_MISMATCH` antes de processar qualquer campo.

### 3.2 Nomenclatura de mensagens

| Categoria | Prefixo | Exemplo |
|---|---|---|
| Cliente → Servidor | `C2S` | `C2SMove`, `C2SCastSkill`, `C2SPickUpItem` |
| Servidor → Cliente | `S2C` | `S2CSnapshot`, `S2CDamageResult`, `S2CItemDrop` |
| Bidirecional (chat) | `Msg` | `MsgChat` |
| Enum compartilhado | sem prefixo | `MoveType`, `DamageType`, `SkillResult` |

### 3.3 Estrutura de um pacote Protobuf no wire

```
┌──────────────┬──────────────────────────────────────────┐
│ 4 bytes      │ N bytes                                  │
│ length (LE)  │ serialized Protobuf message              │
└──────────────┴──────────────────────────────────────────┘
```

- O campo `length` é o tamanho da mensagem serializada em bytes, little-endian, não inclui os 4 bytes de header.
- Não existe campo de opcode separado: o tipo de mensagem é determinado pelo campo `oneof payload` no envelope `ClientEnvelope` / `ServerEnvelope`.

```protobuf
message ClientEnvelope {
  uint32 protocol_version = 1;
  uint64 sequence = 2;       // incrementado pelo cliente; usado para reconciliação
  oneof payload {
    C2SMove       move       = 10;
    C2SCastSkill  cast_skill = 11;
    C2SPickUpItem pick_item  = 12;
    // ... demais mensagens
  }
}

message ServerEnvelope {
  uint64 server_tick = 1;    // tick do servidor no momento do envio
  oneof payload {
    S2CSnapshot     snapshot     = 10;
    S2CDamageResult damage       = 11;
    S2CItemDrop     item_drop    = 12;
    // ... demais mensagens
  }
}
```

### 3.4 Mapeamento GameLogic → Protobuf

Toda chamada de `IViewPlugIn` na GameLogic corresponde a exatamente uma mensagem S2C. A tabela abaixo define o mapeamento canônico:

| IViewPlugIn | Mensagem S2C | Campo(s) principais |
|---|---|---|
| `IObjectMovedPlugIn.ObjectMovedAsync` | `S2CEntityMoved` | `entity_id`, `position` (Vector2F), `velocity` (Vector2F), `move_type` |
| `IShowSkillAnimationPlugIn` | `S2CSkillAnimation` | `caster_id`, `target_id`, `skill_id`, `result` |
| `IObjectHitPlugIn` | `S2CDamageResult` | `attacker_id`, `target_id`, `damage`, `damage_type`, `is_crit`, `is_excellent` |
| `INewPlayersInScopePlugIn` | `S2CEntitiesInScope` | repeated `EntityState` |
| `IObjectsOutOfScopePlugIn` | `S2CEntitiesOutOfScope` | repeated `entity_id` |
| `IShowLoginResultPlugIn` | `S2CLoginResult` | `result` (enum) |
| `IItemDropResultPlugIn` | `S2CItemDrop` | `drop_id`, `item_data`, `position` |
| `IPickUpItemPlugIn` | `S2CPickUpResult` | `result`, `inventory_slot` |

### 3.5 Mensagens de snapshot de posição

`S2CSnapshot` é enviado a cada tick do `GameTickLoop` e contém apenas entidades dentro do AoI do player cujo estado mudou desde o último snapshot enviado para aquele player (delta compression).

```protobuf
message S2CSnapshot {
  uint64 server_tick = 1;
  repeated EntityDelta entities = 2;
}

message EntityDelta {
  uint32 entity_id  = 1;
  float  x          = 2;   // posição contínua
  float  y          = 3;
  float  vel_x      = 4;   // velocidade para interpolação no cliente
  float  vel_y      = 5;
  uint32 flags      = 6;   // bitmask: moving, casting, stunned, dead
}
```

### 3.6 Versionamento

- A versão do protocolo é um `uint32` monotonicamente crescente, definido em `src/Network/Protobuf/ProtocolVersion.cs`.
- Major break (campo removido, semântica mudada): incremento de versão obrigatório.
- Adição de campo opcional (proto3 é backward-compatible por padrão): sem incremento, mas documentado em CHANGELOG.
- O servidor mantém compatibilidade com `protocol_version` atual e `atual - 1` por no máximo um ciclo de release.

---

## 4. Regras de Tick Loop e Sincronização

### 4.1 GameTickLoop — estrutura

`GameTickLoop` é um `IHostedService` criado por instância de `GameMap`. Implementação:

```csharp
// src/GameLogic/GameTickLoop.cs (novo)
public sealed class GameTickLoop : IHostedService, IAsyncDisposable
{
    private const int TargetTickRateHz = 30;  // ajustável por mapa via GameMapDefinition
    private readonly TimeSpan _tickInterval = TimeSpan.FromMilliseconds(1000.0 / TargetTickRateHz);
    private readonly GameMap _map;

    protected async Task ExecuteAsync(CancellationToken ct)
    {
        var lastTick = Stopwatch.GetTimestamp();
        while (!ct.IsCancellationRequested)
        {
            var now   = Stopwatch.GetTimestamp();
            var delta = TimeSpan.FromSeconds((double)(now - lastTick) / Stopwatch.Frequency);
            lastTick  = now;

            await this.ProcessInputBuffersAsync();    // drena ConcurrentQueue<IPlayerInput>
            await this.TickAllEntitiesAsync(delta);   // IGameTickable.TickAsync(delta)
            await this.BroadcastSnapshotsAsync();     // envia S2CSnapshot apenas para o que mudou

            var elapsed   = TimeSpan.FromSeconds((double)(Stopwatch.GetTimestamp() - now) / Stopwatch.Frequency);
            var remaining = _tickInterval - elapsed;
            if (remaining > TimeSpan.Zero)
                await Task.Delay(remaining, ct);
        }
    }
}
```

### 4.2 Frequências por categoria de dado

| Dado | Frequência de processamento | Frequência de envio ao cliente |
|---|---|---|
| Posição de entidade em movimento | Todo tick (30Hz) | Todo tick para objetos próximos; a cada 3 ticks para distantes |
| Resultado de dano / skill | Imediato (dentro do tick) | Imediato (fora do ciclo de snapshot) |
| Entrada no/saída do AoI | Detectado no tick | Enviado no mesmo tick |
| Estado de buff/debuff | Todo tick (verificação de expiração) | Apenas na mudança |
| Stats de personagem | Sob demanda (após equip/desequip/levelup) | Imediato após a ação |
| Posição de entidades imóveis (NPCs parados) | Não processado | Apenas na entrada no AoI |
| Chat | Sem tick (event-driven) | Imediato |

### 4.3 Separação entre física interna e envio de rede

- A fase de tick **nunca escreve diretamente em `IConnection.Output`**.
- A fase de tick atualiza estados internos (`Position`, `CurrentHealth`, `ActiveEffects`, etc.).
- `BroadcastSnapshotsAsync()` é a **única função** que gera e envia `S2CSnapshot` e outros pacotes de estado contínuo — sempre após a fase de física, no mesmo tick.
- Pacotes de resultado imediato (`S2CDamageResult`, `S2CSkillAnimation`) são enfileirados durante a fase de física e despachados em `BroadcastSnapshotsAsync()`.

### 4.4 Modelo de input buffer

```
Cliente → WebSocket → WebSocketConnection.PacketReceived
    → ProtobufPacketHandler.HandlePacketAsync
        → player.InputQueue.Enqueue(new MoveInput { ... })
                                    ↑ ConcurrentQueue<IPlayerInput>

GameTickLoop.ProcessInputBuffersAsync()
    → foreach player: drena InputQueue, chama IPlayerInputHandler.HandleAsync(input)
        → PlayerActions são invocadas aqui, dentro do tick
```

- O `InputQueue` por jogador é um `ConcurrentQueue<IPlayerInput>` com capacidade máxima de 32 entradas.
- Se o queue estiver cheio ao chegar um novo input, o input mais antigo é descartado (não o novo).
- Inputs chegados durante o mesmo tick são processados na ordem de chegada.

### 4.5 Reconciliação de posição

- Todo `C2SMove` carrega o campo `uint64 sequence` (incrementado pelo cliente).
- O servidor processa o movimento e responde com `S2CEntityMoved` contendo o campo `acked_sequence` com o último sequence processado.
- Se o cliente detectar que sua posição local divergiu do `acked_sequence`, aplica correção de posição (snap ou interpolação suave, decisão do cliente Godot — ver `docs/custom/client-prediction.md`).
- O servidor nunca aceita a posição declarada pelo cliente como definitiva — apenas o input (direção/destino) é aceito.

### 4.6 AI de monstros no tick loop

- `BasicMonsterIntelligence` (ou substituto) implementa `IGameTickable`.
- Todos os monstros de um mapa são tickados pelo mesmo `GameTickLoop` do mapa.
- O tick de AI usa o mesmo `deltaTime` do loop.
- Pathfinding A* para monstros é executado sob demanda (não a cada tick) e cacheado por N ticks configuráveis.

---

## 5. Regras de Coordenadas

### 5.1 Dois sistemas de coordenadas coexistem

| Sistema | Tipo | Uso | Exemplo de código |
|---|---|---|---|
| `Vector2F` | `record struct(float X, float Y)` | Posição contínua de entidades em runtime e no protocolo | `entity.Position`, `S2CSnapshot.x/y` |
| `Point` | `record struct(byte X, byte Y)` | Colisão com WalkMap, indexação de bucket, banco de dados | `WalkMap[p.X, p.Y]`, `BucketMap.GetBucket(p)` |

### 5.2 Definições

```csharp
// src/Pathfinding/Point.cs — não modificar
public record struct Point(byte X, byte Y);

// src/GameLogic/Vector2F.cs (novo)
public record struct Vector2F(float X, float Y)
{
    // Escala 1:8 — 1 tile = 8 unidades; range válido [0.0, 2040.0]
    public Point ToTile() => new((byte)(X / 8f), (byte)(Y / 8f));
    public static Vector2F FromTile(Point p) => new(p.X * 8f + 4f, p.Y * 8f + 4f); // centro do tile
    public float DistanceTo(Vector2F other) => MathF.Sqrt((X - other.X) * (X - other.X) + (Y - other.Y) * (Y - other.Y));
}
```

### 5.3 Regras de conversão

- **Vector2F → Point (tile lookup):** `vector.ToTile()` usa divisão inteira por 8 (`(byte)(X / 8f)`). **Nunca use `MathF.Floor(X)` ou cast direto `(byte)x`** — a escala 1:8 exige a divisão; sem ela o resultado é errado em toda a faixa de valores.
- **Point → Vector2F (centro do tile):** `Vector2F.FromTile(p)` retorna o centro do tile (`p.X * 8f + 4f`, `p.Y * 8f + 4f`).
- **Colisão:** sempre convertida para `Point` antes de consultar `WalkMap`. Nunca consultar `WalkMap` com float.
- **Bucket lookup:** sempre convertida para `Point` antes de indexar no `BucketMap`. `bucket = map.GetBucket(position.ToTile())`.
- **Banco de dados:** posições iniciais de personagem, gates e spawn points de monstro continuam armazenadas como `byte X, byte Y` (via `Point`). A conversão para `Vector2F` acontece ao carregar em memória.

### 5.4 `ILocateable` com representação dual

```csharp
// src/GameLogic/ILocateable.cs
public interface ILocateable : IIdentifiable
{
    GameMap? CurrentMap { get; }
    Vector2F Position { get; set; }        // posição contínua (runtime)
    Point TilePosition => Position.ToTile(); // derivado, não armazenado separadamente
}
```

### 5.5 Serialização no protocolo

- Posições enviadas no protocolo Protobuf usam `float x, float y` (Vector2F).
- O cliente Godot usa `x` e `y` diretamente para renderização (1 unidade Godot = 1 unidade do sistema de coordenadas).
- O cliente nunca recebe `Point` (byte) no protocolo — essa conversão é interna do servidor.

### 5.6 Line of Sight

- Implementação: algoritmo de Bresenham sobre o `WalkMap[,]`.
- Localização: `src/GameLogic/LineOfSight.cs` (novo).
- Regra: toda skill com range > 1 tile que não seja AoE deve verificar LoS antes de aplicar dano.
- O LoS opera sobre `Point` (tiles), não sobre `Vector2F`. A posição de origem e destino são convertidas com `ToTile()` antes da verificação.

```csharp
// Assinatura esperada
public static class LineOfSight
{
    public static bool HasLineOfSight(GameMapTerrain terrain, Point from, Point to);
}
```

---

## 6. Regras de Segurança

### 6.1 Transporte

- **TLS 1.3 é obrigatório.** Nenhuma conexão WebSocket sem TLS é aceita em produção. O servidor rejeita conexões `ws://` — apenas `wss://`.
- O certificado TLS é provisionado externamente (ex: Let's Encrypt via reverse proxy). O servidor aceita a conexão já descriptografada do proxy; nunca termina TLS diretamente em desenvolvimento.
- Não existe mais camada de criptografia de protocolo (SimpleModulus, XOR) — TLS é a única camada de criptografia.

### 6.2 Cooldown server-side

- O servidor mantém `Dictionary<SkillId, DateTime> _lastCastTime` por jogador.
- Ao receber `C2SCastSkill`, o servidor verifica: `DateTime.UtcNow - _lastCastTime[skillId] >= skill.CooldownTime`.
- Se a verificação falha, o servidor **descarta o input silenciosamente** (sem desconectar o cliente).
- Se a verificação falha repetidamente (mais de 5 vezes em 1 segundo para a mesma skill), o servidor registra o evento como suspeito e pode aplicar rate limit temporário ao jogador.
- O cliente pode exibir o cooldown visualmente, mas o servidor nunca aceita o estado de cooldown reportado pelo cliente.

### 6.3 Rate limiting de input

- Máximo de **60 inputs por segundo** por conexão (medido como total de mensagens `ClientEnvelope`).
- Máximo de **5 mensagens `C2SMove` por segundo** por jogador (speed cap).
- Máximo de **1 mensagem `C2SCastSkill` por 100ms** por jogador (sem importar a skill).
- Implementação: `RateLimiter` por conexão, verificado em `WebSocketConnection.PacketReceived` antes de enfileirar no `InputQueue`.
- Violação de rate limit: primeiro aviso silencioso. Após 10 violações em 30 segundos: desconexão com log.

### 6.4 Anti-multi-client

- Ao autenticar, `ILoginServer.TryLogInAsync(accountName, serverId)` retorna `false` se a conta já está logada. O servidor envia `S2CLoginResult { result: ALREADY_LOGGED_IN }` e fecha a conexão.
- A fingerprint de hardware (hash de identificadores únicos) é enviada pelo cliente Godot no handshake (`C2SHandshake`). O servidor mantém um registro de fingerprints por conta e alerta (não bloqueia imediatamente) quando uma conta usa fingerprints diferentes na mesma sessão.
- Múltiplas conexões do mesmo IP são permitidas até o limite configurável em `GameConfiguration.MaxConnectionsPerIp` (padrão: 3). Acima disso, novas conexões são rejeitadas.

### 6.5 SecurityCode (PIN de personagem)

- `SecurityCode` nunca é armazenado em texto plano. Hash com Argon2id, salt único por personagem.
- A verificação de SecurityCode ocorre exclusivamente no servidor via `ICharacterSecurityCodeVerifier`.
- O cliente envia o código em texto plano sobre TLS. O servidor calcula o hash e compara.
- Após 5 tentativas erradas consecutivas, o personagem é bloqueado por 15 minutos (configurável).

### 6.6 Soft-delete obrigatório

- `Character`: campo `DeletedAt DateTime?`. Personagens com `DeletedAt != null` são invisíveis para listagem e seleção, mas existem no banco.
- `Item`: campo `IsDeleted bool`. Itens deletados (consumidos, destruídos) têm `IsDeleted = true`.
- A remoção permanente só ocorre via job de manutenção agendado, com janela mínima de 30 dias após soft-delete.

### 6.7 Validação de input de personagem

- **Nome de personagem:** regex `^[a-zA-Z0-9]{3,10}$` (já configurável via `GameConfiguration.CharacterNameRegex`).
- **Nome de conta:** validado no registro; não revalidado em runtime.
- **Chat:** sanitização de XSS e injection antes de retransmitir. Nenhum dado de chat é interpretado como comando pelo servidor sem prefixo explícito de GM.
- **Coordenadas:** `Vector2F.X` e `Vector2F.Y` recebidos do cliente são clamped para `[0, 2040]` antes de qualquer uso (escala 1:8; 255 tiles × 8 unidades/tile = 2040 unidades).

---

## 7. Critérios de Validação

### 7.1 Critérios de conclusão de uma refatoração

Uma refatoração está **concluída** quando todas as condições abaixo são atendidas:

| Critério | Como verificar |
|---|---|
| A GameLogic não tem nenhuma referência a `Network` | `dotnet build` com analyzer de dependência: zero warnings |
| Toda `IViewPlugIn` existente tem implementação Protobuf | Lista de `IViewPlugIn` em `GameLogic/Views/` == lista de ViewPlugIns em `RemoteView/` |
| Todos os inputs de gameplay passam pelo `InputQueue` | Nenhuma `PlayerAction` é chamada diretamente de um handler fora do `ProcessInputBuffersAsync` |
| Nenhum `System.Threading.Timer` em código de gameplay | `grep -r "new Timer" src/GameLogic/` retorna zero resultados |
| Nenhum `Task.Delay` em código de walker ou AI | `grep -r "Task.Delay" src/GameLogic/Walker\|src/GameLogic/NPC` retorna zero |
| Cooldown verificado no servidor antes de processar skill | `SkillAction` contém verificação antes de `IViewPlugIn` ser chamado |
| LoS verificado para skills com range > 1 | `HitAction` chama `LineOfSight.HasLineOfSight` para skills ranged |
| Soft-delete implementado para Character e Item | `Character.DeletedAt` e `Item.IsDeleted` existem no schema e são usados nas ações de delete |
| TLS obrigatório na configuração do WebSocket | `WebSocketServerOptions.SslOptions != null` no startup |
| Zero referências a SimpleModulus, XOR-3, PacketTwister | `grep -r "SimpleModulus\|Xor3\|PacketTwister" src/` retorna zero |

### 7.2 Testabilidade por contrato de interface

Toda `PlayerAction` deve ser testável sem nenhuma dependência de rede. O padrão:

```csharp
// Teste unitário de uma PlayerAction
[Fact]
public async Task CastSkillAction_ShouldCheckCooldown_BeforeApplyingDamage()
{
    var player   = CreateTestPlayer();
    var target   = CreateTestTarget();
    var viewMock = new Mock<IObjectHitPlugIn>();

    player.ViewPlugIns.Register(viewMock.Object);
    player.SetLastCastTime(SkillId.FireBall, DateTime.UtcNow); // cooldown ativo

    await new AreaSkillAttackAction().AttackAsync(player, target, SkillId.FireBall);

    viewMock.Verify(v => v.ObjectGotHitAsync(It.IsAny<HitInfo>()), Times.Never);
}
```

A ausência de `IConnection` no teste confirma que a `PlayerAction` não conhece o protocolo.

### 7.3 Critério de performance mínima

O `GameTickLoop` a 30Hz deve completar cada tick em menos de **20ms** (headroom de 40% do intervalo de 33ms) para até **500 entidades simultâneas** por instância de mapa. Se um tick ultrapassar 20ms, um warning é emitido no log com o breakdown por fase (input, tick, broadcast).

---

## 8. Anti-patterns

As regras a seguir descrevem o que **explicitamente não fazer**. Qualquer PR que introduza um desses padrões é rejeitado automaticamente.

### 8.1 Protocolo dentro da GameLogic

```csharp
// PROIBIDO — dentro de src/GameLogic/
using MUnique.OpenMU.Network;
using MUnique.OpenMU.Network.Packets;

public class SomeAction
{
    public async ValueTask DoAsync(Player player)
    {
        var bytes = new byte[10];
        // monta pacote diretamente
        await player.Connection.Output.WriteAsync(bytes); // VIOLAÇÃO DO P1
    }
}
```

**Correto:** chamar `player.InvokeViewPlugInAsync<IXxxPlugIn>(p => p.XxxAsync(...))`.

### 8.2 Confiar no cooldown do cliente

```csharp
// PROIBIDO
public async ValueTask CastSkillAsync(Player player, SkillId skill, bool clientSaysCooldownOk)
{
    if (!clientSaysCooldownOk) return; // VIOLAÇÃO DO P2
    await ApplyDamage(player, skill);
}
```

**Correto:** o servidor verifica `DateTime.UtcNow - _lastCastTime[skill] >= skill.CooldownTime`.

### 8.3 Hard-delete de entidade de jogador

```csharp
// PROIBIDO
public async ValueTask DeleteCharacterAsync(Character character)
{
    _context.Remove(character); // VIOLAÇÃO DO P8
    await _context.SaveChangesAsync();
}
```

**Correto:** `character.DeletedAt = DateTime.UtcNow; await _context.SaveChangesAsync();`

### 8.4 Timer individual por entidade de gameplay

```csharp
// PROIBIDO em código novo
public class MyMonsterIntelligence
{
    public void Start()
    {
        _timer = new Timer(_ => Tick(), null, 0, 500); // VIOLAÇÃO DO P5
    }
}
```

**Correto:** implementar `IGameTickable` e registrar no `GameTickLoop` do mapa.

### 8.5 Acesso a estado de sessão fora do `ILoginServer`

```csharp
// PROIBIDO
if (_dbContext.Accounts.Any(a => a.Name == name && a.IsLoggedIn)) // VIOLAÇÃO DE §2.5
    return LoginResult.AlreadyLoggedIn;
```

**Correto:** `if (!await _loginServer.TryLogInAsync(name, serverId)) return LoginResult.AlreadyLoggedIn;`

### 8.6 Consultar `WalkMap` com float sem conversão explícita

```csharp
// PROIBIDO
bool walkable = terrain.WalkMap[(byte)position.X, (byte)position.Y]; // cast direto PROIBIDO
```

**Correto:** `bool walkable = terrain.WalkMap[position.ToTile().X, position.ToTile().Y];`

### 8.7 PlayerAction chamada diretamente do handler fora do tick

```csharp
// PROIBIDO — handler chama PlayerAction diretamente no recebimento do pacote
public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
{
    var input = ProtobufSerializer.Deserialize<C2SMove>(packet);
    await new WalkAction().ExecuteAsync(player, input); // VIOLAÇÃO DO §4.4
}
```

**Correto:** `player.InputQueue.Enqueue(new MoveInput(input));` — a `WalkAction` é chamada pelo `GameTickLoop`.

### 8.8 Persistência de configuração de jogo fora do banco

```csharp
// PROIBIDO
public static class GameConstants
{
    public const int BasePhysicalDamage = 50; // VIOLAÇÃO DO P3
}
```

**Correto:** ler de `GameConfiguration.Items` / `SkillDefinition` / `MonsterDefinition` carregados do banco.

### 8.9 Manter suporte ao protocolo legado em novo código

```csharp
// PROIBIDO — código novo nunca deve condicionar por versão de protocolo legado
if (player.ClientVersion.Season < 6)
{
    // lógica para protocolo antigo
}
```

**Correto:** todo código novo assume protocolo Protobuf. Não existem branches de versão de protocolo legado no código novo.

### 8.10 Acessar `Item.Definition` sem verificar null

```csharp
// PROIBIDO
var name = item.Definition.Name.ToString(); // NullReferenceException em potencial
```

**Correto:** `var name = item.Definition?.Name.ToString() ?? string.Empty;` — ou assert explícito com Exception descritiva em contextos onde null é programaticamente impossível.

---

## 9. Inventário de Mudanças

### 9.1 O que é mantido (intocável nesta fase)

| Componente | Localização | Motivo |
|---|---|---|
| Camada de persistência | `src/Persistence/` | Banco de dados é a fonte de verdade |
| Entidades do DataModel | `src/DataModel/` | Contratos de dados maduros e estáveis |
| Seed de itens (Initializers C#) | `src/Persistence/Initialization/` | Funciona corretamente; mudanças via UpdatePlugIn |
| `DefaultDropGenerator` | `src/GameLogic/DefaultDropGenerator.cs` | Lógica de drop correta e testada |
| Opções excellent (`ExcellentOptions`) | `src/Persistence/Initialization/Items/ExcellentOptions.cs` | Data-driven, não precisa de alteração |
| Joias como PlugIns (Bless, Soul, Life) | `src/GameLogic/PlayerActions/ItemConsumeActions/` | Arquitetura correta de plugin |
| Cálculo de dano (crit, excellent, duplo) | `src/GameLogic/MagicEffectActions/`, `HitAction` | Fórmulas validadas |
| Sistema de mapas e instâncias | `src/GameLogic/GameMap.cs`, `GameMapDefinition` | Instanciação por mapa está correta |
| `PlugInManager` | `src/PlugIns/PlugInManager.cs` | Mecanismo de extensão correto |
| `PlayerState` state machine | `src/GameLogic/PlayerState.cs` | Transições de estado corretas |
| `MagicEffect` + `AttributeSystem` | `src/AttributeSystem/`, `src/DataModel/Configuration/` | Sistema de atributos completo |
| `BucketMap<T>` + `Bucket<T>` | `src/GameLogic/BucketMap.cs`, `Bucket.cs` | Estrutura base de AoI (ajustes permitidos) |
| `ILoginServer` in-memory | `src/GameServer/LoginServer.cs` | Mantida agora; interface preparada para migração |

### 9.2 O que é removido

| Componente | Localização | Substituto |
|---|---|---|
| Protocolo binário legado (SimpleModulus) | `src/Network/SimpleModulus/` | TLS 1.3 |
| PacketTwister | `src/Network/PacketTwister/` | Removido sem substituto |
| XOR-3 (campo de senha) | `src/Network/Xor/` | TLS 1.3 |
| Structs de pacotes gerados | `src/Network/Packets/` | Mensagens Protobuf |
| Admin panel Blazor | `src/AdminPanel/` | Sem substituto nesta fase |
| Sistema de frutas | `src/GameLogic/PlayerActions/Character/AddMasterPointAction.cs` (parcial) | Removido |
| Sistema de reset de personagem | `src/Persistence/Initialization/Updates/` (entradas relacionadas) | Removido |
| `ClientVersionResolver` | `src/GameServer/ClientVersionResolver.cs` | Sem versões de cliente a resolver |
| Multi-client por IP além do limite | — | Rate limiting em `WebSocketConnection` |
| Timers individuais de AI | `BasicMonsterIntelligence._aiTimer` | `IGameTickable` no `GameTickLoop` |

### 9.3 O que é substituído

| De | Para | Arquivo de origem | Arquivo alvo |
|---|---|---|---|
| `Connection` (TCP) | `WebSocketConnection` | `src/Network/Connection.cs` | `src/Network/WebSocketConnection.cs` (novo) |
| Protocolo binário | Protobuf | `src/Network/Packets/**` | `src/Network/Protobuf/**` (novo) |
| `Walker` (Task.Delay) | `ContinuousWalker` (delta time) | `src/GameLogic/Walker.cs` | `src/GameLogic/ContinuousWalker.cs` (novo) |
| `Point` como posição de entidade | `Vector2F` (contínuo) | `src/Pathfinding/Point.cs` | `src/GameLogic/Vector2F.cs` (novo); `Point` mantido para tile/colisão |
| Cooldown client-side | Cooldown server-side em `SkillAction` | `src/GameLogic/PlayerActions/Skills/` | Mesmo arquivo, lógica adicionada |
| AoI event-driven puro | AoI integrado ao tick | `src/GameLogic/BucketAreaOfInterestManager.cs` | Mesmo arquivo, método de tick adicionado |
| Timers por subsistema | `GameTickLoop` | Múltiplos | `src/GameLogic/GameTickLoop.cs` (novo) |

### 9.4 O que é criado do zero

| Componente | Localização | Descrição |
|---|---|---|
| `GameTickLoop` | `src/GameLogic/GameTickLoop.cs` | IHostedService por instância de mapa; 30Hz padrão |
| `IGameTickable` | `src/GameLogic/IGameTickable.cs` | Interface para entidades tickáveis |
| `IPlayerInput` + implementações | `src/GameLogic/Inputs/` | `MoveInput`, `CastSkillInput`, `PickUpItemInput`, etc. |
| `IPlayerInputHandler` | `src/GameLogic/IPlayerInputHandler.cs` | Processa inputs do buffer dentro do tick |
| `ContinuousWalker` | `src/GameLogic/ContinuousWalker.cs` | Movimento por integração de velocidade com delta time |
| `Vector2F` | `src/GameLogic/Vector2F.cs` | Posição contínua + conversão para/de `Point` |
| `LineOfSight` | `src/GameLogic/LineOfSight.cs` | Bresenham sobre `WalkMap[,]` |
| `WebSocketConnection` | `src/Network/WebSocketConnection.cs` | Implementa `IConnection` sobre WebSocket |
| `SnapshotBroadcaster` | `src/GameLogic/SnapshotBroadcaster.cs` | Delta compression por player; chamado a cada tick |
| `Projectile` | `src/GameLogic/Projectile.cs` | `ILocateable` + `IGameTickable` para skillshots |
| Mensagens `.proto` | `src/Network/Protobuf/` | C2S e S2C para todos os domínios |
| ViewPlugIns Protobuf | `src/GameServer/RemoteView/Protobuf/` | Implementações dos IViewPlugIn para Protobuf |
| PacketHandlers Protobuf | `src/GameServer/MessageHandler/Protobuf/` | Handlers para mensagens C2S em Protobuf |
| `RateLimiter` | `src/Network/RateLimiter.cs` | Por conexão; verificado antes do enqueue no InputQueue |
| Cast time + skill queue | `src/GameLogic/PlayerActions/Skills/SkillCastQueue.cs` | Queue de cast com interrupt por dano |
