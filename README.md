<img align="left" src="https://encrypted-tbn0.gstatic.com/images?q=tbn:ANd9GcSxBsVvpMSFpgenJxcoNf9IYCxhAL9EbkFPYMsJV3BMoHFfLKE9ZBJiZDHtcTACUyr2PsA&usqp=CAU" width="240px">

# parsec-vdd
✨ Standalone **ParsecVDD**, create a virtual super display without **Parsec**, upto **4K 2160p@240hz**.<br>

<br>

![image](https://user-images.githubusercontent.com/38210249/226080853-2ccd0327-4398-4c58-916f-b002966e7df3.png)

## Getting started

Download and install **Parsec Virtual Display Driver**, there are two versions, just pick one:
- [ParsecVDD v0.38](https://builds.parsec.app/vdd/parsec-vdd-0.38.0.0.exe) (preferred)
- [ParsecVDD v0.41](https://builds.parsec.app/vdd/parsec-vdd-0.41.0.0.exe)

<br>

Use this GUID interface to get the device handle.
```cpp
const GUID PARSEC_VDD_DEVINTERFACE = \
  { 0x00b41627, 0x04c4, 0x429e, { 0xa2, 0x6e, 0x02, 0x65, 0xcf, 0x50, 0xc8, 0xfa } };
  
HANDLE device = OpenDeviceHandle(PARSEC_VDD_DEVINTERFACE);
```

- Try this function to create your `OpenDeviceHandle(GUID)`: [fc152f42@fufesou/RustDeskIddDriver](https://github.com/fufesou/RustDeskIddDriver/blob/fc152f4282cc167b0bb32aa12c97c90788f32c3d/RustDeskIddApp/IddController.c#L722)
- Or hard code 😀 with this file path `\\?\root#display#%(DISPLAY_INDEX)#{00b41627-04c4-429e-a26e-0265cf50c8fa}`

<br>

Here's the way to control the VDD:
```cpp
enum VddCtlCode {
    IOCTL_VDD_CONNECT = 0x22A008,
    IOCTL_VDD_ADD = 0x22E004,
    IOCTL_VDD_UPDATE = 0x22A00C,
};

void VddIoCtl(HANDLE vdd, VddCtlCode code) {
    BYTE InBuffer[32]{};
    int OutBuffer = 0;
    OVERLAPPED Overlapped{};
    DWORD NumberOfBytesTransferred;

    Overlapped.hEvent = CreateEventW(NULL, NULL, NULL, NULL);
    DeviceIoControl(vdd, code, InBuffer, _countof(InBuffer), &OutBuffer, sizeof(OutBuffer), NULL, &Overlapped);
    GetOverlappedResult(vdd, &Overlapped, &NumberOfBytesTransferred, TRUE);

    if (Overlapped.hEvent && Overlapped.hEvent != INVALID_HANDLE_VALUE)
        CloseHandle(Overlapped.hEvent);
}
```

And here is pseudo code to interface with the VDD:

```cpp
void VddThread(HANDLE vdd, bool &running) {
    // Plug in monitor.
    VddIoCtl(vdd, IOCTL_VDD_CONNECT);
    VddIoCtl(vdd, IOCTL_VDD_UPDATE);
    VddIoCtl(vdd, IOCTL_VDD_ADD);
    VddIoCtl(vdd, IOCTL_VDD_UPDATE);
    // Keep monitor connection.
    for (running = true; running; ) {
        Sleep(100);
        VddIoCtl(vdd, IOCTL_VDD_UPDATE);
    }
}

bool PlugInMonitor(HANDLE &vdd, HANDLE &vddThread, bool &running) {
    char devpath[1024];
    for (int idx = 0; idx < 5; idx++) {
        // Hardcode device path.
        sprintf(devpath, "\\\\?\\root#display#000%d#%s", idx, "{00b41627-04c4-429e-a26e-0265cf50c8fa}");    
        vdd = CreateFileA(devpath, GENERIC_READ | GENERIC_WRITE, 0, NULL, OPEN_EXISTING, 0, NULL);

        if (vdd && vdd != INVALID_HANDLE_VALUE) {
            vddThread = CreateThread(VddThread);
            return true;
        }
    }

    return false;
}

void PlugOutMonitor(HANDLE vdd, HANDLE vddThread, bool &running) {
    running = false;
    WaitForSingleObject(vddThread, INFINITE);

    // Reconnect to unplug monitor.
    VddIoCtl(vdd, IOCTL_VDD_CONNECT);
    CloseHandle(vdd);
}
```

A simple usage, see [demo.cc](./demo.cc) to learn more.

```cpp
int main()
{
    bool running;
    HANDLE vdd, vddThread;

    if (PlugInMonitor(vdd, vddThread, running)) {
        Sleep(5000);
        PlugOutMonitor(vdd, vddThread, running);
    }
}
```

## Supported resolutions

Notes:
- Low GPUs, e.g GTX 1650 will not support the highest DCI 4K.
- All these below resolutions are compatible with all refresh rates 24/30/60/144/240 hz.

| Resolution  | Common name         | Aspect ratio
| -           | :-:                 | :-:
| 4096 x 2160 |		DCI 4K      
| 3840 x 2160 |		4K UHD            | 16:9
| 3840 x 1600 |		UltraWide         | 24:10   
| 3840 x 1080 |		UltraWide         | 32:9
| 3440 x 1440 |		                  | 43:18
| 3240 x 2160 |                     | 3:2
| 3200 x 1800 |		3K                | 16:9
| 3000 x 2000 |                     | 3:2
| 2880 x 1800 |		2.8K              | 16:10
| 2880 x 1620 |		2.8K              | 16:9
| 2736 x 1824 |
| 2560 x 1600 |		2K                | 16:10
| 2560 x 1440 |		2K                | 16:9
| 2560 x 1080 |		UltraWide         | 21:9
| 2496 x 1664 |
| 2256 x 1504 |
| 2048 x 1152 |		
| 1920 x 1200 |		FHD               | 16:10
| 1920 x 1080 |		FHD               | 16:9
| 1800 x 1200 |		FHD               | 3:2
| 1680 x 1050 |		HD+               | 16:10
| 1600 x 1200 |		HD+               | 4:3
|  1600 x 900 |		HD+               | 16:9
|  1440 x 900 |		HD                | 16:10
|  1366 x 768 |
|  1280 x 800 |   HD                | 16:10
|  1280 x 720 |  	HD                | 16:9

## ParsecVDD adapter

- Name: Parsec Virtual Display Adapter
- Hardware ID: `Root\Parsec\VDA`
- Adapter GUID: `{00b41627-04c4-429e-a26e-0265cf50c8fa}`
- EDID:

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

Visit http://www.edidreader.com/ to view it online or use an advanced tool [AW EDID Editor](https://www.analogway.com/apac/products/software-tools/aw-edid-editor/)
