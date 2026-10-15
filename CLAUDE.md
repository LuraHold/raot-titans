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

## v2 (2026-10-15) — o que tem
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
- [ ] Sincronizar titãs no multiplayer (os titãs hoje são locais por cliente): seed determinística por partida ou NetworkMessages próprias do Mirror.
- [ ] Visual: tentar vestir o corpo com `mat_training_titan` do jogo / malha skinned de verdade.
- [ ] Sons (passos, rugido, vapor) e partículas de vapor na morte.
- [ ] Mais skills por personagem e detecção do personagem escolhido.
- [ ] Fogo amigo on/off e settings de partida próprios (menu simples via GUI).
