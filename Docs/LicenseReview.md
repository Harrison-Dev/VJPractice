# Third-party license review

Reviewed against the installed package licenses and the Folia reference checkout on 2026-09-21. This is an engineering inventory, not a blanket legal clearance of every use case.

| Component | License / status | Current handling |
| --- | --- | --- |
| LASP, LaspVfx, VfxGraphAssets, ShaderGraphAssets | Unlicense | Packages restored through Unity; adapted particle graph identified in NOTICE. License copies included in StreamingAssets. |
| libsoundio | MIT (Expat), Copyright (c) 2015 Andrew Kelley | Transitive native dependency; full copyright and permission notice included in StreamingAssets. |
| NoiseShader | MIT-style permission, Ashima Arts / Stefan Gustavson | Transitive shader dependency; full notice included in StreamingAssets. |
| Noto Sans CJK JP | SIL OFL 1.1 | Unmodified font and OFL retained; embedded Adobe copyright also extracted into a readable notice. |
| Folia | AGPL-3.0 at reference commit 52081f79a8fce54286d57c7c7c47b568b135a069 | Reference and data/API compatibility only; no Folia renderer or artwork copied. A future code port needs a separate copyleft/Unity compatibility review; attribution alone is not sufficient. |
| BlackHole | Separately installed driver | No driver source, binary or installer is distributed by this repository. Upstream distinguishes GPLv3 source from its official binary distribution terms. Do not bundle its installer on the assumption that the source license alone authorizes it. |
| Unity / URP / VFX Graph | Unity terms and package-specific third-party notices | Engine/package caches are not redistributed in Git. Users obtain Unity and restore packages separately. Public binary releases still need Unity license eligibility and redistribution review. |
| Spotify / LRCLIB | External application and service | Neither software licenses nor API access grant rights to redistribute songs or lyrics. No downloaded commercial songs or full lyrics are committed. Public performances/recordings require their own rights assessment. |

The StreamingAssets notices will be copied by future builds. The previously built local app predates this notice addition and has not been rebuilt in this documentation-only update. No public app release has been published.

The project's original code still has no selected repository-wide license. Public visibility does not itself provide an MIT/Apache-style reuse grant. Choosing that license is a separate owner decision.

Sources: [OFL](https://openfontlicense.org/open-font-license-official-text/), [BlackHole license](https://github.com/ExistentialAudio/BlackHole/blob/master/LICENSE), [Folia](https://github.com/Harrison-Dev/folia-major), and the exact installed package LICENSE files copied under Assets/StreamingAssets/ThirdPartyLicenses.
