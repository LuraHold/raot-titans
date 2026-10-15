# RAOT TITANS — Onboarding da equipe

Mod que transforma o **Roark's Attack on Titan fan game** (PvP puro) num jogo de caçar titãs estilo AoTTG: titãs com variações andando pelo mapa, visão/perseguição, agarrão com devorada real, juntas como pontos de dano, nuca como ponto fraco, waves e skills de personagem (Eren/Levi). Tudo roda como mod MelonLoader client-side; nada do jogo original é alterado em disco além da pasta `Mods`.

## Repositório
- `TitansMain.cs` — todo o mod (IA, corpo articulado, pontuação, HUD). Comece por aqui.
- `TitansMod.csproj` + `NuGet.config` — build .NET (target net472).
- `CLAUDE.md` — estado vivo do projeto (o que está feito, pendências, decisões). **Leitura obrigatória antes de mexer.** Quem usa Claude/Grok: peça "leia o CLAUDE.md e me diga onde paramos".
- `referencia/dump-il2cpp-raot-2.086.cs` — dump completo das classes do jogo (Il2CppDumper). É o "manual da API" do jogo: procure classes aqui antes de inventar.
- `memoria-claude/` — espelho da memória do Claude do projeto.

## Setup na ordem (Windows)
1. Tenha o jogo **raot 2.086** (pasta `raot_2.086_windows`). Não vai no repo (binário de terceiro).
2. Baixe **MelonLoader 0.5.7** (`MelonLoader.x64.zip` do GitHub LavaGang) e extraia DENTRO da pasta do jogo (vira `version.dll` + `MelonLoader\`). **Tem que ser 0.5.7** — a 0.6.x exige runtime .NET 6 e crasha nessa máquina-padrão.
3. Rode o jogo uma vez e feche: o MelonLoader gera `MelonLoader\Managed\*.dll` (as referências do build).
4. Clone este repo. Se o caminho do jogo for outro, ajuste `<GamePath>` no `TitansMod.csproj`.
5. Build: `dotnet build -c Release` (precisa de SDK .NET; qualquer 6+ serve, baixa o pacote de referência net472 sozinho).
6. Copie `bin\Release\net472\Titans.dll` pra `<jogo>\Mods\` **e a pasta `assets\` do repo pra `<jogo>\Mods\TitansAssets\`** (modelo/animações reais do titã, extraídos do AoTTG2; sem ela o mod cai no corpo de primitivas).
7. Abra o jogo → partida offline → teclas abaixo.

## Controles in-game
| Tecla | Ação |
|---|---|
| F6 | Spawna titã aleatório (Normal / Corredor / Saltador / Rastejante) |
| F9 | Liga/desliga modo WAVES (ondas crescentes + bônus) |
| F7 | Remove todos os titãs |
| F8 | Congela/solta titãs |
| F10 | Menu de configurações (dificuldade, toggles, spawn por tipo) |
| T | Skill EREN: titã aliado por 25s que soca os inimigos (cooldown 60s) |
| G | Skill LEVI: 7s de fúria — dash + corte facilitado (cooldown 40s) |
| Y | Skill MIKASA: lançamento teleguiado na nuca do titã mais perto (cd 30s) |
| H | Skill ARMIN: 8s de titãs lentos + nucas marcadas (cd 45s) |

Mecânica: nuca vermelha = ponto fraco (passar voando perto em velocidade); pontos = velocidade × precisão (CORTE LENDÁRIO/PERFEITO/CRÍTICO/fraco). Pulsos = decepar braço; joelhos = derrubar (nuca exposta). Agarrão: corte o pulso da mão que te segura ou vira comida (morte real via comando do jogo).

## Regras permanentes do Jeff
- Repo privado da LuraHold; não subir binários do jogo nem credenciais.
- Sincronizar trabalho via **CLAUDE.md + git pull/push**: antes de mexer, `git pull` e leia o CLAUDE.md; ao terminar, atualize o CLAUDE.md (feito/pendente/próximos) e push.
- Multiplayer: por enquanto os titãs são locais (cada jogador com o mod vê os seus). Sincronizar titãs entre clientes é o próximo grande passo (seed determinística ou mensagens Mirror próprias) — não prometam online sincronizado antes disso.

## Config do jogador
`<jogo>\UserData\MelonPreferences.cfg`, categoria `[Titans]`: escala, velocidade, distância de visão, raio/velocidade de corte, máx. de titãs, pode comer on/off, brilho da nuca.
