# vddswitcher

一个 just work 的用于 sunshine 串流的 vdd 程序。

主要功能：

- 开始串流时创建虚拟显示器

- 自动根据 moonlight 客户端设置的分辨率设置虚拟显示器的分辨率(如果支持的话)

- 结束串流时关闭虚拟显示器

## 使用方法

确保你已经安装了以下驱动

- [parsec-vdd-v0.41](https://builds.parsec.app/vdd/parsec-vdd-0.41.0.0.exe) 

将 `vddswitcher.exe` 和 `vddswitcherd.exe` 放到你喜欢的位置

首次使用需要先配置虚拟桌面为主显示器：

右键开始图标 - 运行 - 输入 `cmd`
在打开的 cmd 窗口中输入 

`cmd /C "vddswitcherd.exe的路径" -x "你的主显示器像素宽度" -y "你的主显示器像素高度" -r "你的主显示器刷新率"`

此时将创建一个和你主显示器相同分辨率的虚拟显示器。

进入显示器配置页，选择创建出来的虚拟显示器，勾选设置为主显示器。(注意，此操作将会让你的桌面变得奇怪，但是这一切都是可以恢复的)

你发现你的鼠标找不到了，一些窗口也被移动到了虚拟显示器，此时不管你做什么都不如重启一下电脑来的方便。

重启电脑之后，你会发现虚拟显示器已经消失了。

打开 sunshine 转到 `Configuration - General` 

最下方 Do Command 栏填入 `cmd /C "vddswitcherd.exe的路径" -x %SUNSHINE_CLIENT_WIDTH% -y %SUNSHINE_CLIENT_HEIGHT% -r %SUNSHINE_CLIENT_FPS%`

例如:

`cmd /C "D:\game\utils\vddswitcher\vddswitcherd.exe"  -x %SUNSHINE_CLIENT_WIDTH% -y %SUNSHINE_CLIENT_HEIGHT% -r %SUNSHINE_CLIENT_FPS%`

Undo Command 栏填入 `cmd /C "vddswitcherd.exe的路径"`

例如:

`cmd /C "D:\game\utils\vddswitcher\vddswitcher.exe"`

保存并提交，等待重启后，使用 moonlight 进入桌面。

进入显示器配置页，将虚拟显示器外的其他显示器禁用来节约能源。
