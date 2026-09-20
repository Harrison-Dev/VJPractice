# Nightflight — VJPractice

A macOS VJ practice instrument built with **Unity 6.0 (6000.0.64f1)** and **URP 17.0.4**. Combine audio-reactive GPU visuals, timed Japanese lyrics, keyboard performance controls, and an iPad-friendly controller on your local network.

This is an early, playable prototype. Its current interface is primarily in Traditional Chinese.

## Features

- **Six visual scenes:** Drift, Prism, Orbit, Tunnel, Matrix, and Fluid.
- **Six independent lyric treatments:** subtitle, diagonal, orbit, typewriter, character rain, and poster — 36 visual/lyric combinations.
- Spotify desktop playback position, track metadata, play/pause, and seek through its macOS scripting interface. Polling runs in a background helper.
- System audio analysis through BlackHole and keijiro LASP, with local audio import and an original 48-second practice track as alternatives.
- LRCLIB lyric search and candidate selection; LRC, enhanced LRC, SRT, VTT, plain text, and Folia JSON import.
- Lyric offset adjustment, manual line stamping, session persistence, and an Editor lyric workspace.
- A touch-friendly LAN controller with scene pads, lyric pads, four parameter faders, transport controls, blackout, freeze, and performance telemetry.

## Quick start

1. Clone this repository and open it with **Unity 6000.0.64f1** in Unity Hub.
2. Let Unity restore the packages listed in `Packages/manifest.json` and `Packages/packages-lock.json`.
3. Choose **VJ Practice → Open Playable Stage**, then press Play.
4. Select the original demo in the audio panel, or configure external audio below.

The main scene is `Assets/VJ/Stage/02_LyricStage.unity`. The earlier geometry experiment is retained in `Assets/VJ/Scenes/01_GeometryPractice.unity`.

### Spotify and system audio

1. Install [BlackHole 2ch](https://github.com/ExistentialAudio/BlackHole) following its official instructions.
2. In macOS Audio MIDI Setup, create a Multi-Output Device containing your speakers and BlackHole. Use matching sample rates; the tested setup uses MacBook speakers as the primary device, 48 kHz, and drift correction for BlackHole.
3. Select that Multi-Output Device as the macOS output. Connecting headphones may change this selection.
4. In Spotify, select **This computer** as the playback device and start a song.
5. In Nightflight, select **Connect Spotify**. Grant the relevant macOS audio/automation permissions if prompted.

BlackHole supplies audio; Spotify's scripting interface supplies song position. This is **not** a generic macOS Now Playing integration. Browser audio can be analyzed through BlackHole, but browser playback position is not synchronized automatically.

No commercial recordings or downloaded full song lyrics are bundled. The included demo audio and text are original practice material. Lyrics are retrieved on demand from third-party sources; availability and timing quality vary.

### iPad / LAN controller

Keep Nightflight running, connect the iPad to the same network, and open the URL shown under **Performance → Copy connection URL** in Safari. Addresses and tokens are generated locally; do not reuse another installation's URL. Network client isolation can prevent access.

Do not run the Editor performance scene and the standalone app simultaneously: both use port `32111`. The controller has been tested in a Mac browser at tablet dimensions; physical iPad validation is still pending. Use a trusted local network; this prototype does not provide HTTPS or Internet-facing authentication.

## Keyboard controls

| Key | Action |
| --- | --- |
| `1`–`6` | Select visual scene |
| `Q W E R T Y` | Select lyric treatment |
| Up / Down | Select energy, density, flow, or echo |
| Left / Right | Adjust selected parameter; hold Shift for larger steps |
| Space / Home | Play or pause / return to the beginning |
| `H` / Esc | Clean output / restore controls |
| `F` / `B` | Freeze visuals / blackout |
| `V` | Tap tempo |
| `[` / `]` | Move lyrics earlier / later by 0.1 seconds |
| Enter | Stamp the selected lyric line at the current time |
| `P` / F9 | Save a screenshot / diagnostic snapshot |

Focus the Game view when using the Editor. Performance shortcuts are suspended while typing into a text field; Esc exits text input.

## Lyric timing and Folia compatibility

Folia JSON import preserves supported word timing, translation, romanization, and offset fields. The Editor Lyric Studio also supports importing the current lyric snapshot from Folia's local API; that connection has not been tested against a running Folia instance.

**The six lyric treatments are custom Unity implementations, not ports of Folia's style renderers.** Folia styles such as Classic, Cadenza, Partita, Fume, and Monet, and Folia visual presets, are not implemented as compatible modes. Background/harmony vocals are not rendered.

Enhanced LRC and supported Folia data can provide word timing. With ordinary line-timed lyrics, character animation is estimated within each line. There is no vocal recognition or forced-alignment model. Use offsets and manual stamping to correct alignment. Saving a session does not write changes back to a lyric provider.

## References and acknowledgements

| Project | How it is used |
| --- | --- |
| [Folia](https://github.com/Harrison-Dev/folia-major) | Inspiration for immersive lyric presentation, unified lyric data, source comparison, timing controls, and local lyric interchange. Reviewed at commit `52081f79a8fce54286d57c7c7c47b568b135a069`. No Folia implementation code or artwork is included; its repository is AGPL-3.0. |
| [keijiro/Lasp](https://github.com/keijiro/Lasp) · 2.1.8 | Actual external audio input and analysis dependency. |
| [keijiro/LaspVfx](https://github.com/keijiro/LaspVfx) · 1.0.3 | The GPU particle graph is adapted from its official `Assets/Test/Particles.vfx` example, licensed under the Unlicense. |
| [keijiro/VfxGraphAssets](https://github.com/keijiro/VfxGraphAssets) · 3.9.1 | Actual subgraph dependency used by the particle graph. |
| [BlackHole](https://github.com/ExistentialAudio/BlackHole) | Separately installed macOS loopback driver; not bundled. |
| [LRCLIB](https://lrclib.net/) | Runtime lyric search and timing source; service content is not bundled. |
| [Noto CJK](https://github.com/notofonts/noto-cjk) | Bundled Noto Sans CJK JP font for Japanese text, under the SIL Open Font License 1.1. |

See the [third-party notice](Assets/VJ/Stage/ThirdParty/NOTICE.md), [particle license](Assets/VJ/Stage/ThirdParty/KEIJIRO-LICENSE.txt), and [font license](Assets/VJ/Stage/Data/Noto-LICENSE.txt). Referenced projects do not imply endorsement or full feature compatibility. Third-party materials retain their respective licenses; this snapshot does not assign a blanket license to all project content.

## Validation and limitations

- Fifteen lyric parsing/timing checks previously passed, covering formats, offsets, gaps, round trips, and line stamping.
- Spotify → BlackHole → LASP, LRCLIB search, keyboard input, and the LAN controller were exercised locally.
- On an Apple M1 Pro, six approximately four-second standalone fullscreen tests with Spotify and BlackHole measured **58.5–59.4 FPS**, with **P95 frame times of 17.0–17.6 ms**. See [raw measurements](Docs/Performance-0.4.json). This is a short test, not a sustained 60 FPS guarantee.
- First-time font loading, lyric retrieval, and app switching may still cause transient stalls.
- The retained YouTube embedding experiment is not a supported playback path: the tested official Humanoid video returned embed error 150.

## Builds and repository contents

For a macOS build, select macOS in Unity Build Profiles and include the main lyric scene. The tested standalone build used the Mono scripting backend. Configure the microphone and Apple Events usage descriptions and signing for your build. Put generated apps under `Builds/`.

Git tracks source, Unity `.meta` files, scenes, settings, original demo assets, package locks, and documentation. It excludes Unity caches, builds, runtime screenshots/logs, local configuration, and personal music placed under `Assets/VJ/Audio/`. Existing local app builds are stored separately from this source snapshot.

Additional Traditional Chinese instructions: [operation guide](Docs/OperationGuide.md).

License inventory and redistribution boundaries: [License review](Docs/LicenseReview.md). Dependency license notices are included under `Assets/StreamingAssets/ThirdPartyLicenses` for future builds.
