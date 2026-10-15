# RAOT TITANS — Plano de Execução

> Este é o jogo da LURA. Roda em cima do RAOT via MelonLoader, mas é tratado como produto: cada fase tem critério de aceite e nada "fecha" só porque compila — fecha quando está bom DENTRO do jogo.

## Visão
Pegar a melhor movimentação ODM que existe (RAOT) e construir em cima dela o jogo de caçar titãs que o AoTTG2 prometia: titãs vivos com animações reais, nuca e juntas que importam, agarrão com devorada, waves, skills de personagem e co-op com os amigos. O jogo parabeniza quem voa bem: pontos nascem de velocidade + precisão.

## Pilares de qualidade (valem pra toda fase)
1. **Parece parte do jogo**: nada de elemento visivelmente quebrado (escala errada, malha sumida, marcador flutuando). Se não dá pra entregar polido, não entrega ainda.
2. **Movimento premiado**: toda mecânica nova deve recompensar voar rápido e preciso.
3. **Fidelidade AoT**: nuca mata, juntas machucam, agarrão come, rugido assusta.
4. **Co-op primeiro**: decisões técnicas não podem fechar a porta do multiplayer.
5. **Estável**: fallback silencioso é bug. Se algo falhar, loga claro no MelonLoader/Latest.log.

## Fases

### F0 — Estabilização do titã real (EM ANDAMENTO, prioridade zero)
Bugs reportados pelo Jeff em 2026-10-15:
- [x] Causa da escala absurda achada (altura medida pré-skinning, 0.34 → fator ~50x). Correção: medir pós-skinning. (código pronto, falta build+teste)
- [x] Causa da cabeça invisível achada (frustum culling por bounds errados da malha). Correção: RecalculateBounds pós-pose. (código pronto, falta build+teste)
- [x] Ganchos do ODM grudarem no corpo do titã (colliders em 11 ossos, layer do chão; raycast de chão do mod ignora titãs). Rodada 2: tamanhos convertidos pra METROS via lossyScale (rig Blender tem escala acumulada; colliders gigantes prendiam o jogador em cima do titã).
- [x] F11 "céu livre" (desliga colliders ceiling/boundary/barrier/killbound; religa no 2º toque).
- [x] Bola vermelha da nuca removida do modelo real (alvo é conhecido).
- [ ] Rosto/boca/expressão: a textura do AoTTG2 TEM rosto, olhos e dentes; PNG decodifica (1024²) e UVs ok. Rodada 1 copiou o shader errado (`Custom/Development/DiffuseWorldUV`, dos prédios, projeta no mundo → mancha azul). Rodada 2: escolha por pontuação (prioriza toon/character, veta WorldUV) + grava no log a lista de shaders do jogo pra cravar o certo.
- [x] Corte na nuca por proximidade REMOVIDO no modelo real: titã agora é `Strikeable` com 5 `StrikePoint` (nuca, 2 pulsos, 2 joelhos) registrado no combate do jogo; só morre com acerto real da lâmina (`OnHit`). Pontos = velocidade no impacto × precisão (distância do ponto de impacto ao centro da nuca).
- [x] Devorada: morte só quando a mão com o jogador chega na `MouthHitbox` (antes matava em 4,6s fixos, no meio do "te olha").
- [ ] Atravessar casas/árvores: implementado desvio por raycast no peito (contorna), saltador pula, e durante a caça quebra objetos pequenos (cerca, barraca, casinha < 90% da altura; nunca terreno/estrada). Validar no City e Forest.
- [ ] Calibrar: altura padrão (~7 jogadores ≈ 12m), alcance de patada/agarrão.
- [x] Trampolim/queda infinita: quando o raio de chão não achava nada, `GroundAt` devolvia a altura de referência (`y+2`) e o titã subia 2m por frame. Agora sem chão = desce devagar e dá meia-volta.
- [x] Olhos (rodada 2, com REA no AoTTG2): a malha dos olhos está no espaço da MALHA (Z pra cima), não no espaço do osso; pendurar no osso dava olho gigante flutuando. Agora olhos (4 variantes `TitanEyes0-3`) e cabelos (`HairM19/F13/F14`, prefabs feitos pro socket do titã) são malhas skinned RÍGIDAS ao osso `head` com a bindpose do head — mesmo caminho do corpo, sem chute de transform.
- [x] Colisão com o mapa (com REA no AoTTG2): `BaseTitan` lá é Rigidbody + CapsuleCollider na raiz, gravidade própria, `CheckGround` curto, `JumpForce`. Replicado: titã agora é corpo físico (cápsula do tamanho dele, massa 5000, rotação travada), anda por velocidade, paredes bloqueiam de verdade, salto é impulso físico, detector de "travou" vira o titã. Colisores de gancho nos ossos viraram triggers (gancho pega, não empurra). O raycast de chão antigo ficou só pro fallback de primitivas.
- [x] Variedade: 5 corpos reais do AoTTG2 (normal/gordo/musculoso/médio/magro) + 3 cabelos (juba do Titã do Eren e 2 cabeleiras) + olhos por variante (corredor: vermelhos e grandes; saltador: amarelos; rastejante: pálidos; normal: sorteio). Validar encaixe do cabelo no City.
**Aceite:** 15 min de sessão com waves sem nenhum bug visual; titã parece nativo do jogo; gancho gruda; dá pra voar sem teto.

### F1 — Experiência de caça completa
- [x] Rugido e sons próprios do titã: 24 SFX extraídos do AoTTG2 (`assets/sfx/*.wav`, carregados como AudioClip em runtime via `TitanSfx`): rugido ao avistar (3 var.), passos (3), risada no agarrão, mordida na devorada, corte de nuca (4) + morte (5), golpe em junta. Som do RAOT fica como fallback.
- VFX de corte na nuca (sangue/vapor direcional), vapor de morte mais denso.
- Ragdoll/queda com peso (hoje a morte é só animação).
- Balanceamento de velocidades/visão por variante, com playtest do Jeff.
- HUD estilizado no tema do jogo (hoje é texto cru).
**Aceite:** matar 10 titãs seguidos "dá gosto": som, impacto visual e pontuação casam.

### F2 — Modo de jogo
- Waves viram modo com rounds, descanso, dificuldade crescente e recorde salvo.
- Menu próprio de partida (estilo do jogo, não IMGUI cru): variantes, densidade, podem comer on/off, fogo amigo (nativo do RAOT), seed.
- Objetivos extras (defender ponto? resgatar NPC?) — decidir com o Jeff.
**Aceite:** dá pra sentar e jogar "o modo titã" meia hora sem tocar em tecla de debug.

### F3 — Co-op real (spike técnico primeiro)
- Hoje: cada jogador vê os próprios titãs (só o alvo considera todos os players).
- Spike: 2 caminhos pra sincronizar — (a) NetworkMessage custom no Mirror via Unhollower (difícil: struct il2cpp), (b) host-authority com seed determinística + relógio da partida + correções periódicas por algum canal existente (chat/atributos de lobby EOS). Estimar 1 dia de investigação ANTES de prometer.
**Aceite:** 2 PCs na mesma partida veem o mesmo titã na mesma posição (tolerância ~2m).

### F4 — Personagens e skills
- Skill travada por personagem escolhido (detectar via CharacterDef).
- Eren: transformação JOGÁVEL (controlar o titã por tempo limitado, socar) — hoje é aliado automático.
- Levi/Mikasa/Armin refinados; cooldowns na HUD estilizada.
**Aceite:** cada personagem joga diferente e dá vontade de revezar.

### F5 — Identidade e distribuição
- Decidir: pacote pra comunidade RAOT (instalador 1-clique) vs jogo interno da equipe.
- Revisar uso dos assets do AoTTG2 (fan-game gratuito; uso não-comercial e privado ok, distribuição pública exige conversa).
- Nome, logo, trailer com gameplay.

## Como trabalhamos (processo)
1. **Ordem é o roadmap.** Bug reportado pelo Jeff fura fila (entra em F0 contínuo).
2. Claude implementa → compila → smoke test automático (jogo abre, mod carrega sem erro) → commit no repo → avisa o Jeff com O QUE testar.
3. Jeff roda o roteiro de teste da fase (TESTPLAN.md, 5 min) e devolve print + sensação. O log `MelonLoader\Latest.log` vale mais que o print: mandar os dois quando algo quebrar.
4. Fase só fecha no critério de aceite, validado pelo Jeff in-game.
5. `CHANGELOG.md` por versão; main sempre estável; experimento grande = branch.
6. Time novo entra pelo `ONBOARDING.md`; estado vivo no `CLAUDE.md`.

## Decisões em aberto (Jeff)
- [ ] F2: qual modo central? (waves puras / defesa de ponto / outra ideia tua)
- [ ] F3 antes ou depois de F2? (co-op cedo muda prioridade)
- [ ] F5: a visão final é mod público pra comunidade RAOT ou jogo fechado da equipe?
