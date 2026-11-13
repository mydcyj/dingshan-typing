# 鼎山打字通

一款 Windows 英文打字练习工具，包含短文测速、四级词汇贪吃蛇和最多四人的局域网联机。

## 开始使用

从 [Releases](../../releases/latest) 下载 EXE，双击运行。单人练习可离线使用。

- **短文练习**支持换文、重练、实时 WPM 和正确率。大小写与标点可分别设置。
- **背景音乐**可读取本地音乐文件夹及其子文件夹，支持选歌、音量、顺序循环、随机播放和单曲循环。
- **贪吃蛇**用英文词转向。方向词按上、下、左、右排在十字形面板里，每个词附中文释义。开局前可选择普通、时空者、胆怯者或无畏者。输入 `start` 开局，无畏者用 `Tab` 切换冲锋输入框。
- **局域网联机**由房主建房，同一网络的玩家扫描后加入。扫描不到时，可输入房主的 IPv4 地址。所有人使用同一版本，房主输入 `start` 开局。

## 从源码构建

在安装了 .NET Framework 4.8 或更新版本的 64 位 Windows 上，于源码目录运行 PowerShell：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Build.ps1
```

构建后，`DingshanTyping.exe` 位于源码目录。程序、图标和词库一起打包进这个文件。

## 许可证

项目源码采用 [MIT](LICENSE)。四级词汇及中文释义来自 ECDICT 的四级词表，词库来源及授权见 [Cet4-LICENSE.txt](Cet4-LICENSE.txt)。
