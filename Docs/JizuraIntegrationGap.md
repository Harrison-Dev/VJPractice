# JIZURA 與 Unity 原生移植的對照

檢查基準：[JIZURA `1b48bea`](https://github.com/852wa/JIZURA/tree/1b48bea2d74e60f9b0ff2c247aa5919ba03a6551)。PR #1 原先只有受 JIZURA 啟發的簡化文字 PV；目前已加入原生 C# 專案讀寫、規劃器、渲染器及 Editor 編輯器。下表記錄尚未達成的原版功能，避免將「可匯入專案」誤認為逐像素相容。

| 能力 | Unity 原生移植現況 | 差距 |
| --- | --- | --- |
| `.jizura.json` | 可匯入、編輯、另存；保留未解析欄位與逐行覆寫 | 只接受 schema v1；未知設定保留資料但不一定影響畫面 |
| 歌詞與時間 | 支援 LRC、`/` 切詞、`*...*` 強調、`!`、`|` 註解；可產生一行多個 timed cuts | 瀏覽器分詞與完整對拍規則尚未等價；沒有音訊分析 |
| 編排 | 移植固定 seed 規劃、12 種原版核心配色、17 個核心 layout ID、進／保持／退場組合；可重抽與逐行鎖定 | layout 與動作由 uGUI 近似；未移植全部參數及 expression packs |
| 裝飾 | 原生實作 9 個核心裝飾 ID、數種 HUD 與背景 accent | 原版 Canvas／shader 技巧、文字加工、相機、轉場及 707 個技巧未全數移植 |
| 編輯器 | Unity Editor Window 可改歌詞、時間、風格、特效、逐行覆寫、歷史、Play Mode 即時預覽 | 原版網頁編輯器的所有互動沒有完全一對一重現 |
| VJ 現場混合 | 原有六種舞台 shader／粒子作底層；JIZURA 原生 Canvas 作上層；有文字疊加、混合演出、JIZURA 全景及連續比例；Energy／Density／Flow／Echo 與音訊頻帶即時輸入，F6／F7 保存獨立舞台配置 | 原版技巧包與舞台 shader 尚無逐效果映射；未實作每個 JIZURA 層的獨立透明度與轉場控制 |
| 字型、語言、格式 | 沿用專案 Noto CJK，使用既有 16:9 RenderTexture 與 F8 單張 PNG | 原版多字型、語言對應、其它畫面比例、MP4／PNG 連番／AE 輸出尚未移植 |

這條路徑完全在 Unity Editor 和執行時以 C#／uGUI／URP 工作，沒有內嵌網頁，也沒有瀏覽器遠端操作盤。原版 JSON 中尚未渲染的 ID 保留於專案，並在編輯器與舞台狀態標示。實作與操作見 [JizuraNativePort.md](JizuraNativePort.md)。

## 驗收範圍

1. 原版 `.jizura.json` 載入後，歌詞、時間、seed、style、逐行覆寫及鎖定能保存重開。
2. 原創示範產生多 cut，Editor Play Mode 輸出有原生文字與不同時點畫面。
3. 編輯器改動能重新規劃並套用到 Play Mode 舞台；未支援的設定要明示。
4. 不執行 Unity Player build；以 Editor Play Mode 和純 C# 規劃器檢查驗證。

JIZURA 原始碼採 MIT 授權。專案內保留授權全文與 notice。參考：[官方 README](https://github.com/852wa/JIZURA/blob/1b48bea2d74e60f9b0ff2c247aa5919ba03a6551/README.md)、[planner](https://github.com/852wa/JIZURA/blob/1b48bea2d74e60f9b0ff2c247aa5919ba03a6551/src/08_planner.js)、[renderer](https://github.com/852wa/JIZURA/blob/1b48bea2d74e60f9b0ff2c247aa5919ba03a6551/src/09_render.js)、[license](https://github.com/852wa/JIZURA/blob/1b48bea2d74e60f9b0ff2c247aa5919ba03a6551/LICENSE)。
