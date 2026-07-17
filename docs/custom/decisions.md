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
