# 圖片來源

2026-10-17 透過官方 Unity CLI 與 `com.unity.pipeline`，在 Unity **6000.4.7f1** 的常駐 GUI Editor 取得。遊戲來源版本為 `e4332a242aa13e15a52b444eb6eaff0488861930`；沒有生成或重畫遊戲場景。

| 檔案 | 來源 |
| --- | --- |
| `game-view.png` | 房間 1 參考路線終點的 Pipeline `capture_game_view --source screen`，960×540 |
| `room-1-start.png` 至 `room-8-start.png` | 八個正式 Demo 場景的開始畫面，320×180 原生像素 |
| `room-1-end.png` 至 `room-8-end.png` | `Verification/DemoLiveInput.cs` 以虛擬鍵盤完成各關路線後的畫面 |
| `sandbox.png`、`room-2-sandbox.png`、`gear-demo.png`、`demo-1-1.png` | 對應獨立場景在 Play 中的 `PhaseSandbox.Frame`，已確認 frame count 與 controller time 前進 |
| `room-selector.png`、`map-editor.png` | 公開遊戲的實際 Chromium 介面，1280×900 viewport 中擷取對話框；已操作畫地形、尖刺、匯出與載入自訂地圖 |
| `character-ready.png`、`character-spent.png`、`character-tired.png` | 正式 `PhasePixels` renderer 產生的 24×28 角色狀態示意：衝刺存量 1、衝刺存量 0、空中耐力 10。這三張是狀態示意，不是鍵盤遊玩截圖 |

八關路線皆完成，死亡數為 0；依序使用 `DemoLiveInput.Advance` 驗證前七次房間切換及檢查點。新版重取圖後的 Pipeline Console 檢查沒有 error。公開介面於 2026-10-17 擷取，`maps.js` 的 SHA-256 與本版本相同（`19712096937dd0f4168076f580840a8f5b07be15dc34fd24df513515810ac3e0`）；已實測格線繪圖、JSON 匯出與自訂地圖試玩。這不代表公開 WebGL 的所有檔案皆與該 commit 一致。

## 重新取圖

依 [`Documentation/Development.md`](../../Development.md) 先確認正確的 resident Editor，設定背景 autotick 與 Play Unfocused。開啟 `PhaseDemo1` 並進入 Play，逐關執行 `Verification/DemoLiveInput.cs` 的 `DemoLiveInput.Run`、再以 `DemoLiveInput.Advance` 前進；腳本會把各關開始與完成的原始 PNG 寫到 `TestResults/DemoRooms/`。核對結果後，複製到本目錄並使用上述檔名。

獨立場景可在 Play 中以 Pipeline `eval` 將 `PhaseSandbox.Frame.EncodeToPNG()` 寫入檔案；先確認 frame count 與 controller time 前進，再取圖。取圖後停止 Play、還原原有背景執行設定。角色示意則使用 `PhasePixels.Draw` 與上表狀態，從 320×180 畫面的原生座標 `(152,92)` 擷取 24×28 區域；角色位置為 `(160,100)`、空中、相位紅、flash=0，衝刺/耐力以外均使用預設值。

來源 SHA-256：

```text
Assets/Phase/DemoRooms.json
b7cedf181ff5a7d1d66dc42d4c38d0bdc6e40aa666cd7fc3e708f68ae979b742
Assets/Phase/Runtime/PhaseController.cs
d063838b5ef3e8181ccc5c3391df9cafaaf16184d250f0f054e95be3ee4faf06
Assets/Phase/Runtime/PhasePixels.cs
91ef9b56a8906241be81e90241f903f9293d9a0d96c6c5f0ad3569857a4e5d7e
```
