#!/usr/bin/env python3
"""Source-level regression guards, NOT a C# compiler or a Unity rendering test."""
from pathlib import Path
import hashlib
import re

ROOT = Path(__file__).resolve().parents[1]
count = 0


def check(condition: bool, description: str) -> None:
    global count
    if not condition:
        raise AssertionError(description)
    count += 1
    print(f"PASS {description}")


def read(path: str) -> str:
    return (ROOT / path).read_text(encoding="utf-8")


stage = read("Assets/VJ/Stage/Runtime/VJStage.cs")
bridge = read("Assets/VJ/Stage/Runtime/VJStage.Motion.cs")
renderer = read("Assets/VJ/Stage/Runtime/Motion/KineticLyricRenderer.cs")
compositor = read("Assets/VJ/Stage/Runtime/Motion/StageCompositor.cs")
planner = read("Assets/VJ/Stage/Runtime/Motion/LyricMotionPlan.cs")
controls = read("Assets/VJ/Stage/Runtime/PerformanceControls.cs")

# Reverse the intended integration edits and remote-control removal, then verify the COMPLETE original blob.
# This detects accidental modifications to transport, parsing hooks or legacy rendering.
reverse = [
    ("public RenderTexture Output=>KineticOutput?KineticOutput:history;", "public RenderTexture Output=>history;"),
    ("ready=true;ConsoleInit();MotionInit();", "ready=true;ConsoleInit();"),
    ("        PrepareMotionOutput();\n        if(!KineticReady)outputCamera.rect=CleanOutput?", "        outputCamera.rect=CleanOutput?"),
    ("        RenderVisual();UpdateMotion();\n", "        RenderVisual();\n"),
    ("    void HandleKey(KeyCode key){\n        if(HandleMotionKey(key)){Event.current.Use();return;}\n", "    void HandleKey(KeyCode key){\n"),
    ("    void OnDestroy(){MotionDispose();Release();", "    void OnDestroy(){Release();"),
    ("        if(!DrawMotionOutput(stage)){if(Blackout){Fill(stage,Color.black);}else DrawLyrics(stage);}", "        if(Blackout){Fill(stage,Color.black);}else DrawLyrics(stage);"),
    (r'\np95ms={P95:F2}\n");Message=', r'\np95ms={P95:F2}\nremote={remote.Url}\n");Message='),
]
original = stage
for current, old in reverse:
    assert original.count(current) == 1, f"Missing or duplicate integration hook: {current}"
    original = original.replace(current, old)
blob = original.encode("utf-8")
sha = hashlib.sha1(b"blob " + str(len(blob)).encode() + b"\0" + blob).hexdigest()
check(sha == "24aec6c48bc624026b9855c4ecfbc6cea884d782", "pre-existing VJStage code preserved outside intentional integration edits")
check("using UnityEngine" not in planner and "System.Random" not in planner and "GetHashCode(" not in planner,
      "portable core avoids Unity and process-dependent randomness")
check("Time.time" not in renderer.replace("// Absolute song time, never Time.time", "") and "Time.deltaTime" not in renderer,
      "lyric renderer has no accumulated wall-clock animation")
check("Position - Document.offsetSeconds" in bridge and "Audio.Bands.x" in bridge, "song clock and audio modulation have separate inputs")
check("endContextRendering += EndContext" in compositor and "endContextRendering -= EndContext" in compositor,
      "URP output callback has symmetric cleanup")
check("(frozen() && hasFrame)" in compositor and "blackout() ? black : presented" in compositor,
      "freeze retains the composited frame and blackout uses a separate texture")
check("const int PoolSize = 32" in renderer and "used >= labels.Length" in renderer,
      "native text pool has an explicit draw budget")
check("if(ApplyMotionControl(action,value))return;" in controls and 'if (action == "lyric") { KineticLyrics = false; return false; }' in bridge,
      "classic and new controls share routing without removing classic modes")
check("RemoteDeck" not in controls and "remote.Url" not in stage and
      not (ROOT / "Assets/VJ/Stage/Runtime/RemoteDeck.cs").exists() and
      not (ROOT / "Assets/VJ/Stage/Resources/RemoteDeck.txt").exists() and
      not (ROOT / "Assets/VJ/Stage/Resources/MotionDeckEnhancement.txt").exists(),
      "browser remote server and page are removed")
check("RenderTexture.active = target" in bridge and "new Rect(0, 0, target.width, target.height)" in bridge,
      "F8 captures the final texture rather than the operator screen")

assets = list((ROOT / "Assets/VJ/Stage/Runtime/Motion").glob("*.cs")) + [
    ROOT / "Assets/VJ/Stage/Runtime/Motion",
    ROOT / "Assets/VJ/Stage/Runtime/VJStage.Motion.cs",
    ROOT / "Assets/VJ/Stage/Editor/KineticOutputValidation.cs",
]
guids = []
for asset in assets:
    meta = Path(str(asset) + ".meta")
    assert meta.is_file(), f"Missing Unity meta: {meta}"
    match = re.search(r"^guid: ([0-9a-f]{32})$", meta.read_text(), re.MULTILINE)
    assert match, f"Invalid GUID: {meta}"
    guids.append(match[1])
check(len(guids) == len(set(guids)), "new Unity assets have unique, committed metadata")
print(f"{count} source invariants passed. Unity compilation, images, and performance require the Editor probe/manual review.")
