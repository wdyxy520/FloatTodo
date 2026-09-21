# WPF 迁移基线

记录日期：2026-09-20。以当前工作区（包含尚未提交的 Memo 重构）为准，不以过期 README 中 Today/Scratch 的描述为准。

## 已有功能

- Core：MemoService 读写 `%LOCALAPPDATA%/FloatTodo/todos.json`，Version=2、Memos 数组；兼容历史 Todos/Notes 文档及待办数组，首次保存新格式前备份。
- 统一记事列表，文本与清单整条转换，保留记事 ID、标题、创建日期。
- 清单完成项按完成状态置底，恢复后按 Order 回到逻辑顺序。
- 默认卡片预览不显示操作栏；进入编辑显示无内框编辑器、更多和完成。
- 空清单行隐藏勾选框，展示添加提示；重复 Enter 不追加空行。
- 450ms 防抖自动保存，失焦及退出时刷新；失败保留草稿并显示重试入口。
- 浅色、深色、跟随系统；Mica/Mica Alt/Acrylic/Solid；Topmost 设置。
- 托盘打开、设置、退出；左右贴边、拖动、hover 展开、自动收起。
- DockGeometry 有纯物理像素几何计算和多显示器安全检查。

## 计划新增或必须重做

- 当前未发现全局热键、单实例和开机启动实现；不能把这些记为已迁移功能。
- 当前窗口动画仍在 Rendering 中调用 SetWindowPos；WinUI 不复用此实现。
- 当前业务操作仍在 TodayView code-behind；WinUI 改为 Toolkit ViewModels 和命令。
- 多屏、DPI 和 60/120/144Hz 需要真实环境复验，已有单测不是实机通过证据。

## 参考图

- [浅色预览](reference/compact-Light.png)
- [深色预览](reference/compact-Dark.png)
- [浅色编辑](reference/editing-Light.png)
- [深色编辑](reference/editing-Dark.png)

截图使用测试记事，不包含用户实际数据。

## 数据与迁移边界

保留 WPF 和全部现有未提交修改；不改用户数据，不在 WPF 与 WinUI 中同时启动数据写入者。
先验证 WinUI 空壳的 folder / portable 发布，再迁移业务与窗口服务。
未完成计划中的发布、安装卸载、DPI 和 smoke 验收前，不删除 WPF。
