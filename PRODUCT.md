# GameClock Overlay

Windows 11 x64 個人桌面時鐘，C# 15、.NET 11、Avalonia 12.1.6。v0.3.0 提供桌面時鐘與選用的 RTSS Shared Memory 遊戲內時鐘。

主要來源位於 src，必要回歸測試位於 tests。桌面顯示、拖曳、穿透、快捷鍵、系統匣及設定保存沿用已驗證的 Avalonia 實作；RTSS 是外部依賴，不隨包提供、不修改全域設定、不新增遊戲注入或自製 Hook。

原 WPF v0.2.0 由 Git 歷史及 v0.2.0 Tag 保留，不同步維護兩套 GUI。新版使用獨立設定路徑，匯入舊版時只讀取舊資料。

版本、限制、建置及授權以 README、CHANGELOG、RELEASE-NOTES-0.3.0、THIRD-PARTY-NOTICES 為準。使用者的既有遊戲人工驗收與本輪隔離自動化結果分開記錄；多螢幕與混合 DPI 尚未實機驗證。
