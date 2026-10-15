---
name: project-raot-titans-mod
description: "Mod MelonLoader pro fan game Roark's AoT (raot 2.086) com titãs + nuca como ponto fraco + pontuação por velocidade/precisão do corte"
metadata:
  node_type: memory
  type: project
  originSessionId: defdc19e-5038-4b98-b344-3ae803070d71
  modified: 2026-10-15T14:23:19.104Z
---

Jogo: `X:\Downloads\raot_2.086_windows` = **Roark's Attack on Titan fan game** 2.086 (Unity 2019.4.37f1 IL2CPP, Mirror, PvP sem titãs nativos; os "training titans" do Combat Training têm Strikeable/StrikePoint e NapeLocation). Mod **Titans** criado em 2026-10-15: fonte em `X:\Downloads\rea-tools\TitansMod` (CLAUDE.md = playbook completo), dump IL2CPP em `X:\Downloads\rea-tools\raot-dump\dump.cs`, SDK .NET 8 portátil em `X:\Downloads\rea-tools\dotnet-sdk`.

Essencial: MelonLoader **0.5.7** (0.6.6 segfaulta sem runtime .NET 6); mod net472 referenciando `MelonLoader\Managed`; F6/F7/F8; nuca brilhante = ponto fraco (corte por proximidade+velocidade, sem hookar Mirror); pontos = velocidade × precisão (CORTE LENDÁRIO/PERFEITO/CRÍTICO/fraco) — Jeff quer que o jogo parabenize a boa movimentação. Compila e carrega limpo; falta Jeff testar in-game. Projeto no Cockpit (grupo OUTROS).
