# C/C++ API Usage

This document describes the legacy single-header C/C++ API at
[`core/parsec-vdd.h`](../core/parsec-vdd.h). It's intentionally minimal — just
enough to add/remove monitors and keep them alive. For project context, see
the [README](../README.md). For supported display modes, see
[PARSEC_VDD_SPECS.md](./PARSEC_VDD_SPECS.md).

---

## Overview

Parsec VDD enables creation and management of virtual displays on Windows 10+ systems. The C/C++ API allows direct control over the driver, including querying status, adding/removing displays, and updating device state.

- Up to **16 virtual displays** per adapter (the legacy header caps at 8 to avoid plugging lag — adjust if needed).
- Supports high resolutions and refresh rates (see the [specs](./PARSEC_VDD_SPECS.md)).
- Can be used independently of the Parsec app.

Check out the README to install the driver.

---

## API Reference

With C++ include, you should add using namespace `parsec_vdd`.

### Device Status

```c
enum DeviceStatus {
    DEVICE_OK = 0,             // Ready to use
    DEVICE_INACCESSIBLE,       // Inaccessible
    DEVICE_UNKNOWN,            // Unknown status
    DEVICE_UNKNOWN_PROBLEM,    // Unknown problem
    DEVICE_DISABLED,           // Device is disabled
    DEVICE_DRIVER_ERROR,       // Device encountered error
    DEVICE_RESTART_REQUIRED,   // Must restart PC to use (could ignore but would have issue)
    DEVICE_DISABLED_SERVICE,   // Service is disabled
    DEVICE_NOT_INSTALLED       // Driver is not installed
};
```

#### Query Device Status

```c
DeviceStatus QueryDeviceStatus(const GUID *classGuid, const char *deviceId);
```

- Checks the status of a device by class GUID and hardware ID.
- Returns a `DeviceStatus` value.

### Device Handle Management

#### Open Device Handle

```c
HANDLE OpenDeviceHandle(const GUID *interfaceGuid);
```

- Opens a handle to the device interface.
- Returns `INVALID_HANDLE_VALUE` or a valid handle.

#### Close Device Handle

```c
void CloseDeviceHandle(HANDLE handle);
```

- Closes a previously opened device handle.

### VDD Core Operations

#### Constants

| Constant           | Value                                    | Description              |
| ------------------ | ---------------------------------------- | ------------------------ |
| `VDD_DISPLAY_ID`   | `"PSCCDD0"`                              | Display device ID        |
| `VDD_DISPLAY_NAME` | `"ParsecVDA"`                            | Display name             |
| `VDD_ADAPTER_GUID` | `{00b41627-04c4-429e-a26e-0265cf50c8fa}` | Adapter GUID             |
| `VDD_CLASS_GUID`   | `{4d36e968-e325-11ce-bfc1-08002be10318}` | Device class GUID        |
| `VDD_HARDWARE_ID`  | `"Root\\Parsec\\VDA"`                    | Hardware ID              |
| `VDD_MAX_DISPLAYS` | `8`                                      | Maximum virtual displays |

#### IOCTL Codes

```c
enum VddCtlCode {
    VDD_IOCTL_ADD     = 0x0022e004,
    VDD_IOCTL_REMOVE  = 0x0022a008,
    VDD_IOCTL_UPDATE  = 0x0022a00c,
    VDD_IOCTL_VERSION = 0x0022e010,
    VDD_IOCTL_UNKONWN = 0x0022a00c,
};
```

#### Generic DeviceIoControl

```c
DWORD VddIoControl(HANDLE vdd, VddCtlCode code, const void *data, size_t size);
```

- Sends an IOCTL to the VDD device.

#### Query Driver Version

```c
int VddVersion(HANDLE vdd);
```

- Returns the minor version of the VDD driver.

#### Update/Ping VDD

```c
void VddUpdate(HANDLE vdd);
```

- **Must** be called every ~100 ms (no longer than ~200 ms apart) to keep displays alive. If pings stop for ~1 second the driver **removes all virtual monitors** — this is its built-in watchdog for crashed hosts. Either run a dedicated thread or schedule a timer.

#### Add Virtual Display

```c
int VddAddDisplay(HANDLE vdd);
```

- Adds a new virtual display.
- Returns the index of the added display.

#### Remove Virtual Display

```c
void VddRemoveDisplay(HANDLE vdd, int index);
```

- Removes the display at the given index.

---

## Example Usage

- Minimal demo using the legacy single-header API: [`core/vdd-demo.cc`](../core/vdd-demo.cc).

---

## Display Modes & Specs

See [PARSEC_VDD_SPECS.md](./PARSEC_VDD_SPECS.md) for supported resolutions and refresh rates.

## Further Reading

- [README.md](../README.md): project overview, app features, lifecycle diagram.
- [PARSEC_VDD_SPECS.md](./PARSEC_VDD_SPECS.md): supported display modes and technical specs.
- [PARSEC_VDD_RE.md](../docs/PARSEC_VDD_RE.md): full reverse-engineered IOCTL reference — struct layouts, lifecycle, status codes.

---
