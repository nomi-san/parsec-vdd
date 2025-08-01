<!-- tracking -->
<img src="https://i.imgur.com/dDUa6GH.png" width="0" height="0" />

<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="https://github.com/user-attachments/assets/74e7db71-6166-49ae-b6c5-7543b15c60eb">
    <img alt="Parsec Virtual Display Driver" src="https://github.com/user-attachments/assets/57202381-021c-428a-ae38-0fc4b2e0ee0c">
  </picture>
</p>

<p align="center">
  ✨ Perfect <strong>virtual display</strong> for game streaming
</p>

<p align="center">
  <a href="#">
    <img src="https://img.shields.io/github/stars/nomi-san/parsec-vdd?style=for-the-badge&logo=github" />
  </a>
  <a href="https://github.com/nomi-san/parsec-vdd/releases">
    <img src="https://img.shields.io/github/downloads/nomi-san/parsec-vdd/total?style=for-the-badge" />
  </a>
</p>

<br>

## ℹ About

This project provides a **standalone solution for creating virtual displays** on
a Windows host using the **Parsec Virtual Display Driver** (VDD), independent of
the **Parsec app**.

The Parsec VDD enables virtual displays on Windows 10+ systems, a feature
available to Parsec Teams and Warp customers. With VDD, users can add up to
three virtual displays to a host machine they connect to, ideal for setups where
physical monitors may be unavailable or when additional displays are beneficial.

Built by Parsec, the VDD leverages the IddCx API (Indirect Display Driver) to
generate virtual displays with support for high resolutions and refresh rates,
including up to 4K and 240 Hz. This capability makes it a versatile tool for
gaming, streaming, or remote work, allowing users to simulate multiple screens
for an enhanced, flexible visual experience.

## 📺 ParsecVDisplay App

ParsecVDisplay is a comprehensive virtual display manager for Parsec VDD, built
with C# and WPF. The app provides an intuitive interface to manage virtual
displays, showing the number of active displays and allowing users to add or
remove specific virtual displays. It also supports features like changing
display resolution, capturing screenshots, and more, making it a versatile tool
for flexible display management.

👉 Check out [Releases](https://github.com/nomi-san/parsec-vdd/releases) to
download it.

<p align="center">
  <img src="https://github.com/nomi-san/parsec-vdd/assets/38210249/71b25bc6-eee1-4d80-94e0-e39eab7f8fb9" />
</p>

## 🚀 Using Core API

### Design notes

Parsec VDD is designed to work with Parsec client-connection sessions. When the
user connects to the host, the app will start controlling the driver, it sends
IO control codes and gets results. When adding a virtual display, you will get
its index to be used for unplugging, the maximum number of displays could be
added up to 16 per adapter. You have to ping the driver periodically to keep
added displays alive, otherwise all of them will be unplugged after a second.
There's no direct way to manipulate added displays, you should call Win32
Display API to change their display mode (see the ParsecVDisplay source).

```mermaid
flowchart LR
  A(app)
  B(vdd)

  A <--->|ioctl| B
  A ..->|ping| B

  B --- X(display1)
  B --- Y(display2)
  B --- Z(display3)

  winapi -->|manipulate| X
```

### Using the code

For detailed instructions and usage examples, refer to the [VDD_LIBRARY_USAGE](./docs/VDD_LIBRARY_USAGE.md).

- The core API is designed as single C/C++ header that can be added to any
  project, 👉 [core/parsec-vdd.h](./core/parsec-vdd.h)
- There is also a simple demo program, 👉 [core/vdd-demo.cc](./core/vdd-demo.cc)

### Picking a driver

You have to install the driver to make them work.

| Version           | Minimum OS      | IddCx | Notes                                                     |
| :---------------- | :-------------- | :---: | :-------------------------------------------------------- |
| [parsec-vdd-0.38] | Windows 10 1607 |  1.0  | Obsolete, may crash randomly.                             |
| [parsec-vdd-0.41] | Windows 10 19H2 |  1.4  | Stable.                                                   |
| [parsec-vdd-0.45] | Windows 10 21H2 |  1.5  | Better streaming color, but may not work on some Windows. |

[parsec-vdd-0.38]: https://builds.parsec.app/vdd/parsec-vdd-0.38.0.0.exe
[parsec-vdd-0.41]: https://builds.parsec.app/vdd/parsec-vdd-0.41.0.0.exe
[parsec-vdd-0.45]: https://builds.parsec.app/vdd/parsec-vdd-0.45.0.0.exe

> All of them also work on Windows Server 2019 or higher.

You can unzip (using 7z) the driver setup above to obtain the driver files and
`nefconw` CLI.

```
vdd-0.45/
  |__ nefconw.exe
  |__ driver/
    |__ mm.cat
    |__ mm.dll
    |__ mm.inf
```

Command line method to install the driver using `nefconw` (admin required):

```
start /wait .\nefconw.exe --remove-device-node --hardware-id Root\Parsec\VDA --class-guid "4D36E968-E325-11CE-BFC1-08002BE10318"
start /wait .\nefconw.exe --create-device-node --class-name Display --class-guid "4D36E968-E325-11CE-BFC1-08002BE10318" --hardware-id Root\Parsec\VDA
start /wait .\nefconw.exe --install-driver --inf-path ".\driver\mm.inf"
```

In addition, you can run the driver setup in silent mode to install it quickly.

```
.\parsec-vdd-0.45.0.0.exe /S
```

## 😥 Known Limitations

> This list shows the known limitations of Parsec VDD.

### 1. HDR support

Parsec VDD does not support HDR on its displays (see the EDID below).
Theoretically, you can unlock support by editing the EDID, then adding HDR
metadata and setting 10-bit+ color depth. Unfortunately, you cannot flash its
firmware like a physical device, or modify the registry value.

All IDDs have their own fixed EDID block inside the driver binary to initialize
the monitor specs. So the solution is to modify this block in the driver DLL
(mm.dll), then reinstall it with `nefconw` CLI (see above).

### 2. Custom resolutions

Before connecting, the virtual display looks in the `HKLM\SOFTWARE\Parsec\vdd`
registry for additional preset resolutions. Currently this supports a maximum of
5 values.

```yaml
HKLM\SOFTWARE\Parsec\vdd:
  - key: [0 -> 5]
    value: { width, height, hz }
```

To unlock this limit, you need to patch the driver DLL the same way as above,
but **5 is enough** for personal use.

## 😑 Known Bugs

> This is a list of known issues when working with standalone Parsec VDD.

### 1. Incompatible with Parsec Privacy Mode

![Alt text](https://i.imgur.com/C74IRgC.png)

If you have enabled "Privacy Mode" in Parsec Host settings, please disable it
and clear the connected display configurations in the following Registry path.

```
HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\GraphicsDrivers\Connectivity
```

This option causes your main display to turn off when virtual displays are
added, making it difficult to turn the display on and disrupting the remote
desktop session.

### 2. // todo

## 🤔 Comparison with other IDDs

The table below shows a comparison with other popular Indirect Display Driver
projects.

| Project                        | Iddcx version | Signed | Gaming | HDR |                               H-Cursor                               | Tweakable | Controller |
| :----------------------------- | :-----------: | :----: | :----: | :-: | :------------------------------------------------------------------: | :-------: | :--------: |
| [usbmmidd_v2]                  |               |   ✅   |   ❌   | ❌  |                                  ❌                                  |           |            |
| [IddSampleDriver]              |      1.2      |   ❌   |   ❌   | ❌  |                                  ❌                                  |           |            |
| [RustDeskIddDriver]            |      1.2      |   ❌   |   ❌   | ❌  |                                  ❌                                  |           |            |
| [Virtual-Display-Driver (HDR)] |     1.10      |   ❌   |        | ✅  |                                  ❌                                  |           |            |
| [virtual-display-rs]           |      1.5      |   ❌   |        | ❌  | [#81](https://github.com/MolotovCherry/virtual-display-rs/issues/81) |    ✅     |     ✅     |
| parsec-vdd                     |      1.5      |   ✅   |   ✅   | ❌  |                                  ✅                                  |    🆗     |     ✅     |

✅ - full support, 🆗 - limited support

[usbmmidd_v2]: https://www.amyuni.com/forum/viewtopic.php?t=3030
[IddSampleDriver]: https://github.com/roshkins/IddSampleDriver
[RustDeskIddDriver]: https://github.com/fufesou/RustDeskIddDriver
[virtual-display-rs]: https://github.com/MolotovCherry/virtual-display-rs
[Virtual-Display-Driver (HDR)]: https://github.com/itsmikethetech/Virtual-Display-Driver

**Signed** means that the driver files have a valid digital signature.
**H-Cursor** means hardware cursor support, without it, you will get a double
cursor on some remote desktop apps. **Tweakable** is the ability to customize
display modes. Visit
[MSDN IddCx versions](https://learn.microsoft.com/en-us/windows-hardware/drivers/display/iddcx-versions)
to check the minimum supported Windows version.

## 📘 Parsec VDD Specs

Common preset display modes:

| Resolution  | Common Name | Aspect Ratio | Refresh Rates (Hz) |
| ----------- | ----------- | ------------ | ------------------ |
| 3840 x 2160 | 4K UHD      | 16:9         | 24/30/60/144/240   |
| 3440 x 1440 | UltraWide   | 21.5:9       | 24/30/60/144/240   |
| 2560 x 1440 | 2K          | 16:9         | 24/30/60/144/240   |
| 2560 x 1080 | UltraWide   | 21:9         | 24/30/60/144/240   |
| 1920 x 1080 | FHD         | 16:9         | 24/30/60/144/240   |
| 1600 x 900  | HD+         | 16:9         | 60/144/240         |
| 1280 x 720  | HD          | 16:9         | 60/144/240         |

Check out [docs/PARSEC_VDD_SPECS](./docs/PARSEC_VDD_SPECS.md) to see full of
preset display modes the driver specs.

## 🤝 Sponsors

<table>
  <tr>
    <td><img src="https://github.com/user-attachments/assets/58e9a6f4-6630-437d-a758-b284c0ed41e7" /></td>
    <td>Free code signing on Windows provided by <a href="https://signpath.io">SignPath.io</a>, certificate by <a href="https://signpath.org">SignPath Foundation</a></td>
  </tr>
</table>

## 🍻 Credits

- Thanks to Parsec for the driver
- The app's background was from old parsecgaming.com
