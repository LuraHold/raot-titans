using System;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;
using Player;
using Object = UnityEngine.Object;
using GameCmds = AoTGameUI.Console.Commands.Game.GameCommands;

[assembly: MelonInfo(typeof(TitansMod.TitansMain), "Titans", "2.0.0", "Jeff & Claude")]
[assembly: MelonGame(null, null)]

namespace TitansMod
{
    public class TitansMain : MelonMod
    {
        // ---------------- tipos ----------------

        private enum TitanKind { Normal, Runner, Jumper, Crawler, Ally }
        private enum TitanState { Wander, Chase, Leap, Grab, Stunned, Dying }

        private class Titan
        {
            public TitanKind kind;
            public TitanState state;
            public GameObject root;
            public Transform tr;
            public float scale;        // multiplicador
            public float height;       // altura em metros
            public float speed;
            public float stateTime;
            public float spawnTime;
            public float bobPhase;
            public float wanderHeading;
            public float nextWanderTurn;
            public float lastLeap;
            public float lastGrabTry;
            public float grabStart;
            public float lastJointHit;

            // esqueleto
            public Transform hips, torso, head, napeAnchor;
            public GameObject napeMarker;
            public Transform armL, armR, handL, handR;
            public Transform legL, legR;
            public Transform kneeL, kneeR, wristL, wristR;
            public bool armLGone, armRGone;
            public Transform grabHand;
        }

        private readonly List<Titan> titans = new List<Titan>();
        private readonly System.Random rng = new System.Random();

        private int kills;
        private int totalScore;
        private int bestCut;
        private bool frozen;

        // waves
        private bool waveMode;
        private int waveNum;
        private float waveNextTime = -1f;

        // skills
        private float erenReadyAt;
        private float erenEndsAt;
        private float leviReadyAt;
        private float leviEndsAt;

        // hud
        private string lastMsg = "";
        private float lastMsgTime = -99f;
        private string cutMsg = "";
        private float cutMsgTime = -99f;

        // config
        private MelonPreferences_Entry<float> cfgScale, cfgSpeed, cfgCutRadius, cfgCutSpeed, cfgViewDist;
        private MelonPreferences_Entry<int> cfgMaxTitans;
        private MelonPreferences_Entry<bool> cfgShowNape, cfgCanEat;

        public override void OnInitializeMelon()
        {
            var cat = MelonPreferences.CreateCategory("Titans");
            cfgScale = cat.CreateEntry("TitanScale", 7.0f, "Escala base do titã");
            cfgSpeed = cat.CreateEntry("TitanSpeed", 6.5f, "Velocidade base (m/s)");
            cfgCutRadius = cat.CreateEntry("NapeCutRadius", 3.5f, "Raio da nuca (m)");
            cfgCutSpeed = cat.CreateEntry("NapeCutMinSpeed", 14.0f, "Vel. mínima pra cortar (m/s)");
            cfgViewDist = cat.CreateEntry("TitanViewDistance", 130.0f, "Distância de visão do titã (m)");
            cfgMaxTitans = cat.CreateEntry("MaxTitans", 12, "Máximo de titãs vivos");
            cfgShowNape = cat.CreateEntry("ShowNapeGlow", true, "Brilho vermelho na nuca");
            cfgCanEat = cat.CreateEntry("TitansCanEat", true, "Titã pode agarrar e comer (mata de verdade)");
            LoggerInstance.Msg("Titans v2. F6 titã | F9 waves | F7 limpar | F8 congelar | T Eren | G Levi");
        }

        public override void OnUpdate()
        {
            try
            {
                if (Input.GetKeyDown(KeyCode.F6)) SpawnTitan(RandomKind(), null);
                if (Input.GetKeyDown(KeyCode.F7)) ClearTitans();
                if (Input.GetKeyDown(KeyCode.F8)) { frozen = !frozen; Say(frozen ? "Titãs congelados" : "Titãs soltos"); }
                if (Input.GetKeyDown(KeyCode.F9)) ToggleWaves();
                if (Input.GetKeyDown(KeyCode.T)) TryEren();
                if (Input.GetKeyDown(KeyCode.G)) TryLevi();
                UpdateWaves();
                UpdateTitans();
            }
            catch (Exception e) { LoggerInstance.Error("OnUpdate: " + e.Message); }
        }

        // ---------------- player ----------------

        private Transform PlayerTransform()
        {
            try { var pm = PlayerMain.Instance; if (pm != null) return pm.transform; } catch { }
            var cam = Camera.main;
            return cam != null ? cam.transform : null;
        }

        private Vector3 PlayerVelocity()
        {
            try { var pm = PlayerMain.Instance; if (pm != null && pm.RigidBody != null) return pm.RigidBody.velocity; } catch { }
            return Vector3.zero;
        }

        private void PushPlayer(Vector3 force)
        {
            try { var pm = PlayerMain.Instance; if (pm != null && pm.RigidBody != null) pm.RigidBody.AddForce(force, ForceMode.VelocityChange); } catch { }
        }

        private void HoldPlayerAt(Vector3 pos)
        {
            try
            {
                var pm = PlayerMain.Instance;
                if (pm != null && pm.RigidBody != null)
                {
                    pm.RigidBody.position = pos;
                    pm.RigidBody.velocity = Vector3.zero;
                }
            }
            catch { }
        }

        private void KillPlayer()
        {
            try { GameCmds.Kill(); }
            catch (Exception e) { LoggerInstance.Warning("Kill do player falhou: " + e.Message); PushPlayer(Vector3.up * 50f); }
        }

        // ---------------- spawn / corpo ----------------

        private TitanKind RandomKind()
        {
            int r = rng.Next(100);
            if (r < 45) return TitanKind.Normal;
            if (r < 70) return TitanKind.Runner;
            if (r < 88) return TitanKind.Jumper;
            return TitanKind.Crawler;
        }

        private void SpawnTitan(TitanKind kind, Vector3? forcePos)
        {
            int alive = AliveCount();
            if (alive >= cfgMaxTitans.Value) { Say("Limite de titãs (" + cfgMaxTitans.Value + ")"); return; }

            var player = PlayerTransform();
            if (player == null) { Say("Entra numa partida primeiro"); return; }

            Vector3 spawnPos;
            if (forcePos.HasValue) spawnPos = forcePos.Value;
            else
            {
                var cam = Camera.main;
                Vector3 fwd = cam != null ? cam.transform.forward : player.forward;
                fwd.y = 0f; if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
                spawnPos = player.position + fwd.normalized * 45f;
            }
            spawnPos = GroundAt(spawnPos, player.position.y);

            // nunca spawnar grudado no player
            Vector3 flat = spawnPos - player.position; flat.y = 0f;
            if (flat.magnitude < 20f)
            {
                Vector3 dir = flat.sqrMagnitude < 0.01f ? Vector3.forward : flat.normalized;
                spawnPos = GroundAt(player.position + dir * 25f, player.position.y);
            }

            float scale = cfgScale.Value;
            float speed = cfgSpeed.Value;
            Color skin = new Color(0.72f, 0.55f, 0.45f);
            switch (kind)
            {
                case TitanKind.Runner: scale *= 0.8f; speed *= 2.1f; skin = new Color(0.55f, 0.4f, 0.33f); break;
                case TitanKind.Jumper: scale *= 0.9f; speed *= 1.25f; skin = new Color(0.6f, 0.62f, 0.42f); break;
                case TitanKind.Crawler: scale *= 0.7f; speed *= 1.7f; skin = new Color(0.5f, 0.45f, 0.5f); break;
                case TitanKind.Ally: scale *= 1.15f; speed *= 1.6f; skin = new Color(0.35f, 0.5f, 0.65f); break;
            }

            var t = BuildTitan(kind, scale, skin);
            t.speed = speed * UnityEngine.Random.Range(0.85f, 1.2f);
            t.tr.position = spawnPos;
            Vector3 look = player.position - spawnPos; look.y = 0f;
            if (look.sqrMagnitude > 0.01f) t.tr.rotation = Quaternion.LookRotation(look);
            t.state = kind == TitanKind.Ally ? TitanState.Chase : TitanState.Wander;
            t.wanderHeading = (float)rng.NextDouble() * 360f;
            titans.Add(t);

            if (kind != TitanKind.Ally)
                Say(KindName(kind) + " apareceu! (" + AliveCount() + " vivos)");
        }

        private string KindName(TitanKind k)
        {
            switch (k)
            {
                case TitanKind.Runner: return "Titã CORREDOR";
                case TitanKind.Jumper: return "Titã SALTADOR";
                case TitanKind.Crawler: return "Titã RASTEJANTE";
                case TitanKind.Ally: return "Titã ALIADO";
                default: return "Titã";
            }
        }

        private Vector3 GroundAt(Vector3 pos, float refY)
        {
            // raio a partir de pouco acima da altura do player, pra não acertar teto do mapa
            Vector3 origin = new Vector3(pos.x, refY + 35f, pos.z);
            RaycastHit hit;
            if (Physics.Raycast(origin, Vector3.down, out hit, 300f))
                return hit.point;
            return new Vector3(pos.x, refY, pos.z);
        }

        private GameObject Part(Transform parent, PrimitiveType prim, Vector3 pos, Vector3 scl, Color c, Vector3? euler = null)
        {
            var go = GameObject.CreatePrimitive(prim);
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scl;
            if (euler.HasValue) go.transform.localRotation = Quaternion.Euler(euler.Value);
            try { go.GetComponent<Renderer>().material.color = c; } catch { }
            return go;
        }

        // Corpo articulado (unidades locais ~ humano 2.4 de altura, escalado por 'scale')
        private Titan BuildTitan(TitanKind kind, float scale, Color skin)
        {
            var t = new Titan { kind = kind, scale = scale, spawnTime = Time.time };
            Color dark = skin * 0.8f;
            Color muscle = new Color(skin.r * 1.08f, skin.g * 0.82f, skin.b * 0.75f);

            var root = new GameObject("TITAN_" + kind);
            t.root = root; t.tr = root.transform;

            var hips = new GameObject("hips").transform;
            hips.SetParent(root.transform, false);
            hips.localPosition = new Vector3(0f, 1.15f, 0f);
            t.hips = hips;

            var torso = new GameObject("torso").transform;
            torso.SetParent(hips, false);
            torso.localPosition = new Vector3(0f, 0.42f, 0f);
            t.torso = torso;
            Part(torso, PrimitiveType.Capsule, new Vector3(0f, 0.1f, 0f), new Vector3(0.78f, 0.52f, 0.46f), skin);
            Part(torso, PrimitiveType.Sphere, new Vector3(0f, -0.18f, 0.08f), new Vector3(0.62f, 0.5f, 0.45f), muscle); // barriga

            var head = new GameObject("head").transform;
            head.SetParent(torso, false);
            head.localPosition = new Vector3(0f, 0.72f, 0.02f);
            t.head = head;
            Part(head, PrimitiveType.Sphere, Vector3.zero, new Vector3(0.42f, 0.46f, 0.44f), skin);
            Part(head, PrimitiveType.Sphere, new Vector3(0f, -0.12f, 0.17f), new Vector3(0.3f, 0.18f, 0.22f), muscle); // mandíbula
            Part(head, PrimitiveType.Sphere, new Vector3(0.1f, 0.06f, 0.17f), new Vector3(0.07f, 0.07f, 0.05f), Color.white);
            Part(head, PrimitiveType.Sphere, new Vector3(-0.1f, 0.06f, 0.17f), new Vector3(0.07f, 0.07f, 0.05f), Color.white);

            // nuca
            var nape = new GameObject("TITAN_NAPE").transform;
            nape.SetParent(torso, false);
            nape.localPosition = new Vector3(0f, 0.52f, -0.26f);
            t.napeAnchor = nape;
            if (cfgShowNape.Value && kind != TitanKind.Ally)
            {
                var glow = Part(nape, PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.16f, new Color(1f, 0.1f, 0.1f));
                glow.name = "nape_glow";
                try
                {
                    var m = glow.GetComponent<Renderer>().material;
                    m.EnableKeyword("_EMISSION");
                    m.SetColor("_EmissionColor", new Color(1.6f, 0.12f, 0.12f));
                }
                catch { }
                t.napeMarker = glow;
            }

            // braços: pivô no ombro
            t.armL = BuildArm(t, torso, -1, skin, dark, out t.handL, out t.wristL);
            t.armR = BuildArm(t, torso, +1, skin, dark, out t.handR, out t.wristR);

            // pernas: pivô no quadril
            t.legL = BuildLeg(t, hips, -1, skin, dark, out t.kneeL);
            t.legR = BuildLeg(t, hips, +1, skin, dark, out t.kneeR);

            if (kind == TitanKind.Crawler)
            {
                hips.localPosition = new Vector3(0f, 0.62f, 0f);
                torso.localRotation = Quaternion.Euler(68f, 0f, 0f); // corpo pra frente, estilo rastejante
            }

            root.transform.localScale = Vector3.one * scale;
            t.height = (kind == TitanKind.Crawler ? 1.5f : 2.45f) * scale;
            return t;
        }

        private Transform BuildArm(Titan t, Transform torso, int side, Color skin, Color dark, out Transform hand, out Transform wrist)
        {
            var shoulder = new GameObject("arm" + side).transform;
            shoulder.SetParent(torso, false);
            shoulder.localPosition = new Vector3(0.5f * side, 0.5f, 0f);
            Part(shoulder, PrimitiveType.Capsule, new Vector3(0f, -0.3f, 0f), new Vector3(0.2f, 0.32f, 0.2f), skin);
            var fore = new GameObject("fore").transform;
            fore.SetParent(shoulder, false);
            fore.localPosition = new Vector3(0f, -0.62f, 0f);
            Part(fore, PrimitiveType.Capsule, new Vector3(0f, -0.22f, 0f), new Vector3(0.16f, 0.26f, 0.16f), dark);
            wrist = new GameObject("wrist").transform;
            wrist.SetParent(fore, false);
            wrist.localPosition = new Vector3(0f, -0.46f, 0f);
            hand = new GameObject("hand").transform;
            hand.SetParent(wrist, false);
            Part(hand, PrimitiveType.Sphere, Vector3.zero, new Vector3(0.22f, 0.18f, 0.24f), dark);
            return shoulder;
        }

        private Transform BuildLeg(Titan t, Transform hips, int side, Color skin, Color dark, out Transform knee)
        {
            var thigh = new GameObject("leg" + side).transform;
            thigh.SetParent(hips, false);
            thigh.localPosition = new Vector3(0.24f * side, -0.05f, 0f);
            Part(thigh, PrimitiveType.Capsule, new Vector3(0f, -0.3f, 0f), new Vector3(0.26f, 0.34f, 0.26f), skin);
            knee = new GameObject("knee").transform;
            knee.SetParent(thigh, false);
            knee.localPosition = new Vector3(0f, -0.62f, 0f);
            Part(knee, PrimitiveType.Capsule, new Vector3(0f, -0.25f, 0f), new Vector3(0.2f, 0.3f, 0.2f), dark);
            Part(knee, PrimitiveType.Cube, new Vector3(0f, -0.54f, 0.08f), new Vector3(0.22f, 0.12f, 0.36f), dark); // pé
            return thigh;
        }

        // ---------------- waves ----------------

        private void ToggleWaves()
        {
            waveMode = !waveMode;
            if (waveMode) { waveNum = 0; waveNextTime = Time.time + 2f; Say("MODO WAVES ligado — prepara!"); }
            else { waveNextTime = -1f; Say("Modo waves desligado"); }
        }

        private void UpdateWaves()
        {
            if (!waveMode) return;
            var player = PlayerTransform();
            if (player == null) return;

            int enemies = 0;
            foreach (var t in titans) if (t.kind != TitanKind.Ally && t.state != TitanState.Dying) enemies++;

            if (enemies == 0 && waveNextTime < 0f)
            {
                int bonus = waveNum * 100;
                if (waveNum > 0) { totalScore += bonus; Announce("WAVE " + waveNum + " LIMPA! +" + bonus + " pts"); }
                waveNextTime = Time.time + 6f;
            }
            if (waveNextTime > 0f && Time.time >= waveNextTime)
            {
                waveNextTime = -1f;
                waveNum++;
                int count = Mathf.Min(2 + waveNum, cfgMaxTitans.Value);
                for (int i = 0; i < count; i++)
                {
                    float ang = (float)rng.NextDouble() * 6.2832f;
                    float dist = 70f + (float)rng.NextDouble() * 60f;
                    Vector3 pos = player.position + new Vector3(Mathf.Cos(ang) * dist, 0f, Mathf.Sin(ang) * dist);
                    SpawnTitan(RandomKind(), pos);
                }
                Announce("WAVE " + waveNum + " — " + count + " titãs chegando!");
            }
        }

        // ---------------- skills ----------------

        private void TryEren()
        {
            if (Time.time < erenReadyAt) { Say("Eren em cooldown (" + Mathf.CeilToInt(erenReadyAt - Time.time) + "s)"); return; }
            var player = PlayerTransform();
            if (player == null) return;
            erenReadyAt = Time.time + 60f;
            erenEndsAt = Time.time + 25f;
            SpawnTitan(TitanKind.Ally, GroundAt(player.position + player.forward * 8f, player.position.y));
            Announce("EREN TRANSFORMOU! Titã aliado por 25s — ele soca os outros!");
        }

        private void TryLevi()
        {
            if (Time.time < leviReadyAt) { Say("Levi em cooldown (" + Mathf.CeilToInt(leviReadyAt - Time.time) + "s)"); return; }
            leviReadyAt = Time.time + 40f;
            leviEndsAt = Time.time + 7f;
            var cam = Camera.main;
            if (cam != null) PushPlayer(cam.transform.forward * 30f);
            Announce("FÚRIA DO LEVI! 7s: dash + corte facilitado e mais forte!");
        }

        private bool LeviActive() { return Time.time < leviEndsAt; }

        // ---------------- IA principal ----------------

        private int AliveCount()
        {
            int n = 0;
            foreach (var t in titans) if (t.kind != TitanKind.Ally && t.state != TitanState.Dying) n++;
            return n;
        }

        private void UpdateTitans()
        {
            if (titans.Count == 0) return;
            var player = PlayerTransform();
            Vector3 pPos = player != null ? player.position : Vector3.zero;
            float pSpeed = PlayerVelocity().magnitude;
            float dt = Time.deltaTime;

            float cutRadius = cfgCutRadius.Value * (LeviActive() ? 1.6f : 1f);
            float cutSpeed = cfgCutSpeed.Value * (LeviActive() ? 0.5f : 1f);
            float jointRadius = Mathf.Max(2f, cutRadius * 0.7f);
            float jointSpeed = cutSpeed * 0.7f;

            for (int i = titans.Count - 1; i >= 0; i--)
            {
                var t = titans[i];
                if (t.root == null) { titans.RemoveAt(i); continue; }
                t.stateTime += dt;
                float age = Time.time - t.spawnTime;

                // ----- morrendo -----
                if (t.state == TitanState.Dying)
                {
                    t.tr.Rotate(Vector3.right, Mathf.Max(0f, 75f * dt * (1.6f - t.stateTime)), Space.Self);
                    if (t.stateTime > 1.2f) t.tr.position += Vector3.down * dt * 0.4f; // afunda devagar (vapor)
                    if (t.stateTime > 6f) { Object.Destroy(t.root); titans.RemoveAt(i); }
                    continue;
                }

                // ----- aliado expira -----
                if (t.kind == TitanKind.Ally && Time.time > erenEndsAt) { StartDeath(t, false); continue; }

                // ----- cortes do player (nuca e juntas) — não vale pro aliado -----
                if (player != null && t.kind != TitanKind.Ally && age > 0.5f)
                {
                    float dNape = Vector3.Distance(pPos, t.napeAnchor.position);
                    if (dNape < cutRadius && pSpeed > cutSpeed) { CutNape(t, pSpeed, dNape, cutRadius, cutSpeed); continue; }

                    if (pSpeed > jointSpeed && Time.time - t.lastJointHit > 0.6f)
                    {
                        if (!t.armLGone && Near(pPos, t.wristL, jointRadius)) { SeverArm(t, false); continue; }
                        if (!t.armRGone && Near(pPos, t.wristR, jointRadius)) { SeverArm(t, true); continue; }
                        if (Near(pPos, t.kneeL, jointRadius) || Near(pPos, t.kneeR, jointRadius)) { StunTitan(t); continue; }
                    }
                }

                if (frozen || player == null) continue;

                // ----- agarrado: segura o player na mão e leva à boca -----
                if (t.state == TitanState.Grab)
                {
                    UpdateGrab(t, pPos, pSpeed, cutSpeed, dt);
                    continue;
                }

                // ----- atordoado (junta acertada): caído, nuca exposta -----
                if (t.state == TitanState.Stunned)
                {
                    float f = Mathf.Clamp01(t.stateTime / 0.4f);
                    t.hips.localPosition = new Vector3(0f, Mathf.Lerp(1.15f, 0.55f, f), 0f);
                    t.torso.localRotation = Quaternion.Euler(Mathf.Lerp(0f, 55f, f), 0f, 0f);
                    if (t.stateTime > 6f)
                    {
                        t.hips.localPosition = new Vector3(0f, t.kind == TitanKind.Crawler ? 0.62f : 1.15f, 0f);
                        t.torso.localRotation = t.kind == TitanKind.Crawler ? Quaternion.Euler(68f, 0f, 0f) : Quaternion.identity;
                        SetState(t, TitanState.Chase);
                    }
                    continue;
                }

                // ----- aliado: caça o titã inimigo mais perto -----
                if (t.kind == TitanKind.Ally) { UpdateAlly(t, dt); continue; }

                // ----- percepção -----
                Vector3 to = pPos - t.tr.position; to.y = 0f;
                float dist = to.magnitude;
                bool sees = dist < 16f ||
                            (dist < cfgViewDist.Value && Vector3.Angle(t.tr.forward, to) < 75f);
                if (t.state == TitanState.Wander && sees) { SetState(t, TitanState.Chase); if (dist > 30f) Say(KindName(t.kind) + " te viu!"); }
                else if (t.state == TitanState.Chase && dist > cfgViewDist.Value * 1.7f) SetState(t, TitanState.Wander);

                // ----- salto do saltador -----
                if (t.state == TitanState.Leap)
                {
                    // voo balístico simples na direção salva em wanderHeading
                    Vector3 dir = Quaternion.Euler(0f, t.wanderHeading, 0f) * Vector3.forward;
                    float tt = t.stateTime;
                    t.tr.position += dir * t.speed * 3.2f * dt + Vector3.up * (9f - 18f * tt) * dt;
                    Vector3 g = GroundAt(t.tr.position, pPos.y);
                    if (tt > 0.25f && t.tr.position.y <= g.y + 0.1f)
                    {
                        t.tr.position = g;
                        SetState(t, TitanState.Chase);
                    }
                    continue;
                }

                if (t.state == TitanState.Wander) UpdateWander(t, pPos, dt);
                else UpdateChase(t, pPos, dist, to, dt);
            }
        }

        private bool Near(Vector3 p, Transform anchor, float r)
        {
            return anchor != null && Vector3.Distance(p, anchor.position) < r;
        }

        private void SetState(Titan t, TitanState s) { t.state = s; t.stateTime = 0f; }

        private void UpdateWander(Titan t, Vector3 pPos, float dt)
        {
            if (Time.time > t.nextWanderTurn)
            {
                t.wanderHeading = (float)rng.NextDouble() * 360f;
                t.nextWanderTurn = Time.time + 4f + (float)rng.NextDouble() * 5f;
            }
            var targetRot = Quaternion.Euler(0f, t.wanderHeading, 0f);
            t.tr.rotation = Quaternion.Slerp(t.tr.rotation, targetRot, 1.2f * dt);
            Step(t, t.speed * 0.35f, dt, pPos.y);
        }

        private void UpdateChase(Titan t, Vector3 pPos, float dist, Vector3 to, float dt)
        {
            if (dist < 0.5f) return;
            Vector3 dir = to / dist;
            t.tr.rotation = Quaternion.Slerp(t.tr.rotation, Quaternion.LookRotation(dir), 2.4f * dt);

            // saltador decola
            if (t.kind == TitanKind.Jumper && dist > 25f && dist < 90f && Time.time - t.lastLeap > 5f)
            {
                t.lastLeap = Time.time;
                t.wanderHeading = Quaternion.LookRotation(dir).eulerAngles.y;
                SetState(t, TitanState.Leap);
                Say("Titã SALTADOR voou na sua direção!");
                return;
            }

            float stopRange = t.height * 0.28f;
            if (dist > stopRange) Step(t, t.speed, dt, pPos.y);

            // tenta agarrar / patada
            bool canGrab = cfgCanEat.Value && !(t.armLGone && t.armRGone) && t.kind != TitanKind.Crawler;
            float reach = t.height * 0.4f;
            bool playerLow = pPos.y < t.tr.position.y + t.height * 0.85f;
            float sinceSpawn = Time.time - t.spawnTime;
            if (dist < reach && playerLow && sinceSpawn > 1.5f && Time.time - t.lastGrabTry > 3.5f)
            {
                t.lastGrabTry = Time.time;
                if (canGrab && rng.Next(100) < 55)
                {
                    t.grabHand = (!t.armRGone) ? t.handR : t.handL;
                    t.grabStart = Time.time;
                    SetState(t, TitanState.Grab);
                    Announce("TE AGARROU! Corta o PULSO dele (voa perto da mão) pra escapar!");
                }
                else
                {
                    Vector3 away = (pPos - t.tr.position).normalized;
                    PushPlayer(away * 24f + Vector3.up * 12f);
                    Say("Patada do titã! Voa pra nuca!");
                }
            }
        }

        private void Step(Titan t, float speed, float dt, float refY)
        {
            Vector3 next = t.tr.position + t.tr.forward * speed * dt;
            next = GroundAt(next, t.tr.position.y + 2f);
            // não deixa teleportar pra plataformas muito acima
            if (next.y > t.tr.position.y + 4f) next.y = t.tr.position.y;
            t.tr.position = next;

            // animação procedural de andar
            t.bobPhase += dt * Mathf.Max(3f, speed * 1.1f);
            float swing = Mathf.Sin(t.bobPhase);
            float amp = t.kind == TitanKind.Runner ? 42f : 28f;
            if (t.legL != null) t.legL.localRotation = Quaternion.Euler(swing * amp, 0f, 0f);
            if (t.legR != null) t.legR.localRotation = Quaternion.Euler(-swing * amp, 0f, 0f);
            if (t.armL != null && !t.armLGone) t.armL.localRotation = Quaternion.Euler(-swing * amp * 0.7f, 0f, 8f);
            if (t.armR != null && !t.armRGone) t.armR.localRotation = Quaternion.Euler(swing * amp * 0.7f, 0f, -8f);
            if (t.kind != TitanKind.Crawler)
                t.torso.localRotation = Quaternion.Euler(Mathf.Abs(swing) * 3f + (t.kind == TitanKind.Runner ? 18f : 4f), 0f, swing * 2.5f);
        }

        private void UpdateGrab(Titan t, Vector3 pPos, float pSpeed, float cutSpeed, float dt)
        {
            float held = Time.time - t.grabStart;
            if (t.grabHand == null) { SetState(t, TitanState.Chase); return; }

            // escape: cortar o pulso da mão que segura
            Transform wrist = t.grabHand == t.handR ? t.wristR : t.wristL;
            if (pSpeed > cutSpeed * 0.55f && Near(pPos, wrist, Mathf.Max(2.5f, cfgCutRadius.Value)))
            {
                SeverArm(t, t.grabHand == t.handR);
                Announce("ESCAPOU cortando o braço! +40 pts");
                totalScore += 40;
                PushPlayer(Vector3.up * 10f);
                StunTitan(t);
                return;
            }

            // mão sobe até a boca
            float f = Mathf.Clamp01(held / 3.2f);
            var arm = t.grabHand == t.handR ? t.armR : t.armL;
            if (arm != null)
                arm.localRotation = Quaternion.Euler(Mathf.Lerp(-30f, -150f, f), Mathf.Lerp(0f, t.grabHand == t.handR ? -35f : 35f, f), 0f);
            HoldPlayerAt(t.grabHand.position);

            if (held > 3.2f)
            {
                Announce("O TITÃ TE COMEU!");
                KillPlayer();
                SetState(t, TitanState.Chase);
            }
        }

        private void UpdateAlly(Titan t, float dt)
        {
            Titan target = null; float best = 99999f;
            foreach (var e in titans)
            {
                if (e == t || e.kind == TitanKind.Ally || e.state == TitanState.Dying) continue;
                float d = Vector3.Distance(t.tr.position, e.tr.position);
                if (d < best) { best = d; target = e; }
            }
            if (target == null) return;

            Vector3 to = target.tr.position - t.tr.position; to.y = 0f;
            float dist = to.magnitude;
            if (dist > 0.5f)
            {
                t.tr.rotation = Quaternion.Slerp(t.tr.rotation, Quaternion.LookRotation(to / dist), 3f * dt);
                if (dist > t.height * 0.35f) Step(t, t.speed, dt, t.tr.position.y);
            }
            if (dist < t.height * 0.45f && Time.time - t.lastGrabTry > 1.6f)
            {
                t.lastGrabTry = Time.time;
                // soco do Eren: mata o titã (metade dos pontos)
                int pts = 50;
                totalScore += pts; kills++;
                Announce("Soco do titã aliado! +" + pts + " pts");
                StartDeath(target, true);
            }
        }

        // ---------------- dano / morte ----------------

        private void CutNape(Titan t, float playerSpeed, float napeDist, float cutRadius, float cutSpeed)
        {
            kills++;
            float speedFactor = playerSpeed / Mathf.Max(1f, cutSpeed);
            float precision = 1f - Mathf.Clamp01(napeDist / Mathf.Max(0.1f, cutRadius));
            float stunBonus = t.state == TitanState.Stunned ? 0.85f : 1f; // caído = mais fácil, vale um pouco menos
            int points = Mathf.RoundToInt(100f * speedFactor * (0.4f + 1.2f * precision) * stunBonus);
            totalScore += points;
            if (points > bestCut) bestCut = points;

            string grade, praise;
            if (speedFactor >= 2.2f && precision >= 0.7f) { grade = "CORTE LENDÁRIO"; praise = "movimentação ABSURDA, mergulho perfeito!"; }
            else if (speedFactor >= 1.7f && precision >= 0.5f) { grade = "CORTE PERFEITO"; praise = "voo rápido e certeiro, elite!"; }
            else if (speedFactor >= 1.3f || precision >= 0.5f) { grade = "CORTE CRÍTICO"; praise = "bom ataque! Mais velocidade = mais pontos."; }
            else { grade = "corte fraco"; praise = "pegou de raspão... capricha no voo."; }

            Announce(grade + "  +" + points + " pts  (" + praise + ")");
            LoggerInstance.Msg(grade + " +" + points + " [vel " + playerSpeed.ToString("F1") + " | precisão " + (precision * 100f).ToString("F0") + "%]");
            StartDeath(t, true);
        }

        private void StartDeath(Titan t, bool fall)
        {
            if (t.napeMarker != null) { try { Object.Destroy(t.napeMarker); } catch { } t.napeMarker = null; }
            SetState(t, TitanState.Dying);
        }

        private void SeverArm(Titan t, bool right)
        {
            t.lastJointHit = Time.time;
            var arm = right ? t.armR : t.armL;
            if (right) t.armRGone = true; else t.armLGone = true;
            if (arm != null) arm.gameObject.SetActive(false);
            totalScore += 25;
            Say("Braço decepado! +25 pts — ele regenera não, hein");
            if (t.state == TitanState.Grab) SetState(t, TitanState.Chase);
        }

        private void StunTitan(Titan t)
        {
            t.lastJointHit = Time.time;
            totalScore += 15;
            SetState(t, TitanState.Stunned);
            Say("Acertou a junta! Titã caiu — NUCA EXPOSTA! +15 pts");
        }

        private void ClearTitans()
        {
            foreach (var t in titans) if (t.root != null) Object.Destroy(t.root);
            titans.Clear();
            Say("Titãs removidos");
        }

        // ---------------- HUD ----------------

        private void Say(string msg) { lastMsg = msg; lastMsgTime = Time.time; }
        private void Announce(string msg) { cutMsg = msg; cutMsgTime = Time.time; LoggerInstance.Msg(msg); }

        public override void OnGUI()
        {
            int alive = AliveCount();
            string wave = waveMode ? ("  |  WAVE " + waveNum) : "";
            string eren = Time.time < erenReadyAt ? ("Eren " + Mathf.CeilToInt(erenReadyAt - Time.time) + "s") : "Eren PRONTO (T)";
            string levi = LeviActive() ? "LEVI ATIVO!" : (Time.time < leviReadyAt ? ("Levi " + Mathf.CeilToInt(leviReadyAt - Time.time) + "s") : "Levi PRONTO (G)");
            string line = "TITANS  |  vivos: " + alive + wave + "  |  abates: " + kills + "  |  pontos: " + totalScore
                + "  |  melhor: " + bestCut + "  |  " + eren + "  |  " + levi;
            GUI.Label(new Rect(14f, 12f, 1400f, 24f), "<b><color=#ff5544>" + line + "</color></b>");
            GUI.Label(new Rect(14f, 34f, 1400f, 22f), "<color=#999999>F6 titã  F9 waves  F7 limpar  F8 congelar  —  nuca vermelha = ponto fraco; juntas (pulsos/joelhos) derrubam</color>");
            if (Time.time - lastMsgTime < 3.5f)
                GUI.Label(new Rect(14f, 56f, 1400f, 24f), "<color=#ffcc44>" + lastMsg + "</color>");
            if (Time.time - cutMsgTime < 5f)
                GUI.Label(new Rect(14f, 80f, 1400f, 28f), "<b><size=18><color=#44ff66>" + cutMsg + "</color></size></b>");
        }
    }
}
