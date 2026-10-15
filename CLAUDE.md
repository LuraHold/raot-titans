# RAOT Titans Mod

> Memória do Claude espelhada em `memoria-claude/`. Onboarding da equipe: `ONBOARDING.md` (leia primeiro se você é novo).

Mod MelonLoader pro **Roark's Attack on Titan fan game** (raot 2.086, Unity 2019.4.37f1 IL2CPP, Mirror). O jogo é PvP sem titãs; o mod adiciona o lado PvE estilo AoTTG: titãs variados com IA, nuca como ponto fraco, juntas como pontos de dano, agarrão/devorada, waves e skills (Eren/Levi). Pontuação premia movimentação (velocidade × precisão do corte) — pedido explícito do Jeff.

## Caminhos
- Jogo: `X:\Downloads\raot_2.086_windows` (MelonLoader **0.5.7**; mod em `Mods\Titans.dll`)
- Fonte: este repo (`TitansMain.cs` = tudo)
- Dump IL2CPP (API do jogo): `referencia/dump-il2cpp-raot-2.086.cs`
- SDK portátil local do Jeff: `X:\Downloads\rea-tools\dotnet-sdk\dotnet.exe`

## Build + instalar
```
dotnet build -c Release
copy bin\Release\net472\Titans.dll X:\Downloads\raot_2.086_windows\Mods\
```

## v4 (2026-10-15) — TITÃS DE VERDADE (modelos do AoTTG2)
- **Pipeline de extração** (`titan-extract/` em X:\Downloads\rea-tools, scripts Python + UnityPy): lê o `resources.assets` do AoTTG2 (Unity 2023, em X:\Downloads\rea-tools\aottg2), exporta o prefab **BasicTitan0**: esqueleto 102 transforms, malhas Body/Head com pesos de skin (pesos vêm de `MeshHandler.m_BoneWeights/m_BoneIndices`, NÃO de m_Skin), bindposes, 5 texturas de pele (vanilla/fat/muscle/medium/skinny) e **19 AnimationClips decodificados** (StreamedClip+DenseClip+ConstantClip, bindings casados por CRC32 do caminho do bone — 168/168 ok) baked a 30fps → formato próprio **TPK1** (`assets/titan.tpk`, ~2MB).
- Rodar de novo se mudar algo: `python export_titan.py` (gera em TitansMod/assets; copiar pra `<jogo>\Mods\TitansAssets\`).
- **TitanPack.cs**: loader TPK1 + montagem runtime (bones → GameObjects, Mesh com boneWeights/bindposes, SkinnedMeshRenderer, material Standard/fallbacks com textura via ImageConversion) + **TitanAnim** (player de clips baked com crossfade).
- **Âncoras nos hitboxes REAIS do rig**: NapeHurtbox (nuca), GrabR/LSocket (mão do agarrão), ForearmR/LHurtbox (corte de pulso), LegR/LHurtbox (joelho), upper_arm.L/R (decepar = escala 0.001).
- **Clips usados por estado**: idle, run.walk (wander/chase normal), run.abnormal (corredor/aliado), Titanjumping (salto), attack.swing.l/r (patada sincronizada com push em +0.75s), attack.scream (rugido ao avistar), grab.ground.front.r → eat.r (agarrão+devorada, morte em 4.6s), sit_down/idle/getup (stun), die.front/back, crawler.idle/run/die.
- Variantes por textura: Normal=vanilla/fat, Corredor=skinny, Saltador=medium, Rastejante=muscle, Aliado=vanilla com tint azul.
- Fallback total: sem `Mods\TitansAssets\titan.tpk` → corpo de primitivas v3 continua funcionando.

## v3 (2026-10-15) — fila executada
- **Vapor**: rajadas de "fumaça" (esferas brancas que sobem e encolhem) na morte, nuca cortada, braço decepado, soco do aliado e transformação do Eren.
- **Sons**: reusa AudioClips do próprio jogo achados em runtime (passos = clips de foot/land em pitch grave; vapor = clips de gás; impacto = hit/impact). AudioSource 3D por titã. Liga/desliga no menu.
- **Pele do jogo**: tenta achar `mat_training_titan` em runtime e aplica no corpo (com tint por variante). Fallback = cor sólida.
- **Multi-alvo**: titãs caçam o jogador MAIS PRÓXIMO (varre `CharacterObject` da cena a cada 1s — inclui amigos no lobby). Agarrão/devorada/patada só valem pro player local (morte remota não dá pra forçar daqui).
- **Skills novas**: Y = Mikasa (lançamento teleguiado na nuca do titã mais perto, cd 30s); H = Armin (8s: titãs 50% mais lentos + nuca marcada grande, cd 45s).
- **Menu F10**: sliders (máx/escala/velocidade/visão), toggles (comer, nuca, sons, pele), botões de spawn por tipo, salvar. Libera o cursor ao abrir.

## v2 (2026-10-15) — base
- Corpo articulado próprio (primitivas com juntas: quadril/torso/cabeça/braços/pernas, andar procedural). O clone do training dummy do v1 foi ABANDONADO: bounds bugados spawnavam o titã em cima do player (bug do "fiquei só voando") e o jogo destruía o clone.
- Variantes: Normal, Corredor (2x vel.), Saltador (pulo balístico), Rastejante (baixo e rápido). F6 = aleatório.
- Percepção: visão 130m num cone de 75° + "audição" 16m; sem ver o player, vagueia pelo mapa.
- Agarrão: mão segura o player (trava posição), leva à boca em 3,2s → **morte real** via `GameCommands.Kill()` (console do jogo). Escape: cortar o pulso da mão (decepa o braço + stun).
- Juntas: pulso = decepa braço (+25); joelho = derruba 6s com nuca exposta (+15, corte na nuca caído vale 85%).
- Nuca: corte por proximidade+velocidade; notas LENDÁRIO/PERFEITO/CRÍTICO/fraco com elogio à movimentação.
- Waves (F9): ondas crescentes num anel de 70-130m, bônus por wave limpa.
- Skills: T = Eren (titã aliado 25s que soca inimigos, cd 60s); G = Levi (7s dash + corte facilitado, cd 40s).
- HUD: vivos, wave, abates, pontos, melhor corte, cooldowns.

## Decisões técnicas (não refazer)
- MelonLoader **0.5.7** net472/Unhollower (0.6.6 exige .NET 6 exato, segfaulta com roll-forward pro 8).
- IA toda no `OnUpdate` do mod, sem MonoBehaviour injetado (menos frágil no IL2CPP).
- Corte por velocidade+proximidade do player, SEM hookar Strikeable/Mirror (validação de servidor).
- Âncoras do jogo: `Player.PlayerMain.Instance` (transform/RigidBody), `AoTGameUI.Console.Commands.Game.GameCommands.Kill()` (morte).
- Raycast de chão parte de `playerY+35` pra baixo (mapas têm TETO — raycast de cima pegava o teto).

## Pendente (depende do Jeff):
- [ ] Testar v2 in-game: F6, F9, agarrão, juntas, T, G — e relatar o que estranhar.
- [ ] Convidar a equipe como collaborators no repo GitHub (LuraHold/raot-titans).

## Próximos passos (fila de trabalho)
- [ ] Sincronizar titãs no multiplayer de verdade (posições iguais pra todos): investigar registrar NetworkMessage própria no Mirror via Unhollower (difícil: struct il2cpp custom) ou seed determinística + relógio de partida.
- [ ] Malha skinned de verdade no titã (hoje é primitivas com material do jogo).
- [ ] Rugido próprio (sintetizar AudioClip via SetData) e animação de comer mais elaborada.
- [ ] Detecção do personagem escolhido pra travar skill por personagem (hoje as 4 skills são livres: T/G/Y/H).
- [ ] Ajustes de balanceamento conforme feedback do Jeff jogando.
