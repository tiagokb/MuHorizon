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
- [ ] **Pivot de armadura no drop** — GLBs de armadura (grupos 7–11) têm pivot nos pés do personagem (origem do BMD do char), então ao cair no chão o modelo fica deslocado do label. A correção correta é no `BmdGltfExporter`: centralizar o pivot no bounding box da geometria exportada antes de gravar o GLB. **Adiar para quando implementar o sistema de equipamentos no personagem** — lá o pivot correto é pré-requisito para encaixe nos bones, então o fix vai ocorrer naturalmente.

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

### Contexto

O sistema BMD de MU usa bones nomeados como attachment points (ex: `RightHand`, `LeftHand`, `Helmet`, `Armor`, `Pants`, `Gloves`, `Boots`). Ao equipar um item, o GLB do item é inserido como filho do bone correspondente do esqueleto do personagem.

### Itens

- [ ] **Mapeamento de bones** — listar os nomes de bone de attachment usados nos BMDs de personagem DW/DK/Elf; documentar em `EntityConfig` ou arquivo de dados.
- [ ] **Socket de weapon** — ao equipar arma na mão direita, instanciar o GLB do item como filho do bone `RightHand` do `AnimationPlayer`; ao desequipar, remover.
- [ ] **Socket de armadura** — helmet, armor, pants, gloves, boots: cada peça substitui a mesh padrão ou é sobreposta ao bone correspondente. MU usa substituição de mesh, não sobreposição — confirmar nos BMDs.
- [ ] **Trigger de atualização** — `GameState.InventoryChanged` → reler slots 0–11 (equipamento) → recarregar sockets.
- [ ] **Itens de dois slots** — armas de duas mãos ocupam `RightHand` + `LeftHand`; garantir que desequipar limpa ambos.

---

## Outros personagens / entidades

- [ ] **Equipamento de outros players** — `S2CEntitiesInScope` envia `ClassNumber` mas não `equipment[]`. Decidir: enviar lista de itens equipados no scope packet para renderizar outros players com seus equipamentos visíveis.
- [ ] **Monstros com GLB correto** — garantir que todos os monstros visíveis no Lorencia têm GLB exportado e estão no `EntityConfig.AnimSet`.

---

## Notas de arquitetura

- Todos os GLBs de item serão carregados on-demand via `GD.Load<PackedScene>($"res://assets/items/{group}_{number}.glb")` — não pré-carregar todos na startup.
- O `MuAssetConverter` precisa de uma nova flag `--items` que processa a pasta `Item/` e exporta GLB + sprite PNG para cada item.
- A prioridade é: drop no chão → inventory icon → equipment no personagem (essa ordem maximiza impacto visual com menor esforço).
