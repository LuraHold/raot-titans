import os, sys
import UnityPy

root = r"X:\Downloads\rea-tools\aottg2\Aottg2Launcher\Release\MainApp\Aottg2_Data"
hits = {}
for fn in os.listdir(root):
    p = os.path.join(root, fn)
    if not os.path.isfile(p):
        continue
    if not (fn.endswith(".assets") or fn.startswith("level") or fn.startswith("resources") or fn.endswith(".resource")):
        continue
    try:
        env = UnityPy.load(p)
    except Exception as e:
        print("ERR", fn, e)
        continue
    for obj in env.objects:
        tn = obj.type.name
        if tn not in ("Mesh", "Texture2D", "AnimationClip", "GameObject", "Material"):
            continue
        try:
            d = obj.read()
            name = getattr(d, "m_Name", "") or ""
        except Exception:
            continue
        ln = name.lower()
        if "titan" in ln or "eren" in ln or "annie" in ln or "colossal" in ln:
            hits.setdefault(fn, []).append((tn, name))

for fn, lst in hits.items():
    print("==", fn, f"({len(lst)} hits)")
    seen = set()
    for tn, name in lst:
        k = (tn, name)
        if k in seen:
            continue
        seen.add(k)
        print(f"   {tn:15} {name}")
