# Daily

一个简洁的 Windows 桌面待办便笺。圆角窗口、暖白底色，支持图钉置顶。

## 使用

双击 `Daily.exe`，无需安装，也无需管理员权限。适用于已启用 .NET Framework 4.8 的 Windows 10 / 11。

- 输入事项，按 **Enter** 或点击 **+** 添加。
- 点击左侧圆圈完成事项，再点一次恢复未完成。
- 点击右侧 **×** 删除事项。
- 右上角图钉切换置顶，绿色背景表示已置顶，首次启动默认开启。
- 拖动顶部空白处移动窗口，拖动任意边缘或四角调整大小（最小 340 × 400）。
- 不显示任务栏按钮，仅显示托盘图标。双击托盘图标显示窗口，右键菜单可显示窗口或退出。
- 拖动标题栏到屏幕左侧、右侧或顶部后松手，窗口会自动收纳成暖白色圆角细条；鼠标移入展开，移走约 0.2 秒后收起。展开后拖离边缘即可取消收纳。支持当前显示器的左右和顶部边缘。
- 左上红色按钮退出，黄色按钮收起到托盘；按 **Ctrl+N** 聚焦输入框。

内容自动保存在 `%LOCALAPPDATA%\DailyTodo\tasks.xml`，上一份保存为 `tasks.xml.bak`。关闭程序或跨天均保留事项，重启恢复窗口大小、位置和置顶状态。程序仅保存到本机。

如提示数据读取失败，请先退出程序并复制备份上述目录，再检查 XML 文件，或使用 `tasks.xml.bak` 恢复为 `tasks.xml`。程序不会自动覆盖无法读取的数据。保存失败时，窗口底部会显示提示，退出时也会提醒。

置顶使用 Windows 标准窗口机制，可以悬浮在普通应用窗口之上；系统安全桌面、独占全屏以及其他置顶窗口不保证被覆盖。

## 从源码构建

使用 Windows PowerShell 和系统自带 .NET Framework C# 编译器，不需要 Node.js 或额外软件包。

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Test
.\bin\DailyTodo.exe
```

测试涵盖事项操作、中文和特殊字符持久化、损坏数据保护、实际窗口的添加/删除/完成操作、长文本换行、列表滚动，以及 Windows 原生置顶标志。

置顶行为参见 [Microsoft WPF Window.Topmost 文档](https://learn.microsoft.com/en-us/dotnet/api/system.windows.window.topmost?view=netframework-4.8.1)。


