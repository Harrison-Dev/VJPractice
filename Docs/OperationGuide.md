# Nightflight VJ Practice · 0.4

Unity 版本 6000.0.64f1。開啟專案後選 VJ Practice → Open Playable Stage，再按 Play；可直接在 Editor 的 Game 視窗測試。

## 播放 Humanoid

1. macOS 聲音輸出選「VJ Monitor + BlackHole」。它同時輸出到 MacBook 喇叭與 BlackHole 2ch；AirPods 連線可能使系統自動切換輸出。
2. Spotify 裝置選「這部電腦」，播放 ZUTOMAYO 的 Humanoid。
3. Nightflight 按「連接 Spotify」。它讀取曲名、播放時間和暫停狀態，選擇 BlackHole 音源並搜尋歌詞。首次使用若 macOS 詢問控制 Spotify 或音訊權限，需允許才能同步。

歌曲由 Spotify 播放，專案不附商業音檔。也能從音源頁匯入自己的音檔，或使用內附原創節奏示範。

歌詞頁顯示來源與行數。Humanoid 使用 LRCLIB 3672778 的 55 行日文歌詞；搜尋與網路服務可能失敗，可手動選擇候選或匯入 LRC。逐字動畫由行時間估算，不等於逐字人聲辨識。可用歌詞偏移、逐行打點和儲存對齊修正。

## 鍵盤

| 按鍵 | 操作 |
|---|---|
| 1–6 | 漂浮、斜光、回聲、隧道、矩陣、流體 |
| A | JIZURA 自動編排 |
| Q W E R T Y | 手選 JIZURA：重擊、斜切、環繞、字流、拼貼、海報；歌詞時間仍同步 |
| 7 / 8 / 9 | 文字疊加／與原舞台混合／JIZURA 全景 |
| K / N / L | 歌詞輸出開關／重抽本句／鎖定本句 |
| F6 / F7 / F8 | 儲存配置／載入配置／擷取最終輸出 PNG |
| ↑ ↓ | 選擇強度／密度／流速／殘影 |
| ← → | 微調；Shift 加大步距 |
| Space / Home | 播放暫停／回到開頭 |
| H / Esc | 乾淨輸出／回到控制台 |
| F / B | 凍結／黑幕 |
| V | Tap BPM |
| [ / ] | 歌詞提前／延後 0.1 秒 |
| Enter | 替選定歌詞行打時間點 |
| P / F9 | 擷取畫面／記錄診斷 |

輸入搜尋文字時不會觸發演出快捷鍵；Esc 離開文字輸入。獨立版擷取畫面存於 Application Support 的 AV Sketchbook/Nightflight VJ Practice/Verification。

舞台特效 1–6 與 JIZURA look 可同時選用。A 讓來源 planner 自動編排；Q/W/E/R/T/Y 會覆寫當下歌詞的構圖與進退場，但不改歌曲 cut 時間。右側 BLEND 可調原舞台與 JIZURA 的比例；左側 VENUE 可調紅色演唱會背景的強度，設為 0 即關閉。背景合成在最終輸出貼圖中，因此 F8 與 Recorder 會錄到相同畫面。F6/F7 會一起儲存與載入 VENUE 強度。音訊分析使用 keijiro LASP；GPU 粒子使用適配 URP 的 keijiro LaspVfx 圖，不是全部仿作套件效果。

## Editor 錄影

在 Play Mode 播放 Unity 本地音樂後，用「VJ Practice → Recording → Start VJ + Local Music」開始，再用「Stop Recording」輸出 `Recordings/` 內的 MP4。錄到的是最終 VJ 畫面與 Unity 音訊，不含控制台。Spotify／外部音源未進 Unity mixer，這個功能無法把它們的聲音收進影片。Unity Recorder 錄音時可能把聲音送給 Recorder，而不從系統喇叭播出。

## 效能驗證

Apple M1 Pro、Unity 6000.0.64f1、獨立版全螢幕、Spotify Humanoid + BlackHole。六組各約 4 秒測試：平均 58.5–59.4 FPS，P95 17.0–17.6 ms。這是短測，不能保證所有負載都鎖定 60。Editor 相同六組測試 P95 約 23–30 ms（Editor 當時視窗較小，並非完全相同條件）。原始獨立版測量見 Performance-0.4.json。

已移出 Unity 主執行緒的 Spotify 輪詢、快取文字樣式、限制視覺 RenderTexture 尺寸，並修正 macOS Mono 網路介面初始化卡住問題。初次搜尋歌詞、載入字型或切换應用程式仍可能短暫停頓。
