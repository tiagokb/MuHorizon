using System.Buffers.Binary;
using System.Net.WebSockets;
using Google.Protobuf;
using MUnique.OpenMU.Network.Protobuf;

// Usage: WsTestClient [url] [username] [password]
// Defaults: ws://localhost:5050/ws/  test0  test0
var url = args.Length > 0 ? args[0] : "ws://localhost:5050/ws/";
var username = args.Length > 1 ? args[1] : "test0";
var password = args.Length > 2 ? args[2] : "test0";

Console.WriteLine($"Target : {url}");
Console.WriteLine($"Account: {username}");
Console.WriteLine();

using var ws = new ClientWebSocket();

// ── Connect ───────────────────────────────────────────────────────────────────
Console.Write("[1] Connecting... ");
try
{
    await ws.ConnectAsync(new Uri(url), CancellationToken.None);
    Console.WriteLine("OK");
}
catch (Exception ex)
{
    Console.WriteLine($"FAIL: {ex.Message}");
    return 1;
}

// ── Ping ──────────────────────────────────────────────────────────────────────
Console.Write("[2] Ping... ");
var pingResult = await RoundTripAsync(ws, new ClientEnvelope { PingRequest = new PingRequest { TimestampMs = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() } });
if (pingResult?.PayloadCase != ServerEnvelope.PayloadOneofCase.PingResponse) { Console.WriteLine("FAIL (no pong)"); return 1; }
Console.WriteLine($"OK  RTT={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - (long)pingResult.PingResponse.ClientTimestampMs}ms");

// ── Login ─────────────────────────────────────────────────────────────────────
Console.Write("[3] Login... ");
var loginResult = await RoundTripAsync(ws, new ClientEnvelope { LoginRequest = new C2SLoginRequest { Username = username, Password = password } });
if (loginResult?.PayloadCase != ServerEnvelope.PayloadOneofCase.LoginResult) { Console.WriteLine("FAIL (no response)"); return 1; }
var loginCode = loginResult.LoginResult.Code;
Console.WriteLine($"{loginCode}");
if (loginCode != LoginResultCode.Ok) { return 1; }

// ── Character list ────────────────────────────────────────────────────────────
Console.Write("[4] Character list... ");
var listResult = await RoundTripAsync(ws, new ClientEnvelope { CharacterListRequest = new C2SCharacterListRequest() });
if (listResult?.PayloadCase != ServerEnvelope.PayloadOneofCase.CharacterList) { Console.WriteLine("FAIL (no character list)"); return 1; }
var characters = listResult.CharacterList.Characters;
Console.WriteLine($"OK  ({characters.Count} character(s))");
foreach (var c in characters)
{
    Console.WriteLine($"      slot={c.Slot} name={c.Name} level={c.Level} class={c.ClassName}");
}

if (characters.Count == 0)
{
    // ── Create character ──────────────────────────────────────────────────────
    Console.Write("[5] Create character 'TestHero'... ");
    var createResult = await RoundTripAsync(ws, new ClientEnvelope
    {
        CreateCharacter = new C2SCreateCharacter { Name = "TestHero", ClassNumber = 0 },
    });
    if (createResult?.PayloadCase != ServerEnvelope.PayloadOneofCase.CreateCharacterResult) { Console.WriteLine("FAIL (no response)"); return 1; }
    var cr = createResult.CreateCharacterResult;
    Console.WriteLine(cr.Success ? $"OK  name={cr.Character.Name}" : "FAIL (server rejected)");
    if (!cr.Success) { return 1; }

    // refresh list
    listResult = await RoundTripAsync(ws, new ClientEnvelope { CharacterListRequest = new C2SCharacterListRequest() });
    characters = listResult?.CharacterList?.Characters ?? new Google.Protobuf.Collections.RepeatedField<CharacterInfo>();
}

// ── Select character ──────────────────────────────────────────────────────────
var first = characters[0];
Console.Write($"[6] Select '{first.Name}'... ");
var selectResult = await RoundTripAsync(ws, new ClientEnvelope { SelectCharacter = new C2SSelectCharacter { Name = first.Name } });
if (selectResult?.PayloadCase != ServerEnvelope.PayloadOneofCase.SelectCharacterResult) { Console.WriteLine("FAIL (no response)"); return 1; }
var sr = selectResult.SelectCharacterResult;
Console.WriteLine(sr.Success ? $"OK  level={sr.Character.Level} class={sr.Character.ClassName}" : "FAIL");
if (!sr.Success) { return 1; }

Console.WriteLine();
Console.WriteLine("ALL PASS");

await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None);
return 0;

// ── helpers ───────────────────────────────────────────────────────────────────

static async Task<ServerEnvelope?> RoundTripAsync(ClientWebSocket ws, ClientEnvelope request)
{
    var payload = request.ToByteArray();
    var frame = new byte[4 + payload.Length];
    BinaryPrimitives.WriteUInt32BigEndian(frame, (uint)payload.Length);
    payload.CopyTo(frame, 4);

    using var sendCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
    await ws.SendAsync(frame, WebSocketMessageType.Binary, true, sendCts.Token);

    var buf = new byte[65536];
    var ms = new MemoryStream();
    using var recvCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    WebSocketReceiveResult result;
    do
    {
        result = await ws.ReceiveAsync(buf, recvCts.Token);
        ms.Write(buf, 0, result.Count);
    } while (!result.EndOfMessage);

    var received = ms.ToArray();
    if (received.Length < 4) return null;

    var len = (int)BinaryPrimitives.ReadUInt32BigEndian(received);
    if (received.Length < 4 + len) return null;

    return ServerEnvelope.Parser.ParseFrom(received, 4, len);
}
