# FloatTodo

Windows 桌面悬浮记事工具。基于 **WinUI 3 / .NET 10 / Windows App SDK 2.0.1 Stable** 构建，以 Unpackaged、x64、自包含形式发布。

## 记事

- 每张卡片是一篇文本或一份清单。预览不显示操作菜单；点击卡片原位编辑，输入区不再套一层边框。
- 编辑态底部提供记事菜单和完成按钮。清单空白项隐藏勾选框；Enter 拆分当前项，Shift+Enter 换行，空项 Enter 结束编辑。
- Ctrl+Enter 完成新建或编辑；文本记事中 Enter 继续用于换行。
- 已完成项置底，取消完成后恢复原顺序。整张记事可以在文本和清单间转换，保留 ID、标题和空行。
- 编辑保存防抖 450 ms，后台串行写入；失焦和关闭时刷新。保存失败保留草稿并显示错误。
- 数据继续使用 `%LOCALAPPDATA%\FloatTodo\todos.json` 和 `settings.json`。旧记事格式在首次保存升级前备份。

## 桌面行为

- 拖动窗口靠近工作区左右边缘吸附，采用 16 DIP 吸附、28 DIP 脱离阈值。收起后主窗口隐藏，仅保留 6 物理像素的独立 HWND 把手。
- 悬停意图延迟 60 ms；展开 220 ms，收起 200 ms。Microsoft.UI.Composition 将背景、边框、标题和内容作为完整面板滑入/滑出；透明 HWND 保持不动，动画完成后隐藏。
- 可中断动画从合成器当前值继续；状态机忽略旧的完成回调。关闭系统动画时直接切换。
- 托盘提供打开、隐藏、设置、退出。Ctrl+Alt+N 打开；第二实例唤醒已有实例。设置支持系统/浅色/深色、置顶、自动收起和当前用户开机启动。

## 开发与发布

需要 .NET 10 SDK、Windows SDK 和 WinUI C# 开发工具。

```powershell
dotnet build FloatTodo.slnx -c Release
dotnet test --project tests/FloatTodo.Core.Tests/FloatTodo.Core.Tests.csproj -c Release
./scripts/publish-winui.ps1 -Installer
```

安装器构建需要 Inno Setup 6。输出：

- `artifacts/publish/win-x64/`：自包含目录。
- `artifacts/portable/FloatTodo.exe`：自包含单文件，运行时解压依赖。
- `artifacts/installer/FloatTodo-Setup-0.2.0.exe`：默认安装到 Program Files，可用 `/CURRENTUSER` 做当前用户安装；支持静默安装、覆盖安装和卸载。用户数据不纳入卸载。

调试时可隔离数据：

```powershell
./artifacts/portable/FloatTodo.exe --data-dir ./artifacts/winui-test-data
```

## 迁移状态

实现及本机验证记录见 [docs/migration/status.md](docs/migration/status.md)。混合 DPI、其他刷新率、系统高对比度、中文输入法和干净 Windows 环境仍须验收；本机窗口启动与行为测试不能代替这些检查。


### 背景与置顶
- 背景设置只保留 Mica / Mica Alt / Acrylic / 纯色，使用系统默认材质参数。材质使用面板内 SystemBackdropElement，随整块面板移动。
- 标题栏图钉切换正常模式置顶；眼睛按钮进入独立桌面预览。预览默认不透明度 75%（40%–100%），强制置顶、暂停自动隐藏、内容区域点击穿透。右下恢复按钮、托盘“打开”和 Ctrl+Alt+N 均可恢复正常模式。
- 记事标题可以隐藏/恢复，内容及开关独立保存。



桌面预览不保存到下次启动；正常模式的置顶/自动隐藏设置不被预览覆盖。编辑卡片通过操作区高度变化实现连续形变，正文不做淡化或缩放。

