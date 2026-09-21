# FloatTodo WinUI 3 迁移与重构实施计划

## 一、最终技术路线

FloatTodo 从现有 WPF 项目迁移至：

```text
WinUI 3
+ Windows App SDK Stable
+ Unpackaged
+ C# / .NET
+ CommunityToolkit.Mvvm
+ Microsoft.UI.Composition
+ Win32 Interop
+ Self-contained deployment
+ Inno Setup
```

发布提供两种形式：

```text
便携 / 测试版
    ↓
FloatTodo.exe
Single-file Self-contained

正式安装版
    ↓
Self-contained Folder Publish
    ↓
Inno Setup
    ↓
FloatTodo-Setup-x.x.x.exe
```

正式版本**不强求内部 Single-file**。

安装器本身已经是一个 `Setup.exe`，正常 folder publish 对 WinUI Runtime、资源、升级和故障排查更加稳妥。

当前不采用：

```text
MSIX
WiX
Raw DirectComposition
WPF + Composition 混合长期架构
```

---

# 二、核心设计原则

整个项目按五层职责拆分：

```text
┌──────────────────────┐
│      FloatTodo.Core  │
│ Models / Storage     │
└──────────┬───────────┘
           │
           ▼
┌──────────────────────┐
│      ViewModels      │
│ CommunityToolkit.Mvvm│
└──────────┬───────────┘
           │
           ▼
┌──────────────────────┐
│      WinUI Views     │
│ UI / VisualState     │
└──────────────────────┘

       + Platform Services

Windowing   → Win32 / AppWindow
Animation   → Microsoft.UI.Composition
Shell       → Tray / Hotkey / Startup
```

遵循：

> **Core 管数据，ViewModel 管状态，WinUI 管界面，Composition 管动画，Win32 管窗口与系统集成。**

避免重新出现当前 WPF `WindowDockService` 一类“大一统 Service”，同时管理：

```text
鼠标
Timer
窗口位置
动画
显示器
设置保存
Dock 状态
```

---

# 三、目标 Solution 结构

```text
FloatTodo/
│
├─ src/
│  │
│  ├─ FloatTodo.Core/
│  │  ├─ Models/
│  │  ├─ Services/
│  │  ├─ Storage/
│  │  └─ Settings/
│  │
│  ├─ FloatTodo.Wpf/
│  │  └─ 迁移期间保留，只作为功能/视觉参考
│  │
│  └─ FloatTodo.WinUI/
│     │
│     ├─ App.xaml
│     ├─ App.xaml.cs
│     │
│     ├─ Views/
│     │  ├─ MainWindow.xaml
│     │  └─ TodayView.xaml
│     │
│     ├─ Controls/
│     │  ├─ MemoCard.xaml
│     │  ├─ ChecklistItemView.xaml
│     │  └─ EdgeHandleView.xaml
│     │
│     ├─ ViewModels/
│     │  ├─ MainViewModel.cs
│     │  ├─ TodayViewModel.cs
│     │  ├─ MemoViewModel.cs
│     │  └─ ChecklistItemViewModel.cs
│     │
│     ├─ Services/
│     │  │
│     │  ├─ Windowing/
│     │  │  ├─ DockController.cs
│     │  │  ├─ WindowPlacementService.cs
│     │  │  ├─ MonitorService.cs
│     │  │  └─ EdgeHandleController.cs
│     │  │
│     │  ├─ Animation/
│     │  │  └─ PanelAnimationController.cs
│     │  │
│     │  ├─ Tray/
│     │  │  └─ TrayIconService.cs
│     │  │
│     │  ├─ Hotkeys/
│     │  │  └─ GlobalHotKeyService.cs
│     │  │
│     │  └─ Lifecycle/
│     │     ├─ SingleInstanceService.cs
│     │     └─ StartupService.cs
│     │
│     ├─ Interop/
│     │  └─ NativeMethods.cs
│     │
│     ├─ Themes/
│     ├─ Converters/
│     └─ Assets/
│
├─ tests/
│  ├─ FloatTodo.Core.Tests/
│  └─ FloatTodo.WinUI.Tests/
│
├─ installer/
│  └─ FloatTodo.iss
│
└─ artifacts/
   ├─ portable/
   └─ installer/
```

---

# 四、Windows App SDK 版本策略

只使用：

```text
Stable Channel
```

不使用：

```text
Preview
Experimental
```

微软当前稳定通道用于生产应用，并提供正式支持。

项目中明确固定 Windows App SDK 版本，不使用浮动版本。

原则：

```text
升级 SDK
↓
单独 PR
↓
Build + Smoke Test
↓
再合并
```

避免 UI 重构过程中 SDK 自动变化。

---

# 五、Phase 0：冻结 WPF 基线

开始迁移后，停止继续大规模优化 WPF UI。

WPF 只允许：

```text
严重 Bug Fix
数据兼容修复
迁移所需参考
```

不再投入：

```text
新的动画系统
新的 UI 架构
复杂的新功能
```

保留当前 WPF 截图以及核心行为作为 Reference。

需要记录基线功能：

```text
Memo 创建/编辑/删除
Checklist 创建
Checklist 勾选/恢复
完成项排序
标题
Memo / Checklist 转换
Preview / Editing
Context Menu
Light / Dark
Tray
Hotkey
TopMost
左右贴边
Hover 展开
自动收起
拖动
多显示器
DPI
启动/退出
数据保存
```

---

# 六、Phase 1：建立 WinUI 3 Unpackaged 项目

新建：

```text
FloatTodo.WinUI
```

而不是修改：

```text
FloatTodo.Wpf.csproj
```

引用：

```text
FloatTodo.Core
CommunityToolkit.Mvvm
```

基础配置：

```xml
<PropertyGroup>
    <OutputType>WinExe</OutputType>

    <WindowsPackageType>None</WindowsPackageType>

    <WindowsAppSDKSelfContained>true</WindowsAppSDKSelfContained>
</PropertyGroup>
```

目标平台第一阶段只支持：

```text
win-x64
```

暂不处理：

```text
x86
ARM64
```

### Phase 1 验收

必须实现：

```text
dotnet restore
dotnet build -c Release
dotnet publish -c Release -r win-x64
```

且最小 WinUI 窗口可以正常启动。

---

# 七、Phase 2：在迁 UI 前验证发布链

这是整个迁移中特别重要的一步。

不要等 UI 全部迁完以后才测试 Unpackaged。

## 2.1 Folder Self-contained

首先生成：

```text
publish/
├─ FloatTodo.exe
├─ Windows App SDK runtime
├─ WinUI dependencies
├─ *.dll
└─ Assets/
```

验证在没有开发环境的 Windows 11 上直接运行。

Windows App SDK 官方支持 Self-contained 模式，将运行时随应用一起部署。

---

## 2.2 Portable Single EXE

然后单独建立：

```text
PublishPortable.pubxml
```

目标：

```text
artifacts/portable/FloatTodo.exe
```

典型属性：

```xml
<WindowsPackageType>None</WindowsPackageType>

<WindowsAppSDKSelfContained>true</WindowsAppSDKSelfContained>

<SelfContained>true</SelfContained>

<PublishSingleFile>true</PublishSingleFile>

<IncludeAllContentForSelfExtract>true</IncludeAllContentForSelfExtract>

<EnableMsixTooling>true</EnableMsixTooling>
```

当前微软官方说明：

```text
Unpackaged
+
Windows App SDK Self-contained
+
.NET Self-contained
```

可使用 `PublishSingleFile`；运行时依赖会在首次运行提取到临时位置。

因此 Portable 版定义为：

> **单文件分发，而不是零解压执行。**

---

# 八、Phase 3：正式建立 MVVM

采用：

```text
CommunityToolkit.Mvvm
```

微软维护的 MVVM Toolkit 可直接用于 WinUI 3，并提供轻量的 Observable/Command 基础设施。

主要使用：

```text
ObservableObject
ObservableProperty
RelayCommand
AsyncRelayCommand
```

避免人为设计庞大的 MVVM Framework。

---

# 九、ViewModel 设计

## TodayViewModel

负责：

```text
Memos
CreateMemo
CreateChecklist
DeleteMemo
Save
Reload
Reorder
```

---

## MemoViewModel

负责：

```text
Id
Title
Text

MemoType

IsEditing
ShowTitle

Items

EnterEdit
FinishEdit

ConvertToMemo
ConvertToChecklist

AddItem
RemoveItem
Delete
```

---

## ChecklistItemViewModel

负责：

```text
Text
IsCompleted

ToggleCompleted
Delete
```

---

# 十、禁止 View 承担业务逻辑

当前 WPF 类似：

```csharp
OnCompletedClick()
{
    // 排序
    // 保存
    // 修改模型
}
```

迁移以后改为：

```text
UI Event
↓
Command
↓
ViewModel
↓
Model / Service
```

Code-behind 允许存在，但只处理纯 View 行为：

```text
Focus
TextBox selection
Pointer capture
VisualState
Composition Visual 获取
Window HWND 获取
```

不强求“零 Code-behind”。

---

# 十一、Phase 4：迁移核心 UI

建议迁移顺序：

```text
TodayView
↓
MemoCard
↓
Memo Preview
↓
Memo Editing
↓
Checklist
↓
Composer
↓
Context Menu
```

不要一开始追求动画。

先完成：

> **功能等价。**

---

# 十二、MemoCard 单独组件化

不要再让整个 TodayView 变成一个巨大 XAML。

建立：

```text
MemoCard.xaml
```

结构：

```text
MemoCard
│
├─ PreviewRoot
│
│  ├─ Title
│
│  ├─ Text Preview
│
│  └─ Checklist Preview
│
└─ EditorRoot
   ├─ TitleEditor
   ├─ TextEditor / ChecklistEditor
   └─ EditToolbar
```

---

# 十三、Preview / Editing 状态重新设计

不再延续 WPF：

```text
Border.Tag = True
DataTrigger
Setter
Visibility
```

定义明确状态：

```text
MemoViewMode
├─ Preview
└─ Editing
```

定义内容类型：

```text
MemoType
├─ Text
└─ Checklist
```

ViewModel 管：

```text
IsEditing
MemoType
```

WinUI `VisualStateManager` 管纯视觉变化。

---

# 十四、卡片编辑体验

继续采用已经确定的 Google Keep 式方向。

## Preview

默认只显示：

```text
Title
正文
Checklist
```

不显示：

```text
更多按钮
完成按钮
添加一项
编辑框
```

点击 Card：

```text
Preview
↓
Editing
```

---

## Editing

正文直接在卡片纸面编辑：

```text
无独立 TextBox 边框
透明 Background
```

避免：

```text
Card
└─ Border
   └─ TextBox Border
      └─ Focus Border
```

Preview 与 Editing：

```text
Padding
FontSize
LineHeight
Text position
```

尽可能一致。

达到：

> 点击文字后像“原地出现光标”。

---

# 十五、Checklist 编辑

空项目采用：

```text
添加一项…
```

只有实际输入以后才显示：

```text
☐ 项目文本
```

避免同时存在：

```text
空 Checkbox
+
“添加一项”
```

造成视觉误导。

Enter：

```text
当前项有内容
↓
创建下一项
```

空白项：

```text
Enter
↓
结束连续添加
```

---

# 十六、Phase 5：主题与 Fluent UI

优先使用 WinUI 自带：

```text
ThemeResource
System Brushes
CornerRadius
Typography
```

减少当前 WPF 自己维护的：

```text
TextPrimaryBrush
TextSecondaryBrush
CardBackgroundBrush
CardBorderBrush
...
```

自定义资源只保留 FloatTodo 真正具有产品特征的部分。

支持：

```text
Light
Dark
High Contrast
Windows Accent
```

---

# 十七、Mica

主窗口优先使用 Windows App SDK 原生 SystemBackdrop / Mica。

原则：

```text
Window
└─ Mica Backdrop

Content
├─ cards
└─ controls
```

避免：

```text
Window Mica
+
Root 半透明
+
Card 半透明
+
Editor 半透明
```

多层 alpha stacking。

---

# 十八、Phase 6：重构窗口架构

这一阶段是迁 WinUI 3 的核心收益之一。

采用：

```text
MainWindow
+
EdgeHandleWindow
```

而不是继续让 MainWindow：

```text
320px
↓
SetWindowPos 到屏幕外
↓
只露 6px
```

---

# 十九、EdgeHandleWindow

收起状态：

```text
                    屏幕边缘
                       │
                       │██
                       │██
                       │██
```

实际：

```text
EdgeHandleWindow Width ≈ 4~8 px
```

MainWindow：

```text
Hidden
```

EdgeHandle 负责：

```text
Hover
Click
Dock interaction entry
```

它是真实窄 HWND，不存在 320px 透明区域挡鼠标。

---

# 二十、MainWindow

展开时：

```text
Show MainWindow
```

直接把 MainWindow HWND 设置到：

```text
最终展开位置
```

不再逐帧移动。

即：

```text
SetWindowPos()
```

只负责：

```text
最终位置
拖动
Dock
Monitor relocation
```

而不是动画。

---

# 二十一、DockController 状态机

建立明确状态：

```text
Detached
DockedVisible
DockedHidden
Showing
Hiding
Dragging
```

所有输入：

```text
Mouse
Pointer
Hover
Drag
Hotkey
Tray
```

统一转为：

```text
DesiredState
```

然后：

```text
DockController
↓
WindowController
+
AnimationController
```

禁止：

```text
MouseEnter → ShowWindow()
MouseLeave → HideWindow()
Timer → SetWindowPos()
```

各自直接操纵窗口。

---

# 二十二、Hover Intent

展开不是立即触发。

初始：

```text
50~70ms
```

目的只用于过滤：

```text
鼠标偶然扫过屏幕边缘
```

Hover delay 与动画 duration 完全独立。

---

# 二十三、Phase 7：Composition 动画

使用：

```text
Microsoft.UI.Composition
```

而不是：

```text
Raw DirectComposition
```

WinUI 元素可直接参与 Composition Visual Tree，因此无需 WPF 那套额外的视觉桥接。

---

# 二十四、展开动画

MainWindow HWND：

```text
直接位于最终坐标
```

Root Visual 初始：

右侧 Dock：

```text
Translation.X = +32 ~ +48px
Opacity = 0
```

左侧 Dock：

```text
Translation.X = -32 ~ -48px
Opacity = 0
```

动画：

```text
Translation.X → 0
Opacity → 1
```

必要时：

```text
Clip → Full
```

不要动画：

```text
Width
Margin
GridLength
MainWindow X position
```

---

# 二十五、为什么不滑完整 320px

不要：

```text
+320 → 0
```

推荐：

```text
+40 → 0
+
Opacity
+
Clip
```

用户视觉上仍然会理解为：

> “从右侧展开。”

但实际运动距离短很多：

```text
更快
更轻
更接近 Windows 11 Shell
```

---

# 二十六、初始动画参数

第一版从：

```text
Show:
170~190ms

Translation:
36~48px → 0

Opacity:
0 → 1
```

开始。

Hide：

```text
120~150ms

Translation:
0 → 24~32px

Opacity:
1 → 0
```

曲线先使用 Fluent 风格快速进入、柔和落地的 easing。

最终参数以实际 144Hz 屏幕观察结果为准。

---

# 二十七、动画必须可中断

必须支持：

```text
Showing
↓
鼠标离开
Hiding
↓
鼠标回来
Showing
```

不能：

```text
Cancel
↓
跳到起点
↓
重新播放
```

目标：

```text
position continuous
```

尽量实现：

```text
velocity continuous
```

如果普通 KeyFrame Animation 反向效果不自然，再评估：

```text
SpringNaturalMotionAnimation
```

---

# 二十八、双 HWND 视觉交接

## Show

```text
EdgeHandle 可见
↓
MainWindow 在最终位置 Show
↓
Main Composition 第一帧开始
↓
隐藏 EdgeHandle
```

## Hide

```text
先恢复 EdgeHandle
↓
Main Composition 收起
↓
MainWindow Hide
```

允许边缘：

```text
1~2 px overlap
```

防止出现：

```text
空白缝
闪帧
```

---

# 二十九、Raw DirectComposition 暂不使用

明确禁止第一版引入：

```text
IDCompositionDevice
IDCompositionVisual
DXGI
D3D11
Direct2D
DirectWrite 自绘 UI
```

只有出现可重复证明的 Composition 性能瓶颈以后再评估。

对 FloatTodo：

```text
Microsoft.UI.Composition
```

已经处于正确抽象层级。

---

# 三十、Phase 8：Win32 系统能力迁移

WinUI 3 不负责替代 Win32。

继续保留 Win32 实现：

```text
HWND
Monitor
WorkArea
WM_NCHITTEST
RegisterHotKey
Shell_NotifyIcon
Window placement
```

但是重新分 Service。

---

# 三十一、TrayIconService

继续：

```text
Shell_NotifyIcon
```

功能：

```text
Open FloatTodo
Hide
Settings
Exit
```

Tray 与 ViewModel 解耦。

Tray Action：

```text
Tray
↓
Application command
↓
Window/Dock controller
```

---

# 三十二、GlobalHotKeyService

负责：

```text
RegisterHotKey
WM_HOTKEY
UnregisterHotKey
```

ViewModel 不直接处理 HWND message。

---

# 三十三、SingleInstanceService

确保：

```text
FloatTodo.exe
FloatTodo.exe
```

不会产生：

```text
两个 Tray
两个数据库写入者
两个 EdgeHandle
```

第二个实例：

```text
检测已有实例
↓
通知已有实例
↓
Show / Activate
↓
退出
```

---

# 三十四、StartupService

负责：

```text
开机启动
```

由于采用 Unpackaged，不依赖 MSIX StartupTask。

使用传统 Windows 桌面方案实现，并由设置项控制。

---

# 三十五、Phase 9：多显示器和 DPI

明确验证：

```text
100%
125%
150%
175%
200%
```

以及：

```text
主屏 100%
副屏 150%
```

测试：

```text
Dock
Drag between monitors
Hidden handle
Show
Hide
Window position restore
```

坐标系统必须明确：

```text
physical pixels
vs
DIPs
```

不能在 DockController 内混用。

---

# 三十六、数据兼容

`FloatTodo.Core` 优先直接复用。

WinUI 版本继续读取 WPF 版本现有数据。

尽量保持：

```text
Storage path
JSON schema
Memo ID
Settings key
```

全部兼容。

若必须修改：

```text
StorageVersion
```

并建立：

```text
v1
↓
v2 migration
```

禁止让迁移版本首次启动后清空原有 Todo。

---

# 三十七、Phase 10：测试策略

本次明确：

> **少写 UI Test。**

测试投入放在真正稳定、收益高的区域。

---

## Core Tests

继续覆盖：

```text
Serialization
Storage
Settings
Memo rules
Migration
```

---

## ViewModel Tests

重点覆盖：

```text
Create Memo
Create Checklist
Delete
Edit
Convert
Checklist add/remove
Complete
Restore
Completed reorder
Save
```

这些测试：

```text
快
稳定
无需 HWND
无需 WinUI
```

---

## 不做大量 UI 自动化

不测试：

```text
Margin 是否 8px
圆角是否 7px
ContextMenu 偏移 2px
某个 Border Brush
动画第 23 帧的位置
```

避免 UI 测试成为维护负担。

---

# 三十八、UI Smoke Test

Release 前人工执行：

```text
□ 首次启动
□ 创建 Memo
□ 编辑 Memo
□ 删除 Memo
□ Checklist
□ 添加项目
□ 勾选项目
□ 恢复项目
□ Context Menu
□ Preview/Edit
□ Light
□ Dark
□ Tray
□ Global Hotkey
□ Single Instance
□ Dock Left
□ Dock Right
□ Hover Show
□ Auto Hide
□ 快速 Show/Hide/Show
□ 拖动
□ Multi Monitor
□ DPI
□ Restart
□ 读取旧 WPF 数据
□ Portable Release
□ Installer Release
```

---

# 三十九、性能验收

迁移完成后的动画代码中，不允许：

```text
CompositionTarget.Rendering
↓
每帧 SetWindowPos
```

也不允许：

```text
每帧 MonitorFromWindow
每帧保存 Settings
每帧 Layout
```

MainWindow 滑出动画阶段：

```text
应用线程
↓
提交 Animation
↓
Composition system
↓
DWM
```

作为主要执行路径。

---

# 四十、性能测试重点

重点不是平均 FPS，而是：

```text
首帧延迟
帧间隔一致性
Animation interruption
窗口交接闪烁
Mica 初始化
输入响应
```

重点测试：

```text
60 Hz
120 Hz
144 Hz
```

---

# 四十一、Phase 11：正式发布链

最终使用两个 Publish Profile。

## Portable

```text
PublishPortable.pubxml
```

输出：

```text
artifacts/portable/
└─ FloatTodo.exe
```

特点：

```text
Unpackaged
Self-contained
Single-file
```

适合：

```text
自己使用
测试
快速发送
便携运行
```

---

## Installer

```text
PublishInstaller.pubxml
```

输出：

```text
artifacts/publish/win-x64/
├─ FloatTodo.exe
├─ dll
├─ runtime
└─ assets
```

特点：

```text
Unpackaged
Self-contained
Folder publish
```

不启用：

```text
PublishSingleFile
```

然后交给：

```text
Inno Setup
```

---

# 四十二、Inno Setup

建立：

```text
installer/FloatTodo.iss
```

正式输出：

```text
FloatTodo-Setup-x.x.x.exe
```

安装：

```text
C:\Program Files\FloatTodo\
```

提供：

```text
开始菜单快捷方式
桌面快捷方式（可选）
开机启动（可选）
安装后启动
卸载
静默安装
升级覆盖
```

Windows：

```text
设置
→ 应用
→ 已安装的应用
→ FloatTodo
→ 卸载
```

正常显示。

---

# 四十三、升级策略

第一阶段使用简单 Major-style upgrade：

```text
1.0
↓
1.1
↓
1.2
```

新安装器：

```text
识别已有 FloatTodo
↓
关闭旧进程
↓
更新程序目录
↓
保留用户数据
↓
启动新版本
```

用户数据永远不要存：

```text
Program Files
```

而保留在用户 AppData 数据目录。

---

# 四十四、暂不做自动更新

第一轮迁移不同时设计：

```text
Updater
```

避免 Scope 扩张。

未来稳定以后可增加：

```text
UpdateService
```

例如：

```text
GitHub Releases
自有服务器
```

但与 WinUI 迁移分开实施。

---

# 四十五、代码签名

开发阶段：

```text
非阻塞
```

正式公开分发前：

```text
FloatTodo.exe
FloatTodo-Setup.exe
```

均进入代码签名流程。

---

# 四十六、Phase 12：WPF 删除条件

只有下面全部通过才删除：

```text
FloatTodo.Wpf
```

验收：

```text
□ 核心功能完整
□ 数据完全兼容
□ MVVM 结构完成
□ WinUI UI 可用
□ Tray 正常
□ Hotkey 正常
□ Single instance 正常
□ Dock 左右正常
□ EdgeHandle 正常
□ Auto-hide 正常
□ Composition 动画正常
□ Mica 正常
□ Light/Dark 正常
□ Multi-monitor 正常
□ DPI 正常
□ Portable 发布正常
□ Inno Setup 安装正常
□ 卸载正常
□ Release Smoke Test 通过
```

然后删除：

```text
FloatTodo.Wpf
WPF UI Tests
WPF ThemeHelper
旧动画实现
旧 WindowDockService
无用 WPF dependencies
```

---

# 四十七、建议实际执行顺序

整个迁移严格按：

```text
01 新建 FloatTodo.WinUI
        ↓
02 配置 Unpackaged
        ↓
03 配置 Windows App SDK Stable
        ↓
04 验证 self-contained folder publish
        ↓
05 验证 portable single EXE
        ↓
06 引用 FloatTodo.Core
        ↓
07 引入 CommunityToolkit.Mvvm
        ↓
08 建立 ViewModels
        ↓
09 TodayView
        ↓
10 MemoCard
        ↓
11 Checklist
        ↓
12 Preview / Editing
        ↓
13 Theme / Mica
        ↓
14 MainWindow / AppWindow / HWND
        ↓
15 Tray / Hotkey / Single instance
        ↓
16 EdgeHandleWindow
        ↓
17 DockController 状态机
        ↓
18 Microsoft.UI.Composition 动画
        ↓
19 Multi-monitor / DPI
        ↓
20 Inno Setup
        ↓
21 Release Smoke Test
        ↓
22 删除 WPF
```

---

# 四十八、明确禁止边迁移边扩需求

第一轮不要同时加入：

```text
Raw DirectComposition
Direct2D 自绘
数据库重构
云同步
账号系统
插件系统
自动更新
ARM64
MSIX
WiX
大量新 Todo 功能
```

迁移目标只有：

> **WPF 功能等价 + 更合理的 MVVM + 更清晰的窗口架构 + 更好的 Windows 11 原生视觉与动画。**

---

# 四十九、第一阶段完成定义

第一阶段不是：

> “WinUI 窗口能打开。”

而是：

```text
FloatTodo.WinUI
✓ 能编译
✓ 能读取原有数据
✓ Memo/Checklist 完整
✓ Tray/Hotkey 完整
✓ EdgeHandle 完整
✓ Dock 完整
✓ Composition 动画完整
✓ Light/Dark 完整
✓ 多屏/DPI 可用
✓ Portable EXE 可发布
✓ Setup.exe 可安装
✓ WPF 可删除
```

这才算迁移完成。

---

# 五十、最终架构

```text
                         FloatTodo.Core
                               │
                    Models / Storage / Settings
                               │
                               ▼
                     CommunityToolkit.Mvvm
                               │
                         ViewModels
                               │
                               ▼
                         WinUI 3 Views
                               │
          ┌────────────────────┼────────────────────┐
          │                    │                    │
          ▼                    ▼                    ▼

      Windowing            Composition            Shell

   AppWindow/HWND        Visual Animation      Tray / Hotkey
   DockController        Translation           Single Instance
   EdgeHandleWindow      Opacity               Startup
   Monitor/DPI           Clip

          │                    │                    │
          └────────────────────┼────────────────────┘
                               ▼
                              DWM
```

核心原则最终保持：

```text
业务状态 ≠ UI 状态 ≠ 窗口状态 ≠ 动画状态
```

四者分别管理，通过明确接口连接。

这样以后再增加：

```text
搜索
标签
置顶
归档
提醒
设置页
自动更新
```

都不需要重新推倒窗口和动画架构。