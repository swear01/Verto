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

月夜的染色採石場：骨白兜帽旅人 Wick、連成整片的石材與低亮度夜景，忠實沿用使用者示範版。
來源：[PHASE demo](https://phase-three-color.swear01.chatgpt.site/)。固定 renderer SHA-256：`b7178e0f6c0a613a10a92d276a2b6f9c37db75420a0ec342654504278a982c1d`；來源記錄見 `Verification/ArtReference.json`、`Documentation/ThirdPartyNotices.md`。

## Colors

以上為主要色彩 tokens；完整石材底色、邊緣色與混色規則以 `Assets/Phase/Runtime/PhasePixels.cs` 為準。眼睛使用目前相位色；圍巾獨立使用琥珀／石板灰，避免混淆。

## Layout

原生畫面 320×180，地形格 8×8，Point 取樣。Web 保持固定原生 render target（`matchWebGLToCanvasSize: false`），以 pixelated CSS 放大至視窗容納的最大 16:9 區域；不同螢幕比例只保留必要留黑。原生 Editor Game View 維持整數倍置中放大。保留現有兩個房間、碰撞物理與左右畫面邊緣出口。

## Elevation & Depth

同色石格依世界座標連接成錯縫磚，只畫露出的亮頂、側邊與暗底。夜空、月亮、固定星點、遠山與殘柱一次繪製後保持靜止，亮度低於角色和地形。

## Components

- Wick：骨白斗篷、暗臉、深色靴；眼睛表示相位。可衝刺時圍巾為琥珀色、長 5 格；用盡後為石板灰、長 3 格並下垂。姿勢沿用既有狀態與時間；攀爬低體力以 1 像素抖動表示。
- 石材：未啟用時為淡底與虛線邊；與角色重疊而延後實體化時提高亮度並改用實線。方正邊緣與決定性的磚縫保持清晰。
- 移動平台：黃銅鉚釘箱，保留既有路徑與站點。衝刺殘影使用相位色剪影與玩家目前朝向。
- 房間切換：沿行進方向水平滑動（0.18 秒，cubic ease-out）；位移取整數像素，兩個房間不混色。離開房間的快照不畫玩家，抵達房間保留一個 Wick；抵達動畫結束前凍結物理與輸入。
- Web 工具列：右上方並排放置全螢幕與鍵位設定圖示，按鈕至少 44×44 CSS 像素；使用可存取名稱、可見鍵盤焦點與全螢幕狀態。全螢幕套用整個頁面，設定仍以原生 dialog 開啟並保留逐項還原；切換被拒時顯示另開分頁重試的提示。文字留在 Web 設定與必要錯誤訊息中，遊戲 canvas 保持零說明文字。
- 地形快取：以相位與 blocked 集合區分；方塊清單身分或數量改變時失效。原地修改既有方塊內容不會使快取失效。

## Do's and Don'ts

- 保留遊戲零說明文字；不移入來源的尖刺、傳送門、HUD 或關卡戰役。
- 美術由執行時程式繪製；沒有生成或下載產品圖像、字型。來源 PNG 僅供驗證，不作為 Player 素材。
