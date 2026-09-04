# Visual & Equipment System

Backlog de melhorias visuais do mundo e do sistema de equipamentos.
Itens com `[ ]` pendentes, `[x]` concluídos.

---

## World feel — escala e movimento

- [x] **Velocidade de movimento** — corrigido: `GameTickLoop` usava `MoveSpeedUnitsPerSec = 80f` (10 tiles/sec); agora usa `player.MovementSpeedServerUnitsPerSec` derivado da fórmula legada (base DW: 24 units/sec = 3 tiles/sec).
- [ ] **Sensação de lentidão** — 3 tiles/sec é correto para MU base, mas na perspectiva 3D parece estranho. Avaliar ajuste de câmera (distância/FOV) para compensar a percepção, ou aceitar e ajustar junto com o sistema de running boots que já aumenta para 3.75 tiles/sec.
- [x] **Animação de walk dessincronizada** — `EntityAnimator.SetWalkSpeedScale(tilesPerSec)` ajusta `SpeedScale = tilesPerSec / 10f` (referência: SpeedScale=1.0 foi calibrado a ~10 tiles/sec, o valor errado anterior). Velocidade medida por delta-posição/delta-tempo em `OwnPlayer.ApplySnapshot` e `EntityRegistry.UpdatePosition`.
- [ ] **Tamanho dos mobs inconsistente** — alguns mobs aparecem gigantes, outros minúsculos. Causa não confirmada; investigar se é problema no exporter BMD→GLB (escala do skeleton/mesh) ou se os BMDs originais já têm tamanhos heterogêneos sem fator de correção aplicado.
- [ ] **Ajuste de câmera** — FOV, distância e ângulo calibrados para o tamanho real do mundo (256×256 tiles × 0.8f = 204.8 world units).
- [ ] **Iluminação** — luz direcional + ambient calibrados para o tamanho de tile do MU; sombras suaves.

---

## Itens no chão

- [x] **Modelo 3D do drop** — `SpawnDrop` tenta `res://assets/items/{group}_{number}.glb`; se existir, instancia o GLB; senão usa fallback colorido. Categorias: vermelho=arma (0-5), azul=armadura (6-11), roxo=wings (12), verde=misc.
- [x] **Fallback** — esfera colorida por `item_group` via `MakeDropOrb(ItemDropColor(group))`.
- [x] **Billboard label** — `Label3D` acima do drop com nome via `ItemDatabase.Get()` ou `"X Zen"` para dinheiro; outline para legibilidade.

---

## Ícones de item no inventário

- [ ] **Sprites extraídos** — `MuAssetConverter` exporta os sprites 2D de cada item (OZJ/PNG) mapeados por `(group, number)` para uso no inventário e tooltips.
- [ ] **Alternativa render-to-texture** — renderizar o modelo 3D do item em um `SubViewport` e usar como ícone (melhor qualidade, mais custoso; decidir conforme volume de itens).

> **GLBs de drop gerados**: `muconv --items` exportou 314 GLBs para `assets/items/{group}_{number}.glb`. Mapeamento baseado em `ZzzOpenData.cpp OpenItems()`. Gaps no range (ex: `7_3..15` faltando) são esperados — arquivos não existem no data set original.

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
