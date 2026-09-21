# FloatTodo v0.3.5

FloatTodo 是专为 Windows 打造的轻量桌面悬浮记事与待办备忘工具。本次 v0.3.5 带来 Todo 卡片编辑动效深度优化与操作栏同步展开体验，并进一步巩固了原生拖拽与数据持久化稳定性。

FloatTodo is a lightweight floating sticky notes and todo memo utility for Windows. v0.3.5 brings synchronized action toolbar animations for todo cards, seamless checklist-to-text transitions, and reinforced persistence stability.

---

### ✨ 新特性与优化 / Features & Improvements

- 🎯 **Todo 卡片编辑操作栏同步展开与动效平滑化 / Synchronized Editor Actions Animation**
  - 解耦“＋ 添加一项”按钮与外层操作区的双重状态依赖，按钮依据清单形态直接就绪，外层展开动画在第一帧即可精准测量完整展开高度（~70px）。
  - 彻底消除因展开高度少算（仅 ~38px）导致底部操作栏在 240ms 几何裁切内被遮挡、并在动画结束后突变弹出的顿挫感。添加按钮与底部常用功能按钮（完成、转换、排序、删除）实现原子级同步浮现。
  - 修复卡片在清单与纯文本之间动态切换时，添加按钮无法实时同步显示/隐藏的问题。

- 🚀 **Windows 原生级 0 延迟拖拽与边缘磁吸 / Native Dragging & Magnetic Snap**
  - 接入 Windows 原生模态拖动循环（Native Move Loop），位移直接由 OS 内核及 GPU DWM 合成层硬件加速处理，告别橡皮筋迟滞，跟手度大幅提升。
  - 在 Win32 消息管线中实时拦截 `WM_MOVING (0x0216)` 消息，屏幕边缘 16 DIP 干脆吸附与 28 DIP 脱离迟滞，杜绝边缘抖动。

- 💾 **记事保存与持久化调度协调 / Reliable Memo Persistence Coordination**
  - 完善 `MemoSaveCoordinator` 批处理与防抖机制，保障高频编辑与多卡片切换时的安全写入。

---

### 📦 下载与安装 / Download & Install

- **安装版 (Installer)**: `FloatTodo-Setup-0.3.5.exe` — 推荐日常使用，集成快捷方式与开机自启选项。
- **便携版 (Portable)**: `FloatTodo-Portable-0.3.5.zip` — 解压即用，无须安装。

*运行环境要求：Windows 10 (1809+) 或 Windows 11 (x64)*
