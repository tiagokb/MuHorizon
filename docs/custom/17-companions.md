# 17 — Companheiros: levantamento do legado e proposta

Status: **proposta** (2026-10-22). Detalha o §7 do `16-game-design-plan.md`.

## 1. Como o legado funciona hoje

### Itens e slots
- Todos os pets e montarias usam **o mesmo slot de equipamento** (`InventoryConstants.PetSlot` = 8, tipo de slot "Pet"):
  Guardian Angel, Imp, Uniria, Dinorant, Dark Horse, Fenrir, Demon, Spirit of Guardian, Rudolf, Panda, Unicorn, Skeleton.
- **Dark Raven** é exceção: equipa na mão (`RightHandSlot` = 1), no lugar da arma secundária.
- Os 12 slots de equipamento (0–11) estão todos ocupados; o inventário começa no slot 12.

### Passivas e buffs (já são dados — bom)
Os bônus vêm de `ItemBasePowerUpDefinition` no banco (atributo + valor + agregação), ex.:

| Item | Bônus |
|---|---|
| Guardian Angel | dano recebido ×0,8, +50 HP |
| Imp | dano ×1,3 |
| Uniria / Dark Horse / Fenrir | velocidade +2 (também embaixo d'água) |
| Dinorant | dano ×1,15, dano recebido ×0,9, velocidade +2 |
| Demon | dano base ×1,4, velocidade de ataque +10 |
| Spirit of Guardian | dano recebido ×0,7, +50 HP |
| Panda / Unicorn | defesa +50, exp +50% / zen ×1,5 |
| Skeleton | dano base ×1,2, vel. ataque +10, exp +30% |

- Velocidade de montaria entra no nosso movimento contínuo (`Stats.MovementSpeed` → `Player.GetClientMovementSpeed`).
- Flags como `IsHorseEquipped` / `IsDinorantEquipped` liberam skills (Earthshake exige o Dark Horse) e ataques especiais.
- Adicionar pet/montaria nova com passivas novas = **só dados** (item + power-ups). Isso já atende o "liberdade de adicionar".

### Corvo (Dark Raven)
- `RavenCommandManager` (implementa `IPetCommandManager`): modos **Idle, AttackRandom, AttackWithOwner, AttackTarget** — já é "comando", não skill.
- Não existe como entidade no mundo: é um atacante virtual com laço próprio (`Task.Run` + delay), fora do tick loop (P5).
- Tem nível/experiência de pet (`RavenAttributeSystem`, `PetExperienceFormula`).
- Só o corvo ataca ("the Raven is currently the only pet that can attack").

### Invocações (Elfa e afins)
- Skill `SummonMonster` cria um **Monster real** no mapa (`Player.CreateSummonedMonsterAsync`), um por vez (`Player.Summon`).
- IA fixa (`SummonedMonsterIntelligence`): segue o dono e ataca o monstro mais perto dele (raio 8); revida quem bate no dono. **Sem modos.**
- Sobrevive a troca de mapa (é re-adicionado com o dono).

### Client (nosso)
- Não mostra pet/montaria (o visual de equipamento vai só até o slot 7, asas).
- Invocações aparecem como NPC comum.
- Não há controle de pet nem de invocação.

## 2. Proposta

### Estrutura
1. **Slot de montaria separado**: novo slot de equipamento **Mount** (Uniria, Dinorant, Dark Horse, Fenrir, e futuras); o slot **Pet** fica para Angel, Imp, Demon, Spirit, Panda, Unicorn, Skeleton etc. Passivas continuam sendo power-ups dos itens.
2. **Controlador único de companheiro** (`CompanionIntelligence`), no tick loop, com modos:
   **Seguir · Atacar o meu alvo · Atacar livre · Defender (quem me ataca) · Parado**.
   Troca de modo por mensagem própria (`C2SCompanionMode`) e um controle no HUD — nunca por skill.
3. **Invocações da Elfa** continuam skills (ultimate), mas o monstro invocado usa o controlador → ganha os modos.
4. **Corvo** vira **entidade no mundo** usando o mesmo controlador (visível, segue o DL, ataca conforme o modo). Mantém nível/exp de pet.
5. **Dark Horse**: montaria exclusiva do DL (requisito de classe do item já existe) com skill própria (Earthshake já exige o cavalo).
6. **Visual** (pets seguindo, montaria embaixo do personagem): depois, junto do polimento visual (assets/modelos).

### Ordem de entrega
| # | Entrega | Tamanho |
|---|---|---|
| A | Slot de montaria separado + slots de pet/montaria na janela de equipamento | médio (inclui migração de slots) |
| B | Controlador de companheiro + modos + controle no HUD, aplicado às invocações | médio |
| C | Corvo como entidade usando o controlador | médio |
| D | Visual de pets/montarias | grande (assets), polimento |

## 3. Decisões em aberto
1. **Slot novo de montaria**: os slots 0–11 estão ocupados e o inventário começa no 12. Criar o slot 12 empurra o inventário uma posição → migração que soma 1 no `ItemSlot` dos itens de inventário (não do baú). Alternativa: um índice "fora da grade" (ex. 236+) sem mexer no inventário — menos risco, mas foge do padrão.
2. **Corvo/invocações atacáveis?** No legado o corvo não pode ser atacado. Companheiro com HP que morre (e reaparece depois de X s) ou invulnerável?
3. **Limite de invocações**: hoje 1 por vez. Manter 1, ou depender da skill/talento?
