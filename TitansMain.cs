using System;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;
using UnhollowerRuntimeLib;
using Player;
using AoTGameEngine.Character;
using Object = UnityEngine.Object;
using GameCmds = AoTGameUI.Console.Commands.Game.GameCommands;

[assembly: MelonInfo(typeof(TitansMod.TitansMain), "Titans", "3.0.0", "Jeff & Claude")]
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
            public float scale;
            public float height;
            public float speed;
            public float speedMul = 1f;
            public float stateTime;
            public float spawnTime;
            public float bobPhase;
            public float lastStepSign;
            public float wanderHeading;
            public float nextWanderTurn;
            public float lastLeap;
            public float lastGrabTry;
            public float grabStart;
            public float lastJointHit;

            public Transform hips, torso, head, napeAnchor;
            public GameObject napeMarker;
            public Transform armL, armR, handL, handR;
            public Transform legL, legR;
            public Transform kneeL, kneeR, wristL, wristR;
            public bool armLGone, armRGone;
            public Transform grabHand;
            public AudioSource voice;
            public List<Renderer> skinParts = new List<Renderer>();
        }

        private struct PlayerRef { public Vector3 pos; public bool isLocal; }

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
        private float erenReadyAt, erenEndsAt;
        private float leviReadyAt, leviEndsAt;
        private float mikasaReadyAt;
        private float arminReadyAt, arminEndsAt;

        // menu F10
        private bool menuOpen;
        private Rect menuRect = new Rect(60f, 120f, 360f, 430f);

        // players na cena (cache)
        private readonly List<PlayerRef> players = new List<PlayerRef>();
        private float lastPlayerScan;

        // audio / material do jogo (scavenge)
        private AudioClip clipStomp, clipSteam, clipHit;
        private Material titanSkinMat;
        private float lastScavenge = -99f;

        // hud
        private string lastMsg = ""; private float lastMsgTime = -99f;
        private string cutMsg = ""; private float cutMsgTime = -99f;

        // config
        private MelonPreferences_Entry<float> cfgScale, cfgSpeed, cfgCutRadius, cfgCutSpeed, cfgViewDist;
        private MelonPreferences_Entry<int> cfgMaxTitans;
        private MelonPreferences_Entry<bool> cfgShowNape, cfgCanEat, cfgSounds, cfgGameSkin;

        public override void OnInitializeMelon()
        {
            var cat = MelonPreferences.CreateCategory("Titans");
            cfgScale = cat.CreateEntry("TitanScale", 7.0f, "Escala base do titã");
            cfgSpeed = cat.CreateEntry("TitanSpeed", 6.5f, "Velocidade base (m/s)");
            cfgCutRadius = cat.CreateEntry("NapeCutRadius", 3.5f, "Raio da nuca (m)");
            cfgCutSpeed = cat.CreateEntry("NapeCutMinSpeed", 14.0f, "Vel. mínima pra cortar (m/s)");
            cfgViewDist = cat.CreateEntry("TitanViewDistance", 130.0f, "Distância de visão (m)");
            cfgMaxTitans = cat.CreateEntry("MaxTitans", 12, "Máximo de titãs vivos");
            cfgShowNape = cat.CreateEntry("ShowNapeGlow", true, "Brilho vermelho na nuca");
            cfgCanEat = cat.CreateEntry("TitansCanEat", true, "Titã pode agarrar e comer");
            cfgSounds = cat.CreateEntry("TitanSounds", true, "Sons de passos/vapor (reusa áudio do jogo)");
            cfgGameSkin = cat.CreateEntry("UseGameSkin", true, "Usar material de titã do próprio jogo");
            LoggerInstance.Msg("Titans v3. F6 titã | F9 waves | F10 menu | T Eren | G Levi | Y Mikasa | H Armin");
        }

        public override void OnUpdate()
        {
            try
            {
                if (Input.GetKeyDown(KeyCode.F6)) SpawnTitan(RandomKind(), null);
                if (Input.GetKeyDown(KeyCode.F7)) ClearTitans();
                if (Input.GetKeyDown(KeyCode.F8)) { frozen = !frozen; Say(frozen ? "Titãs congelados" : "Titãs soltos"); }
                if (Input.GetKeyDown(KeyCode.F9)) ToggleWaves();
                if (Input.GetKeyDown(KeyCode.F10)) ToggleMenu();
                if (Input.GetKeyDown(KeyCode.T)) TryEren();
                if (Input.GetKeyDown(KeyCode.G)) TryLevi();
                if (Input.GetKeyDown(KeyCode.Y)) TryMikasa();
                if (Input.GetKeyDown(KeyCode.H)) TryArmin();
                ScanPlayers();
                UpdateWaves();
                UpdateTitans();
            }
            catch (Exception e) { LoggerInstance.Error("OnUpdate: " + e.Message); }
        }

        // ---------------- players (local + remotos) ----------------

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
                if (pm != null && pm.RigidBody != null) { pm.RigidBody.position = pos; pm.RigidBody.velocity = Vector3.zero; }
            }
            catch { }
        }

        private void KillPlayer()
        {
            try { GameCmds.Kill(); }
            catch (Exception e) { LoggerInstance.Warning("Kill falhou: " + e.Message); PushPlayer(Vector3.up * 50f); }
        }

        // Todos os personagens na cena (local + amigos no lobby): titã caça o mais perto
        private void ScanPlayers()
        {
            if (Time.time - lastPlayerScan < 1f) return;
            lastPlayerScan = Time.time;
            players.Clear();
            var local = PlayerTransform();
            Vector3 localPos = local != null ? local.position : Vector3.zero;
            try
            {
                var found = Object.FindObjectsOfType(Il2CppType.Of<CharacterObject>());
                foreach (var o in found)
                {
                    var c = o.TryCast<CharacterObject>();
                    if (c == null) continue;
                    Vector3 p = c.transform.position;
                    players.Add(new PlayerRef { pos = p, isLocal = local != null && (p - localPos).sqrMagnitude < 9f });
                }
            }
            catch { }
            if (players.Count == 0 && local != null)
                players.Add(new PlayerRef { pos = localPos, isLocal = true });
        }

        private bool NearestPlayer(Vector3 from, out Vector3 pos, out bool isLocal)
        {
            pos = Vector3.zero; isLocal = false;
            float best = float.MaxValue; bool ok = false;
            foreach (var p in players)
            {
                float d = (p.pos - from).sqrMagnitude;
                if (d < best) { best = d; pos = p.pos; isLocal = p.isLocal; ok = true; }
            }
            if (!ok)
            {
                var local = PlayerTransform();
                if (local != null) { pos = local.position; isLocal = true; ok = true; }
            }
            return ok;
        }

        // ---------------- scavenge de assets do jogo ----------------

        private void Scavenge()
        {
            if (Time.time - lastScavenge < 30f && (clipStomp != null || !cfgSounds.Value) && (titanSkinMat != null || !cfgGameSkin.Value)) return;
            lastScavenge = Time.time;
            try
            {
                if (cfgSounds.Value && (clipStomp == null || clipSteam == null || clipHit == null))
                {
                    var clips = Resources.FindObjectsOfTypeAll(Il2CppType.Of<AudioClip>());
                    foreach (var o in clips)
                    {
                        var c = o.TryCast<AudioClip>();
                        if (c == null) continue;
                        string n = (c.name ?? "").ToLowerInvariant();
                        if (clipStomp == null && (n.Contains("foot") || n.Contains("land") || n.Contains("slide"))) clipStomp = c;
                        if (clipSteam == null && n.Contains("gas")) clipSteam = c;
                        if (clipHit == null && (n.Contains("impact") || n.Contains("hit") || n.Contains("cut") || n.Contains("slash"))) clipHit = c;
                    }
                }
                if (cfgGameSkin.Value && titanSkinMat == null)
                {
                    var mats = Resources.FindObjectsOfTypeAll(Il2CppType.Of<Material>());
                    foreach (var o in mats)
                    {
                        var m = o.TryCast<Material>();
                        if (m == null) continue;
                        string n = (m.name ?? "").ToLowerInvariant();
                        if (n.Contains("training_titan")) { titanSkinMat = m; break; }
                    }
                }
            }
            catch { }
        }

        private void PlaySfx(Titan t, AudioClip clip, float volume, float pitch)
        {
            if (!cfgSounds.Value || clip == null || t.voice == null) return;
            try
            {
                t.voice.clip = clip;
                t.voice.volume = volume;
                t.voice.pitch = pitch;
                t.voice.Play();
            }
            catch { }
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
            if (AliveCount() >= cfgMaxTitans.Value && kind != TitanKind.Ally) { Say("Limite de titãs (" + cfgMaxTitans.Value + ")"); return; }
            var player = PlayerTransform();
            if (player == null) { Say("Entra numa partida primeiro"); return; }
            Scavenge();

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

            if (kind != TitanKind.Ally) Say(KindName(kind) + " apareceu! (" + AliveCount() + " vivos)");
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
            Vector3 origin = new Vector3(pos.x, refY + 35f, pos.z);
            RaycastHit hit;
            if (Physics.Raycast(origin, Vector3.down, out hit, 300f)) return hit.point;
            return new Vector3(pos.x, refY, pos.z);
        }

        private GameObject Part(Titan t, Transform parent, PrimitiveType prim, Vector3 pos, Vector3 scl, Color c, bool isSkin = true)
        {
            var go = GameObject.CreatePrimitive(prim);
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scl;
            try
            {
                var r = go.GetComponent<Renderer>();
                if (isSkin && titanSkinMat != null && cfgGameSkin.Value)
                {
                    r.material = new Material(titanSkinMat);
                    r.material.color = c;
                }
                else r.material.color = c;
                if (isSkin) t.skinParts.Add(r);
            }
            catch { }
            return go;
        }

        private Titan BuildTitan(TitanKind kind, float scale, Color skin)
        {
            var t = new Titan { kind = kind, scale = scale, spawnTime = Time.time };
            Color dark = skin * 0.8f;
            Color muscle = new Color(skin.r * 1.08f, skin.g * 0.82f, skin.b * 0.75f);

            var root = new GameObject("TITAN_" + kind);
            t.root = root; t.tr = root.transform;

            try
            {
                t.voice = root.AddComponent<AudioSource>();
                t.voice.spatialBlend = 1f;
                t.voice.maxDistance = 350f;
                t.voice.rolloffMode = AudioRolloffMode.Linear;
                t.voice.playOnAwake = false;
            }
            catch { }

            var hips = new GameObject("hips").transform;
            hips.SetParent(root.transform, false);
            hips.localPosition = new Vector3(0f, 1.15f, 0f);
            t.hips = hips;

            var torso = new GameObject("torso").transform;
            torso.SetParent(hips, false);
            torso.localPosition = new Vector3(0f, 0.42f, 0f);
            t.torso = torso;
            Part(t, torso, PrimitiveType.Capsule, new Vector3(0f, 0.1f, 0f), new Vector3(0.78f, 0.52f, 0.46f), skin);
            Part(t, torso, PrimitiveType.Sphere, new Vector3(0f, -0.18f, 0.08f), new Vector3(0.62f, 0.5f, 0.45f), muscle);

            var head = new GameObject("head").transform;
            head.SetParent(torso, false);
            head.localPosition = new Vector3(0f, 0.72f, 0.02f);
            t.head = head;
            Part(t, head, PrimitiveType.Sphere, Vector3.zero, new Vector3(0.42f, 0.46f, 0.44f), skin);
            Part(t, head, PrimitiveType.Sphere, new Vector3(0f, -0.12f, 0.17f), new Vector3(0.3f, 0.18f, 0.22f), muscle);
            Part(t, head, PrimitiveType.Sphere, new Vector3(0.1f, 0.06f, 0.17f), new Vector3(0.07f, 0.07f, 0.05f), Color.white, false);
            Part(t, head, PrimitiveType.Sphere, new Vector3(-0.1f, 0.06f, 0.17f), new Vector3(0.07f, 0.07f, 0.05f), Color.white, false);

            var nape = new GameObject("TITAN_NAPE").transform;
            nape.SetParent(torso, false);
            nape.localPosition = new Vector3(0f, 0.52f, -0.26f);
            t.napeAnchor = nape;
            if (cfgShowNape.Value && kind != TitanKind.Ally)
            {
                var glow = Part(t, nape, PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.16f, new Color(1f, 0.1f, 0.1f), false);
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

            t.armL = BuildArm(t, torso, -1, skin, dark, out t.handL, out t.wristL);
            t.armR = BuildArm(t, torso, +1, skin, dark, out t.handR, out t.wristR);
            t.legL = BuildLeg(t, hips, -1, skin, dark, out t.kneeL);
            t.legR = BuildLeg(t, hips, +1, skin, dark, out t.kneeR);

            if (kind == TitanKind.Crawler)
            {
                hips.localPosition = new Vector3(0f, 0.62f, 0f);
                torso.localRotation = Quaternion.Euler(68f, 0f, 0f);
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
            Part(t, shoulder, PrimitiveType.Capsule, new Vector3(0f, -0.3f, 0f), new Vector3(0.2f, 0.32f, 0.2f), skin);
            var fore = new GameObject("fore").transform;
            fore.SetParent(shoulder, false);
            fore.localPosition = new Vector3(0f, -0.62f, 0f);
            Part(t, fore, PrimitiveType.Capsule, new Vector3(0f, -0.22f, 0f), new Vector3(0.16f, 0.26f, 0.16f), dark);
            wrist = new GameObject("wrist").transform;
            wrist.SetParent(fore, false);
            wrist.localPosition = new Vector3(0f, -0.46f, 0f);
            hand = new GameObject("hand").transform;
            hand.SetParent(wrist, false);
            Part(t, hand, PrimitiveType.Sphere, Vector3.zero, new Vector3(0.22f, 0.18f, 0.24f), dark);
            return shoulder;
        }

        private Transform BuildLeg(Titan t, Transform hips, int side, Color skin, Color dark, out Transform knee)
        {
            var thigh = new GameObject("leg" + side).transform;
            thigh.SetParent(hips, false);
            thigh.localPosition = new Vector3(0.24f * side, -0.05f, 0f);
            Part(t, thigh, PrimitiveType.Capsule, new Vector3(0f, -0.3f, 0f), new Vector3(0.26f, 0.34f, 0.26f), skin);
            knee = new GameObject("knee").transform;
            knee.SetParent(thigh, false);
            knee.localPosition = new Vector3(0f, -0.62f, 0f);
            Part(t, knee, PrimitiveType.Capsule, new Vector3(0f, -0.25f, 0f), new Vector3(0.2f, 0.3f, 0.2f), dark);
            Part(t, knee, PrimitiveType.Cube, new Vector3(0f, -0.54f, 0.08f), new Vector3(0.22f, 0.12f, 0.36f), dark);
            return thigh;
        }

        // ---------------- vapor ----------------

        private void SteamBurst(Vector3 pos, float size, int count)
        {
            for (int i = 0; i < count; i++)
            {
                var puff = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                var col = puff.GetComponent<Collider>();
                if (col != null) Object.Destroy(col);
                puff.name = "titan_steam";
                puff.transform.position = pos + new Vector3(
                    ((float)rng.NextDouble() - 0.5f) * size,
                    (float)rng.NextDouble() * size * 0.5f,
                    ((float)rng.NextDouble() - 0.5f) * size);
                float s = size * (0.25f + (float)rng.NextDouble() * 0.5f);
                puff.transform.localScale = Vector3.one * s;
                try { puff.GetComponent<Renderer>().material.color = new Color(0.92f, 0.92f, 0.95f); } catch { }
                steam.Add(new Steam { go = puff, born = Time.time, rise = 2f + (float)rng.NextDouble() * 4f, life = 1.5f + (float)rng.NextDouble() * 2f });
            }
        }

        private class Steam { public GameObject go; public float born; public float rise; public float life; }
        private readonly List<Steam> steam = new List<Steam>();

        private void UpdateSteam(float dt)
        {
            for (int i = steam.Count - 1; i >= 0; i--)
            {
                var s = steam[i];
                if (s.go == null) { steam.RemoveAt(i); continue; }
                float age = Time.time - s.born;
                s.go.transform.position += Vector3.up * s.rise * dt;
                s.go.transform.localScale *= (1f - 0.8f * dt / s.life);
                if (age > s.life) { Object.Destroy(s.go); steam.RemoveAt(i); }
            }
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
            SteamBurst(player.position + player.forward * 8f + Vector3.up * 5f, 8f, 14);
            Announce("EREN TRANSFORMOU! Titã aliado por 25s!");
        }

        private void TryLevi()
        {
            if (Time.time < leviReadyAt) { Say("Levi em cooldown (" + Mathf.CeilToInt(leviReadyAt - Time.time) + "s)"); return; }
            leviReadyAt = Time.time + 40f;
            leviEndsAt = Time.time + 7f;
            var cam = Camera.main;
            if (cam != null) PushPlayer(cam.transform.forward * 30f);
            Announce("FÚRIA DO LEVI! 7s: dash + corte facilitado!");
        }

        private void TryMikasa()
        {
            if (Time.time < mikasaReadyAt) { Say("Mikasa em cooldown (" + Mathf.CeilToInt(mikasaReadyAt - Time.time) + "s)"); return; }
            var player = PlayerTransform();
            if (player == null) return;
            Titan target = null; float best = float.MaxValue;
            foreach (var t in titans)
            {
                if (t.kind == TitanKind.Ally || t.state == TitanState.Dying || t.napeAnchor == null) continue;
                float d = (t.napeAnchor.position - player.position).sqrMagnitude;
                if (d < best) { best = d; target = t; }
            }
            if (target == null) { Say("Nenhum titã pro impulso da Mikasa"); return; }
            mikasaReadyAt = Time.time + 30f;
            Vector3 nape = target.napeAnchor.position;
            Vector3 behind = nape - target.tr.forward * -8f; // 8m atrás da nuca
            behind = nape + (nape - target.tr.position).normalized * 6f + Vector3.up * 1f;
            HoldPlayerAt(behind);
            PushPlayer((nape - behind).normalized * (cfgCutSpeed.Value + 6f));
            Announce("IMPULSO DA MIKASA! Você foi lançada na nuca — finaliza!");
        }

        private void TryArmin()
        {
            if (Time.time < arminReadyAt) { Say("Armin em cooldown (" + Mathf.CeilToInt(arminReadyAt - Time.time) + "s)"); return; }
            arminReadyAt = Time.time + 45f;
            arminEndsAt = Time.time + 8f;
            foreach (var t in titans)
            {
                if (t.kind == TitanKind.Ally) continue;
                t.speedMul = 0.5f;
                if (t.napeMarker != null) try { t.napeMarker.transform.localScale = Vector3.one * 0.4f; } catch { }
            }
            Announce("PLANO DO ARMIN! Titãs lentos e nucas marcadas por 8s!");
        }

        private bool LeviActive() { return Time.time < leviEndsAt; }

        // ---------------- IA ----------------

        private int AliveCount()
        {
            int n = 0;
            foreach (var t in titans) if (t.kind != TitanKind.Ally && t.state != TitanState.Dying) n++;
            return n;
        }

        private void UpdateTitans()
        {
            float dt = Time.deltaTime;
            UpdateSteam(dt);
            if (titans.Count == 0) return;

            var localTr = PlayerTransform();
            Vector3 localPos = localTr != null ? localTr.position : Vector3.zero;
            float pSpeed = PlayerVelocity().magnitude;

            float cutRadius = cfgCutRadius.Value * (LeviActive() ? 1.6f : 1f);
            float cutSpeed = cfgCutSpeed.Value * (LeviActive() ? 0.5f : 1f);
            float jointRadius = Mathf.Max(2f, cutRadius * 0.7f);
            float jointSpeed = cutSpeed * 0.7f;

            bool arminOver = Time.time > arminEndsAt;

            for (int i = titans.Count - 1; i >= 0; i--)
            {
                var t = titans[i];
                if (t.root == null) { titans.RemoveAt(i); continue; }
                t.stateTime += dt;
                float age = Time.time - t.spawnTime;

                if (arminOver && t.speedMul < 1f)
                {
                    t.speedMul = 1f;
                    if (t.napeMarker != null) try { t.napeMarker.transform.localScale = Vector3.one * 0.16f; } catch { }
                }

                if (t.state == TitanState.Dying)
                {
                    t.tr.Rotate(Vector3.right, Mathf.Max(0f, 75f * dt * (1.6f - t.stateTime)), Space.Self);
                    if (t.stateTime > 1.2f) t.tr.position += Vector3.down * dt * 0.4f;
                    if (rng.Next(100) < 20) SteamBurst(t.napeAnchor != null ? t.napeAnchor.position : t.tr.position + Vector3.up * t.height * 0.7f, t.height * 0.25f, 1);
                    if (t.stateTime > 6f) { SteamBurst(t.tr.position + Vector3.up * 2f, t.height * 0.5f, 10); Object.Destroy(t.root); titans.RemoveAt(i); }
                    continue;
                }

                if (t.kind == TitanKind.Ally && Time.time > erenEndsAt) { StartDeath(t); continue; }

                // cortes do player LOCAL
                if (localTr != null && t.kind != TitanKind.Ally && age > 0.5f)
                {
                    float dNape = Vector3.Distance(localPos, t.napeAnchor.position);
                    if (dNape < cutRadius && pSpeed > cutSpeed) { CutNape(t, pSpeed, dNape, cutRadius, cutSpeed); continue; }

                    if (pSpeed > jointSpeed && Time.time - t.lastJointHit > 0.6f)
                    {
                        if (!t.armLGone && Near(localPos, t.wristL, jointRadius)) { SeverArm(t, false); continue; }
                        if (!t.armRGone && Near(localPos, t.wristR, jointRadius)) { SeverArm(t, true); continue; }
                        if (Near(localPos, t.kneeL, jointRadius) || Near(localPos, t.kneeR, jointRadius)) { StunTitan(t); continue; }
                    }
                }

                if (frozen) continue;

                if (t.state == TitanState.Grab) { UpdateGrab(t, localPos, pSpeed, cutSpeed, dt); continue; }

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

                if (t.kind == TitanKind.Ally) { UpdateAlly(t, dt); continue; }

                // alvo = jogador mais próximo (local ou amigo do lobby)
                Vector3 targetPos; bool targetIsLocal;
                if (!NearestPlayer(t.tr.position, out targetPos, out targetIsLocal)) continue;

                Vector3 to = targetPos - t.tr.position; to.y = 0f;
                float dist = to.magnitude;
                bool sees = dist < 16f || (dist < cfgViewDist.Value && Vector3.Angle(t.tr.forward, to) < 75f);
                if (t.state == TitanState.Wander && sees) { SetState(t, TitanState.Chase); if (dist > 30f && targetIsLocal) Say(KindName(t.kind) + " te viu!"); }
                else if (t.state == TitanState.Chase && dist > cfgViewDist.Value * 1.7f) SetState(t, TitanState.Wander);

                if (t.state == TitanState.Leap)
                {
                    Vector3 dir = Quaternion.Euler(0f, t.wanderHeading, 0f) * Vector3.forward;
                    float tt = t.stateTime;
                    t.tr.position += dir * t.speed * 3.2f * dt + Vector3.up * (9f - 18f * tt) * dt;
                    Vector3 g = GroundAt(t.tr.position, targetPos.y);
                    if (tt > 0.25f && t.tr.position.y <= g.y + 0.1f)
                    {
                        t.tr.position = g;
                        PlaySfx(t, clipStomp, 1f, 0.45f);
                        SetState(t, TitanState.Chase);
                    }
                    continue;
                }

                if (t.state == TitanState.Wander) UpdateWander(t, targetPos, dt);
                else UpdateChase(t, targetPos, targetIsLocal, dist, to, dt);
            }
        }

        private bool Near(Vector3 p, Transform anchor, float r)
        {
            return anchor != null && Vector3.Distance(p, anchor.position) < r;
        }

        private void SetState(Titan t, TitanState s) { t.state = s; t.stateTime = 0f; }

        private void UpdateWander(Titan t, Vector3 refPos, float dt)
        {
            if (Time.time > t.nextWanderTurn)
            {
                t.wanderHeading = (float)rng.NextDouble() * 360f;
                t.nextWanderTurn = Time.time + 4f + (float)rng.NextDouble() * 5f;
            }
            t.tr.rotation = Quaternion.Slerp(t.tr.rotation, Quaternion.Euler(0f, t.wanderHeading, 0f), 1.2f * dt);
            Step(t, t.speed * 0.35f * t.speedMul, dt);
        }

        private void UpdateChase(Titan t, Vector3 targetPos, bool targetIsLocal, float dist, Vector3 to, float dt)
        {
            if (dist < 0.5f) return;
            Vector3 dir = to / dist;
            t.tr.rotation = Quaternion.Slerp(t.tr.rotation, Quaternion.LookRotation(dir), 2.4f * dt);

            if (t.kind == TitanKind.Jumper && dist > 25f && dist < 90f && Time.time - t.lastLeap > 5f)
            {
                t.lastLeap = Time.time;
                t.wanderHeading = Quaternion.LookRotation(dir).eulerAngles.y;
                SetState(t, TitanState.Leap);
                if (targetIsLocal) Say("Titã SALTADOR voou na sua direção!");
                return;
            }

            float stopRange = t.height * 0.28f;
            if (dist > stopRange) Step(t, t.speed * t.speedMul, dt);

            bool canGrab = cfgCanEat.Value && !(t.armLGone && t.armRGone) && t.kind != TitanKind.Crawler;
            float reach = t.height * 0.4f;
            bool playerLow = targetPos.y < t.tr.position.y + t.height * 0.85f;
            if (targetIsLocal && dist < reach && playerLow && Time.time - t.spawnTime > 1.5f && Time.time - t.lastGrabTry > 3.5f)
            {
                t.lastGrabTry = Time.time;
                if (canGrab && rng.Next(100) < 55)
                {
                    t.grabHand = (!t.armRGone) ? t.handR : t.handL;
                    t.grabStart = Time.time;
                    SetState(t, TitanState.Grab);
                    Announce("TE AGARROU! Corta o PULSO dele pra escapar!");
                }
                else
                {
                    PushPlayer((targetPos - t.tr.position).normalized * 24f + Vector3.up * 12f);
                    PlaySfx(t, clipHit, 1f, 0.6f);
                    Say("Patada do titã! Voa pra nuca!");
                }
            }
        }

        private void Step(Titan t, float speed, float dt)
        {
            Vector3 next = t.tr.position + t.tr.forward * speed * dt;
            next = GroundAt(next, t.tr.position.y + 2f);
            if (next.y > t.tr.position.y + 4f) next.y = t.tr.position.y;
            t.tr.position = next;

            t.bobPhase += dt * Mathf.Max(3f, speed * 1.1f);
            float swing = Mathf.Sin(t.bobPhase);

            // passo tocou o chão (troca de sinal) = som de pisada
            if (t.lastStepSign != 0f && Mathf.Sign(swing) != t.lastStepSign)
                PlaySfx(t, clipStomp, Mathf.Clamp01(t.scale / 10f), 0.35f + (float)rng.NextDouble() * 0.15f);
            t.lastStepSign = Mathf.Sign(swing);

            float amp = t.kind == TitanKind.Runner ? 42f : 28f;
            if (t.legL != null) t.legL.localRotation = Quaternion.Euler(swing * amp, 0f, 0f);
            if (t.legR != null) t.legR.localRotation = Quaternion.Euler(-swing * amp, 0f, 0f);
            if (t.armL != null && !t.armLGone) t.armL.localRotation = Quaternion.Euler(-swing * amp * 0.7f, 0f, 8f);
            if (t.armR != null && !t.armRGone) t.armR.localRotation = Quaternion.Euler(swing * amp * 0.7f, 0f, -8f);
            if (t.kind != TitanKind.Crawler)
                t.torso.localRotation = Quaternion.Euler(Mathf.Abs(swing) * 3f + (t.kind == TitanKind.Runner ? 18f : 4f), 0f, swing * 2.5f);
        }

        private void UpdateGrab(Titan t, Vector3 localPos, float pSpeed, float cutSpeed, float dt)
        {
            float held = Time.time - t.grabStart;
            if (t.grabHand == null) { SetState(t, TitanState.Chase); return; }

            Transform wrist = t.grabHand == t.handR ? t.wristR : t.wristL;
            if (pSpeed > cutSpeed * 0.55f && Near(localPos, wrist, Mathf.Max(2.5f, cfgCutRadius.Value)))
            {
                SeverArm(t, t.grabHand == t.handR);
                Announce("ESCAPOU cortando o braço! +40 pts");
                totalScore += 40;
                PushPlayer(Vector3.up * 10f);
                StunTitan(t);
                return;
            }

            float f = Mathf.Clamp01(held / 3.2f);
            var arm = t.grabHand == t.handR ? t.armR : t.armL;
            if (arm != null)
                arm.localRotation = Quaternion.Euler(Mathf.Lerp(-30f, -150f, f), Mathf.Lerp(0f, t.grabHand == t.handR ? -35f : 35f, f), 0f);
            HoldPlayerAt(t.grabHand.position);

            if (held > 3.2f)
            {
                Announce("O TITÃ TE COMEU!");
                PlaySfx(t, clipHit, 1f, 0.4f);
                KillPlayer();
                SetState(t, TitanState.Chase);
            }
        }

        private void UpdateAlly(Titan t, float dt)
        {
            Titan target = null; float best = float.MaxValue;
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
                if (dist > t.height * 0.35f) Step(t, t.speed, dt);
            }
            if (dist < t.height * 0.45f && Time.time - t.lastGrabTry > 1.6f)
            {
                t.lastGrabTry = Time.time;
                totalScore += 50; kills++;
                PlaySfx(t, clipHit, 1f, 0.5f);
                SteamBurst(target.tr.position + Vector3.up * target.height * 0.6f, target.height * 0.3f, 8);
                Announce("Soco do titã aliado! +50 pts");
                StartDeath(target);
            }
        }

        // ---------------- dano / morte ----------------

        private void CutNape(Titan t, float playerSpeed, float napeDist, float cutRadius, float cutSpeed)
        {
            kills++;
            float speedFactor = playerSpeed / Mathf.Max(1f, cutSpeed);
            float precision = 1f - Mathf.Clamp01(napeDist / Mathf.Max(0.1f, cutRadius));
            float stunBonus = t.state == TitanState.Stunned ? 0.85f : 1f;
            int points = Mathf.RoundToInt(100f * speedFactor * (0.4f + 1.2f * precision) * stunBonus);
            totalScore += points;
            if (points > bestCut) bestCut = points;

            string grade, praise;
            if (speedFactor >= 2.2f && precision >= 0.7f) { grade = "CORTE LENDÁRIO"; praise = "movimentação ABSURDA!"; }
            else if (speedFactor >= 1.7f && precision >= 0.5f) { grade = "CORTE PERFEITO"; praise = "voo rápido e certeiro, elite!"; }
            else if (speedFactor >= 1.3f || precision >= 0.5f) { grade = "CORTE CRÍTICO"; praise = "bom ataque! Mais velocidade = mais pontos."; }
            else { grade = "corte fraco"; praise = "pegou de raspão... capricha no voo."; }

            Announce(grade + "  +" + points + " pts  (" + praise + ")");
            LoggerInstance.Msg(grade + " +" + points + " [vel " + playerSpeed.ToString("F1") + " | precisão " + (precision * 100f).ToString("F0") + "%]");
            PlaySfx(t, clipHit, 1f, 1.1f);
            SteamBurst(t.napeAnchor.position, t.height * 0.2f, 10);
            StartDeath(t);
        }

        private void StartDeath(Titan t)
        {
            if (t.napeMarker != null) { try { Object.Destroy(t.napeMarker); } catch { } t.napeMarker = null; }
            PlaySfx(t, clipSteam, 0.9f, 0.6f);
            SetState(t, TitanState.Dying);
        }

        private void SeverArm(Titan t, bool right)
        {
            t.lastJointHit = Time.time;
            var arm = right ? t.armR : t.armL;
            if (right) t.armRGone = true; else t.armLGone = true;
            if (arm != null)
            {
                SteamBurst(arm.position, t.height * 0.15f, 6);
                arm.gameObject.SetActive(false);
            }
            totalScore += 25;
            PlaySfx(t, clipHit, 1f, 0.9f);
            Say("Braço decepado! +25 pts");
            if (t.state == TitanState.Grab) SetState(t, TitanState.Chase);
        }

        private void StunTitan(Titan t)
        {
            t.lastJointHit = Time.time;
            totalScore += 15;
            PlaySfx(t, clipStomp, 1f, 0.3f);
            SetState(t, TitanState.Stunned);
            Say("Acertou a junta! Titã caiu — NUCA EXPOSTA! +15 pts");
        }

        private void ClearTitans()
        {
            foreach (var t in titans) if (t.root != null) Object.Destroy(t.root);
            titans.Clear();
            Say("Titãs removidos");
        }

        // ---------------- menu F10 ----------------

        private void ToggleMenu()
        {
            menuOpen = !menuOpen;
            try
            {
                if (menuOpen) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
                else { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
            }
            catch { }
        }

        private void DrawMenu()
        {
            float x = menuRect.x, y = menuRect.y, w = menuRect.width;
            GUI.Box(menuRect, "");
            GUI.Label(new Rect(x + 12f, y + 8f, w - 24f, 22f), "<b>TITANS — Configurações (F10 fecha)</b>");
            float cy = y + 36f;

            GUI.Label(new Rect(x + 12f, cy, w - 24f, 20f), "Máx. titãs: " + cfgMaxTitans.Value); cy += 20f;
            cfgMaxTitans.Value = Mathf.RoundToInt(GUI.HorizontalSlider(new Rect(x + 12f, cy, w - 24f, 16f), cfgMaxTitans.Value, 1f, 30f)); cy += 24f;

            GUI.Label(new Rect(x + 12f, cy, w - 24f, 20f), "Escala: " + cfgScale.Value.ToString("F1") + "x"); cy += 20f;
            cfgScale.Value = Mathf.Round(GUI.HorizontalSlider(new Rect(x + 12f, cy, w - 24f, 16f), cfgScale.Value, 3f, 15f) * 10f) / 10f; cy += 24f;

            GUI.Label(new Rect(x + 12f, cy, w - 24f, 20f), "Velocidade: " + cfgSpeed.Value.ToString("F1") + " m/s"); cy += 20f;
            cfgSpeed.Value = Mathf.Round(GUI.HorizontalSlider(new Rect(x + 12f, cy, w - 24f, 16f), cfgSpeed.Value, 2f, 16f) * 10f) / 10f; cy += 24f;

            GUI.Label(new Rect(x + 12f, cy, w - 24f, 20f), "Visão: " + cfgViewDist.Value.ToString("F0") + " m"); cy += 20f;
            cfgViewDist.Value = Mathf.Round(GUI.HorizontalSlider(new Rect(x + 12f, cy, w - 24f, 16f), cfgViewDist.Value, 30f, 300f)); cy += 28f;

            cfgCanEat.Value = GUI.Toggle(new Rect(x + 12f, cy, w - 24f, 20f), cfgCanEat.Value, " Titãs podem agarrar e comer"); cy += 22f;
            cfgShowNape.Value = GUI.Toggle(new Rect(x + 12f, cy, w - 24f, 20f), cfgShowNape.Value, " Brilho vermelho na nuca"); cy += 22f;
            cfgSounds.Value = GUI.Toggle(new Rect(x + 12f, cy, w - 24f, 20f), cfgSounds.Value, " Sons (passos/vapor)"); cy += 22f;
            cfgGameSkin.Value = GUI.Toggle(new Rect(x + 12f, cy, w - 24f, 20f), cfgGameSkin.Value, " Pele de titã do jogo"); cy += 28f;

            float half = (w - 30f) / 2f;
            if (GUI.Button(new Rect(x + 12f, cy, half, 24f), "Normal")) SpawnTitan(TitanKind.Normal, null);
            if (GUI.Button(new Rect(x + 18f + half, cy, half, 24f), "Corredor")) SpawnTitan(TitanKind.Runner, null);
            cy += 28f;
            if (GUI.Button(new Rect(x + 12f, cy, half, 24f), "Saltador")) SpawnTitan(TitanKind.Jumper, null);
            if (GUI.Button(new Rect(x + 18f + half, cy, half, 24f), "Rastejante")) SpawnTitan(TitanKind.Crawler, null);
            cy += 32f;

            if (GUI.Button(new Rect(x + 12f, cy, w - 24f, 26f), "Salvar configurações")) { MelonPreferences.Save(); Say("Configurações salvas"); }
            cy += 30f;
            if (GUI.Button(new Rect(x + 12f, cy, w - 24f, 26f), "Fechar (F10)")) ToggleMenu();
        }

        // ---------------- HUD ----------------

        private void Say(string msg) { lastMsg = msg; lastMsgTime = Time.time; }
        private void Announce(string msg) { cutMsg = msg; cutMsgTime = Time.time; LoggerInstance.Msg(msg); }

        private string SkillLabel(string name, string key, float readyAt, float activeUntil)
        {
            if (Time.time < activeUntil) return name + " ATIVO";
            if (Time.time < readyAt) return name + " " + Mathf.CeilToInt(readyAt - Time.time) + "s";
            return name + " (" + key + ")";
        }

        public override void OnGUI()
        {
            int alive = AliveCount();
            string wave = waveMode ? ("  |  WAVE " + waveNum) : "";
            string line = "TITANS  |  vivos: " + alive + wave + "  |  abates: " + kills + "  |  pontos: " + totalScore + "  |  melhor: " + bestCut
                + "  |  " + SkillLabel("Eren", "T", erenReadyAt, erenEndsAt)
                + "  |  " + SkillLabel("Levi", "G", leviReadyAt, leviEndsAt)
                + "  |  " + SkillLabel("Mikasa", "Y", mikasaReadyAt, 0f)
                + "  |  " + SkillLabel("Armin", "H", arminReadyAt, arminEndsAt);
            GUI.Label(new Rect(14f, 12f, 1600f, 24f), "<b><color=#ff5544>" + line + "</color></b>");
            GUI.Label(new Rect(14f, 34f, 1600f, 22f), "<color=#999999>F6 titã  F9 waves  F10 menu  F7 limpar  F8 congelar  —  nuca = morte; pulsos/joelhos = dano</color>");
            if (Time.time - lastMsgTime < 3.5f)
                GUI.Label(new Rect(14f, 56f, 1600f, 24f), "<color=#ffcc44>" + lastMsg + "</color>");
            if (Time.time - cutMsgTime < 5f)
                GUI.Label(new Rect(14f, 80f, 1600f, 28f), "<b><size=18><color=#44ff66>" + cutMsg + "</color></size></b>");
            if (menuOpen) DrawMenu();
        }
    }
}
