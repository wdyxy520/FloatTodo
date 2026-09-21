# FloatTodo v0.3.4

FloatTodo 是专为 Windows 打造的轻量桌面悬浮记事与待办备忘工具。本次 v0.3.4 带来 Windows 原生级极速跟手窗口拖拽体验与 QQ 式实时边缘磁力吸附。

FloatTodo is a lightweight floating sticky notes and todo memo utility for Windows. v0.3.4 introduces native-grade zero-latency window dragging and QQ-style real-time magnetic edge snapping.

---

### ✨ 新特性与优化 / Features & Improvements

- 🚀 **Windows 原生级 0 延迟拖拽 / Native Zero-Latency Window Dragging**
  - 接入 Windows 原生模态拖动循环（Native Move Loop），通过 `InputNonClientPointerSource` 将标题栏区域注册为系统原生 Caption。
  - 彻底移除在 XAML UI 线程中由鼠标事件驱动 `SetWindowPos` 的陈旧机制，位移直接由 OS 内核及 GPU DWM 合成层硬件加速处理，彻底告别拖拽时的橡皮筋迟滞与延迟感，达到极致跟手。
  - 标题栏功能按钮与拖拽区域精准隔离，按钮点击响应依然灵敏独立。

- 🧲 **QQ 级 `WM_MOVING` 原生实时边缘磁吸 / Real-Time Magnetic Edge Snapping**
  - 在 Win32 消息管线中实时拦截 `WM_MOVING (0x0216)` 消息，在内存中直接原地改写候选坐标指针 `RECT*`，零 CPU/GPU 额外开销。
  - 靠近屏幕左/右边缘 16 DIP 时瞬时干脆吸附；贴边后保留 28 DIP 迟滞脱离阈值（Hysteresis），杜绝在屏幕边缘微抖。
  - 拖拽松手（`WM_EXITSIZEMOVE`）瞬间无缝落位并启动自动贴边隐藏（AutoHide）计时器。

- 📐 **边框 Resize 缩放手柄与边缘吸附对齐 / Border Resize Snapping**
  - 四周边框调整大小时，若拉伸边界靠近屏幕边缘（≤16 DIP），自动磁吸对齐到屏幕边界。
  - 调整大小结束后若停靠在边缘，自动保持贴合对齐（`AlignToEdge`），并保证窗口最小安全尺寸（宽≥280，高≥220）。

---

### 📦 下载与安装 / Download & Install

- **安装版 (Installer)**: `FloatTodo-Setup-0.3.4.exe` — 推荐日常使用，集成快捷方式与开机自启选项。
- **便携版 (Portable)**: `FloatTodo-Portable-0.3.4.zip` — 解压即用，无须安装。

*运行环境要求：Windows 10 (1809+) 或 Windows 11 (x64)*
