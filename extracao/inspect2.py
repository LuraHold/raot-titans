import UnityPy, sys
sys.stdout.reconfigure(encoding="utf-8")
p = r"X:\Downloads\rea-tools\aottg2\Aottg2Launcher\Release\MainApp\Aottg2_Data\resources.assets"
env = UnityPy.load(p)
byid = {o.path_id: o for o in env.objects}

print("== clips (r..z) ==")
names = set()
for o in env.objects:
    if o.type.name != "AnimationClip": continue
    try: n = o.read().m_Name
    except Exception: continue
    if n.lower().startswith("amarture_ver2"):
        short = n.split("|", 1)[1]
        if short[:1].lower() >= "r":
            names.add((short, o.path_id))
for n, pid in sorted(names): print(f"  {n} (pid {pid})")

print("\n== mesh body_0 ==")
# find mesh named body_0
for o in env.objects:
    if o.type.name != "Mesh": continue
    d = o.read()
    if d.m_Name == "body_0":
        print("pid", o.path_id)
        for a in dir(d):
            if a.startswith("m_") and a in ("m_VertexCount",):
                print(a, getattr(d, a))
        skin = getattr(d, "m_Skin", None)
        print("skin entries:", len(skin) if skin else None)
        bind = getattr(d, "m_BindPose", None)
        print("bindposes:", len(bind) if bind is not None else None)
        # vertex extraction helpers
        for cand in ("export", "read_mesh"):
            print(cand, hasattr(d, cand))
        try:
            from UnityPy.helpers.MeshHelper import MeshHandler
            mh = MeshHandler(d)
            mh.process()
            print("MeshHandler ok verts:", len(mh.m_Vertices) if mh.m_Vertices is not None else None,
                  "norms:", len(mh.m_Normals) if mh.m_Normals is not None else None,
                  "uv:", len(mh.m_UV0) if mh.m_UV0 is not None else None,
                  "idx:", len(mh.m_IndexBuffer) if mh.m_IndexBuffer is not None else None)
        except Exception as e:
            print("MeshHandler fail:", e)
        break

print("\n== clip binding ==")
clip = byid[7777].read()
cbc = getattr(clip, "m_ClipBindingConstant", None)
print("bindingconstant:", cbc is not None)
if cbc:
    gb = cbc.genericBindings
    print("genericBindings:", len(gb))
    from collections import Counter
    print(Counter(g.attribute for g in gb[:2000]))
    print("sample:", [(g.path, g.attribute) for g in gb[:6]])
mc = clip.m_MuscleClip
c = mc.m_Clip
data = c.data
sc = data.m_StreamedClip
dc = data.m_DenseClip
cc = data.m_ConstantClip
print("streamed words:", len(sc.data), "curveCount:", sc.curveCount)
print("dense frames:", dc.m_FrameCount, "curves:", dc.m_CurveCount, "begin:", dc.m_BeginTime, "sr:", dc.m_SampleRate, "samples:", len(dc.m_SampleArray))
print("constant:", len(cc.data))
print("muscle start/stop:", mc.m_StartTime, mc.m_StopTime)
