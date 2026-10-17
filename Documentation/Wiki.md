# PHASE 中文 Wiki

Wiki 網頁在 [`wiki/index.html`](wiki/index.html)，介紹遊戲背景、操作、角色狀態、物件機制、八個房間、網頁地圖編輯器及 Unity 關卡製作。所有核心內容皆可離線閱讀，不需要安裝前端套件。

直接用瀏覽器開啟該檔案，或從 repo 根目錄執行：

```sh
python3 -m http.server 8775 --bind 127.0.0.1 --directory Documentation/wiki
```

然後開啟 [本機 Wiki](http://127.0.0.1:8775)。按 Ctrl+C 可停止伺服器。

內容與圖片對照主分支 `e4332a242aa13e15a52b444eb6eaff0488861930`。八關 Demo、獨立測試場景與同學製作的手工 `PhaseDemo1.1` 分別列出，後者附平台色序與編關參考；既有公開遊戲預覽是否同步，需另外驗證。

頁面可由任何靜態伺服器提供。這次加入 repo 與本機預覽，不變更遊戲的發布設定。

## 更新與檢查

修改 `wiki/index.html`、`wiki/style.css` 與需要的 `wiki/app.js`。機制或關卡更新後，同步更新文字、來源版本及受影響圖片；圖片來源見 [`wiki/images/README.md`](wiki/images/README.md)。

首次檢查時，從 repo 根目錄安裝既有的驗證依賴與 Chromium；之後只需執行最後一行：

```sh
cd Verification
npm ci
npx --no-install playwright install chromium --no-shell
cd ..
node Verification/WikiSmoke.cjs
```

此檢查涵蓋桌面與手機版面、圖片、頁內連結、折疊內容與停用 JavaScript 的閱讀情境；截圖輸出到 `TestResults/Wiki/`。它不驗證 Unity 遊戲或公開預覽是否更新。
