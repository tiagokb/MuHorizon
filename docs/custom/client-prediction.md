# Client-Side Prediction — Arquitetura para o Client Godot 4

> Documento normativo. Deve ser lido e aprovado **antes** de qualquer código de movimento no client Godot.  
> Modelo de referência: League of Legends (input imediato, servidor autoritativo, correção suave).  
> Integra com: `09-implementation-plan.md` Fase 3, `08-refactoring-rules.md` seção Tick Loop.

**Idioma:** Português  
**Escala de coordenadas:** 1 tile = 8 unidades; range [0.0, 2040.0]; ver Seção 2 para conversões.

---

## Sumário

1. [Princípios do modelo](#1-princípios-do-modelo)
2. [Coordenadas e escala](#2-coordenadas-e-escala)
3. [Arquitetura de estados no client](#3-arquitetura-de-estados-no-client)
4. [Fluxo por frame](#4-fluxo-por-frame)
5. [Numeração de sequência (sequence numbers)](#5-numeração-de-sequência-sequence-numbers)
6. [Input buffering](#6-input-buffering)
7. [Reconciliação e correção suave](#7-reconciliação-e-correção-suave)
8. [Entidades remotas (outros jogadores e mobs)](#8-entidades-remotas-outros-jogadores-e-mobs)
9. [Protocolo de movimento (Protobuf)](#9-protocolo-de-movimento-protobuf)
10. [Integração com o tick loop do servidor (30 Hz)](#10-integração-com-o-tick-loop-do-servidor-30-hz)
11. [Anti-patterns proibidos](#11-anti-patterns-proibidos)
12. [Glossário](#12-glossário)

---

## 1. Princípios do modelo

### P1 — Input imediato, sem lag artificial
O personagem local **inicia o movimento imediatamente** quando o jogador clica em um ponto do mapa, sem aguardar confirmação do servidor. O servidor confirma ou corrige depois. O jogo é top-down point-and-click — não há movimento contínuo por direção (WASD).

### P2 — Servidor é autoritativo em tudo
Posição, colisão, alcance de skill, dano — tudo é calculado no servidor. O client nunca decide "acertei o inimigo". O client só decide "onde mostro meu personagem agora enquanto espero a confirmação".

### P3 — Sem turn rate
Não há rotação gradual. O personagem muda de direção instantaneamente. Isso simplifica a predição e elimina a classe de erros "o server recebeu o input antes da rotação completar".

### P4 — Correção suave, sem rubber-band visível
Quando o servidor corrige a posição, o client **interpola** suavemente até a posição correta em até 150ms. Saltos bruscos (rubber-band) são proibidos, exceto quando a divergência supera o limite de pânico (> 3 tiles = > 24 unidades).

### P5 — Input buffering durante animação de cast
Enquanto uma animação de cast não cancelável estiver tocando, inputs de movimento são guardados em buffer e executados ao término da animação. O buffer tem capacidade máxima de 3 inputs; o mais antigo é descartado se cheio.

### P6 — Sem extrapolação de entidades remotas
Outros jogadores e mobs são movidos por **interpolação** entre o último snapshot recebido e o snapshot anterior — nunca por extrapolação. Isso evita artefatos quando o servidor diverge da trajetória extrapolada. A consequência é que entidades remotas aparecem com ~33ms de atraso relativo ao server.

---

## 2. Coordenadas e escala

### Referência canônica

```
1 tile  = 8 unidades no espaço do client
Mapa máximo: 255 tiles × 255 tiles = 2040 × 2040 unidades
Range válido: [0.0, 2040.0] em X e Y
Centro de tile: tile T → posição T * 8f + 4f
```

### Conversões (replicar exatamente do servidor)

```csharp
// Client (GDScript/C# Godot)
static byte ToTile(float pos) => (byte)(pos / 8f);
static float FromTile(byte tile) => tile * 8f + 4f; // centro do tile
```

```gdscript
# GDScript equivalente
static func to_tile(pos: float) -> int:
    return int(pos / 8.0)

static func from_tile(tile: int) -> float:
    return tile * 8.0 + 4.0
```

> **Nunca** usar posição em tiles diretamente como coordenada Godot. A coordenada Godot é sempre em unidades (float). Os tiles são usados exclusivamente para colisão e comunicação com o servidor.

### Mapeamento para Godot 2D (se usando câmera top-down)

O espaço Godot pode usar `position` diretamente com estas unidades. 1 unidade Godot = 1 unidade do sistema de coordenadas acima. Não aplicar fator de zoom ou escala adicional no nó raiz — ajustar o zoom da câmera conforme desejado visualmente.

---

## 3. Arquitetura de estados no client

O client mantém **quatro representações** do estado de movimento do personagem local:

```
┌─────────────────────────────────────────────────────────┐
│              Estado do Personagem Local                  │
│                                                         │
│  predicted_pos  : Vector2    ← onde o client acha que  │
│                                 está (renderizado)      │
│                                                         │
│  destination    : Vector2?   ← ponto clicado atual;    │
│                                 null = personagem parado│
│                                                         │
│  server_pos     : Vector2    ← última posição confirmada│
│                                 pelo servidor           │
│                                                         │
│  pending_inputs : List<Input>← inputs enviados mas      │
│                                 não confirmados         │
└─────────────────────────────────────────────────────────┘
```

### Ciclo de vida dos estados

1. Clique no mapa → define `destination` → armazena em `pending_inputs` → envia `C2SMoveRequest` → client começa a mover `predicted_pos` em direção ao destino
2. Personagem chega ao destino (`distance(predicted_pos, destination) < stop_threshold`) → `destination = null` → personagem para sozinho, sem input de "stop"
3. Novo clique → substitui `destination` anterior → o input mais recente em `pending_inputs` passa a ser o destino ativo
4. Snapshot chega → atualiza `server_pos` → simular posição reconciliada a partir de `server_pos` em direção ao destino do último pending input → resultado é o novo `predicted_pos`
5. Se novo `predicted_pos` diverge do atual → corrigir suavemente (ver Seção 7)

---

## 4. Fluxo por frame

```
_process(delta):
│
├─ 1. Detectar clique no mapa (Input.is_action_just_pressed("click"))
│      Se animação não cancelável: buffer o destino clicado, sair
│      Se buffer cheio (>3): descartar o mais antigo
│      Senão:
│          destination = ponto_clicado
│          sequence_number++
│          pending_inputs.append(PendingInput{seq=sequence_number,
│                                            destination=destination,
│                                            timestamp=Time.get_ticks_msec()})
│          enviar C2SMoveRequest{sequence_number, dest_x, dest_y,
│                                hint_pos_x=predicted_pos.x, hint_pos_y=predicted_pos.y}
│
├─ 2. Se destination != null:
│      var dir = (destination - predicted_pos).normalized()
│      predicted_pos += dir * MOVEMENT_SPEED * delta
│      Se distance(predicted_pos, destination) < STOP_THRESHOLD:
│          predicted_pos = destination   ← snap final para precisão
│          destination = null            ← personagem parou sozinho
│      Interpolar suavemente se correção pendente (ver Seção 7)
│
├─ 3. Renderizar personagem em predicted_pos
│
└─ 4. Interpolar entidades remotas entre snapshots recebidos
```

```gdscript
const MOVEMENT_SPEED  := 80.0  # unidades por segundo (10 tiles/s; 1 tile = 8 unidades)
const STOP_THRESHOLD  :=  0.5  # distância mínima para considerar "chegou"
```

> **Nota:** O client **não** executa colisão completa — apenas verifica os limites `[0, 2040]`. A colisão real (WalkMap) é autoritativa no servidor. Isso significa que o client pode visualmente atravessar uma parede por 33ms antes da correção chegar. Esse trade-off é aceitável para o modelo LoL.

---

## 5. Numeração de sequência (sequence numbers)

### Regras

- `sequence_number` é um `uint32` incrementado a cada input enviado
- Começa em `1` ao entrar no mapa (não em `0`, para distinguir de "não inicializado")
- Overflow em `uint32.MaxValue` → volta para `1` (não para `0`)
- O servidor ecoa o `sequence_number` do último input processado em cada snapshot

### Uso na reconciliação

Ao receber um snapshot com `last_processed_seq = N`:
1. Remover de `pending_inputs` todos os inputs com `seq ≤ N`
2. Partir de `server_pos` e reaplicar todos os inputs restantes em ordem
3. O resultado é o novo `predicted_pos` calculado

### Estrutura de um input pendente

```csharp
public record struct PendingInput(
    uint Seq,
    Vector2 Destination,   // ponto de destino do clique (em unidades, não tiles)
    ulong Timestamp        // Time.GetTicksMsec() no momento do envio
);
```

---

## 6. Input buffering

### Quando bufferizar

| Situação | Comportamento |
|---|---|
| Animação de cast não cancelável ativa | Buffer o input; executar ao término |
| Canal de rede indisponível | Buffer o input; reenviar quando conectar |
| Jitter: servidor não respondeu nos últimos 5 ticks | Executar localmente; manter em `pending_inputs` |

### Capacidade do buffer

- Máximo de **3 inputs** em buffer de animação
- Máximo de **60 inputs** em `pending_inputs` (≈ 2 segundos a 30Hz)
- Se `pending_inputs` superar 60 → desconectar e exibir "Conexão perdida"

### Execução do buffer de animação

```gdscript
func _on_animation_finished(anim_name: String) -> void:
    if anim_buffer.size() > 0:
        var buffered = anim_buffer.pop_front()
        apply_input(buffered)
```

---

## 7. Reconciliação e correção suave

### Algoritmo completo

```gdscript
func on_snapshot_received(snapshot: S2CSnapshot) -> void:
    # 1. Atualizar posição autoritativa
    server_pos = snapshot.position

    # 2. Remover inputs já processados
    pending_inputs = pending_inputs.filter(
        func(i): return i.seq > snapshot.last_processed_seq
    )

    # 3. Simular posição reconciliada
    #    No modelo point-and-click, o personagem move-se em direção ao destino
    #    do clique mais recente ainda não confirmado. Simular a partir de server_pos.
    var reconciled := server_pos
    if pending_inputs.size() > 0:
        var active_dest: Vector2 = pending_inputs.back().destination
        var elapsed_s := (Time.get_ticks_msec() - snapshot.timestamp_ms) / 1000.0
        reconciled = simulate_move_toward(server_pos, active_dest, elapsed_s)

    # 4. Calcular divergência
    var error := reconciled.distance_to(predicted_pos)

    # 5. Limite de pânico (> 3 tiles = > 24 unidades)
    if error > 24.0:
        predicted_pos = reconciled  # teleporte imediato
        destination = reconciled if pending_inputs.size() > 0 else null
        correction_target = null
        return

    # 6. Divergência tolerável → corrigir suavemente
    if error > 0.5:  # tolerância mínima (< 0.5 unidades: ignorar)
        correction_target = reconciled
        correction_start = predicted_pos
        correction_elapsed = 0.0

func _process(delta: float) -> void:
    if correction_target != null:
        correction_elapsed += delta
        var t := clampf(correction_elapsed / CORRECTION_DURATION, 0.0, 1.0)
        predicted_pos = correction_start.lerp(correction_target, t)
        if t >= 1.0:
            correction_target = null

func simulate_move_toward(from: Vector2, to: Vector2, elapsed: float) -> Vector2:
    var dist := from.distance_to(to)
    var max_move := MOVEMENT_SPEED * elapsed
    if dist <= max_move:
        return to  # chegaria ao destino dentro do tempo
    return from + (to - from).normalized() * max_move
```

### Constantes

```gdscript
const CORRECTION_DURATION := 0.15   # 150ms para completar a correção
const PANIC_THRESHOLD     := 24.0   # > 3 tiles: teleporte imediato
const MIN_ERROR_THRESHOLD :=  0.5   # < 0.5 unidades: ignorar divergência
```

### Por que 150ms

- A 30Hz, o RTT típico de LAN é 1–2 ticks (33–66ms)
- 150ms = ~4.5 ticks → tempo suficiente para suavizar correções de até 2–3 ticks de atraso
- Acima de 150ms a correção começa a parecer "escorregadia"; abaixo de 50ms começa a parecer rubber-band

---

## 8. Entidades remotas (outros jogadores e mobs)

### Interpolação entre snapshots

Entidades remotas **não** têm predição. Elas são renderizadas interpolando entre os dois últimos snapshots recebidos:

```gdscript
var snap_old: RemoteSnapshot  # snapshot anterior
var snap_new: RemoteSnapshot  # snapshot mais recente

func _process(delta: float) -> void:
    interp_time += delta
    var duration := (snap_new.timestamp - snap_old.timestamp) / 1000.0
    var t := clampf(interp_time / duration, 0.0, 1.0)
    position = snap_old.pos.lerp(snap_new.pos, t)

func on_new_snapshot(snap: RemoteSnapshot) -> void:
    snap_old = snap_new
    snap_new = snap
    interp_time = 0.0
```

### Lag de renderização de entidades remotas

Entidades remotas aparecem com **33ms de atraso** relativo ao servidor (1 tick). Isso é intencional — é o custo de não extrapolar. Para skillshots que requerem lead (mira à frente do alvo), o servidor aplica o lag de rede de ambos os jogadores no cálculo de hit.

### Entidades com animações

Para animações de ataque, morte, etc.: o servidor envia um `S2CEntityEvent` com o tipo do evento. O client inicia a animação localmente. A posição continua sendo controlada pela interpolação de snapshots.

---

## 9. Protocolo de movimento (Protobuf)

### C2S: Input do jogador

```protobuf
// movement.proto

message C2SMoveRequest {
    uint32 sequence_number = 1;  // incrementado a cada clique
    float dest_x           = 2;  // ponto de destino onde o jogador clicou (em unidades, não tiles)
    float dest_y           = 3;
    float hint_pos_x       = 4;  // posição atual predita pelo client (hint para o servidor)
    float hint_pos_y       = 5;
}
```

> `dest_x` e `dest_y` são as coordenadas absolutas do ponto clicado no mapa, em unidades (escala 1:8). O servidor recebe o destino, calcula o caminho via pathfinding e move o personagem tick a tick até lá. O personagem **para sozinho** ao atingir o destino — não há input de "stop". Um novo clique substitui o destino anterior.

### S2C: Snapshot de posição

```protobuf
message S2CEntityDelta {
    uint32 object_id        = 1;
    float pos_x             = 2;  // posição em unidades (não tiles)
    float pos_y             = 3;
    float vel_x             = 4;  // velocidade atual
    float vel_y             = 5;
    uint32 last_processed_seq = 6; // apenas para o jogador local; 0 para entidades remotas
}

message S2CSnapshot {
    uint64 server_tick      = 1;
    uint64 timestamp_ms     = 2;  // Time.GetTicksMsec() no servidor
    repeated S2CEntityDelta entities = 3;
}
```

### Framing (4-byte length prefix)

Todos os pacotes Protobuf no canal WebSocket usam framing com 4 bytes de comprimento em big-endian:

```
[uint32 length][bytes payload]
```

O comprimento inclui apenas o payload Protobuf, não os 4 bytes de length.

---

## 10. Integração com o tick loop do servidor (30 Hz)

### Janela de processamento por tick

```
Tick N (33ms):
│
├── ProcessInputs
│   ├── Consumir C2SMoveRequest da fila do jogador
│   ├── Validar seq: seq deve ser > último processado
│   ├── Definir novo destino no ContinuousWalker via SetTarget(dest_x, dest_y)
│   └── Registrar last_processed_seq = seq do input processado
│
├── TickEntities
│   ├── Mover todos os jogadores em direção ao seu destino atual (ContinuousWalker)
│   ├── Verificar colisão com WalkMap (tile = (byte)(pos / 8f))
│   ├── Parar automaticamente ao atingir o destino
│   └── Clamp [0, 2040]
│
└── BroadcastSnapshots
    ├── Para cada jogador: montar S2CSnapshot com entidades no AoI
    ├── Incluir last_processed_seq no S2CEntityDelta do próprio jogador
    └── Enviar via WebSocket (plain WS → nginx → WSS)
```

### Múltiplos inputs no mesmo tick

Se o cliente enviou mais de um `C2SMoveRequest` entre dois ticks:
- O servidor processa apenas o **último** recebido no tick — um novo clique substitui o destino anterior; não há acumulação de destinos
- O `last_processed_seq` no snapshot reflete o seq do input processado
- O cliente remove da `pending_inputs` tudo com `seq ≤ last_processed_seq`

Isso implica que a 30Hz, o cliente pode enviar cliques a qualquer taxa — o servidor amostra uma vez por tick e usa o destino mais recente.

### Sem client timestamp no servidor

O servidor **não usa** `timestamp_ms` do client para nada gameplay-crítico. O timestamp é enviado como hint para debug e métricas de RTT, mas a física é baseada exclusivamente no `deltaTime` do tick (33ms fixos).

---

## 11. Anti-patterns proibidos

| Anti-pattern | Por que é proibido | Alternativa correta |
|---|---|---|
| Mover entidade remota por extrapolação | Quando servidor diverge, o artefato visual é pior que o atraso | Interpolação entre dois snapshots |
| Confiar na posição `hint_pos` enviada pelo cliente como definitiva | Permite wall-hacking e teleporte | Servidor recalcula posição via pathfinding a partir do destino; `hint_pos` é apenas para debug/métricas |
| Enviar direção normalizada contínua (WASD) em jogo point-and-click | Impede que o personagem pare sozinho; quebra kiting e posicionamento preciso | Enviar destino absoluto (`dest_x`, `dest_y`) — o ponto clicado no mapa |
| Não zerar `destination` ao chegar no destino | Personagem continua tentando chegar ao destino após parar; vibração no ponto final | Verificar `distance(predicted_pos, destination) < STOP_THRESHOLD` a cada frame; zerar quando atingido |
| Ignorar `last_processed_seq` e simular a partir do inicio | Acumula erro indefinidamente | Remover inputs confirmados antes de simular a partir de `server_pos` |
| Teleportar para posição corrigida imediatamente (rubber-band) | Experiência visual ruim; desorientante | Interpolar em 150ms; usar pânico apenas para erro > 3 tiles |
| Aplicar cooldown de skill no client | Permite spam de skills | Cooldown é verificado no servidor; client pode mostrar UI informativa |
| Usar colisão completa no client | Complexidade de sync com WalkMap do servidor | Client verifica apenas limites `[0, 2040]`; WalkMap é autoritativa no servidor |
| Corrigir posição sem simular a partir de `server_pos` em direção ao destino | Desfaz movimento legítimo ainda não confirmado | Sempre usar `simulate_move_toward(server_pos, active_dest, elapsed)` após receber snapshot |
| Descartar snapshots fora de ordem | Ignora informação útil em redes com reordenamento | Usar `server_tick` para ordenar; descartar apenas snapshots **mais antigos** que o último aplicado |

---

## 12. Glossário

| Termo | Definição |
|---|---|
| **predicted_pos** | Posição renderizada do personagem local; resultado da predição client-side |
| **destination** | Ponto de destino do clique do jogador (Vector2 em unidades); `null` quando o personagem está parado |
| **server_pos** | Última posição autoritativa recebida do servidor via snapshot |
| **pending_inputs** | Lista de inputs (cliques) enviados mas ainda não confirmados pelo servidor |
| **sequence_number** | Contador uint32 incrementado a cada C2SMoveRequest enviado |
| **last_processed_seq** | Echo do servidor: seq do último input processado no tick |
| **reconciliação** | Processo de simular posição a partir de `server_pos` em direção ao destino ativo, para calcular `predicted_pos` |
| **simulate_move_toward** | Função que calcula onde o personagem estaria se tivesse se movido de `from` em direção a `to` por `elapsed` segundos |
| **correção suave** | Interpolação de 150ms entre predicted_pos atual e posição reconciliada |
| **limite de pânico** | Divergência > 24 unidades (> 3 tiles): teleporte imediato sem interpolação |
| **stop_threshold** | Distância mínima (0.5 unidades) até o destino abaixo da qual o personagem é considerado "chegou" e para |
| **interpolação de entidades remotas** | Renderização de outros jogadores/mobs entre dois snapshots consecutivos |
| **input buffering** | Armazenamento temporário de cliques durante animação não cancelável |
| **tick** | Uma iteração do GameTickLoop do servidor; duração = 33ms (30Hz) |
| **RTT** | Round-trip time: tempo entre envio do clique e recebimento do snapshot que o confirma |
| **tile** | Unidade de grid do servidor; 1 tile = 8 unidades no espaço de coordenadas |
