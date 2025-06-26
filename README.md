# EZSubtitleEditor

這是一款由 [JustTryIt](https://www.youtube.com/@joetryit) 開發的字幕編輯工具，內建強大的 AI 語音轉文字功能，讓您能輕鬆、快速地為影片製作字幕。

## ✨ 主要功能

*   直觀的字幕編輯介面
*   整合 OpenAI Whisper 模型，提供高準確度的語音辨識
*   支援多種影音格式
*   時間軸預覽與調整

## 🚀 開始使用

### 系統需求

1.  **Windows 作業系統**
2.  **.NET 8 Desktop Runtime**: 您可以從 [微軟官方網站](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) 下載並安裝。
3.  **Python 環境**: 建議使用 Python 3.9 或 3.10。
4.  **CUDA (NVIDIA GPU 使用者)**: 若您擁有 NVIDIA 顯示卡並希望使用 GPU 加速，請務必安裝 [NVIDIA CUDA Toolkit](https://developer.nvidia.com/cuda-toolkit-archive)。

### 安裝依賴套件

本專案的 AI 功能依賴一些 Python 套件。請透過 pip 安裝它們：

```bash
pip install openai-whisper torch numba
```

### 建置與執行

1.  **Clone 倉庫**
    ```bash
    git clone https://github.com/lkjo/EZSubtitleEditor.git
    cd EZSubtitleEditor
    ```

2.  **使用 Visual Studio**
    *   使用 Visual Studio 2022 開啟 `EZSubtitleEditor.sln` 檔案。
    *   在頂端工具列選擇 `Release` 和 `x64` 設定。
    *   按下 `F5` 或點擊「開始」按鈕來建置並執行專案。

3.  **使用 .NET CLI**
    ```bash
    # 建置專案
    dotnet build -c Release

    # 執行程式 (路徑可能因版本有些許不同)
    ./SubtitleEditor.UI/bin/Release/net8.0-windows/win-x64/EZSubtitleEditor.exe
    ```

## 📝 注意事項

*   首次使用 AI 功能時，程式會自動從網路下載所需的 Whisper 模型檔案，請耐心等候。
*   本專案的設定檔為 `SubtitleEditor.UI/appsettings.json`，您可以在其中調整部分應用程式行為。

---
