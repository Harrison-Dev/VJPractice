# 文字 PV v1 — JIZURA-inspired kinetic lyrics

這是 Nightflight 的 **Unity 原生、逐句編排 MVP**，不是 JIZURA 網頁嵌入，也不是其 renderer 或 JSON 格式的相容移植。
既有 Spotify／音源、LyricDocument、歌詞解析與六種傳統歌詞模式保留。

## 開始使用

1. 使用專案既有 Unity **6000.0.64f1 / URP 17.0.4**，執行 `VJ Practice → Open Playable Stage`，按 Play。
2. 在右側「音源」選 **原創示範**。不需要 Spotify、BlackHole、外部歌曲或 AI API。
3. 切到「演出」。新版預設啟用「K 自動文字 PV」；若正在傳統模式，按 K 開啟。
4. 按 H 看乾淨輸出；按 Esc 回到操作台。按 F8 儲存**最終合成貼圖**，而不是包含操作台的螢幕截圖。

目前是每句歌詞一個演出 cut，含五種版面：Hero 巨大文字、Vertical 直排、Diagonal 斜排海報、Grid 重複字陣、Typewriter 打字。
進場（淡入／滑入／縮放）、退場（淡出／下落／縮小）、配色及輕微動作，由冷靜／流行／故障三種風格與 seed 決定。
色差使用兩份偏移的文字副本，不是對 JIZURA ghost pass 的逐像素重現。

## 操作

| 操作 | 鍵盤 | 說明 |
| --- | --- | --- |
| 文字 PV / 傳統模式切換 | K | 傳統六模式的實作不變 |
| 下一個風格 | M | 冷靜、流行、故障；下一個非空歌詞段落生效 |
| 下一句重抽 | N | 不打斷目前畫面；遇到已鎖定句子則不修改並消耗本次操作 |
| 鎖定 / 解鎖目前配置 | L | 鎖住版面、動作、配色風格與 variation，不鎖音樂播放 |
| 儲存本曲配置 | F6 | 儲存 seed、已產生的 cut、revision 與鎖定 |
| 載入本曲配置 | F7 | 不符合本曲文字或 schema 的檔案會被拒絕 |
| 最終貼圖 PNG | F8 | Editor 存 `Verification/`；Player 存 persistentDataPath/Verification |
| 六種傳統模式 | Q W E R T Y | 同時關閉文字 PV，保留舊字幕／斜切／環繞／打字／字雨／海報 |
| 背景、凍結、黑幕、全螢幕 | 1–6、F、B、H | 沿用操作概念；新模式的凍結與黑幕也作用於輸出貼圖 |

右側面板和 LAN 控制器都能操作風格、重抽、鎖定、儲存與載入。LAN 頁面新增區塊在原控制器下方；更新版本後重新整理網頁。
輸入搜尋或檔案路徑時，仍沿用原本的快捷鍵抑制規則。

### 四個旋鈕

- Energy：進場衝擊、輕微漂移幅度與低頻縮放。
- Density：重複字陣的兩排／三排密度，不改歌詞播放速度。
- Flow：持續文字動作的頻率；歌詞時鐘不跟著加速。
- Echo：色差副本的透明度與偏移幅度。

## 時間與重現

文字用 `Position - Document.offsetSeconds` 採樣，進退場是絕對時間函式，不累加 deltaTime。
有可靠且覆蓋整句的逐詞資料時，打字模式使用原有 word intervals；否則以行時間估計。沒有語音辨識或自動對嘴。

風格與重抽在**下一個非空且不同的歌詞 cue**生效；手動跳轉到另一句也算邊界。空白／間奏不消耗操作。
變更風格後，未鎖定的句子在下次拜訪時依新風格規劃；鎖定的句子保留原風格。
輸入相同文字、seed、revision 與風格會得到相同規劃；字型、Unity 版本、音訊輸入和背景 feedback 不是跨平台逐像素保證。
跳轉時會清除背景 feedback；色差文字沒有跨幀累積依賴。

配置放在 `Application.persistentDataPath/MotionPlan-<SHA256>.json`；key 由歌曲標題、歌手和全文內容產生，不包含音訊。
切曲只會自動讀取先前**明確儲存**的對應配置，不沿用上一首歌的鎖定。更改時間／offset 不改配置 key。
尚未到下一句生效的指令不存檔。先等它生效，再按 F6。

## 輸出契約與範圍

**文字 PV 開啟時**，`VJStage.KineticOutput` 是原 URP camera（含背景、粒子、原生文字 Canvas）經過凍結／黑幕選擇後的輸出。
`VJStage.Output` 此時也回傳同一張貼圖。預設 1280×720、16:9；Inspector 可改 KineticWidth / KineticHeight，上限 1920×1080。
操作台預覽使用 letterbox，不因 H 切換而改變貼圖大小。

**切回六種傳統模式時，Output 仍維持舊的背景 history 契約，並不包含 IMGUI 歌詞。**
需要完整影像時請使用新文字 PV 模式和 `KineticOutput`（關閉時為 null）；不要把 fallback 的舊 Output 當成全畫面。
本次刻意不重寫六種舊 renderer，避免把範圍擴成一次全面 UI 遷移。

新輸出使用三張貼圖：live camera、held/presented frame、blackout。B 的優先權高於 F；黑幕解除後，凍結中的 held frame 不會被黑色覆蓋。
沒有新增 Syphon、NDI、Spout、錄影編碼器或透明影片輸出；F8 是 PNG 靜態驗收出口。

## 維護與限制

`Runtime/Motion/LyricMotionPlan.cs` 是可獨立測試的規劃核心；`KineticLyricRenderer` 管有上限的 32 個原生 Text 物件；
`StageCompositor` 管 URP 事件與貼圖生命週期；`VJStage.Motion.cs` 負責時鐘、歌詞、存檔與控制橋接。
沒有新增外部套件或字型；沿用已在專案中的 UGUI 和 Noto CJK 字型。

長直排與長字陣會降級為 Hero。預覽每句最多 160 個 text elements，超過會顯示省略號與提示，原歌詞資料不會改動。
`StringInfo` 避免直接截斷 surrogate pair 與 combining sequence，但不宣稱完整 emoji 字型覆蓋。
第一版沒有 JIZURA project JSON 匯入、逐詞切鏡、筆畫破碎、切片遮罩、完整 expression pack 或自動辨識主副歌。
動態字型首次出現、字陣與高 Echo 的成本需要實機測量，不能沿用舊版短測試的 60 FPS 數字。

## 參考與授權界線

概念參考 [JIZURA](https://github.com/852wa/JIZURA) 的 layout / enter / hold / exit 組合、風格限制、seed 和鎖定工作流。
本 PR 的 C# 與 UI 是獨立實作，**沒有複製其 JavaScript、expression packs、美術或字型**，不宣稱完整相容或作者背書。
既有字型與依賴的授權仍依專案原有 notices；工具可用不代表歌曲或歌詞可以再散布。

驗收與目前的證據界線見 [KineticValidation.md](KineticValidation.md)。
