# FloatTodo v0.3.3

FloatTodo 是专为 Windows 打造的轻量桌面悬浮记事与待办备忘工具。本次 v0.3.3 重点优化窗口拖拽移动跟手度、卡片内部动画流畅度以及列表增删刷新体验。

FloatTodo is a lightweight floating sticky notes and todo memo utility for Windows. v0.3.3 focuses on ultra-responsive window dragging, smooth card expanding transitions, and stable list item insertions.

---

### ✨ 新特性与优化 / Features & Improvements

- 🚀 **窗口拖拽跟手度与性能重构 / Ultra-Responsive Window Dragging**
  - 废除渲染帧轮询机制，改由 Windows 原始输入事件（`PointerMoved`）实时驱动，彻底消除 30~50ms 滞后延迟，达到硬件级绝对贴手。
  - 拖拽过程全面缓存窗口尺寸与坐标，实现零跨进程 `GetWindowRect` 查询开销。
  - 边缘缩放手柄宽度微调至 8 DIP，并在缩放期间锁定光标，彻底杜绝快速缩放时光标闪烁跳变回普通鼠标箭头的问题。
  - 优化贴靠状态下的 Resize 校准，调整大小时保持贴边绝对对齐。

- 🌊 **卡片展开动画丝滑升级 / Silky Smooth Card Transitions**
  - 卡片操作栏展开动画对齐 Fluent Design 减速曲线，时长统一调整为 240ms，与外层相邻卡片的隐式位移动画严丝合缝完全同步。
  - 引入 Opacity 透明度渐入淡出，消除内容被硬截断的生硬视觉感。
  - 移除清单内部与 Compositor 冲突的原生重排动画，彻底消除勾选完成项下沉或拖拽时的抽搐与残影闪烁。

- ⚡ **新增记事列表刷新与状态稳定 / Stable List Item Insertions**
  - 移除编辑态动态修改 ListView 拖拽属性的做法，转由事件层精准取消，彻底解决新增记事时偶发整屏列表闪烁重构的问题。
  - 优化空卡片创建逻辑，连续点击新增时直接复用或原位转换，杜绝“先删后插”导致的列表剧烈晃动。

---

### 📦 下载与安装 / Download & Install

- **安装版 (Installer)**: `FloatTodo-Setup-0.3.3.exe` — 推荐日常使用，集成快捷方式与开机自启选项。
- **便携版 (Portable)**: `FloatTodo-Portable-0.3.3.zip` — 解压即用，无须安装。

*运行环境要求：Windows 10 (1809+) 或 Windows 11 (x64)*
