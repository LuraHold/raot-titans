import UnityPy, sys
sys.stdout.reconfigure(encoding="utf-8")
p = r"X:\Downloads\rea-tools\aottg2\Aottg2Launcher\Release\MainApp\Aottg2_Data\resources.assets"
env = UnityPy.load(p)
byid = {o.path_id: o for o in env.objects}
def deref(pp):
    try:
        return byid.get(pp.path_id) if pp and pp.path_id else None
    except Exception:
        return None
def nm(o):
    try: return o.read().m_Name
    except Exception: return "?"
def walk(go_obj, path, out):
    go = go_obj.read()
    mypath = path + "/" + go.m_Name if path else go.m_Name
    tr = None
    for c in go.m_Component:
        co = deref(c.component)
        if co is None: continue
        tn = co.type.name
        if tn == "Transform": tr = co
        if tn in ("MeshRenderer", "SkinnedMeshRenderer"):
            d = co.read()
            mats = [nm(deref(m)) for m in d.m_Materials]
            mesh = "?"
            if tn == "SkinnedMeshRenderer":
                mo = deref(d.m_Mesh); mesh = nm(mo) if mo else "?"
            else:
                for c2 in go.m_Component:
                    co2 = deref(c2.component)
                    if co2 is not None and co2.type.name == "MeshFilter":
                        mo = deref(co2.read().m_Mesh); mesh = nm(mo) if mo else "?"
            out.append(f"{tn:20} {mypath}  mesh={mesh} mats={mats}")
    if tr:
        for ch in tr.read().m_Children:
            cho = deref(ch)
            if cho is None: continue
            chgo = deref(cho.read().m_GameObject)
            if chgo is not None: walk(chgo, mypath, out)
for pid in [13444, 15427, 15678, 15679, 14732]:
    out = []
    walk(byid[pid], "", out)
    print("==", nm(byid[pid]), "pid", pid)
    for l in out: print("  ", l)
