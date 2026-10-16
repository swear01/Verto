---
name: PHASE
description: 忠實移植示範版的月夜染色採石場
colors:
  ink: "#0b0f16"
  cloak-light: "#eadfc5"
  cloak-shade: "#b3a386"
  cloak-hem: "#7d6e5c"
  face: "#1a1f2c"
  phase-red: "#ff8f91"
  phase-blue: "#79c9ff"
  phase-green: "#78d892"
  stone-neutral: "#a9b8c5"
  scarf-ready: "#ffb24d"
  scarf-ready-shade: "#d5732b"
  scarf-spent: "#5a6578"
  scarf-spent-shade: "#414a5a"
  brass-highlight: "#f2dc8a"
  night-top: "#0a1018"
  night-bottom: "#121e2b"
---
# PHASE 美術

## Overview

**Creative North Star: "月夜的染色採石場"**

骨白兜帽旅人 Wick、連成整片的石材與低亮度夜景，忠實沿用使用者示範版。螢幕比例改變時延伸可見世界，保留角色、石材與動作的原生像素尺度。
來源：[PHASE demo](https://phase-three-color.swear01.chatgpt.site/)。固定 renderer SHA-256：`b7178e0f6c0a613a10a92d276a2b6f9c37db75420a0ec342654504278a982c1d`；來源記錄見 `Verification/ArtReference.json`、`Documentation/ThirdPartyNotices.md`。

**Key Characteristics:**

- 180 原生像素高、依視窗比例調整寬度；8×8 地形格。
- Point 取樣與 pixelated 放大，Web 與原生 Editor 填滿視窗。
- 眼睛表示相位、圍巾表示衝刺。
- 夜景固定於世界座標，亮度低於主體。

## Colors

以上為主要色彩 tokens；完整石材底色、邊緣色與混色規則以 `Assets/Phase/Runtime/PhasePixels.cs` 為準。

**The 相位與衝刺分開表示 Rule.** 眼睛使用目前相位色；圍巾獨立使用琥珀／石板灰，避免混淆。

## Layout

原生畫面固定 180 像素高，寬度為 `round(180 × 視窗寬 / 視窗高)`（至少 1 像素）；地形格維持 8×8。Web 以相同尺寸調整 canvas render target（`matchWebGLToCanvasSize: false`），並以 pixelated CSS 填滿頁面；原生 Editor camera 使用完整 Game View。Point 取樣保持像素邊緣，不以固定 16:9 畫面留黑或額外背景補邊。

寬視野將房間寬度、整片地板碰撞與右側出口延伸至原生視寬，障礙物與移動平台座標不變。窄視野保留 320 像素世界寬度，camera 以整數像素跟隨玩家並限制於房間邊界。調整大小保留玩家與房間狀態；縮短右邊界時只將玩家限制回房間內，不觸發額外旅行。保留現有兩個房間、碰撞物理與左右邊緣出口。

## Elevation & Depth

**The 像素層次 Rule.** 同色石格依世界座標連接成錯縫磚，只畫露出的亮頂、側邊與暗底。夜空、月亮、固定星點、遠山與殘柱固定於世界座標，亮度低於角色和地形；寬視野依原有規則延伸夜空、星點、山丘、採石場殘柱與地板。背景快取隨視寬或視野原點改變而重建，鏡頭跟隨不使背景自行動畫。

## Components

- Wick：骨白斗篷、暗臉、深色靴；眼睛表示相位。可衝刺時圍巾為琥珀色、長 5 格；用盡後為石板灰、長 3 格並下垂。姿勢沿用既有狀態與時間；攀爬低體力以 1 像素抖動表示。
- 石材：未啟用時為淡底與虛線邊；與角色重疊而延後實體化時提高亮度並改用實線。方正邊緣與決定性的磚縫保持清晰。
- 移動平台：黃銅鉚釘箱，保留既有路徑與站點。衝刺殘影使用相位色剪影與玩家目前朝向。
- **The 房間滑動 Rule.** 沿行進方向水平滑動（0.18 秒，cubic ease-out）；位移取整數像素，兩個房間不混色。離開房間的快照不畫玩家，抵達房間保留一個 Wick；抵達動畫結束前凍結物理與輸入。滑動支援當前原生視寬；滑動途中改變視寬時完成既有房間交接並丟棄舊尺寸快照，直接繪製抵達房間。
- Web 工具列：右上方並排放置全螢幕與鍵位設定圖示，按鈕至少 44×44 CSS 像素；使用可存取名稱、可見鍵盤焦點與全螢幕狀態。全螢幕套用整個頁面，設定仍以原生 dialog 開啟並保留逐項還原；切換被拒時顯示另開分頁重試的提示。文字留在 Web 設定與必要錯誤訊息中，遊戲 canvas 保持零說明文字。
- 像素表面：視寬改變時調整 texture、buffer 與顯示 sprite；視寬或視野原點改變時清除背景與地形快取。地形快取仍以相位與 blocked 集合區分；方塊清單身分或數量改變時重建格線。任意原地修改既有方塊內容不會自行使格線快取失效。

## Do's and Don'ts

- 保留遊戲零說明文字。
- 美術由執行時程式繪製；沒有生成或下載產品圖像、字型。
- 不移入來源的尖刺、傳送門、HUD 或關卡戰役。
- 不把來源 PNG 作為 Player 素材；來源 PNG 僅供驗證。
