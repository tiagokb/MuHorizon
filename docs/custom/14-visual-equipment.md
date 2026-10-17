# Visual & Equipment System

Backlog de melhorias visuais do mundo e do sistema de equipamentos.
Itens com `[ ]` pendentes, `[x]` concluídos.

---

## World feel — escala e movimento

- [x] **Velocidade de movimento** — corrigido: `GameTickLoop` usava `MoveSpeedUnitsPerSec = 80f` (10 tiles/sec); agora usa `player.MovementSpeedServerUnitsPerSec` derivado da fórmula legada (base DW: 24 units/sec = 3 tiles/sec).
- [ ] **Sensação de lentidão** — 3 tiles/sec é correto para MU base, mas na perspectiva 3D parece estranho. Avaliar ajuste de câmera (distância/FOV) para compensar a percepção, ou aceitar e ajustar junto com o sistema de running boots que já aumenta para 3.75 tiles/sec.
- [x] **Animação de walk dessincronizada** — `EntityAnimator.SetWalkSpeedScale(tilesPerSec)` ajusta `SpeedScale = tilesPerSec / 10f` (referência: SpeedScale=1.0 foi calibrado a ~10 tiles/sec, o valor errado anterior). Velocidade medida por delta-posição/delta-tempo em `OwnPlayer.ApplySnapshot` e `EntityRegistry.UpdatePosition`.
- [ ] **Tamanho dos mobs inconsistente** — alguns mobs aparecem gigantes, outros minúsculos. Causa não confirmada; investigar se é problema no exporter BMD→GLB (escala do skeleton/mesh) ou se os BMDs originais já têm tamanhos heterogêneos sem fator de correção aplicado.
- [x] **Ajuste de câmera** — `WorldCamera`: `BaseDist=10` calibrado para o mundo real (204.8 Godot units); pitch-on-zoom (close=55° isométrico, far=65° top-down); `MinZoom=0.25`, `MaxZoom=1.7`; target elevado 0.9u para não cortar cabeça do personagem. Escala documentada no CLAUDE.md.
- [x] **Iluminação** — `ProceduralSkyMaterial` (céu azul/horizonte claro), `DirectionalLight3D` com sombras PSSM, fill light azul-frio 0.28 energy; tonemap Reinhard + glow sutil.

---

## World feel — ambiente e polimento visual

- [ ] **Grama** — billboards atuais são muito uniformes; melhorar densidade, variação de cor/tamanho e animação de vento (shader). Avaliar substituição por `MultiMesh` com rotação aleatória para performance.
- [ ] **Textura do terreno** — splatmap gerado, mas transições entre tiles podem ser mais suaves; avaliar triplanar mapping nos taludes.
- [ ] **Água / lagos** — Lorencia tem áreas de água no terrain; adicionar mesh de água com shader de animação e refração simples.
- [ ] **Partículas ambientes** — poeira, folhas, etc. para dar vida ao mundo.
- [ ] **Sombras de entidade no chão** — blob shadow simples embaixo de cada personagem/mob para ancoragem visual (substituto barato de shadow map por entidade).

---

## Itens no chão

- [x] **Modelo 3D do drop** — `SpawnDrop` tenta `res://assets/items/{group}_{number}.glb`; se existir, instancia o GLB; senão usa fallback colorido. Categorias: vermelho=arma (0-5), azul=armadura (6-11), roxo=wings (12), verde=misc.
- [x] **Fallback** — esfera colorida por `item_group` via `MakeDropOrb(ItemDropColor(group))`.
- [x] **Billboard label** — `Label3D` acima do drop com nome via `ItemDatabase.Get()` ou `"X Zen"` para dinheiro; outline para legibilidade.
- [x] **Escala por grupo** — `ItemDropConfig` (scripts/ItemDropConfig.cs): `BaseMultiplier` (knob global) × `GroupMultipliers[group]` × `EntityVisual.ModelScale`. Proporções derivadas de `ZzzInventory.cpp` (arma=1.0×, armadura~1.56×, wings=0.8×, poção=1.4×).
- [x] **Mapeamento GLB corrigido** — `ItemBmdMappings()` em `MuAssetConverter/Program.cs` reflete `ZzzOpenData.cpp OpenItems()` exatamente: itens 12/7–11 → `Gem01–05.bmd` (Orbs), 12/36–39 → `wing08–11.bmd` (tier-3 wings), 12/41–43 → `Wing42–44.bmd`. Scrolls, Jewels, gift boxes também mapeados.
- [ ] **Pivot de armadura no drop** — GLBs de armadura (grupos 7–11) têm pivot nos pés do personagem (origem do BMD do char), então ao cair no chão o modelo fica deslocado do label. A correção correta é no `BmdGltfExporter`: centralizar o pivot no bounding box da geometria exportada antes de gravar o GLB. Afeta só o drop: no personagem a armadura usa as peças skinned de `parts/` (não os GLBs de `items/`), então o equipamento não depende deste fix.

> **GLBs de drop gerados**: `muconv --items` exportou 340 GLBs para `assets/items/{group}_{number}.glb`. Mapeamento baseado em `ZzzOpenData.cpp OpenItems()`. Gaps no range são esperados — arquivos não existem no data set original.

---

## Ícones de item no inventário

### Como o client legado renderiza (ground truth)

O client legado **não usa sprites 2D nem render-to-texture**. Usa uma técnica OpenGL clássica:
- `RenderItem3D(sx, sy, Width, Height, Type, Level, ...)` — chamado por slot de inventário.
  - Calcula `Position` em view-space a partir do centro do slot + `MousePosition`.
- `RenderObjectScreen(modelId, level, ..., Position, ...)` — configura e desenha o modelo 3D.
  - Enorme if-else de `Scale` por tipo (faixa 0.001–0.007 em unidades OpenGL de view-space).
  - Enorme if-else de `ObjectSelect.Angle` (rotação preset por tipo, ex: espadas `{0, 270, 15}`, armaduras `{−90, 0, 0}`).
  - Chama `RenderPartObject(o, ...)` — draw OpenGL direto no framebuffer principal, injetando projeção 3D no meio do render 2D do HUD.
- **Sem SubViewport, sem offscreen buffer** — hack OpenGL puro: troca temporariamente a matriz de projeção para render 3D e volta para 2D.
- Funções-fonte: `ZzzInventory.cpp:10080` (`RenderItem3D`), `ZzzInventory.cpp:8031` (`RenderObjectScreen`).

### Abordagem Godot (quando implementar)

Em Godot 4, a injeção 3D-no-2D do legado não é diretamente replicável. A equivalência é um **SubViewport por slot visível** (ou um SubViewport compartilhado):

1. **SubViewport** com `Camera3D` olhando de cima-diagonal (isométrico) — ângulo ~(−45°, 0°, 45°) ou similar ao `ObjectSelect.Angle` padrão.
2. **Camera3D** usa `projection = Orthogonal`, `size` calibrado para que o item preencha bem o slot.
3. Instanciar o `{group}_{number}.glb` dentro do SubViewport, aplicar `ItemDropConfig.Scale(group)` e a rotação de inventário (derivada das seções de `Angle[]` em `RenderObjectScreen`).
4. Usar `ViewportTexture` do SubViewport como `TextureRect` no slot do inventário.
5. **Estratégia de atualização**: `update_mode = ONCE` — re-render só quando o inventário muda, não a cada frame. Com 32 slots visíveis isso é 32 SubViewports inativos a maior parte do tempo.
6. **Iluminação**: `WorldEnvironment` + luz branca direta dentro do SubViewport (`o->LightEnable = false` no legado = sem luz dinâmica, só albedo flat — replicar com `ShadingMode = Unshaded` ou `OmniLight3D` frontal neutro).

- [ ] **Ícones de inventário (SubViewport)** — implementar quando o inventário visual for prioridade.

---

## Equipamentos no personagem

**Status:** implementado (2026-10-16) — personagem modular, armadura por peça, armas/escudo/asas em sockets.

### Como o legado faz (ground truth)

- **Armadura não é anexada a bone** — cada peça (`HelmMale05.bmd`, `ArmorClass02.bmd`, ...) é uma malha *skinned*
  que usa os **índices de bone do `player.bmd`** (os nomes de bone da própria peça são diferentes e são ignorados:
  `b->Transform(o->BoneTransform, ...)` com os transforms do personagem). Por isso o export usa sempre o esqueleto do
  `player.bmd` com os vértices da peça.
- **Corpo padrão** = `{Helm,Armor,Pant,Glove,Boot}Class{NN}` da classe (`MODEL_BODY_* + SkinIndex`):
  `Class01..07` (DW, DK, Elf, MG, DL, Sum, RF), `Class2NN` 2ª classe, `Class3NN` 3ª classe. O "Helm" do corpo é a cabeça.
- **Item → BMD** (`ZzzOpenData.cpp OpenPlayers`): número `n` → `{Part}Male{n+1}`, exceto 10–14 → `{Part}Elf{n-9}`,
  29–33 → `HDK_*`, 34–38 → `CW_*`, e lacunas por set (sem elmo/luva em alguns). Remodelagens por classe:
  Summoner 10/11 → `*ElfC`, Rage Fighter 5/6/8/9 → `*Monk`, Dark Lord elmos 0/5/6/8/9 → `MaskHelmMale`.
- **Elmo aberto** (Bronze, Pad, Vine..Spirit, 63/65/68/70) desenha a cabeça da classe junto (`SetCharacterScale`).
- **Armas**: slot 0 → bone 33 `knife_gdf` (mão direita), slot 1 → bone 42 `hand_bofdgne01` (mão esquerda),
  sem rotação extra (o transform do bone raiz da arma já é o offset). Flechas/virotes não são desenhados.
- **Asas**: bone 47 `Bone05`, offset local (0,0,15) BMD → (0,15,0) Y-up; animam continuamente.

### Implementação

- **Conversor** — `muconv --player <Data/Player> <assets/models/Player>` substitui o antigo `--bake-player`:
  `player.glb` (esqueleto + 283 animações, sem malha — skin "solta" para o Godot criar o `Skeleton3D`) e
  `parts/{stem}.glb` (421 peças, cada uma skinned no esqueleto do player, sem animações).
  Os 4 GLBs assados (`player_class/male/elf/monk.glb`, ~20 MB cada, animações duplicadas) foram removidos.
- **Cliente** — `EquipmentConfig` (dados: item → stem, sockets, fallbacks) + `CharacterAppearance` (Node que troca
  as malhas no `Skeleton3D` e cria `BoneAttachment3D` para armas/asas). As skins das peças têm *binds nomeados*,
  então ligam no esqueleto do personagem por nome de bone. `SetEquipment` recebe o snapshot completo e só
  recarrega a camada que mudou.
- **Próprio jogador** — veste a partir de `GameState.Inventory` (slots 0–7) a cada `InventoryChanged`
  (o servidor não ecoa aparência para o dono).
- **Outros jogadores** — `EntityState.equipment` (field 11) no spawn e `S2CAppearanceChanged` (field 36, snapshot
  completo) via `ProtobufAppearanceChangedPlugIn : IAppearanceChangedPlugIn`.

### Itens

- [x] **Mapeamento de bones** — `EquipmentConfig.SocketFor` (`knife_gdf`, `hand_bofdgne01`, `Bone05`).
- [x] **Socket de weapon** — arma/escudo como filho de `BoneAttachment3D`; troca/remoção ao mudar o slot.
- [x] **Armadura** — substituição de malha por camada (Head, Helm, Armor, Pants, Gloves, Boots) com fallback para o corpo da classe.
- [x] **Trigger de atualização** — `GameState.InventoryChanged` → `OwnPlayer.ApplyEquipment`.
- [x] **Itens de dois slots** — não se aplica: arma de duas mãos ocupa só o slot 0 no MU; o slot 1 fica vazio no servidor.
- [ ] **Brilho por nível / excellent / ancient** — `level`, `excellent`, `ancient` já chegam no `EquipmentPiece`; falta o efeito (legado: `RenderPartObjectEffect`).
- [ ] **Asas com textura JPG** (ex.: asas de elfa `12_0`) renderizam com fundo preto — no legado são blend aditivo; ajustar material no exporter ou no cliente.
- [ ] **Itens nas costas em safezone** — o legado guarda arma/escudo nas costas dentro da cidade (`RenderCharacterBackItem`, `Link=true` + ângulos por tipo).
- [ ] **Escala de armas do Rage Fighter** (`IsRagefighterCommonWeapon`: offset + escala 0.9) e capas (Dark Lord/Fighter usam cloth physics).
- [ ] **Tamanho do repositório** — `parts/` tem ~200 MB porque cada GLB embute as próprias texturas; avaliar texturas externas compartilhadas ou Git LFS.

---

## Outros personagens / entidades

- [x] **Equipamento de outros players** — `EntityState.equipment[]` no scope + `S2CAppearanceChanged` em trocas.
- [ ] **Monstros com GLB correto** — garantir que todos os monstros visíveis no Lorencia têm GLB exportado e estão no `EntityConfig.AnimSet`.

---

## Notas de arquitetura

- Todos os GLBs de item serão carregados on-demand via `GD.Load<PackedScene>($"res://assets/items/{group}_{number}.glb")` — não pré-carregar todos na startup.
- O `MuAssetConverter` precisa de uma nova flag `--items` que processa a pasta `Item/` e exporta GLB + sprite PNG para cada item.
- A prioridade é: drop no chão → inventory icon → equipment no personagem (essa ordem maximiza impacto visual com menor esforço).
