using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
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
                Loaded = p;
                log("TitanPack: " + p.bones.Count + " bones, " + p.meshes.Count + " meshes, " + p.clips.Count + " clips, " + p.matTex.Count + " materiais");
                return true;
            }
            catch (Exception e) { log("TitanPack falhou: " + e); return false; }
        }

        private static string RStr(BinaryReader r) { int n = r.ReadUInt16(); return System.Text.Encoding.UTF8.GetString(r.ReadBytes(n)); }
        private static Vector3 RV3(BinaryReader r) { return new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle()); }
        private static Quaternion RQ(BinaryReader r) { return new Quaternion(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle()); }

        public Texture2D GetTexture(string file)
        {
            if (string.IsNullOrEmpty(file)) return null;
            Texture2D t;
            if (texCache.TryGetValue(file, out t)) return t;
            try
            {
                byte[] data = File.ReadAllBytes(Path.Combine(dir, file));
                t = new Texture2D(2, 2, TextureFormat.RGBA32, true);
                ImageConversion.LoadImage(t, data, false);
                texCache[file] = t;
                return t;
            }
            catch { return null; }
        }

        // Monta uma instância: hierarquia de bones + SMRs skinnados. Retorna root; bones out por índice.
        public GameObject BuildInstance(string bodyTexFile, Color tint, Material shaderDonor, out Transform[] boneTr)
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

            foreach (var m in meshes)
            {
                var mesh = new Mesh();
                mesh.vertices = m.verts;
                mesh.normals = m.normals;
                mesh.uv = m.uv;
                var bw = new BoneWeight[m.verts.Length];
                for (int v = 0; v < bw.Length; v++)
                {
                    var w = new BoneWeight();
                    w.boneIndex0 = m.w0[v]; w.weight0 = m.f0[v];
                    w.boneIndex1 = m.w1[v]; w.weight1 = m.f1[v];
                    w.boneIndex2 = m.w2[v]; w.weight2 = m.f2[v];
                    w.boneIndex3 = m.w3[v]; w.weight3 = m.f3[v];
                    bw[v] = w;
                }
                mesh.boneWeights = bw;
                mesh.bindposes = m.bindposes;
                mesh.subMeshCount = m.subs.Length;
                for (int s = 0; s < m.subs.Length; s++)
                    mesh.SetTriangles(m.subs[s].tris, s);
                mesh.RecalculateBounds();

                var go = new GameObject(m.name);
                go.transform.SetParent(root.transform, false);
                var smr = go.AddComponent<SkinnedMeshRenderer>();
                var btr = new Transform[m.boneIdx.Length];
                for (int bi = 0; bi < btr.Length; bi++) btr[bi] = boneTr[m.boneIdx[bi]];
                smr.bones = btr;
                smr.sharedMesh = mesh;
                smr.rootBone = boneTr[FindBone("hip")] ?? root.transform;
                smr.updateWhenOffscreen = true;

                var mats = new Material[m.subs.Length];
                for (int s = 0; s < m.subs.Length; s++)
                {
                    string texFile;
                    matTex.TryGetValue(m.subs[s].mat, out texFile);
                    // corpo usa a textura escolhida da variante
                    if (m.name == "Body" && !string.IsNullOrEmpty(bodyTexFile)) texFile = bodyTexFile;
                    mats[s] = MakeMaterial(GetTexture(texFile), tint, shaderDonor);
                }
                smr.materials = mats;
            }
            return root;
        }

        public int FindBone(string name)
        {
            for (int i = 0; i < bones.Count; i++) if (bones[i].name == name) return i;
            return 0;
        }

        public static Material MakeMaterial(Texture2D tex, Color tint, Material donor)
        {
            Shader sh = null;
            try { sh = Shader.Find("Standard"); } catch { }
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
            try { if (tex != null) m.mainTexture = tex; } catch { }
            try { m.color = tint; } catch { }
            return m;
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
