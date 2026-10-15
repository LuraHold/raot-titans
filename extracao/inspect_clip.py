import UnityPy, sys
sys.stdout.reconfigure(encoding="utf-8")
p = r"X:\Downloads\rea-tools\aottg2\Aottg2Launcher\Release\MainApp\Aottg2_Data\resources.assets"
env = UnityPy.load(p)
byid = {o.path_id: o for o in env.objects}
clip = byid[7777].read()  # Amarture_VER2|idle
print("name:", clip.m_Name)
for attr in ("m_Legacy", "m_SampleRate", "m_WrapMode"):
    print(attr, getattr(clip, attr, "?"))
for attr in ("m_RotationCurves", "m_CompressedRotationCurves", "m_EulerCurves", "m_PositionCurves", "m_ScaleCurves", "m_FloatCurves"):
    v = getattr(clip, attr, None)
    print(attr, len(v) if v is not None else None)
mc = getattr(clip, "m_MuscleClip", None)
print("muscle:", type(mc).__name__ if mc else None)
if mc:
    c = getattr(mc, "m_Clip", None)
    print(" clip data:", type(c).__name__ if c else None)
    if c:
        data = c.data if hasattr(c, "data") else c
        for a in dir(data):
            if a.startswith("m_"):
                print("   ", a)
