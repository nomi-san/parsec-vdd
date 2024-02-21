# vddswitcher

一个 just work 的用于 sunshine 串流的 vdd 程序。

主要功能：

- 开始串流时创建虚拟显示器，禁用实体显示器

- 自动根据 moonlight 客户端设置的分辨率设置虚拟显示器的分辨率(如果不支持你设备的分辨率，请根据3自定义，最多可配置5个自定义分辨率)

- 结束串流时关闭虚拟显示器

## 使用方法

### 1.安装[Virtual Display Driver parsec-vdd-v0.41](https://builds.parsec.app/vdd/parsec-vdd-0.41.0.0.exe)
### 2.将 [`vddswitcher.exe`](https://github.com/VergilGao/vddswitcher/releases/download/v0.2.1/vddswitcher.exe) 和 [`vddswitcherd.exe`](https://github.com/VergilGao/vddswitcher/releases/download/v0.2.1/vddswitcherd.exe) 放到你喜欢的位置
### 3.自定义分辨率
> 对照下面支持的分辨率，如果你需要的分辨率在列举中可以跳过此步骤
查看parsec官网，根据教程在注册表添加自己需要的分辨率 [VDD Advanced Configuration](https://support.parsec.app/hc/en-us/articles/4423615425293-VDD-Advanced-Configuration)
### 4.配置sunshine
打开 sunshine 转到 `Configuration - General` 

最下方 Do Command 栏填入 `cmd /C "vddswitcherd.exe的路径" -x %SUNSHINE_CLIENT_WIDTH% -y %SUNSHINE_CLIENT_HEIGHT% -r %SUNSHINE_CLIENT_FPS%`

例如:
`cmd /C "D:\game\utils\vddswitcher\vddswitcherd.exe"  -x %SUNSHINE_CLIENT_WIDTH% -y %SUNSHINE_CLIENT_HEIGHT% -r %SUNSHINE_CLIENT_FPS%`

Undo Command 栏填入 `cmd /C taskkill /f /t /im vddswitcherd.exe`

保存并提交

### 5.启动moonlight连接到sunshine，在屏幕设置中设置仅在屏幕2显示

> 以上即可实现连接到sunshine自动禁用实体显示器，断开sunshine关闭虚拟显示器。注意这里说的关闭是在moonlight退出，仅退出串流画面是不会关闭的

## 命令行单独使用
```cmd
vddswitcherd.exe -x 屏幕宽度 -y 屏幕高度 -r 刷新率

##例如开启一个1920x1080@60的虚拟显示器
vddswitcherd.exe -x 1920 -y 1080 -r 60
```

## 如何关闭虚拟显示器
1. 方法一：任务管理器找到vddswitcherd.exe关闭
2. 方法二：命令行输入`cmd /C taskkill /f /t /im vddswitcherd.exe`

## 支持的分辨率

- 1920x1080@60
- 1920x1080@240
- 1920x1080@144
- 1920x1080@30
- 1920x1080@24
- 3840x2160@240
- 3840x2160@144
- 3840x2160@60
- 3840x2160@30
- 3840x2160@24
- 3200x1800@240
- 3200x1800@144
- 3200x1800@60
- 3200x1800@30
- 3200x1800@24
- 2880x1620@240
- 2880x1620@144
- 2880x1620@60
- 2880x1620@30
- 2880x1620@24
- 2560x1600@240
- 2560x1600@144
- 2560x1600@60
- 2560x1600@30
- 2560x1600@24
- 2560x1440@240
- 2560x1440@144
- 2560x1440@60
- 2560x1440@30
- 2560x1440@24
- 2048x1152@240
- 2048x1152@144
- 2048x1152@60
- 1920x1200@240
- 1920x1200@144
- 1920x1200@60
- 1680x1050@240
- 1680x1050@144
- 1680x1050@60
- 1600x900@240
- 1600x900@144
- 1600x900@60
- 1440x900@240
- 1440x900@144
- 1440x900@60
- 1366x768@240
- 1366x768@144
- 1366x768@60
- 1280x800@240
- 1280x800@144
- 1280x800@60
- 1280x720@240
- 1280x720@144
- 1280x720@60
- 3840x1600@240
- 3840x1600@144
- 3840x1600@60
- 3840x1600@30
- 3840x1600@24
- 3840x1080@240
- 3840x1080@144
- 3840x1080@60
- 3840x1080@30
- 3840x1080@24
- 3440x1440@240
- 3440x1440@144
- 3440x1440@60
- 3440x1440@30
- 3440x1440@24
- 2560x1080@240
- 2560x1080@144
- 2560x1080@60
- 2560x1080@30
- 2560x1080@24
- 4096x2160@240
- 4096x2160@144
- 4096x2160@60
- 4096x2160@30
- 4096x2160@24
- 1600x1200@240
- 1600x1200@144
- 1600x1200@60
- 1600x1200@30
- 1600x1200@24
- 2880x1800@60
- 3000x2000@60
- 2736x1824@60
- 2256x1504@60
- 3240x2160@60
- 2496x1664@60
- 1800x1200@60
