#!/usr/bin/env python3
"""Source-level regression guards, NOT a C# compiler or a Unity rendering test."""
from pathlib import Path
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

# The live deck deliberately replaces the six classic lyric modes. Guard the
# integration paths and keep the removed selection API from returning.
check("MotionInit();" in stage and "PrepareMotionOutput();" in stage and
      "UpdateMotion();" in stage and "MotionDispose();" in stage and
      "DrawMotionOutput(stage)" in stage and "JizuraFromDocument(doc)" in stage,
      "Unity stage still initializes, updates, presents and disposes native output")
check("LyricMode" not in stage and "LyricMode" not in controls and
      'case "lyric"' not in controls and 'if (action == "lyric")' not in bridge and
      "DrawAnimatedLine" not in stage,
      "selectable legacy subtitle modes are removed")
check("SetJizuraManualLook" in bridge and "JizuraManualLook" in controls and
      'case "jizuraLook"' in read("Assets/VJ/Stage/Runtime/VJStage.Jizura.cs"),
      "live JIZURA looks route through keyboard and operator controls")
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
check("if(ApplyMotionControl(action,value))return;" in controls and
      "ApplyJizuraControl(action, value)" in bridge,
      "performance controls route through the native JIZURA layer")
check("RemoteDeck" not in controls and "remote.Url" not in stage and
      not (ROOT / "Assets/VJ/Stage/Runtime/RemoteDeck.cs").exists() and
      not (ROOT / "Assets/VJ/Stage/Resources/RemoteDeck.txt").exists() and
      not (ROOT / "Assets/VJ/Stage/Resources/MotionDeckEnhancement.txt").exists(),
      "browser remote server and page are removed")
check("RenderTexture.active = target" in bridge and "new Rect(0, 0, target.width, target.height)" in bridge,
      "F8 captures the final texture rather than the operator screen")
native_stage = read("Assets/VJ/Stage/Runtime/VJStage.Jizura.cs")
native_planner = read("Assets/VJ/Stage/Runtime/Jizura/JizuraPlanner.cs")
native_renderer = read("Assets/VJ/Stage/Runtime/Jizura/JizuraNativeRenderer.cs")
check("JizuraPlanner.Build" in native_stage and "JizuraFromDocument(doc)" in stage and
      'EndsWith(".jizura.json"' in stage, "original JIZURA project feeds the Unity stage")
check("public static JizuraPlan Build" in native_planner and "public readonly List<JizuraCut> cuts" in native_planner and
      "JizuraNativeRenderer" in native_renderer and "CurrentUnsupported" in native_renderer,
      "native JIZURA planner and renderer expose timed cuts and unsupported IDs")

assets = list((ROOT / "Assets/VJ/Stage/Runtime/Motion").glob("*.cs")) + [
    ROOT / "Assets/VJ/Stage/Runtime/Motion",
    ROOT / "Assets/VJ/Stage/Runtime/VJStage.Motion.cs",
    ROOT / "Assets/VJ/Stage/Editor/KineticOutputValidation.cs",
] + list((ROOT / "Assets/VJ/Stage/Runtime/Jizura").glob("*.cs")) + [
    ROOT / "Assets/VJ/Stage/Runtime/Jizura",
    ROOT / "Assets/VJ/Stage/Runtime/VJStage.Jizura.cs",
    ROOT / "Assets/VJ/Stage/Editor/JizuraStudio.cs",
    ROOT / "Assets/VJ/Stage/Editor/JizuraValidation.cs",
    ROOT / "Assets/VJ/Stage/Editor/VJStageRecorder.cs",
    ROOT / "Assets/VJ/Stage/Resources/JizuraDemo.json",
    ROOT / "Assets/VJ/Stage/ThirdParty/JIZURA-LICENSE.txt",
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
