# WinUI 迁移进度与验收

## 已实施

- WPF 基线及用户实施计划保存于 `baseline.md`、`plan.md`、`reference/`；WPF 主项目及测试已删除。
- WinUI CLI 模板安装；新建 `src/FloatTodo.WinUI`，Windows App SDK 已升级至 2.0.1、Toolkit 8.4.0、BuildTools 10.0.26100.6584。Unpackaged / x64 / .NET 与 Windows App SDK 均自包含。
- 独立 `FloatTodo.ViewModels`：Main、Today、Memo、ChecklistItem；显式 MemoType/MemoViewMode。业务测试不加载 WinUI。
- 原位无边框卡片编辑、预览隐藏菜单、清单回车拆分/空项结束、完成置底及取消恢复顺序、全文/清单转换、空白新卡片完成后移除。
- 450 ms 防抖、后台串行写入、UI 线程快照、失焦/关闭刷新、写入错误保留草稿。复用原 MemoService 路径、格式与迁移备份逻辑。
- 系统/浅色/深色主题；原生资源和编辑控件。采用透明宿主 + SystemBackdropElement 材质层，Mica / Mica Alt / Acrylic / 纯色可选，采用内置背景的默认参数，材质随面板移动。
- WindowPlacementService、MonitorService、DockController、独立原生 EdgeHandleController HWND；实际宽度 6 px，不再留一个透明的大窗口接鼠标。
- Microsoft.UI.Composition 整面板宽度位移、220 ms 展开、200 ms 收起，包含背景、边框、标题和内容。StartingValue 从合成器接续，过期完成回调失效；当前实现保持位置连续，未声称严格速度连续。
- 60 ms hover 意图、450 ms 离开延迟、编辑/设置/拖动时抑制自动收起、系统关闭动画时直接切换。
- Shell_NotifyIcon 托盘（含 Explorer 重启重新添加）、Ctrl+Alt+N、命名内核对象单实例唤醒、HKCU Run 开机启动。
- 发布脚本、目录与单文件 profile、Inno Setup 脚本；默认 Program Files 安装，可用 `/CURRENTUSER` 安装。管理员安装下开机启动由应用内设置开启，避免安装器写到管理员的 HKCU。

## 已验证（2026-09-20）

- `dotnet build FloatTodo.slnx -c Release --no-restore`：零警告、零错误。
- Core/ViewModel：57 项通过；WPF 回归：1 项通过。
- 最小模板先完成 Debug 真窗口启动、目录 publish 真窗口启动、single EXE 真窗口启动，然后接入 UI。
- 最终 WinUI 的目录版、便携版和安装目录 exe 均显示响应正常的真实窗口。
- 独立测试数据的 UI Automation smoke：新建清单、输入、完成、自动保存落盘、第二实例退出并唤醒、12 轮移入移出、主 HWND/把手交接、正常关闭，均通过。日志 `artifacts/winui-smoke-result.txt`。
- 浅色预览与深色编辑做过实际截图检查；预览没有菜单，编辑没有内层输入框边框。截图在 `reference/`。
- 本机当前显示环境为 125% 缩放；未将本机验证外推到其他 DPI 或刷新率。
- Inno Setup 6.7.3 成功生成 Setup.exe；隔离目录当前用户静默安装、安装后启动、覆盖安装、卸载均通过；独立记事文件 SHA-256 在安装升级卸载前后保持不变。日志 `artifacts/installer-smoke-result.txt`。

## 待完成验收


- 管理员 Program Files 安装与升级，以及没有开发环境的干净 Windows 机器。
- 100/150/175/200% 与混合 DPI、左右负坐标屏、移除显示器、侧边任务栏。
- 60/120/144 Hz 实际帧时间采样；当前行为测试不代表帧率已达标。
- 高对比度、中文输入法组合输入、长清单压力、系统关机时草稿刷新。
- 人工验收托盘菜单、热键冲突、开机启动和卸载清理启动项。
- 已完成 WPF 主工程与测试工程删除。

## 记录说明

本次使用 `--data-dir artifacts/winui-test-data` 进行 UI 和发布测试，没有用迁移后的调试程序写入用户默认记事目录。默认生产路径保持兼容。测试中已修复原生把手缺少 SS_NOTIFY 导致 hover 不到达、窗口关闭回调重入、多个子类窗口钩子 ID 冲突等问题。

初次 NuGet 大包下载多次中断，使用官方 CDN 分段下载并校验 SHA-512 后还原成功；恢复缓存位于忽略的 artifacts 下，不影响正常发布。仓库 NuGet.config 限定官方源，不修改机器全局源。


## 本次窗口行为修正
- 启动前及激活时隐藏任务栏/切换器入口，移除原生标题与边框，改用随主题的紧凑标题区。
- 共用 WPF DockGeometry：16 DIP 吸附、28 DIP 脱离；自定义标题拖动采用原生鼠标捕获。
- 透明 HWND 保持固定，完整 XAML 面板执行合成位移动画，结束后隐藏 HWND。
- 用户要求自行测试，已停止交互自动化；最新拖动、左右收起及快速反向尚待手工验收，不据此声称性能验收通过。


## 交互修正（待用户手工验收）
- 标题和四边/四角采用 XAML 捕获与物理坐标拖动；清除系统非客户区输入拦截，不进入 Windows Snap 移动缩放循环。
- 手动打开后等待鼠标进入再启用自动收起。
- 点击卡片外或窗口失活结束编辑；预览与编辑共用 TextBox 字体、宽度和换行设置。
- 顶部新建入口原位展开编辑，结束后收回，草稿纳入原有自动保存。
- 遵照用户要求不运行测试或交互自动化，只编译和打包。

## 拖动与材质、动效更新（待手工验收）
- 用 CompositionTarget.Rendering 替代 16ms 拖动计时器；缓存显示器信息，忽略未变化的位置，保留 WPF 吸附阈值。
- Windows App SDK 2.0.1 原生 SystemBackdropElement 提供面板内系统材质；透明宿主保持原方案，未增加 Community Toolkit 依赖。
- 背景不透明度与离开后整体不透明度分别配置；默认离开后为 45%，鼠标进入、拖动或设置打开时恢复。
- 标题栏置顶开关；记事标题显示状态持久化，隐藏不清空内容。
- 编辑器/预览和新建入口使用合成淡入淡出、位置过渡及装饰背景高度过渡；文字不做缩放。
- 只编译打包，遵照用户要求不运行测试/交互自动化，未做帧率或性能验收。


## 背景简化与卡片闪烁修正（待手工验收）
- 删除背景不透明度、着色、明度 UI 及自定义材质控制器；改用 MicaBackdrop / DesktopAcrylicBackdrop。
- ActualThemeChanged 只更新纯色背景，不注销/重新添加 backdrop target；材质对象仅在材质类型变化时替换。
- 卡片预览/编辑共用同一组文本框，切换 IsReadOnly/交互权限。取消重叠淡入淡出、背景 Scale.Y 和新建草稿延迟清理；只保留布局位移过渡。
- 自动淡化仍保留为可选项；点击穿透仅提出产品建议，尚未实现。
- 已构建整个 Debug 解决方案，未运行测试或应用；主题异常与视觉效果仍需用户手工确认。


## 正常 / 桌面预览模式（待手工验收）
- 删除旧自动淡化设置和鼠标离开透明逻辑；预览不透明度默认 75%，范围 40%–100%。
- 独立 CompactMemoCard / DesktopPreviewView，禁编辑、标题栏和新增入口；模式状态不持久化。
- 预览强制置顶，暂停自动隐藏；退出恢复正常模式用户配置。采用 layered + transparent + noactivate 原生输入样式；独立 owned 恢复按钮窗口保持可点击，托盘和 Ctrl+Alt+N 是额外恢复入口。
- 进入前结束编辑、关闭 popup；退出先恢复输入。页面整体淡化过渡有代次校验，避免旧动画回调覆盖新模式。
- 编辑卡片操作区使用 170ms 高度布局动画，文字不缩放不交叉淡化；输入框显式 I-beam，父容器正文区域不覆盖鼠标光标。
- Debug 输出被正在运行的 FloatTodo 锁定；未停止用户进程，改为 Release 编译。遵照要求未做运行/交互测试，跨应用穿透和恢复仍需用户验收。


## 主题配置回调修正
- 根据 OnDefaultSystemBackdropConfigurationChanged 堆栈，补齐 TransparentWindowBackdrop 的重写：透明宿主不处理主题/激活配置，也不调用可能拒绝空 target 的基类实现。
- 保留面板内置 Mica/Acrylic 的独立主题响应，未吞掉全局异常。
- 编译验证；按用户要求不运行交互测试。

