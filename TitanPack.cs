using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnhollowerRuntimeLib;
using Object = UnityEngine.Object;

namespace TitansMod
{
    // Carrega o pacote TPK1 (modelo+esqueleto+animações do titã extraídos do AoTTG2)
    // e monta instâncias skinned de verdade em runtime no RAOT.
    public class TitanPack
    {
        public class BoneDef { public string name; public int parent; public Vector3 pos; public Quaternion rot; public Vector3 scl; }
        public class SubDef { public string mat; public int[] tris; }
        public class MeshDef
        {
            public string name;
            public Vector3[] verts, normals;
            public Vector2[] uv;
            public int[] w0, w1, w2, w3;      // índices locais (na lista de bones do SMR)
            public float[] f0, f1, f2, f3;    // pesos
            public int[] boneIdx;             // índice local -> índice no esqueleto
            public Matrix4x4[] bindposes;
            public SubDef[] subs;
        }
        public class ClipTrack { public int bone; public bool hasPos, hasRot; public Vector3[] pos; public Quaternion[] rot; }
        public class ClipDef { public string name; public float dur; public int frames; public ClipTrack[] tracks; }

        public float rawHeight = 2.45f; // altura do modelo em unidades nativas (medida dos vértices)
        public List<BoneDef> bones = new List<BoneDef>();
        public List<MeshDef> meshes = new List<MeshDef>();
        public Dictionary<string, string> matTex = new Dictionary<string, string>();
        public Dictionary<string, ClipDef> clips = new Dictionary<string, ClipDef>();

        private readonly Dictionary<string, Texture2D> texCache = new Dictionary<string, Texture2D>();
        private string dir;

        public static TitanPack Loaded;

        public static bool TryLoad(string assetDir, Action<string> log)
        {
            try
            {
                string f = Path.Combine(assetDir, "titan.tpk");
                if (!File.Exists(f)) { log("titan.tpk não achado em " + assetDir); return false; }
                var p = new TitanPack { dir = assetDir };
                using (var r = new BinaryReader(File.OpenRead(f)))
                {
                    if (new string(r.ReadChars(4)) != "TPK1") { log("magic inválido"); return false; }
                    int nb = r.ReadInt32();
                    for (int i = 0; i < nb; i++)
                    {
                        var b = new BoneDef();
                        b.name = RStr(r);
                        b.parent = r.ReadInt32();
                        b.pos = RV3(r); b.rot = RQ(r); b.scl = RV3(r);
                        p.bones.Add(b);
                    }
                    int nm = r.ReadInt32();
                    for (int i = 0; i < nm; i++)
                    {
                        var m = new MeshDef();
                        m.name = RStr(r);
                        int nv = r.ReadInt32();
                        m.verts = new Vector3[nv]; m.normals = new Vector3[nv]; m.uv = new Vector2[nv];
                        m.w0 = new int[nv]; m.w1 = new int[nv]; m.w2 = new int[nv]; m.w3 = new int[nv];
                        m.f0 = new float[nv]; m.f1 = new float[nv]; m.f2 = new float[nv]; m.f3 = new float[nv];
                        for (int v = 0; v < nv; v++) m.verts[v] = RV3(r);
                        for (int v = 0; v < nv; v++) m.normals[v] = RV3(r);
                        for (int v = 0; v < nv; v++) m.uv[v] = new Vector2(r.ReadSingle(), r.ReadSingle());
                        for (int v = 0; v < nv; v++)
                        {
                            m.w0[v] = r.ReadUInt16(); m.f0[v] = r.ReadSingle();
                            m.w1[v] = r.ReadUInt16(); m.f1[v] = r.ReadSingle();
                            m.w2[v] = r.ReadUInt16(); m.f2[v] = r.ReadSingle();
                            m.w3[v] = r.ReadUInt16(); m.f3[v] = r.ReadSingle();
                        }
                        int nbi = r.ReadInt32();
                        m.boneIdx = new int[nbi];
                        for (int v = 0; v < nbi; v++) m.boneIdx[v] = r.ReadUInt16();
                        int nbp = r.ReadInt32();
                        m.bindposes = new Matrix4x4[nbp];
                        for (int v = 0; v < nbp; v++)
                        {
                            var mat = new Matrix4x4();
                            for (int rr = 0; rr < 4; rr++)
                                for (int cc = 0; cc < 4; cc++)
                                    mat[rr, cc] = r.ReadSingle();
                            m.bindposes[v] = mat;
                        }
                        int ns = r.ReadInt32();
                        m.subs = new SubDef[ns];
                        for (int s = 0; s < ns; s++)
                        {
                            var sd = new SubDef();
                            sd.mat = RStr(r);
                            int ni = r.ReadInt32();
                            sd.tris = new int[ni];
                            for (int ti = 0; ti < ni; ti++) sd.tris[ti] = r.ReadInt32();
                            m.subs[s] = sd;
                        }
                        p.meshes.Add(m);
                    }
                    int nmat = r.ReadInt32();
                    for (int i = 0; i < nmat; i++) { string k = RStr(r); p.matTex[k] = RStr(r); }
                    int nc = r.ReadInt32();
                    for (int i = 0; i < nc; i++)
                    {
                        var c = new ClipDef();
                        c.name = RStr(r);
                        c.dur = r.ReadSingle();
                        c.frames = r.ReadInt32();
                        int nt = r.ReadInt32();
                        c.tracks = new ClipTrack[nt];
                        for (int ti = 0; ti < nt; ti++)
                        {
                            var tr = new ClipTrack();
                            tr.bone = r.ReadUInt16();
                            tr.hasPos = r.ReadByte() != 0;
                            tr.hasRot = r.ReadByte() != 0;
                            if (tr.hasPos) tr.pos = new Vector3[c.frames];
                            if (tr.hasRot) tr.rot = new Quaternion[c.frames];
                            for (int fr2 = 0; fr2 < c.frames; fr2++)
                            {
                                if (tr.hasPos) tr.pos[fr2] = RV3(r);
                                if (tr.hasRot) tr.rot[fr2] = RQ(r);
                            }
                            c.tracks[ti] = tr;
                        }
                        p.clips[c.name] = c;
                    }
                }
                float minY = float.MaxValue, maxY = float.MinValue;
                foreach (var m in p.meshes)
                    foreach (var v in m.verts)
                    {
                        if (v.y < minY) minY = v.y;
                        if (v.y > maxY) maxY = v.y;
                    }
                if (maxY > minY + 0.2f) p.rawHeight = maxY - minY;
                Loaded = p;
                log("TitanPack: " + p.bones.Count + " bones, " + p.meshes.Count + " meshes, " + p.clips.Count + " clips, altura crua " + p.rawHeight.ToString("F2"));
                return true;
            }
            catch (Exception e) { log("TitanPack falhou: " + e); return false; }
        }

        private static string RStr(BinaryReader r) { int n = r.ReadUInt16(); return System.Text.Encoding.UTF8.GetString(r.ReadBytes(n)); }
        private static Vector3 RV3(BinaryReader r) { return new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle()); }
        private static Quaternion RQ(BinaryReader r) { return new Quaternion(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle()); }

        public static Action<string> Log = s => { };

        public void ClearTextureCache() { texCache.Clear(); }

        public Texture2D GetTexture(string file)
        {
            if (string.IsNullOrEmpty(file)) return null;
            Texture2D t;
            if (texCache.TryGetValue(file, out t)) return t;
            string path = Path.Combine(dir, file);
            try
            {
                byte[] data = File.ReadAllBytes(path);
                t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                bool ok = false;
                try { ok = ImageConversion.LoadImage(t, data, false); }
                catch (Exception e1)
                {
                    Log("LoadImage(3 args) falhou: " + e1.Message);
                    try { ok = ImageConversion.LoadImage(t, data); } catch (Exception e2) { Log("LoadImage(2 args) falhou: " + e2.Message); }
                }
                if (!ok || t.width <= 2)
                {
                    Log("textura NÃO decodificada: " + file + " (ok=" + ok + ", " + t.width + "x" + t.height + ")");
                    texCache[file] = null;
                    return null;
                }
                try { t.name = file; } catch { }
                try { t.hideFlags = HideFlags.DontUnloadUnusedAsset; } catch { } // sobrevive à troca de cena
                Log("textura ok: " + file + " " + t.width + "x" + t.height);
                texCache[file] = t;
                return t;
            }
            catch (Exception e) { Log("textura falhou " + file + ": " + e.Message); return null; }
        }

        // Monta uma instância: hierarquia de bones + malhas skinnadas por CPU.
        // (BoneWeight[] não passa pelo interop do Unhollower, então o skinning é feito na mão a cada frame.)
        public GameObject BuildInstance(string bodyTexFile, Color tint, Material shaderDonor, out Transform[] boneTr, out List<CpuSkin> skins)
        {
            boneTr = new Transform[bones.Count];
            for (int i = 0; i < bones.Count; i++)
            {
                var b = bones[i];
                var go = new GameObject(b.name);
                var tr = go.transform;
                if (b.parent >= 0) tr.SetParent(boneTr[b.parent], false);
                tr.localPosition = b.pos;
                tr.localRotation = b.rot;
                tr.localScale = b.scl;
                boneTr[i] = tr;
            }
            var root = boneTr[0].gameObject;
            skins = new List<CpuSkin>();

            foreach (var m in meshes)
            {
                var mesh = new Mesh();
                mesh.vertices = m.verts;
                mesh.normals = m.normals;
                mesh.uv = m.uv;
                mesh.subMeshCount = m.subs.Length;
                for (int s = 0; s < m.subs.Length; s++)
                    mesh.SetTriangles(m.subs[s].tris, s);
                // bounds generoso fixo: skinning por CPU não atualiza bounds por frame
                mesh.RecalculateBounds();
                var mb = mesh.bounds;
                mesh.bounds = new Bounds(mb.center, mb.size * 2.5f);

                var go = new GameObject(m.name);
                go.transform.SetParent(root.transform, false);
                var mf = go.AddComponent<MeshFilter>();
                mf.sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();

                var mats = new Material[m.subs.Length];
                for (int s = 0; s < m.subs.Length; s++)
                {
                    string texFile;
                    matTex.TryGetValue(m.subs[s].mat, out texFile);
                    if (m.name == "Body" && !string.IsNullOrEmpty(bodyTexFile)) texFile = bodyTexFile;
                    mats[s] = MakeMaterial(GetTexture(texFile), tint, shaderDonor);
                }
                mr.materials = mats;

                skins.Add(new CpuSkin(m, mesh, go.transform, boneTr));
            }
            return root;
        }

        public int FindBone(string name)
        {
            for (int i = 0; i < bones.Count; i++) if (bones[i].name == name) return i;
            return 0;
        }

        private static bool loggedShader;
        private static Shader texturedShader;
        private static bool texturedShaderSearched;

        // O RAOT é low-poly chapado: o build só traz as variantes SEM textura do Standard.
        // Então pegamos o shader mais usado pelos materiais que TÊM textura no jogo (personagens).
        private static Shader FindTexturedShader()
        {
            if (texturedShaderSearched) return texturedShader;
            texturedShaderSearched = true;
            // Pontua cada shader de material texturizado: queremos o dos PERSONAGENS (usa UV da malha),
            // nunca o dos prédios ("WorldUV"/triplanar projeta no espaço do mundo e vira mancha).
            try
            {
                var counts = new Dictionary<string, int>();
                var byName = new Dictionary<string, Shader>();
                var mats = Resources.FindObjectsOfTypeAll(Il2CppType.Of<Material>());
                foreach (var o in mats)
                {
                    var m = o.TryCast<Material>();
                    if (m == null || m.shader == null) continue;
                    Texture mt = null;
                    try { mt = m.mainTexture; } catch { }
                    if (mt == null) continue;
                    string n = m.shader.name ?? "";
                    int c; counts.TryGetValue(n, out c); counts[n] = c + 1;
                    byName[n] = m.shader;
                }
                var listing = new System.Text.StringBuilder();
                string best = null; int bestScore = int.MinValue;
                foreach (var kv in counts)
                {
                    string n = kv.Key; string l = n.ToLowerInvariant();
                    listing.Append(n).Append("=").Append(kv.Value).Append("; ");
                    int score = 0;
                    // vetos absolutos: nunca usar
                    if (l.Contains("worlduv") || l.Contains("triplanar") || l.Contains("world")) continue;
                    if (l.Contains("ui") || l.Contains("sprite") || l.Contains("particle") || l.Contains("skybox") || l.Contains("hidden") || l.Contains("textmesh") || l.Contains("gui") || l.Contains("unlit") || l.Contains("terrain") || l.Contains("water") || l.Contains("fx") || l.Contains("line") || l.Contains("blit")) continue;
                    if (l.Contains("toon") || l.Contains("cel") || l.Contains("character") || l.Contains("skin") || l.Contains("cloth") || l.Contains("player")) score += 300;
                    if (l.Contains("diffuse") || l.Contains("lit") || l.Contains("standard")) score += 100;
                    score += Math.Min(kv.Value, 50);
                    if (score > bestScore) { bestScore = score; best = n; }
                }
                Log("shaders texturizados carregados: " + listing);
                if (best != null)
                {
                    texturedShader = byName[best];
                    Log("shader escolhido pro titã: '" + best + "' (score " + bestScore + ")");
                }
                else Log("nenhum shader texturizado adequado; usando Standard");
            }
            catch (Exception e) { Log("FindTexturedShader: " + e.Message); }
            return texturedShader;
        }

        // No boot (menu) os materiais dos personagens podem não estar carregados: deixa procurar de novo na partida.
        public static void ResetShaderCache() { texturedShaderSearched = false; texturedShader = null; loggedShader = false; }

        public static Material MakeMaterial(Texture2D tex, Color tint, Material donor)
        {
            Shader sh = FindTexturedShader();
            if (sh == null) { try { sh = Shader.Find("Standard"); } catch { } }
            if (sh == null) { try { sh = Shader.Find("Legacy Shaders/Diffuse"); } catch { } }
            if (sh == null) { try { sh = Shader.Find("Diffuse"); } catch { } }
            Material m = null;
            if (sh != null) { try { m = new Material(sh); } catch { } }
            if (m == null && donor != null) { try { m = new Material(donor); } catch { } }
            if (m == null)
            {
                // último recurso: rouba o material de qualquer renderer vivo na cena
                try
                {
                    var any = Object.FindObjectOfType<Renderer>();
                    if (any != null) m = new Material(any.sharedMaterial);
                }
                catch { }
            }
            if (m == null) throw new Exception("nenhum shader disponível");
            try { if (tex != null) m.mainTexture = tex; } catch (Exception e) { Log("mainTexture falhou: " + e.Message); }
            // shaders custom podem usar outro nome de slot
            string[] slots = { "_MainTex", "_BaseMap", "_BaseColorMap", "_Albedo", "_Diffuse", "_Texture", "_Tex", "_BaseTex" };
            foreach (var s in slots) { try { if (tex != null && m.HasProperty(s)) m.SetTexture(s, tex); } catch { } }
            try { m.color = tint; } catch { }
            try { if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", tint); } catch { }
            try { m.mainTextureScale = Vector2.one; m.mainTextureOffset = Vector2.zero; } catch { }
            if (!loggedShader)
            {
                loggedShader = true;
                string got = "?";
                try { got = m.mainTexture != null ? m.mainTexture.name : "NULA"; } catch { }
                Log("shader do titã: " + (m.shader != null ? m.shader.name : "?") + ", mainTexture=" + got);
            }
            return m;
        }
    }

    // Skinning por CPU: v' = meshLocal^-1 * Σ w_i * boneL2W_i * bind_i * v
    // Toda a matemática em floats puros (sem operadores interop por elemento).
    public class CpuSkin
    {
        private readonly TitanPack.MeshDef def;
        private readonly Mesh mesh;
        private readonly Transform meshTr;
        private readonly Transform[] boneRefs;   // bones do SMR (já resolvidos pro esqueleto)
        private readonly float[][] binds;        // 3x4 por bone (linhas 0..2 da bindpose)
        private readonly Vector3[] outVerts;
        private readonly float[][] final;        // matrizes finais 3x4 por bone

        public CpuSkin(TitanPack.MeshDef m, Mesh mesh_, Transform meshTr_, Transform[] skeleton)
        {
            def = m; mesh = mesh_; meshTr = meshTr_;
            boneRefs = new Transform[m.boneIdx.Length];
            for (int i = 0; i < boneRefs.Length; i++) boneRefs[i] = skeleton[m.boneIdx[i]];
            binds = new float[m.bindposes.Length][];
            for (int i = 0; i < binds.Length; i++)
            {
                var b = m.bindposes[i];
                binds[i] = new float[12] { b.m00, b.m01, b.m02, b.m03, b.m10, b.m11, b.m12, b.m13, b.m20, b.m21, b.m22, b.m23 };
            }
            outVerts = new Vector3[m.verts.Length];
            final = new float[boneRefs.Length][];
            for (int i = 0; i < final.Length; i++) final[i] = new float[12];
        }

        private static void Mul34(float[] a, float[] b, float[] o)
        {
            // o = a * b (ambas 3x4, linha de baixo implícita 0,0,0,1)
            o[0] = a[0] * b[0] + a[1] * b[4] + a[2] * b[8];
            o[1] = a[0] * b[1] + a[1] * b[5] + a[2] * b[9];
            o[2] = a[0] * b[2] + a[1] * b[6] + a[2] * b[10];
            o[3] = a[0] * b[3] + a[1] * b[7] + a[2] * b[11] + a[3];
            o[4] = a[4] * b[0] + a[5] * b[4] + a[6] * b[8];
            o[5] = a[4] * b[1] + a[5] * b[5] + a[6] * b[9];
            o[6] = a[4] * b[2] + a[5] * b[6] + a[6] * b[10];
            o[7] = a[4] * b[3] + a[5] * b[7] + a[6] * b[11] + a[7];
            o[8] = a[8] * b[0] + a[9] * b[4] + a[10] * b[8];
            o[9] = a[8] * b[1] + a[9] * b[5] + a[10] * b[9];
            o[10] = a[8] * b[2] + a[9] * b[6] + a[10] * b[10];
            o[11] = a[8] * b[3] + a[9] * b[7] + a[10] * b[11] + a[11];
        }

        private static readonly float[] tmpA = new float[12];
        private static readonly float[] tmpB = new float[12];
        private static readonly float[] tmpC = new float[12];

        private static void From(Matrix4x4 m, float[] o)
        {
            o[0] = m.m00; o[1] = m.m01; o[2] = m.m02; o[3] = m.m03;
            o[4] = m.m10; o[5] = m.m11; o[6] = m.m12; o[7] = m.m13;
            o[8] = m.m20; o[9] = m.m21; o[10] = m.m22; o[11] = m.m23;
        }

        public void Apply()
        {
            From(meshTr.worldToLocalMatrix, tmpA);
            for (int i = 0; i < boneRefs.Length; i++)
            {
                var bt = boneRefs[i];
                if (bt == null) continue;
                From(bt.localToWorldMatrix, tmpB);
                // final = meshW2L * (boneL2W * bind)
                Mul34(tmpB, binds[i], tmpC);
                Mul34(tmpA, tmpC, final[i]);
            }
            var verts = def.verts;
            for (int v = 0; v < verts.Length; v++)
            {
                float px = verts[v].x, py = verts[v].y, pz = verts[v].z;
                float x = 0f, y = 0f, z = 0f;
                float w = def.f0[v];
                if (w > 0f) { var f = final[def.w0[v]]; x += w * (f[0] * px + f[1] * py + f[2] * pz + f[3]); y += w * (f[4] * px + f[5] * py + f[6] * pz + f[7]); z += w * (f[8] * px + f[9] * py + f[10] * pz + f[11]); }
                w = def.f1[v];
                if (w > 0f) { var f = final[def.w1[v]]; x += w * (f[0] * px + f[1] * py + f[2] * pz + f[3]); y += w * (f[4] * px + f[5] * py + f[6] * pz + f[7]); z += w * (f[8] * px + f[9] * py + f[10] * pz + f[11]); }
                w = def.f2[v];
                if (w > 0f) { var f = final[def.w2[v]]; x += w * (f[0] * px + f[1] * py + f[2] * pz + f[3]); y += w * (f[4] * px + f[5] * py + f[6] * pz + f[7]); z += w * (f[8] * px + f[9] * py + f[10] * pz + f[11]); }
                w = def.f3[v];
                if (w > 0f) { var f = final[def.w3[v]]; x += w * (f[0] * px + f[1] * py + f[2] * pz + f[3]); y += w * (f[4] * px + f[5] * py + f[6] * pz + f[7]); z += w * (f[8] * px + f[9] * py + f[10] * pz + f[11]); }
                outVerts[v] = new Vector3(x, y, z);
            }
            mesh.vertices = outVerts;
        }

        // Mede o tamanho REAL pós-skinning (em espaço local do root). Chamar depois de um Apply().
        public void Measure(ref float minY, ref float maxY)
        {
            for (int v = 0; v < outVerts.Length; v++)
            {
                float y = outVerts[v].y;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }
        }

        public void RefreshBounds()
        {
            try
            {
                mesh.RecalculateBounds();
                var b = mesh.bounds;
                mesh.bounds = new Bounds(b.center, b.size * 2f + Vector3.one * 0.5f);
            }
            catch { }
        }
    }

    // Player de animação: toca os clips baked com crossfade.
    public class TitanAnim
    {
        private readonly TitanPack pack;
        private readonly Transform[] bones;
        private TitanPack.ClipDef cur, prev;
        private float time, prevTime, fadeLeft, fadeDur;
        private bool loop, prevLoop;
        public string Current { get { return cur != null ? cur.name : ""; } }
        public bool Finished { get { return cur != null && !loop && time >= cur.dur; } }

        public TitanAnim(TitanPack p, Transform[] b) { pack = p; bones = b; }

        public void Play(string name, bool looping, float fade = 0.18f)
        {
            TitanPack.ClipDef c;
            if (!pack.clips.TryGetValue(name, out c)) return;
            if (cur == c && loop == looping) return;
            prev = cur; prevTime = time; prevLoop = loop;
            cur = c; time = 0f; loop = looping;
            fadeDur = fadeLeft = Mathf.Max(0.0001f, fade);
        }

        public void Update(float dt)
        {
            if (cur == null) return;
            time += dt;
            if (loop && cur.dur > 0.05f) { if (time > cur.dur) time %= cur.dur; }
            else time = Mathf.Min(time, cur.dur);
            float blend = 1f;
            if (fadeLeft > 0f)
            {
                fadeLeft -= dt;
                blend = 1f - Mathf.Clamp01(fadeLeft / fadeDur);
                if (prev != null)
                {
                    prevTime += dt;
                    if (prevLoop && prev.dur > 0.05f && prevTime > prev.dur) prevTime %= prev.dur;
                }
            }

            foreach (var tr in cur.tracks)
            {
                var bone = bones[tr.bone];
                if (bone == null) continue;
                Vector3 p; Quaternion q;
                Sample(cur, tr, time, out p, out q);
                if (blend < 1f && prev != null)
                {
                    var ptr = FindTrack(prev, tr.bone);
                    if (ptr != null)
                    {
                        Vector3 pp; Quaternion pq;
                        Sample(prev, ptr, Mathf.Min(prevTime, prev.dur), out pp, out pq);
                        if (tr.hasPos && ptr.hasPos) p = Vector3.Lerp(pp, p, blend);
                        if (tr.hasRot && ptr.hasRot) q = Quaternion.Slerp(pq, q, blend);
                    }
                }
                if (tr.hasPos) bone.localPosition = p;
                if (tr.hasRot) bone.localRotation = q;
            }
        }

        private static TitanPack.ClipTrack FindTrack(TitanPack.ClipDef c, int bone)
        {
            foreach (var t in c.tracks) if (t.bone == bone) return t;
            return null;
        }

        private static void Sample(TitanPack.ClipDef c, TitanPack.ClipTrack tr, float t, out Vector3 p, out Quaternion q)
        {
            float ft = Mathf.Clamp(t, 0f, c.dur) * 30f;
            int f0 = Mathf.Clamp(Mathf.FloorToInt(ft), 0, c.frames - 1);
            int f1 = Mathf.Min(f0 + 1, c.frames - 1);
            float a = Mathf.Clamp01(ft - f0);
            p = Vector3.zero; q = Quaternion.identity;
            if (tr.hasPos) p = Vector3.Lerp(tr.pos[f0], tr.pos[f1], a);
            if (tr.hasRot) q = Quaternion.Slerp(tr.rot[f0], tr.rot[f1], a);
        }
    }
}
