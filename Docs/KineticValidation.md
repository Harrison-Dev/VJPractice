# Kinetic v1 validation

## 已執行與未執行必須分開

| 層級 | 測試內容 | 本次狀態 |
| --- | --- | --- |
| C# production core | 24 個測試：seed、順序獨立、重抽、鎖定、延後操作、間奏、seek、時間邊界、Unicode、長句 fallback、JSON、schema、文化設定 | GitHub Actions 已執行通過；PR 最新 commit 的 checks 為準 |
| Source invariants | 13 項：七個接入點外 VJStage 完整 blob 不變、clock、callback cleanup、RT freeze/blackout、池上限、按鈕路由、LAN 邊界、F8 貼圖來源、meta | 本機 Python 檢查已通過，亦納入 CI |
| LAN JavaScript | 新增控制區塊的 JavaScript 語法 | 本機 `node --check` 已通過；不是 Safari 互動實測 |
| Unity compilation / GPU | Unity 6000.0.64f1 + URP、字型、真正的 Canvas 與 RT | **尚未執行**：本次環境沒有 Unity Editor |
| macOS / iPad / sustained FPS | Player、實體 iPad、持續效能、Spotify 同步 | **尚未執行**；不得把 core CI 成功當成這些項目通過 |

初次 core 成功的可追溯紀錄：[run 36003861068](https://github.com/Harrison-Dev/VJPractice/actions/runs/36003861068)。
沒有提供或捏造本次 Unity 畫面截圖、影片、編譯成功或 FPS 測量。

## 不需要 Unity 的測試

```sh
dotnet run --project Tools/Motion.Tests/Motion.Tests.csproj --configuration Release
python3 Tools/validate_motion_sources.py
```

.NET 測試直接編譯 production `LyricMotionPlan.cs`，不是 Python 改寫的模型。JSON round trip 的 .NET 測試使用 System.Text.Json；Unity JsonUtility 另外在下方 Editor probe 測。
source guard 會反轉七個接入點並比對原 `VJStage.cs` Git blob `24aec6c48bc624026b9855c4ecfbc6cea884d782`，因此修改舊邏輯必須明確更新基準。

## Unity 可重複的 output probe

1. 開啟 `02_LyricStage`，Play，從「音源」選 **原創示範**。
2. 等到一個非空歌詞的中段，按 Space 暫停歌曲。不要在歌詞開始／結束淡入淡出邊界測試。
3. 執行 **VJ Practice → Kinetic → Verify output freeze and blackout (Play Mode)**。
4. 成功時 Console 有 `VJ_KINETIC_OUTPUT_PROBE_PASS`，並產生 `Verification/Kinetic-Probe.png`。

probe 會實際檢查 Unity JSON round trip、原生 Canvas 測試色塊出現在最終 RT、非空白畫面、凍結後原始 RGB hash 不變、黑幕為零，以及解除黑幕後凍結畫面仍保留。
它短暫顯示 magenta 測試色塊，完成或拋錯後恢復原本 Kinetic / Frozen / Blackout 狀態，不改音源、歌詞、播放位置或存檔。
先前黑色貼圖並不能證明 compositor 正常，所以 baseline 全黑會失敗。測試色塊不包含在最後的 PNG。
這仍然**不能證明日文排版好看、每個字形都正確或效能達標**，需要下面的人工檢查。

## 合併前人工清單

- [ ] Unity scripts 與 URP shader 沒有編譯／缺少字型材質錯誤，初次 Play 正常。
- [ ] 原創範例依次出現文字；F8 PNG 同時含文字、背景、粒子，不含操作台。
- [ ] 用冷靜／流行／故障與重抽檢查 Hero、Vertical、Diagonal、Grid、Typewriter；日文長短句、英文、翻譯不被不當裁掉。
- [ ] M/N 不改目前那句，下一個非空 cue 才生效；L 鎖定後重播、改風格、重抽不改該配置。
- [ ] F6 儲存、F7 載入、換曲、回原曲：配置正確且不串到另一首歌。
- [ ] 前後 seek、offset 調整、暫停／恢復、F 與 B 任意順序操作，不殘留上一句或黑幕。
- [ ] Q–Y、六個原有歌詞 pad 皆能回到原模式，背景 1–6 與播放操作不受影響。
- [ ] H / Esc 和視窗大小變更不改固定輸出解析度；退出 Play 不殘留 Canvas / RenderTexture / SRP callback。
- [ ] LAN 頁面重新整理後看得到下方文字 PV 區塊；實體 iPad Safari 指令、狀態與原本 fader 都正確。
- [ ] 1280×720 與 1920×1080、Density/Echo 高值、字型冷啟動，持續量測 FPS/P95/GC；記錄實測，不以舊版數字代替。

建議截圖使用原創示範，不把第三方完整歌詞或商業音樂上傳進 PR。

## 第一版明確不驗收的範圍

JIZURA 全效果相容、project JSON 互通、主副歌辨識、forced alignment、透明素材／影片／Syphon 輸出，以及六個舊 IMGUI 歌詞模式的合成 RT。
這些不是本次 core CI 或 output probe 的承諾。
