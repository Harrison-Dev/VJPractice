# PR #1 與 JIZURA 的整合差距

檢查基準：[JIZURA `1b48bea`](https://github.com/852wa/JIZURA/tree/1b48bea2d74e60f9b0ff2c247aa5919ba03a6551) 與本專案 PR #1 的 `5c225dd`。這份紀錄用於釐清功能與驗收目標；目前 PR 是受 JIZURA 啟發的 Unity 原生簡化版，尚未整合 JIZURA 的專案、規劃器或渲染器。

| 能力 | JIZURA | PR #1 |
| --- | --- | --- |
| 歌詞編排 | 一行可切成多個 timed cuts；可用 `/` 指定切點、`*...*` 強調、`!` 觸發衝擊、`|` 加註解，並可對拍 | 每行一個 cut；讀取既有 `LyricDocument` 時間，沒有 JIZURA 語法或對拍切鏡 |
| 變體 | 一鍵重新生成風格、氛圍、動作、配色、構成；可前後瀏覽方案、只重抽一項、逐行覆寫或鎖定 | 三種 mood、目前句重抽與鎖定；沒有完整方案歷史、整體變體或分項重抽 |
| 表現 | 24 種 style 與 707 個技巧，涵蓋版面、進場、保持、退場、裝飾、文字加工、背景、相機、畫面效果、轉場 | 五種版面、三種進場和三種退場；色差文字；其餘技巧類別沒有對應引擎 |
| 專案互通 | 儲存／開啟 `.jizura.json`，另可輸出 AE 編排 JSON | 私有 `MotionPlan-<SHA256>.json`；不能讀寫 JIZURA 格式 |
| 字型與語言 | 多書體、語言偵測與日／中／韓字型對應 | 固定專案內 Noto Sans CJK JP |
| 輸出 | MP4、PNG 連番、透明前／後景、綠／黑背景；多種畫面比例、解析度和 fps | 即時 16:9 RenderTexture 與單張 F8 PNG |

JIZURA 是以 JavaScript、Canvas 2D、WebAudio 與 WebCodecs 實作的瀏覽器應用；PR #1 的渲染路徑是 Unity UGUI/URP。要在 Unity 中呈現同一個 JIZURA 專案，需要先決定是使用其原始引擎，還是建立有明確支援範圍的 Unity 渲染轉接層。只加更多面板按鈕或把 JIZURA JSON 複製進專案，無法取得對應的視覺效果。

## 若目標是在 Unity 使用 JIZURA 專案，最低驗收

1. 能載入使用者從 JIZURA 儲存的 `.jizura.json`，並保留歌詞、時間、seed、style、逐行覆寫與鎖定。
2. 同一專案在 Unity 中有多 cut 編排，預覽顯示的版面、動作、配色與 JIZURA 所選技巧有可驗證的對應；未支援技巧須明示，不能默默換成五種既有版面。
3. 重新生成、逐行重抽與鎖定能在 Editor 中看見結果，且存檔後重開可重現。
4. 使用原創示範在 Editor Play Mode 驗證畫面與輸出；依使用者要求，不以建置 Player 作為此輪測試。

JIZURA 原始碼是 MIT 授權；若納入其程式碼，需要一併保留授權與相關第三方 notices。參考：[官方 README](https://github.com/852wa/JIZURA/blob/1b48bea2d74e60f9b0ff2c247aa5919ba03a6551/README.md)、[planner](https://github.com/852wa/JIZURA/blob/1b48bea2d74e60f9b0ff2c247aa5919ba03a6551/src/08_planner.js)、[renderer](https://github.com/852wa/JIZURA/blob/1b48bea2d74e60f9b0ff2c247aa5919ba03a6551/src/09_render.js)、[license](https://github.com/852wa/JIZURA/blob/1b48bea2d74e60f9b0ff2c247aa5919ba03a6551/LICENSE)。
