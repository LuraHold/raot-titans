import UnityPy, sys, os
sys.stdout.reconfigure(encoding="utf-8")
SRC = r"X:\Downloads\rea-tools\aottg2\Aottg2Launcher\Release\MainApp\Aottg2_Data\resources.assets"
OUT = r"X:\Downloads\rea-tools\TitansMod\assets\sfx"
os.makedirs(OUT, exist_ok=True)
WANTED = {
    "TitanRoarSound": "roar1", "Roar2Sound": "roar2", "AnnieRoar": "roar3",
    "TitanStep1Sound": "step1", "TitanStep2Sound": "step2", "TitanStep3Sound": "step3",
    "Bite1Sound": "bite1", "Bite2Sound": "bite2",
    "BladeNape1Var1": "nape1", "BladeNape2Var1": "nape2", "BladeNape3Var1": "nape3", "BladeNape4Var1": "nape4",
    "NapeHitSound": "napehit",
    "Death1Sound": "death1", "Death2Sound": "death2", "Death3Sound": "death3", "Death4Sound": "death4", "Death5Sound": "death5",
    "TitanLaugh1Sound": "laugh1", "TitanLaugh2Sound": "laugh2",
    "LimbHitSound": "limbhit", "HitSound": "hit", "BladeHitSound": "bladehit",
    "Breath1Sound": "breath1",
}
env = UnityPy.load(SRC)
done = 0
for obj in env.objects:
    if obj.type.name != "AudioClip":
        continue
    try:
        d = obj.read()
    except Exception:
        continue
    if d.m_Name not in WANTED:
        continue
    try:
        samples = d.samples  # dict nome -> bytes wav
        if not samples:
            print("sem samples:", d.m_Name); continue
        data = list(samples.values())[0]
        fp = os.path.join(OUT, WANTED[d.m_Name] + ".wav")
        open(fp, "wb").write(data)
        print(f"ok {d.m_Name} -> {os.path.basename(fp)} ({len(data)//1024} KB)")
        done += 1
    except Exception as e:
        print("falhou", d.m_Name, e)
print("exportados:", done, "/", len(WANTED))
