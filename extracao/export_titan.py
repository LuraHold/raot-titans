"""Exporta o BasicTitan do AoTTG2 (Unity 2023) pro formato TPK1 que o mod Titans lê no RAOT (Unity 2019).
Pipeline: prefab -> esqueleto; SMRs -> malha+pesos+bindposes; materiais -> png; AnimationClips -> frames baked 30fps.
"""
import os, sys, struct, zlib, math, json
import UnityPy

sys.stdout.reconfigure(encoding="utf-8")
SRC = r"X:\Downloads\rea-tools\aottg2\Aottg2Launcher\Release\MainApp\Aottg2_Data\resources.assets"
OUT = r"X:\Downloads\rea-tools\TitansMod\assets"
os.makedirs(OUT, exist_ok=True)

PREFAB_PID = 13444  # BasicTitan0
CLIPS_WANTED = [
    "idle", "run.walk", "run.abnormal", "Titanjumping", "TitanFalling",
    "attack.swing.l", "attack.swing.r", "attack.stomp", "attack.scream",
    "grab.ground.front.r", "eat.r", "die.front", "die.back",
    "sit_down", "sit_idle", "sit_getup",
    "crawler.idle", "crawler.run", "crawler.die",
]
BAKE_FPS = 30.0

env = UnityPy.load(SRC)
byid = {o.path_id: o for o in env.objects}

def deref(pptr):
    try:
        if pptr and pptr.path_id:
            return byid.get(pptr.path_id)
    except Exception:
        pass
    return None

# ---------- 1. esqueleto ----------
prefab = byid[PREFAB_PID]
bones = []          # dicts: name, parent, pos, rot, scl, path
bone_index_by_tid = {}  # transform path_id -> index
smr_objs = []       # (goName, smr_reader)

def get_transform(go_obj):
    go = go_obj.read()
    for c in go.m_Component:
        co = deref(c.component)
        if co is not None and co.type.name == "Transform":
            return co
    return None

def v3(v): return (v.x, v.y, v.z)
def q4(q): return (q.x, q.y, q.z, q.w)

def walk(go_obj, parent_idx, path):
    go = go_obj.read()
    t_obj = get_transform(go_obj)
    t = t_obj.read()
    name = go.m_Name
    mypath = name if parent_idx < 0 else (path + "/" + name if path else name)
    idx = len(bones)
    bones.append({
        "name": name, "parent": parent_idx,
        "pos": v3(t.m_LocalPosition), "rot": q4(t.m_LocalRotation), "scl": v3(t.m_LocalScale),
        "path": "" if parent_idx < 0 else mypath,
    })
    bone_index_by_tid[t_obj.path_id] = idx
    for c in go.m_Component:
        co = deref(c.component)
        if co is not None and co.type.name == "SkinnedMeshRenderer":
            smr_objs.append((name, co))
    for ch in t.m_Children:
        cho = deref(ch)
        if cho is None: continue
        chgo = deref(cho.read().m_GameObject)
        if chgo is not None:
            walk(chgo, idx, "" if parent_idx < 0 else mypath)

walk(prefab, -1, "")
print(f"esqueleto: {len(bones)} transforms, SMRs: {[n for n,_ in smr_objs]}")

# caminho relativo ao root (root fora do path, padrão Unity)
crc_to_bone = {}
for i, b in enumerate(bones):
    if b["path"]:
        h = zlib.crc32(b["path"].encode("utf-8")) & 0xFFFFFFFF
        crc_to_bone[h] = i

# ---------- 2. malhas ----------
from UnityPy.helpers.MeshHelper import MeshHandler

def flat_to_tuples(arr, n):
    if arr is None: return None
    if len(arr) and isinstance(arr[0], (int, float)):
        return [tuple(arr[i*n:(i+1)*n]) for i in range(len(arr)//n)]
    out = []
    for e in arr:
        if n == 3: out.append((e.x, e.y, e.z) if hasattr(e, "x") else tuple(e[:3]))
        else: out.append((e.x, e.y) if hasattr(e, "x") else tuple(e[:2]))
    return out

meshes = []   # dict: name, verts, normals, uv, weights(list of 4x(boneIdxLocal,w)), bindposes, boneIndices(skel), submeshes [(matName, [indices])]
materials_used = {}  # matName -> texture png filename

def read_material(mat_obj):
    m = mat_obj.read()
    name = m.m_Name.replace("/", "_")
    if name in materials_used:
        return name
    texfile = ""
    try:
        props = m.m_SavedProperties
        for entry in props.m_TexEnvs:
            key = entry[0] if isinstance(entry, (list, tuple)) else getattr(entry, "first", None) or entry
            val = entry[1] if isinstance(entry, (list, tuple)) else getattr(entry, "second", None)
            kname = key if isinstance(key, str) else getattr(key, "name", str(key))
            if kname != "_MainTex": continue
            texp = deref(val.m_Texture)
            if texp is not None:
                tex = texp.read()
                texfile = f"tex_{tex.m_Name}.png"
                fp = os.path.join(OUT, texfile)
                if not os.path.exists(fp):
                    tex.image.save(fp)
            break
    except Exception as e:
        print("  material tex fail", name, e)
    materials_used[name] = texfile
    return name

for go_name, smr_obj in smr_objs:
    smr = smr_obj.read()
    mesh_obj = deref(smr.m_Mesh)
    if mesh_obj is None:
        print("SMR sem mesh:", go_name); continue
    mesh = mesh_obj.read()
    mh = MeshHandler(mesh)
    mh.process()
    verts = flat_to_tuples(mh.m_Vertices, 3)
    norms = flat_to_tuples(mh.m_Normals, 3) or [(0,1,0)]*len(verts)
    uv = flat_to_tuples(mh.m_UV0, 2) or [(0,0)]*len(verts)

    # pesos (Unity 2023: streams de vértice via MeshHandler)
    mw = getattr(mh, "m_BoneWeights", None)
    mi = getattr(mh, "m_BoneIndices", None)
    weights = []
    if mw and mi and len(mw) == len(verts):
        for wts, idc in zip(mw, mi):
            weights.append(tuple(zip(list(idc)[:4], list(wts)[:4])))
    else:
        print("  SEM SKIN em", go_name, "-> rigidez no root")
        weights = [((0, 1.0), (0, 0.0), (0, 0.0), (0, 0.0))] * len(verts)

    bind = [tuple(getattr(m, f"e{r}{c}", 0.0) for r in range(4) for c in range(4)) if hasattr(m, "e00")
            else tuple(m) for m in (mesh.m_BindPose or [])]

    bone_skel_idx = []
    for bp in smr.m_Bones:
        to = deref(bp)
        bone_skel_idx.append(bone_index_by_tid.get(to.path_id, 0) if to is not None else 0)

    # submeshes + materiais
    idx_buf = list(mh.m_IndexBuffer)
    submeshes = []
    mats = list(smr.m_Materials)
    for si, sm in enumerate(mesh.m_SubMeshes):
        first = sm.firstByte // 2 if getattr(sm, "indexFormat", 0) == 0 else sm.firstByte // 4
        count = sm.indexCount
        tri = idx_buf[first:first + count]
        mat_name = ""
        if si < len(mats):
            mo = deref(mats[si])
            if mo is not None:
                mat_name = read_material(mo)
        submeshes.append((mat_name, tri))

    meshes.append({
        "name": go_name, "verts": verts, "normals": norms, "uv": uv,
        "weights": weights, "bindposes": bind, "boneIdx": bone_skel_idx,
        "submeshes": submeshes,
    })
    print(f"mesh {go_name}: {len(verts)} verts, {len(bind)} bindposes, {len(bone_skel_idx)} bones, submeshes {[(m, len(t)) for m, t in submeshes]}")

# texturas extras de variantes de corpo (pro mod trocar a skin)
for mat_pid_name in ["TitanBody1Mat", "TitanBody2Mat", "TitanBody3Mat", "TitanBody4Mat", "TitanBody5Mat"]:
    for o in env.objects:
        if o.type.name == "Material":
            try:
                if o.read().m_Name == mat_pid_name:
                    read_material(o)
                    break
            except Exception:
                pass

# ---------- 3. clips ----------
def decode_streamed(sc):
    raw = struct.pack("<%dI" % len(sc.data), *sc.data)
    frames = []
    pos = 0
    n = len(raw)
    while pos + 8 <= n:
        time, cnt = struct.unpack_from("<fi", raw, pos); pos += 8
        keys = []
        for _ in range(cnt):
            idx, c0, c1, c2, c3 = struct.unpack_from("<i4f", raw, pos); pos += 20
            keys.append((idx, c0, c1, c2, c3))
        frames.append((time, keys))
    return frames

def sample_streamed(frames, curve_count, t):
    # estado por curva: (keyTime, coeffs) do último key <= t
    vals = [0.0] * curve_count
    state = [None] * curve_count
    for ft, keys in frames:
        if ft > t and ft not in (float("-inf"),):
            break
        for idx, c0, c1, c2, c3 in keys:
            if 0 <= idx < curve_count:
                state[idx] = (ft, c0, c1, c2, c3)
    for i, s in enumerate(state):
        if s is None: continue
        ft, c0, c1, c2, c3 = s
        dx = max(0.0, t - (ft if math.isfinite(ft) else 0.0))
        vals[i] = ((c0 * dx + c1) * dx + c2) * dx + c3
    return vals

def bake_clip(clip_reader):
    mc = clip_reader.m_MuscleClip
    data = mc.m_Clip.data
    sc, dc, cc = data.m_StreamedClip, data.m_DenseClip, data.m_ConstantClip
    stream_count = int(sc.curveCount)
    dense_count = int(dc.m_CurveCount)
    const_vals = list(cc.data)
    total = stream_count + dense_count + len(const_vals)
    duration = float(mc.m_StopTime - mc.m_StartTime)
    frames_streamed = decode_streamed(sc) if stream_count else []

    gb = clip_reader.m_ClipBindingConstant.genericBindings
    # layout de curvas na ordem dos bindings
    layout = []  # (boneIdx or -1, attr, offset, dims)
    off = 0
    for g in gb:
        attr = g.attribute
        dims = 4 if attr == 2 else 3
        bidx = crc_to_bone.get(g.path, -1)
        layout.append((bidx, attr, off, dims))
        off += dims
    if off != total:
        print(f"  AVISO: layout {off} != total {total}")

    nframes = max(2, int(round(duration * BAKE_FPS)) + 1)
    tracks = {}  # boneIdx -> list per frame of (pos(3) or None, quat(4) or None)
    for f in range(nframes):
        t = min(duration, f / BAKE_FPS) + float(mc.m_StartTime)
        allvals = [0.0] * total
        if stream_count:
            sv = sample_streamed(frames_streamed, stream_count, t)
            allvals[0:stream_count] = sv
        if dense_count:
            fi = int((t - float(dc.m_BeginTime)) * float(dc.m_SampleRate))
            fi = max(0, min(int(dc.m_FrameCount) - 1, fi))
            for j in range(dense_count):
                allvals[stream_count + j] = float(dc.m_SampleArray[fi * dense_count + j])
        base = stream_count + dense_count
        for j, v in enumerate(const_vals):
            allvals[base + j] = float(v)

        for bidx, attr, off2, dims in layout:
            if bidx < 0: continue
            tr = tracks.setdefault(bidx, [[None, None] for _ in range(nframes)])
            vals = allvals[off2:off2 + dims]
            if attr == 1:
                tr[f][0] = tuple(vals)
            elif attr == 2:
                tr[f][1] = tuple(vals)
            elif attr == 4:
                # euler graus -> quat (ordem ZXY do Unity)
                rx, ry, rz = [math.radians(v) for v in vals]
                cx, sx_ = math.cos(rx/2), math.sin(rx/2)
                cy, sy_ = math.cos(ry/2), math.sin(ry/2)
                cz, sz_ = math.cos(rz/2), math.sin(rz/2)
                qx = sx_*cy*cz + cx*sy_*sz_
                qy = cx*sy_*cz - sx_*cy*sz_
                qz = cx*cy*sz_ - sx_*sy_*cz
                qw = cx*cy*cz + sx_*sy_*sz_
                tr[f][1] = (qx, qy, qz, qw)
            # attr 3 = escala: ignorado
    return duration, nframes, tracks

clips_out = []
by_short = {}
for o in env.objects:
    if o.type.name != "AnimationClip": continue
    try: n = o.read().m_Name
    except Exception: continue
    if n.startswith("Amarture_VER2|"):
        by_short[n.split("|", 1)[1]] = o

for short in CLIPS_WANTED:
    o = by_short.get(short)
    if o is None:
        print("clip não achado:", short); continue
    cr = o.read()
    dur, nf, tracks = bake_clip(cr)
    clips_out.append((short, dur, nf, tracks))
    print(f"clip {short}: {dur:.2f}s, {nf} frames, {len(tracks)} bones")

# ---------- 4. escrever TPK1 ----------
def wstr(f, s):
    b = s.encode("utf-8")
    f.write(struct.pack("<H", len(b))); f.write(b)

out_path = os.path.join(OUT, "titan.tpk")
with open(out_path, "wb") as f:
    f.write(b"TPK1")
    # esqueleto
    f.write(struct.pack("<i", len(bones)))
    for b in bones:
        wstr(f, b["name"])
        f.write(struct.pack("<i", b["parent"]))
        f.write(struct.pack("<10f", *b["pos"], *b["rot"], *b["scl"]))
    # malhas
    f.write(struct.pack("<i", len(meshes)))
    for m in meshes:
        wstr(f, m["name"])
        f.write(struct.pack("<i", len(m["verts"])))
        for v in m["verts"]: f.write(struct.pack("<3f", *v))
        for v in m["normals"]: f.write(struct.pack("<3f", *v))
        for v in m["uv"]: f.write(struct.pack("<2f", *v[:2]))
        for w in m["weights"]:
            for bi, bw in list(w)[:4] + [(0, 0.0)] * (4 - len(w)):
                f.write(struct.pack("<Hf", int(bi) & 0xFFFF, float(bw)))
        f.write(struct.pack("<i", len(m["boneIdx"])))
        for bi in m["boneIdx"]: f.write(struct.pack("<H", bi))
        f.write(struct.pack("<i", len(m["bindposes"])))
        for bp in m["bindposes"]: f.write(struct.pack("<16f", *bp))
        f.write(struct.pack("<i", len(m["submeshes"])))
        for mat, tri in m["submeshes"]:
            wstr(f, mat)
            f.write(struct.pack("<i", len(tri)))
            for ix in tri: f.write(struct.pack("<i", int(ix)))
    # materiais
    f.write(struct.pack("<i", len(materials_used)))
    for name, texf in materials_used.items():
        wstr(f, name); wstr(f, texf)
    # clips
    f.write(struct.pack("<i", len(clips_out)))
    for short, dur, nf, tracks in clips_out:
        wstr(f, short)
        f.write(struct.pack("<fi", dur, nf))
        f.write(struct.pack("<i", len(tracks)))
        for bidx, frames in tracks.items():
            f.write(struct.pack("<H", bidx))
            # flags: tem pos? tem rot?
            has_pos = any(fr[0] is not None for fr in frames)
            has_rot = any(fr[1] is not None for fr in frames)
            f.write(struct.pack("<BB", 1 if has_pos else 0, 1 if has_rot else 0))
            last_p = bones[bidx]["pos"]; last_q = bones[bidx]["rot"]
            for fr in frames:
                if has_pos:
                    p = fr[0] or last_p; last_p = p
                    f.write(struct.pack("<3f", *p))
                if has_rot:
                    q = fr[1] or last_q; last_q = q
                    f.write(struct.pack("<4f", *q))

size = os.path.getsize(out_path)
print(f"\nOK -> {out_path} ({size/1024:.0f} KB)")
print("texturas:", json.dumps(materials_used, ensure_ascii=False))
# diagnóstico CRC
matched = sum(1 for g in by_short["idle"].read().m_ClipBindingConstant.genericBindings if g.path in crc_to_bone)
print(f"bindings do idle com bone correspondente: {matched}/{len(by_short['idle'].read().m_ClipBindingConstant.genericBindings)}")
