import UnityPy, sys
sys.stdout.reconfigure(encoding="utf-8")
env = UnityPy.load(r"X:\Downloads\rea-tools\aottg2\Aottg2Launcher\Release\MainApp\Aottg2_Data\resources.assets")
byid = {o.path_id: o for o in env.objects}
def deref(pp):
    try: return byid.get(pp.path_id) if pp and pp.path_id else None
    except Exception: return None
def nm(o):
    try: return o.read().m_Name
    except Exception: return "?"
def mat_tex(mo):
    try:
        m = mo.read()
        out = []
        for e in m.m_SavedProperties.m_TexEnvs:
            k = e[0] if isinstance(e,(list,tuple)) else getattr(e,"first",e)
            v = e[1] if isinstance(e,(list,tuple)) else getattr(e,"second",None)
            kn = k if isinstance(k,str) else getattr(k,"name",str(k))
            t = deref(v.m_Texture)
            if t is not None: out.append(f"{kn}={nm(t)}")
        cols = []
        for e in m.m_SavedProperties.m_Colors:
            k = e[0] if isinstance(e,(list,tuple)) else getattr(e,"first",e)
            v = e[1] if isinstance(e,(list,tuple)) else getattr(e,"second",None)
            kn = k if isinstance(k,str) else getattr(k,"name",str(k))
            cols.append(f"{kn}=({v.r:.2f},{v.g:.2f},{v.b:.2f},{v.a:.2f})")
        sh = deref(m.m_Shader)
        return f"shader={nm(sh) if sh else '?'} tex[{', '.join(out)}] cols[{', '.join(cols[:4])}]"
    except Exception as e:
        return "err " + str(e)
def walk(go_obj, depth=0, maxd=4):
    go = go_obj.read()
    tr = None; info = []
    for c in go.m_Component:
        co = deref(c.component)
        if co is None: continue
        if co.type.name == "Transform":
            tr = co; t = co.read()
            info.append(f"pos=({t.m_LocalPosition.x:.3f},{t.m_LocalPosition.y:.3f},{t.m_LocalPosition.z:.3f}) scl=({t.m_LocalScale.x:.3f},{t.m_LocalScale.y:.3f},{t.m_LocalScale.z:.3f}) rot=({t.m_LocalRotation.x:.2f},{t.m_LocalRotation.y:.2f},{t.m_LocalRotation.z:.2f},{t.m_LocalRotation.w:.2f})")
        if co.type.name == "MeshFilter":
            mo = deref(co.read().m_Mesh); info.append("mesh=" + (nm(mo) if mo else "?"))
        if co.type.name in ("MeshRenderer","SkinnedMeshRenderer"):
            d = co.read()
            for mp in d.m_Materials:
                mo = deref(mp)
                if mo is not None: info.append(f"mat {nm(mo)}: {mat_tex(mo)}")
    print("  " * depth + go.m_Name + "  " + " | ".join(info))
    if tr and depth < maxd:
        for ch in tr.read().m_Children:
            cho = deref(ch)
            if cho is None: continue
            chgo = deref(cho.read().m_GameObject)
            if chgo is not None: walk(chgo, depth + 1, maxd)
targets = {"TitanEyes0","TitanEyes1","TitanEyes2","TitanEyes3","HairF13","HairF14"}
seen = set()
for o in env.objects:
    if o.type.name != "GameObject": continue
    n = nm(o)
    if n in targets and n not in seen:
        seen.add(n); print("==", n, "pid", o.path_id); walk(o)
# GOs que usam as malhas HairM19/HairM20
for o in env.objects:
    if o.type.name != "MeshFilter": continue
    mo = deref(o.read().m_Mesh)
    if mo is not None and nm(mo) in ("HairM19","HairM20"):
        go = deref(o.read().m_GameObject)
        if go is not None: print("== GO com", nm(mo), "pid", go.path_id); walk(go, 0, 1)
EOF
