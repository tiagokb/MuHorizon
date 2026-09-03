# Visual & Equipment System

Backlog de melhorias visuais do mundo e do sistema de equipamentos.
Itens com `[ ]` pendentes, `[x]` concluídos.

---

## World feel — escala e movimento

- [ ] **Investigar velocidade de movimento aparente** — o personagem parece se mover em speedhack. Provável causa: a velocidade base do MU (`Stats.Speed`) foi calibrada para a engine original; em Godot com `ServerToWorld = 0.1f` a proporção câmera/tile pode ampliar visualmente essa velocidade. Investigar: comparar tiles percorridos/segundo com o MU original, ajustar `Stats.Speed` na `GameConfiguration` ou a distância da câmera.
- [ ] **Ajuste de câmera** — FOV, distância e ângulo calibrados para o tamanho real do mundo (256×256 tiles × 0.8f = 204.8 world units). Atualmente parece que o mundo é pequeno pq a câmera está muito próxima para a escala dos assets.
- [ ] **Iluminação** — luz direcional + ambient calibrados para o tamanho de tile do MU; sombras suaves.

---

## Itens no chão

- [ ] **Modelo 3D do drop** — converter BMDs de itens para GLB (estende o `MuAssetConverter` existente). `EntityRegistry.SpawnDrop` já cria o `Area3D`; adicionar o GLB como filho.
- [ ] **Fallback** — se o GLB não existir, usar uma esfera/quad colorida por categoria de item (arma, armadura, joia, poção).
- [ ] **Billboard label** — nome do item acima do drop (aparece ao passar o mouse ou sempre, dependendo da configuração).

---

## Ícones de item no inventário

- [ ] **Sprites extraídos** — `MuAssetConverter` exporta os sprites 2D de cada item (OZJ/PNG) mapeados por `(group, number)` para uso no inventário e tooltips.
- [ ] **Alternativa render-to-texture** — renderizar o modelo 3D do item em um `SubViewport` e usar como ícone (melhor qualidade, mais custoso; decidir conforme volume de itens).

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
