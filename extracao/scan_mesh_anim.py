import os
import UnityPy

p = r"X:\Downloads\rea-tools\aottg2\Aottg2Launcher\Release\MainApp\Aottg2_Data\resources.assets"
env = UnityPy.load(p)
meshes, clips, gos = [], [], []
for obj in env.objects:
    tn = obj.type.name
    if tn not in ("Mesh", "AnimationClip", "GameObject"):
        continue
    try:
        d = obj.read()
        name = getattr(d, "m_Name", "") or ""
    except Exception:
        continue
    ln = name.lower()
    if tn == "Mesh" and ("titan" in ln or "body" in ln or "head" in ln or "hair" in ln):
        meshes.append((name, obj.path_id))
    elif tn == "AnimationClip" and "titan" in ln:
        clips.append((name, obj.path_id))
    elif tn == "GameObject" and "titan" in ln:
        gos.append((name, obj.path_id))

print("== MESHES ==")
for n, pid in sorted(set(meshes))[:60]: print(f"  {n}  (pid {pid})")
print("== ANIMATION CLIPS ==")
for n, pid in sorted(set(clips))[:80]: print(f"  {n}  (pid {pid})")
print("== GAMEOBJECTS ==")
for n, pid in sorted(set(gos))[:40]: print(f"  {n}  (pid {pid})")
