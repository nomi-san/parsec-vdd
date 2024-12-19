# Parsec VDD Specs

This document provides detailed specifications for the Parsec Virtual Display
Driver (VDD). It includes information about the preset display modes, adapter,
monitor and common usage scenarios.

## Preset display modes

All of the following display modes are set by driver default.

| Resolution      | Common Name |    Aspect Ratio    |  Refresh Rates (Hz)  |
| --------------- | :---------: | :----------------: | :------------------: |
| 4096 x 2160     |   DCI 4K    |  1.90:1 (256:135)  |   24/30/60/144/240   |
| 3840 x 2160     |   4K UHD    |        16:9        |   24/30/60/144/240   |
| 3840 x 1600     |  UltraWide  |       24:10        |   24/30/60/144/240   |
| 3840 x 1080     |  UltraWide  | 32:9 (2x 16:9 FHD) |   24/30/60/144/240   |
| 3440 x 1440     |             |   21.5:9 (43:18)   |   24/30/60/144/240   |
| 3240 x 2160     |             |        3:2         |          60          |
| 3200 x 1800     |     3K      |        16:9        |   24/30/60/144/240   |
| 3000 x 2000     |             |        3:2         |          60          |
| 2880 x 1800     |    2.8K     |       16:10        |          60          |
| 2880 x 1620     |    2.8K     |        16:9        |   24/30/60/144/240   |
| 2736 x 1824     |             |                    |          60          |
| 2560 x 1600     |     2K      |       16:10        |   24/30/60/144/240   |
| 2560 x 1440     |     2K      |        16:9        |   24/30/60/144/240   |
| 2560 x 1080     |  UltraWide  |        21:9        |   24/30/60/144/240   |
| 2496 x 1664     |             |                    |          60          |
| 2256 x 1504     |             |                    |          60          |
| 2048 x 1152     |             |                    |      60/144/240      |
| 1920 x 1200     |     FHD     |       16:10        |      60/144/240      |
| **1920 x 1080** |   **FHD**   |      **16:9**      | 24/30/**60**/144/240 |
| 1800 x 1200     |     FHD     |        3:2         |          60          |
| 1680 x 1050     |     HD+     |       16:10        |      60/144/240      |
| 1600 x 1200     |     HD+     |        4:3         |   24/30/60/144/240   |
| 1600 x 900      |     HD+     |        16:9        |      60/144/240      |
| 1440 x 900      |     HD      |       16:10        |      60/144/240      |
| 1366 x 768      |             |                    |      60/144/240      |
| 1280 x 800      |     HD      |       16:10        |      60/144/240      |
| 1280 x 720      |     HD      |        16:9        |      60/144/240      |

Notes:

- Default display mode is 1920 x 1080 @ 60 Hz.
- All resolutions are compatible with 60 Hz.
- Low GPU such as GTX 1650 may get bugged when streaming in DCI 4K.

To add more display modes (up to 5), check out this
[official guide](https://support.parsec.app/hc/en-us/articles/32361359271444-VDD-Advanced-Configuration)
from Parsec.

## Driver implementation

- Type: user mode
- IddCx version: 1.4 or 1.5
- IO control codes:

```c
// add monitor
CTL_CODE(FILE_DEVICE_UNKNOWN, 0x800 + 1, METHOD_BUFFERED, FILE_READ_ACCESS | FILE_WRITE_ACCESS)
// remove monitor
CTL_CODE(FILE_DEVICE_UNKNOWN, 0x800 + 2, METHOD_BUFFERED, FILE_WRITE_ACCESS)
// update timing
CTL_CODE(FILE_DEVICE_UNKNOWN, 0x800 + 3, METHOD_BUFFERED, FILE_WRITE_ACCESS)
// query version
CTL_CODE(FILE_DEVICE_UNKNOWN, 0x800 + 4, METHOD_BUFFERED, FILE_READ_ACCESS | FILE_WRITE_ACCESS)
// set preferred adapter LUID
CTL_CODE(FILE_DEVICE_UNKNOWN, 0x800 + 5, METHOD_BUFFERED, FILE_WRITE_ACCESS)
```

## Adapter info

| Property     | Value                                    |
| ------------ | ---------------------------------------- |
| Name         | `Parsec Virtual Display Adapter`         |
| Hardware ID  | `Root\Parsec\VDA`                        |
| Class GUID   | `{4d36e968-e325-11ce-bfc1-08002be10318}` |
| Adapter GUID | `{00b41627-04c4-429e-a26e-0265cf50c8fa}` |

## Monitor info

| Property | Value                    |
| -------- | ------------------------ |
| ID       | `PSCCDD0`                |
| Name     | `ParsecVDA`              |
| EDID     | (see the hex code below) |

```
00 FF FF FF FF FF FF 00  42 63 D0 CD ED 5F 84 00
11 1E 01 04 A5 35 1E 78  3B 57 E0 A5 54 4F 9D 26
12 50 54 27 CF 00 71 4F  81 80 81 40 81 C0 81 00
95 00 B3 00 01 01 86 6F  80 A0 70 38 40 40 30 20
35 00 E0 0E 11 00 00 1A  00 00 00 FD 00 30 A5 C1
C1 29 01 0A 20 20 20 20  20 20 00 00 00 FC 00 50
61 72 73 65 63 56 44 41  0A 20 20 20 00 00 00 10
00 00 00 00 00 00 00 00  00 00 00 00 00 00 01 C6
02 03 10 00 4B 90 05 04  03 02 01 11 12 13 14 1F
8A 4D 80 A0 70 38 2C 40  30 20 35 00 E0 0E 11 00
00 1A FE 5B 80 A0 70 38  35 40 30 20 35 00 E0 0E
11 00 00 1A FC 7E 80 88  70 38 12 40 18 20 35 00
E0 0E 11 00 00 1E A4 9C  80 A0 70 38 59 40 30 20
35 00 E0 0E 11 00 00 1A  02 3A 80 18 71 38 2D 40
58 2C 45 00 E0 0E 11 00  00 1E 00 00 00 00 00 00
00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 A6
```

Notes:

- Visit [edidreader.com](http://www.edidreader.com/) to view it online or use an
  advanced tool
  [AW EDID Editor](https://www.analogway.com/apac/products/software-tools/aw-edid-editor/).
- The EDID could be used to replace HDMI dongle's EDID to get better display
  timing. Use
  [EDID Writer](https://www.monitortests.com/forum/Thread-EDID-DisplayID-Writer)
  to replace.
