# Combat Improvements

Backlog de melhorias de combate. Itens com `[ ]` pendentes, `[x]` concluídos.
Atualizar conforme feedback de playtest.

---

## Em andamento / próximos

### Skill casting — comportamento de range

- [x] **Directional** — dispara na direção do cursor sem restrição de distância. Correto, não cancelar.
- [x] **GroundTarget (Teleport)** — clamp ao range máximo na direção do clique; nunca rejeita.
- [ ] **GroundTarget AoE (Flame, Hellfire, etc.)** — mesmo comportamento do teleport: se clicar além do range, solta no ponto máximo na direção do clique. Hoje cai no `StartPathTo` (bug). Bloqueado por falta de posição em `C2SCastSkill` — precisa adicionar `float target_x / target_y` ao proto e handler no servidor.
- [ ] **Targeted fora do range** — em vez de cancelar silenciosamente, o personagem deve **andar em direção ao alvo até entrar no range e então soltar automaticamente** (igual ao `_pendingNpcInteractId`). Implementação: campo `_pendingSkillCast = (slot, targetId)` + check em `_Process`.

---

## Animação / Lock de cast — fundação para PvP

### Contexto

Hoje o lock de animação é fraco: dá pra mover e castár rapidamente sem que o personagem pare visualmente. Isso é funcionalmente válido para PvE, mas elimina toda a profundidade de PvP.

O objetivo final é ter um sistema de **cast lock** onde cada skill tem uma janela de vulnerabilidade — o período em que o caster está parado e pode ser interrompido/punido. Isso abre mecânicas de PvP como:

- Snipe de cast: ver o mago cavar um Meteor de 2s → entrar no direcional nele antes de sair do range
- Interrupt: skill de stun/empurrão cancela o cast do oponente
- Animation cancel consciente: o player experiente sabe exatamente quando pode mover sem cancelar

### O que precisa ser construído

- [ ] **Cast time por skill** — campo `CastTimeMs` em `SkillInfo` (ex: Meteor = 1500ms, Teleport = 300ms, Flame = 0ms). Durante o cast o player fica locked: sem movimento, sem novo cast.
- [ ] **S2CSkillCastStarted** — pacote do servidor notificando início de cast (para outros jogadores verem a animação de "carregando" do caster). Essencial para o opponent reagir.
- [ ] **Cast interrupt** — qualquer habilidade de stun/knock que acerte um player em cast cancela o cast e dispara `S2CCastInterrupted`. No cliente: limpa o `_castLockSec`.
- [ ] **Distinção `_castLockSec` vs `_actionLockSec`** — já existe essa separação (castLock = uninterruptível por GoIdle, actionLock = cancelável). Estender: `castLock` deve ser clearável por efeitos de interrupt vindos do servidor.
- [ ] **Animação de cast no EntityVisual** — durante o cast, o EntityAnimator toca a anim de "casting/charging" correspondente à skill; ao completar, toca a anim de disparo.

### Nota de arquitetura

Toda a duração de cast deve ser **server-authoritative**: o servidor envia o início, valida a conclusão e aplica o efeito. O cliente apenas anima e respeita os pacotes. Não calcular dano ou aplicar efeito antes de receber confirmação do servidor.

---

## Polish de feedback visual

- [ ] **Cursor shape**: mudar cursor para `PointingHand` ao hover em mob/NPC, `Arrow` em terreno, `Target` ao carregar skill Targeted.
- [ ] **Highlight de alvo**: brilho/outline no mob selecionado como alvo de skill ou auto-attack.
- [ ] **Indicador AoE no mob**: quando skill AoE-on-target (futura), mostrar disco de raio ao redor do alvo (não do cursor).
- [ ] **Float de dano**: números de dano subindo sobre o mob atingido (cor diferente por tipo: físico branco, magia azul, critical amarelo).

---

## Notas de design (não implementar ainda)

- **Interrupt window**: só skills com `CastTimeMs > 300` deveriam ser interrompíveis. Flash de 100ms de ataque básico não faz sentido cancelar.
- **Jank de movimento pós-cast**: após um cast com lock longo (ex: Meteor 1.5s), o player deveria ter um breve "recovery frame" de ~100ms antes de poder mover, para evitar que um clique antecipado durante o cast dispare movimento imediato ao fim do lock.
- **Cancellation consciente**: right-click / ESC durante um cast longo (Targeted com walk approach) deveria cancelar o approach E o cast pendente. GoIdle já faz isso para `_actionLockSec`; precisará limpar também o `_pendingSkillCast`.
