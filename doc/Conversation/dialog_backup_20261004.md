# 對話進度備份 - 2026-10-04

- **專案名稱**：WebAPICore (雲端現代化進銷存與倉儲管理系統 ERP)
- **備份日期**：2026-10-04
- **當前雲端部署連結 (Render & Scalar)**：
  - 🚀 **Render 雲端服務主網址**：[https://webapicore.onrender.com/](https://webapicore.onrender.com/)
  - 📚 **Scalar 現代化 API 互動文件**：[https://webapicore.onrender.com/scalar/v1](https://webapicore.onrender.com/scalar/v1)
  - 🐙 **GitHub 專案原始碼**：[https://github.com/zhangs1124/WebAPICore](https://github.com/zhangs1124/WebAPICore)

---

## 📌 今日核心討論與重要進度整理

### 1. 身分驗證機制深度解析與架構選型
- **JWT 認證危機 (The JWT Dilemma)**：
  - **無法即時註銷 (No Immediate Revocation)**：JWT 無狀態特性導致發出後即使使用者密碼更改或遭停權，伺服器無法即刻廢止其權杖。
  - **前端儲存風險**：`localStorage` 容易遭受 XSS 腳本竊取。
  - **重放攻擊 (Replay Attack)**：Bearer Token 認票不認人，封包若遭竊取可在有效時間內重送請求。
- **動態驗證解決方案**：
  - **雙 Token 動態旋轉 (Access Token + Refresh Token Rotation)**：
    - 短效 Access Token（5~15 分鐘）呼叫業務 API。
    - Refresh Token 存放於 `HttpOnly` Cookie，換發時舊票立即作廢銷毀，若發現舊票重用則觸發盜用告警並全面登出。
  - **霍克驗證 (Hawk Authentication)**：
    - 每次請求以本地密鑰、時間戳記 (Timestamp)、Nonce、URL、Method 計算 HMAC-SHA256 動態簽章，密鑰不傳出，防重放攻擊。
  - **DPoP (RFC 9449)**：現代 OAuth 標準，前端以私鑰對每一次 HTTP 請求進行動態簽章，Token 綁定客戶端私鑰。
- **Session-based vs. Token-based 選型結論**：
  - **內部 ERP / 企業管理後台 (現狀)**：推薦 `Session + Cookie`，最高即時控制權（主管一鍵停權立即踢下線），防止 XSS。
  - **前後端分離 / 跨域 / App / Open API**：採用 `短效 Access Token + 動態 Refresh Token 旋轉` 或 `DPoP`。
  - **Scalar / Swagger 支援度**：加上身分驗證後，Scalar / Swagger 文件不會消失，反而會啟用右上角「Authorize 🔒」功能，便於貼上 Token 或自動隨帶 Cookie 進行測試。

### 2. 系統說明文件與 GitHub 成果交付
- **全面升級 [`README.md`](../../README.md)**：
  - 納入 **RBAC 4 大角色矩陣**（Admin, Manager, Purchaser, Warehouse）、預設帳密與倉管進價機密遮蔽規則。
  - 說明 **兩層級手風琴選單**、全寬大表與右側專注填表抽屜 (Drawer Panel)。
  - 繪製 **採購審批生命週期圖 (Mermaid)** 與系統架構圖。
  - 標記 **49 項單元與整合測試 100% 通過** 與 Playwright 端對端測試驗證。
- **Git 備份與同步**：
  - Commit：`dfb5139` (`docs: update comprehensive ERP README with RBAC, purchase workflow, and drawers`)
  - 已成功推送到 GitHub 遠端儲存庫 `origin/main`。

---

## 🎯 測試帳號速查表

| 角色 | 帳號 | 密碼 | 權限說明 |
| :--- | :--- | :--- | :--- |
| 👑 Admin | `admin` | `Admin888!` | 系統全域最高權限 |
| 👔 Manager | `manager` | `Manager888!` | 營運主管，可審核採購單、維護供應商與查看成本 |
| 👤 Purchaser | `purchaser` | `Buyer888!` | 採購專員，開立採購單、受部門列級隔離 (RLS) 保護 |
| 👷 Warehouse | `warehouse` | `Worker888!` | 倉管員，入庫驗收與盤點，進價成本自動遮蔽為 `***` |
