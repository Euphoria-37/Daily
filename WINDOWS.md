# Daily

一个简洁的 Windows 桌面待办便笺。圆角窗口、暖白底色，支持图钉置顶。

## 使用

从 [Releases](https://github.com/march7th-26/Daily/releases/latest) 下载 `Daily.exe`。

双击 `Daily.exe`，无需安装，也无需管理员权限。适用于已启用 .NET Framework 4.8 的 Windows 10 / 11。

- 输入事项，按 **Enter** 或点击 **+** 添加。
- 主界面每日语句按系统日期从[一言](https://hitokoto.cn/)随机获取文学、哲学类短句，当天保持不变；跨天会自动更新。断网保留已有语句，30 分钟后重试。仅请求语句，不发送待办内容。
- 点击左侧圆圈完成事项，再点一次恢复未完成。
- 鼠标移入条目（或键盘聚焦条目）时显示归档和 **×** 操作；点击 **×** 删除事项。
- 点击归档图标，将事项移入左上角“历史记录”。历史记录按归档时间倒序展示，保留创建、完成和归档时间，支持恢复或删除。旧事项缺失的时间显示“未记录”。
- 双击待办文字直接编辑，Enter 保存覆盖，Esc 取消；点击其他位置也会保存。空白内容不覆盖原事项。键盘聚焦圆圈后可按 F2 编辑。
- 新事项记录创建时间；勾选完成记录完成时间，取消完成则清空完成时间。编辑、归档和恢复不改变创建时间。
- 左上角图钉切换置顶，绿色背景表示已置顶，首次启动默认开启。旁边的刷新图标用于检查更新。
- 拖动顶部空白处移动窗口，拖动任意边缘或四角调整大小（最小 340 × 400）。
- 不显示任务栏按钮，仅显示托盘图标。双击托盘图标显示窗口，右键菜单可显示窗口或退出。
- 托盘右键选择 **检查更新…**，有新版本时可选择下载并自动重启。只检查 GitHub 正式发布版；不更新到旧版本。
- 拖动标题栏到屏幕左侧、右侧或顶部后松手，窗口会自动收纳成暖白色圆角细条；鼠标移入展开，停留在细条或面板范围内保持展开，移走约 0.2 秒后收起。展开后拖离边缘即可取消收纳。支持当前显示器的左右和顶部边缘。
- 右上角 **×** 退出，**−** 收起到托盘；按 **Ctrl+N** 聚焦输入框。

内容自动保存在 `%LOCALAPPDATA%\DailyTodo\tasks.xml`，上一份保存为 `tasks.xml.bak`。关闭程序或跨天均保留事项，重启恢复窗口大小、位置和置顶状态。程序仅保存到本机。

更新前会保存当前编辑并备份数据为 `tasks.xml.update.bak`（仅保留一份）。更新只替换程序，原待办、完成状态和设置继续使用；数据保存或备份失败会取消更新。下载会校验大小、SHA-256 和程序版本，文件替换或启动失败会恢复原程序。程序目录需要可写权限。

程序保留一份 `Daily.exe.previous` 作为旧程序备份。更新下载缓存位于 `%LOCALAPPDATA%\DailyTodo\Updates`，超过一天的缓存会在启动时清理。更新请求不包含待办内容，也不向 GitHub 同步个人数据。

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

发布新版本时同步修改 `Version.cs` 中的版本号，GitHub Release 标签使用相同的 `v主版本.次版本.修订号`，上传构建出的 `Daily.exe`。更新接口使用 GitHub 返回的附件 SHA-256 校验值。

置顶行为参见 [Microsoft WPF Window.Topmost 文档](https://learn.microsoft.com/en-us/dotnet/api/system.windows.window.topmost?view=netframework-4.8.1)。


