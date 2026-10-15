# RAOT TITANS — o jogo da equipe

> **Se você recebeu este repositório pra trabalhar com o seu Claude/Grok/Cursor:** cole este README inteiro na primeira mensagem do seu agente e peça "leia tudo, depois leia `CLAUDE.md` e `ROADMAP.md`, e me diga o que está pendente e onde eu posso ajudar sem atropelar ninguém". Este arquivo foi escrito pra isso.

---

## 1. O que é isto

Um jogo de caçar titãs no estilo *Attack on Titan*, construído em cima do **RAOT** (Roark's Attack on Titan fan game, Unity 2019.4 IL2CPP) — que tem a melhor movimentação ODM que existe, mas é PvP puro, sem titãs. Nós injetamos o lado PvE por um mod MelonLoader (`Titans.dll`) e assets reais de titã extraídos por engenharia reversa do **AoTTG2** (modelo, esqueleto, 19 animações, 24 sons, 4 olhos, 5 corpos, 3 cabelos).

É tratado como **produto**, não como modzinho: cada fase do roadmap tem critério de aceite validado jogando. Dono do projeto: **Jeff** (LURA). Ele decide prioridade e valida in-game; o resto da equipe implementa.

**Estado hoje (15/10/2026, fim do dia):** titãs reais andando, rugindo, agarrando e comendo o jogador; corte de nuca pelo sistema de golpe do próprio RAOT; juntas (pulso/joelho) que decepam/derrubam; 4 variantes (normal, corredor, saltador, rastejante) + titã aliado do Eren; waves; 4 skills; sons reais; física real (Rigidbody + cápsula, como no AoTTG2). Ver seção 6 pro que está em validação.

---

## 2. Mapa do repositório

| Caminho | O que é |
|---|---|
| `game/raot_2.086_windows/` | **O jogo completo, pronto pra jogar**: RAOT 2.086 + MelonLoader 0.5.7 + `Mods/Titans.dll` + `Mods/TitansAssets/` (pacote de titã e sons). Abra `raot.exe` e pronto. |
| `TitansMain.cs` | O mod inteiro: IA dos titãs, estados, pontuação, skills, waves, menu, HUD. |
| `TitanPack.cs` | Loader do pacote `titan.tpk` (formato TPK2), montagem do titã em runtime, skinning por CPU (`CpuSkin`), player de animação (`TitanAnim`). |
| `TitanSfx.cs` | Carrega os WAV do AoTTG2 como AudioClip em runtime. |
| `TitansMod.csproj`, `NuGet.config` | Build .NET (target net472, referências em `game/.../MelonLoader/Managed`). |
| `assets/` | `titan.tpk` (modelo+rig+animações), `tex_*.png` (peles), `sfx/*.wav` (sons). Cópia do que está em `game/.../Mods/TitansAssets/`. |
| `extracao/` | Scripts Python (UnityPy) que extraem tudo do AoTTG2. `export_titan.py` gera o `titan.tpk`; `export_audio.py` os WAV. Só precisa rodar se mudar o que extraímos. |
| `referencia/dump-il2cpp-raot-2.086.cs` | Dump de TODAS as classes do RAOT (Il2CppDumper). É o manual da API do jogo. Procure aqui antes de supor como algo funciona. |
| `CLAUDE.md` | Estado vivo do projeto + decisões técnicas + lições (o que NÃO refazer). Leitura obrigatória. |
| `ROADMAP.md` | Plano de execução por fases (F0–F5) com critérios de aceite e checklist do que fechou. |
| `TESTPLAN.md` | Roteiros de teste de 5 min por fase. |
| `ONBOARDING.md` | Setup passo a passo (mais curto que este README). |
| `memoria-claude/` | Espelho da memória do Claude do Jeff sobre o projeto. |

Fora do repo (só na máquina do Jeff, não precisa pra trabalhar): o AoTTG2 extraído e o dump do código dele (`X:\Downloads\rea-tools\aottg2*`). Se precisar re-extrair algo do AoTTG2, baixe o AoTTG2 no itch.io e ajuste os caminhos no topo dos scripts.

---

## 3. Rodar em 2 minutos (jogar)

1. Clone o repo (é grande, ~900MB, por causa do jogo).
2. Abra `game/raot_2.086_windows/raot.exe`.
3. Partida **offline** (Forest ou City) → teclas:

| Tecla | Ação |
|---|---|
| F6 | Spawna titã aleatório |
| F9 | Modo WAVES (ondas crescentes + bônus) |
| F10 | Menu: dificuldade, toggles, spawn por tipo, salvar |
| F11 | Céu livre (desliga tetos/limites invisíveis do mapa) |
| F7 / F8 | Limpar titãs / congelar |
| T | Eren: titã aliado por 25s (cd 60s) |
| G | Levi: 7s de fúria, dash + raio de corte maior (cd 40s) |
| Y | Mikasa: lançamento teleguiado na nuca mais perto (cd 30s) |
| H | Armin: 8s de titãs lentos (cd 45s) |

Mecânica: a nuca é o único ponto que mata, e só com **golpe real da lâmina** (sistema `Strikeable` do RAOT). Pontos = velocidade no impacto × precisão no centro da nuca (notas LENDÁRIO / PERFEITO / CRÍTICO / fraco). Pulso cortado = braço decepado; joelho = cai 6s com a nuca exposta. Agarrão: ele te levanta, olha e leva à boca; a morte só acontece quando a mão chega na boca; escape cortando o pulso.

Config do jogador: `game/.../UserData/MelonPreferences.cfg` `[Titans]`.

---

## 4. Compilar o mod (quem vai mexer em código)

Requisitos: .NET SDK 6+ (o Jeff usa um SDK 8 portátil), o jogo dentro do repo (já está).

```
dotnet build -c Release
copy bin\Release\net472\Titans.dll game\raot_2.086_windows\Mods\
```

Se mudou `assets/` (re-exportou o `titan.tpk` ou sons), copie o conteúdo de `assets/` pra `game/raot_2.086_windows/Mods/TitansAssets/`.

Smoke test automático que usamos: abrir o jogo, esperar o log `MelonLoader\Latest.log` mostrar `[Titans] TitanPack: ... clips` e nenhuma linha `ERROR`/`Exception`, fechar. Teste de verdade é jogar o roteiro do `TESTPLAN.md`.

**Restrições técnicas que já custaram horas (não repita):**
- MelonLoader tem que ser **0.5.7** (a 0.6.x exige runtime .NET 6 exato e crasha).
- `BoneWeight[]` NÃO passa pelo interop do Unhollower → por isso o skinning é feito por CPU no `CpuSkin`. Não tente voltar pra `SkinnedMeshRenderer`.
- `GUILayout` no Unhollower exige arrays de opções → usamos `GUI` com `Rect`.
- O `Standard` do RAOT só tem variantes sem textura; o mod copia o shader de um material texturizado do jogo em runtime (`FindTexturedShader`). Se o titã vier liso, olhe a linha `shader escolhido` no log.
- Assets criados no menu são descartados na troca de cena: texturas levam `HideFlags.DontUnloadUnusedAsset`.
- O jogo tem **tetos invisíveis** nos mapas (`MapCeiling`, `ForestCeiling`, `* Boundary`). F11 desliga.
- Olhos/cabelo do AoTTG2 estão no **espaço da malha** (Z pra cima), não no espaço do osso: são tratados como malha skinned rígida ao osso `head`. Não pendure malha direto em osso.
- Titã é corpo físico (`Rigidbody` + `CapsuleCollider`), como o `BaseTitan` do AoTTG2. Não mova titã por `transform.position` com raycast de chão (era o bug do "trampolim").
- **Layers**: a cápsula do titã fica na layer do JOGADOR (`PlayerMain.Instance.gameObject.layer`). Na layer do chão ela atravessa o mapa (chão não colide com chão). Os colisores de gancho nos ossos ficam na layer do chão e são triggers.

---

## 5. Como trabalhamos juntos (pra não atropelar)

1. **O ROADMAP manda.** Antes de começar qualquer coisa: `git pull`, leia `CLAUDE.md` (estado) e `ROADMAP.md` (fila). Pegue um item da fase atual. Não pule fase.
2. **Um item por pessoa por vez.** Ao pegar um item, escreva seu nome ao lado dele no `ROADMAP.md` (`- [ ] (Fulano) ...`) e faça commit+push disso ANTES de codar. É a nossa "trava": quem vê o nome não pega o mesmo item.
3. **Branch por item**: `git checkout -b feat/nome-curto`. `main` fica sempre jogável. Quando terminar e o smoke test passar: PR (ou merge direto se for pequeno) e avise o Jeff com **o que testar**.
4. **Definition of done** = funciona DENTRO do jogo e não quebra o que já existia. Compilar não é terminar. Rode o roteiro da fase no `TESTPLAN.md`.
5. **Bug reportado pelo Jeff fura a fila** e entra na F0 contínua do ROADMAP. Quando fechar, marque `[x]` com uma linha dizendo a causa raiz (não só "corrigido").
6. **Duas tentativas falharam? Pare de chutar.** Vá à referência: `referencia/dump-il2cpp-raot-2.086.cs` (API do RAOT), o dump do AoTTG2 (peça ao Jeff) ou projetos no GitHub. Escreva no commit "a referência X faz assim". Essa regra existe porque chutes custaram ao Jeff várias rodadas de teste.
7. **CLAUDE.md é o diário**: ao fechar o dia, atualize "Estado", "Pendente" e "Lições". Quem chega depois precisa conseguir continuar só lendo ele.
8. **Log sempre**: `game/.../MelonLoader/Latest.log` vale mais que print. Mande os dois ao reportar algo.
9. **Não commitar**: logs, `bin/`, `obj/`, o AoTTG2 bruto, credenciais. O jogo em `game/` SIM (é a forma de todo mundo ter a mesma build).
10. **Assets do AoTTG2**: fan-game gratuito, uso privado da equipe. Antes de qualquer distribuição pública do nosso jogo, isso tem que ser revisto (F5 do ROADMAP).

---

## 6. Pendências AGORA (o que falta, por prioridade)

**Em validação pelo Jeff (rodada 6, 15/10 ~13:30):**
- [ ] Olhos aparecendo no lugar certo (4 variantes) e cabelo encaixado — primeira implementação correta (rígido ao head) acabou de subir; precisa de print.
- [ ] Colisão física: titã não atravessa mais casas/paredes; sem "trampolim" na beira de telhado. (Rodada 6 atravessava o CHÃO: a cápsula estava na layer do chão, e chão-com-chão não colide na matriz do RAOT. Rodada 7 coloca a cápsula na layer do jogador. **Ainda não validado pelo Jeff.**)
- [ ] Textura do titã (corpo e cabeça) em partida — depende do shader copiado; conferir linha `shader escolhido` no log.

**F0 restante:**
- [ ] Calibrar alcance de patada/agarrão e altura padrão (hoje 7 "jogadores" ≈ 12m).
- [ ] Hitbox de dano das juntas: ainda pode estar "frágil" (mira assistida do RAOT puxa o ponto mais próximo). Ajustar raios se o Jeff reportar.

**F1 (próxima, nesta ordem):**
- [x] Sons reais do AoTTG2.
- [ ] VFX de corte na nuca (sangue/vapor direcional) + vapor de morte denso.
- [ ] Ragdoll / queda com peso na morte (hoje é só a animação `die.*`).
- [ ] Balanceamento das 4 variantes jogando com o Jeff.
- [ ] HUD no estilo do jogo (hoje é texto cru no canto).

**Fases seguintes:** F2 modo de jogo completo, F3 co-op real (sync dos titãs entre jogadores — spike técnico primeiro), F4 skills por personagem + Eren jogável, F5 identidade/distribuição. Detalhes e critérios de aceite no `ROADMAP.md`. Três decisões ainda são do Jeff (modo central da F2; co-op antes ou depois do modo; mod público vs jogo fechado).

---

## 7. Arquitetura em 1 minuto (pra orientar seu agente)

- `TitansMain.OnUpdate` roda a cada frame: lê teclas, varre jogadores (`CharacterObject`), atualiza waves e cada titã (lista `titans`).
- Titã = `Titan` (classe interna): estado (`Wander/Chase/Leap/Grab/Stunned/Dying`), referências a ossos-âncora (`NapeHurtbox`, `GrabRSocket`, `ForearmHurtbox`, `LegHurtbox`, `MouthHitbox`), `TitanAnim`, lista de `CpuSkin`, `Rigidbody`, `Strikeable` com 5 `StrikePoint`.
- Golpe: o RAOT chama `Strikeable.OnHit` → `OnTitanStruck` decide nuca/pulso/joelho.
- Devorada: `UpdateGrab` segura o jogador no socket da mão e mata (`GameCommands.Kill()`) quando a mão chega na `MouthHitbox`.
- Movimento: `Step` → velocidade no Rigidbody; `AvoidObstacles` faz raycasts em 3 alturas pra contornar/pular/quebrar props.
- Visual: `TitanPack.BuildInstance(Look)` monta ossos + malhas (corpo variante, cabeça, olhos, cabelo) e materiais com textura do AoTTG2.
- Fallback: sem `titan.tpk`, tudo cai no corpo de primitivas (`BuildPrimitiveTitan`) e sons improvisados — serve pra testar lógica sem assets.

---

## 8. Prompt sugerido pro seu agente

> Leia o README.md, depois CLAUDE.md e ROADMAP.md deste repositório. Resuma: estado atual, pendências por prioridade, regras de trabalho em equipe. Depois me proponha UM item da fase atual que eu possa pegar, me diga o que preciso ler no código pra isso (arquivos e funções), e lembre de eu marcar meu nome no ROADMAP antes de começar. Qualquer mecânica de titã que não exista no código: procure primeiro em `referencia/dump-il2cpp-raot-2.086.cs` como o RAOT faz e me explique antes de implementar.
