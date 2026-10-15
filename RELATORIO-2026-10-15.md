# RAOT TITANS — Relatório do dia 1 (15/10/2026)

## O que é o projeto
Transformar o RAOT (Roark's Attack on Titan fan game, Unity 2019.4 IL2CPP, PvP puro com a melhor movimentação ODM que existe) num jogo de caçar titãs estilo Attack on Titan: titãs vivos, nuca como ponto fraco, agarrão com devorada, waves, skills de personagem e co-op. Feito como mod MelonLoader (Titans.dll) + assets reais extraídos por engenharia reversa do AoTTG2. Tratado como produto da equipe, com roadmap e critérios de aceite. Repositório privado: github.com/LuraHold/raot-titans.

## Engenharia reversa (REA)
- RAOT dumpado por completo (Il2CppDumper): 9.000+ classes mapeadas. Descobertas-chave: PlayerMain.Instance (posição/física do jogador), sistema de golpe Strikeable/StrikePoint (o mesmo dos bonecos de treino), GameCommands.Kill() (morte real do jogador), CharacterObject (todos os jogadores em cena), nomes dos tetos/limites invisíveis dos mapas.
- AoTTG2 (Unity 2023) dumpado e dissecado: BaseTitan/BaseCharacter (física Rigidbody + cápsula, gravidade própria, salto por impulso, DisableArm, StateAction), BasicTitanSetup (variantes de olhos e cabelos).
- Pipeline próprio em Python (UnityPy) que extrai do AoTTG2 e gera o pacote TPK2 que o mod lê: esqueleto de 102 ossos (com os hitboxes oficiais: NapeHurtbox, GrabSocket, ForearmHurtbox, LegHurtbox, MouthHitbox), 5 corpos skinned (normal, gordo, musculoso, médio, magro), cabeça, 4 variantes de olhos, 3 cabelos, 5 texturas de pele, 19 animações decodificadas do formato comprimido do Unity (StreamedClip/DenseClip/ConstantClip, bindings casados por CRC32 de caminho, 168/168), 24 sons (rugido, passos, mordida, corte de nuca, mortes, risada, golpe em junta).

## O mod (o que funciona hoje)
- Titãs com modelo e animações REAIS do AoTTG2, montados em runtime no RAOT (skinning por CPU, porque BoneWeight não passa pelo interop do MelonLoader; player de animação próprio com crossfade).
- Variantes: Normal, Corredor (2x velocidade, olhos vermelhos "psicopata"), Saltador (pulo balístico, juba do Eren), Rastejante (anda baixo e rápido, animações de crawler). Corpos, olhos e cabelos diferentes por variante e sorteio.
- IA: visão em cone (130m) + audição (16m); vagueia quando não vê; ruge ao avistar; persegue o jogador mais próximo (inclui amigos no lobby); patada sincronizada com a animação de swing; desvio de obstáculos por raycast em 3 alturas; quebra props pequenos (caixas, barris, cercas); saltador pula obstáculo.
- Agarrão e devorada: levanta o jogador, olha, leva à boca (animações reais) e mata de verdade quando a mão chega na boca; escape cortando o pulso.
- Combate pelo sistema do próprio RAOT: o titã é um Strikeable com 5 pontos (nuca, 2 pulsos, 2 joelhos). Só morre com golpe real da lâmina na nuca. Pulso = braço decepado (malha colapsa no ombro, perde o agarrão daquele lado). Joelho = cai sentado 6s com a nuca exposta.
- Pontuação que premia movimentação (pedido do Jeff): velocidade no impacto × precisão no centro da nuca → CORTE LENDÁRIO / PERFEITO / CRÍTICO / fraco, com elogio na tela; bônus por junta, escape e wave limpa; placar com total e melhor corte.
- Waves (F9): ondas crescentes num anel ao redor, bônus por wave.
- Skills: T Eren (titã aliado que soca inimigos por 25s), G Levi (fúria: dash + raio de corte maior), Y Mikasa (lançamento teleguiado na nuca), H Armin (titãs lentos + nucas marcadas).
- Sons reais, vapor na morte/corte/decepar, gancho do ODM gruda no corpo (colisores nos ossos), F11 desliga tetos/limites invisíveis, menu F10 (dificuldade, toggles, spawn por tipo), HUD com status e cooldowns.
- Física: titã é corpo físico (Rigidbody + cápsula na layer do jogador), paredes bloqueiam, salto é impulso, detector de "travou".
- Fallback: sem o pacote de assets, tudo cai num corpo de primitivas e continua jogável.

## Bugs encontrados e resolvidos (com causa raiz)
- Titã spawnava em cima do jogador e o arremessava (bounds do clone do boneco de treino) → corpo próprio.
- MelonLoader 0.6.6 crashava (exige .NET 6 exato) → 0.5.7.
- Modelo real não aparecia (BoneWeight[] rejeitado pelo interop) → skinning por CPU.
- Titãs 50x maiores (altura medida antes do skinning) → medir pós-skinning.
- Cabeça invisível (frustum culling por bounds errados) → RecalculateBounds pós-pose.
- Jogador preso em cima do titã (colisores de gancho gigantes por escala acumulada do rig do Blender) → tamanhos em metros / lossyScale.
- Pele lisa sem rosto (Standard do RAOT só tem variantes sem textura; shader copiado errado era o dos prédios "WorldUV") → escolha por pontuação do shader de materiais texturizados do jogo.
- Cabeça branca (textura carregada no menu descartada na troca de cena) → DontUnloadUnusedAsset.
- Olho gigante flutuando (malha dos olhos está no espaço da malha, Z pra cima, não no espaço do osso) → olhos/cabelo como malha skinned rígida ao osso head.
- "Trampolim" pro céu (raycast de chão sem acerto devolvia y+2 por frame) → física real.
- Titã atravessando o chão (cápsula na layer do chão; chão não colide com chão) → layer do jogador. [instalado, ainda não validado]
- Nuca morrendo por proximidade e juntas "frágeis" (mira assistida puxa o ponto mais próximo) → golpe real + raios menores nas juntas + velocidade mínima e cooldown.
- Devorada matando antes de chegar na boca → morte por distância mão–boca.

## Organização e equipe
- Repositório com o jogo completo pronto pra jogar (game/), fonte do mod, pacote de assets, scripts de extração, dump da API do RAOT.
- README robusto pra colar no agente de IA de qualquer membro: estado, mapa de arquivos, como rodar/compilar, restrições técnicas que já custaram horas, 10 regras de trabalho em equipe (um item por pessoa marcado no ROADMAP antes de codar, branch por item, main sempre jogável, done = funciona no jogo, bug do Jeff fura fila, 2 tentativas falhas = pesquisar a referência), pendências por prioridade e prompt pronto.
- ROADMAP F0–F5 com critérios de aceite; TESTPLAN com roteiros de 5 min; CLAUDE.md como diário técnico; ONBOARDING; projeto no Cockpit.

## Pendências
- Validação do Jeff (rodada 7): olhos no lugar, cabelo encaixado, colisão com chão e paredes, textura do corpo/cabeça em partida.
- F0: calibrar alcance de patada/agarrão e altura padrão; conferir hitbox das juntas.
- F1 (ordem): VFX de corte na nuca e vapor denso → ragdoll na morte → balanceamento das variantes → HUD no estilo do jogo.
- F2 modo de jogo completo; F3 co-op real (sync dos titãs entre jogadores, spike técnico antes); F4 skills por personagem + Eren jogável; F5 identidade/distribuição (revisar uso dos assets do AoTTG2 antes de qualquer distribuição pública).
- Decisões do Jeff: modo central da F2; co-op antes ou depois do modo; mod público vs jogo fechado da equipe.
- Convidar a equipe como collaborator no repo.
