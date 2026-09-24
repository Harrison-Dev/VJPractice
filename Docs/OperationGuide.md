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
| Q W E R T Y | 字幕、斜切、環繞、打字、字雨、海報 |
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

視覺與歌詞分開選，可組合 36 種搭配。建議先試 4 + T（隧道／字雨）、2 + Y（斜光／海報）、6 + E（流體／環繞）。音訊分析使用 keijiro LASP；GPU 粒子使用適配 URP 的 keijiro LaspVfx 圖，不是全部仿作套件效果。

## 效能驗證

Apple M1 Pro、Unity 6000.0.64f1、獨立版全螢幕、Spotify Humanoid + BlackHole。六組各約 4 秒測試：平均 58.5–59.4 FPS，P95 17.0–17.6 ms。這是短測，不能保證所有負載都鎖定 60。Editor 相同六組測試 P95 約 23–30 ms（Editor 當時視窗較小，並非完全相同條件）。原始獨立版測量見 Performance-0.4.json。

已移出 Unity 主執行緒的 Spotify 輪詢、快取文字樣式、限制視覺 RenderTexture 尺寸，並修正 macOS Mono 網路介面初始化卡住問題。初次搜尋歌詞、載入字型或切换應用程式仍可能短暫停頓。
