import UnityPy, sys
sys.stdout.reconfigure(encoding="utf-8")

p = r"X:\Downloads\rea-tools\aottg2\Aottg2Launcher\Release\MainApp\Aottg2_Data\resources.assets"
env = UnityPy.load(p)
byid = {o.path_id: o for o in env.objects}

def deref(pptr):
    try:
        if pptr and pptr.path_id:
            return byid.get(pptr.path_id)
    except Exception:
        pass
    return None

def goname(obj):
    try:
        return obj.read().m_Name
    except Exception:
        return "?"

def walk(go_obj, depth, out, maxdepth=6):
    go = go_obj.read()
    comps = []
    for c in go.m_Component:
        co = deref(c.component)
        if co is None: continue
        tn = co.type.name
        extra = ""
        if tn == "SkinnedMeshRenderer":
            d = co.read()
            m = deref(d.m_Mesh)
            extra = f" mesh={goname(m) if m else '?'} bones={len(d.m_Bones)}"
        if tn == "Animator":
            d = co.read()
            ctrl = deref(d.m_Controller)
            extra = f" controller={goname(ctrl) if ctrl else '?'}"
        comps.append(tn + extra)
    out.append("  " * depth + f"{go.m_Name}  [{', '.join(c for c in comps if c != 'Transform')}]")
    if depth >= maxdepth: return
    # children via transform
    for c in go.m_Component:
        co = deref(c.component)
        if co is not None and co.type.name in ("Transform",):
            t = co.read()
            for ch in t.m_Children:
                cho = deref(ch)
                if cho is None: continue
                chgo = deref(cho.read().m_GameObject)
                if chgo is not None:
                    walk(chgo, depth + 1, out, maxdepth)

target_pid = int(sys.argv[1]) if len(sys.argv) > 1 else 13444
out = []
walk(byid[target_pid], 0, out)
print("\n".join(out[:120]))

print("\n== CLIPS Amarture ==")
names = set()
for o in env.objects:
    if o.type.name != "AnimationClip": continue
    try: n = o.read().m_Name
    except Exception: continue
    if n.lower().startswith("amarture"):
        names.add((n, o.path_id))
for n, pid in sorted(names):
    print(f"  {n} (pid {pid})")
