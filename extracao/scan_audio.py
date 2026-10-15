import UnityPy, sys, os
sys.stdout.reconfigure(encoding="utf-8")
root = r"X:\Downloads\rea-tools\aottg2\Aottg2Launcher\Release\MainApp\Aottg2_Data"
keys = ["titan", "roar", "scream", "eat", "bite", "step", "stomp", "grab", "hit", "die", "death", "nape", "slash", "blood"]
for fn in os.listdir(root):
    p = os.path.join(root, fn)
    if not os.path.isfile(p) or not (fn.endswith(".assets") or fn.startswith("level") or fn.startswith("resources")):
        continue
    try:
        env = UnityPy.load(p)
    except Exception:
        continue
    for obj in env.objects:
        if obj.type.name != "AudioClip":
            continue
        try:
            d = obj.read()
            n = d.m_Name
        except Exception:
            continue
        ln = n.lower()
        if any(k in ln for k in keys):
            print(f"{fn}  {n}  (pid {obj.path_id}, {getattr(d,'m_Length',0):.1f}s)")
