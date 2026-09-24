# JIZURA Unity 原生移植

PR #1 的新版文字 PV 使用 C# 專案模型、規劃器、uGUI 渲染器與 Unity Editor Window。它讀寫原版 `.jizura.json`，不啟動瀏覽器或 WebView；即時畫面送進原本的 URP `KineticOutput` RenderTexture。

## 在 Unity Editor 使用

1. 使用 Unity 6000.0.64f1 開啟 `Assets/VJ/Stage/02_LyricStage.unity`，按 Play。舞台會載入 `Assets/VJ/Stage/Resources/JizuraDemo.json`，展示原生多 cut 文字 PV。
2. 從選單 `VJ Practice → JIZURA Studio` 開啟原生編輯器。可匯入原版 `.jizura.json`，調整歌詞、時間、風格／特效、逐行配置與鎖定，並勾選即時同步到 Play Mode 舞台。
3. 在舞台右側的演出頁，可切換文字 PV、開啟編輯器、匯入 `.jizura.json`。M 切換三種演出風格、N 重抽當前行、L 鎖定、F6／F7 存取 `CurrentJizura.jizura.json`、F8 擷取最終 RenderTexture。F 凍結、B 黑幕及 H 乾淨輸出沿用。
4. 原版 JSON 的逐行版面／效果 ID 若尚未移植，專案仍會保留該設定；編輯器與舞台狀態會列出未支援項。預覽是 Unity 的重寫版本，不保證與瀏覽器像素一致。

## 可驗證的範圍

`JizuraProject` 讀寫 schema v1 並保留未知欄位；`JizuraPlanner` 從原版歌詞語法生成多 cut。`JizuraNativeRenderer` 支援 17 個核心 layout ID、12 種核心配色及 9 個核心裝飾 ID，以原生 Canvas 繪製文字與圖形；動畫由歌曲絕對時間取樣，跳轉與暫停不依賴上一幀。`JizuraStudio` 可編輯、存檔、重抽、鎖定，並在 Editor Play Mode 即時預覽。

在 Editor 選單執行 `VJ Practice → JIZURA → Verify project and planner`，再於 Play Mode 執行 `Verify native output (Play Mode)`。前者檢查匯入／另存、多 cut、語法、鎖定與 deterministic reroll；後者檢查原生 Canvas、非空 RenderTexture 及兩個不同 cut，截圖存於 `Verification/Jizura-native-*.png`。`VJ Practice → Kinetic → Verify output freeze and blackout (Play Mode)` 檢查凍結與黑幕。此流程沒有建立 Player。

## 尚未移植

原版 707 個完整技巧與 expression packs、瀏覽器 `Intl.Segmenter` 精確分詞、多字型／語言自動切換、逐像素 shader／Canvas 特效、非 16:9 輸出，以及影片、PNG 連番與 AE 輸出。目前介面可保存部分原版設定，但不會假裝已經渲染它們。詳見 [JizuraIntegrationGap.md](JizuraIntegrationGap.md)。

移植參照 [JIZURA](https://github.com/852wa/JIZURA) 的 MIT 程式碼；授權見 `Assets/VJ/Stage/ThirdParty/JIZURA-LICENSE.txt`。
