# Parsec VDD — Reverse-Engineered Reference

Protocol-level documentation of the Parsec Virtual Display Driver as exercised
by Parsec's host application. This focuses on what the kernel-mode driver
expects from user-mode callers: device discovery, IOCTL formats, lifecycle,
and timing. It's intended as a reference for re-implementations (CLI tools,
managers, third-party hosts).

---

## 1. Architecture

Parsec VDD is an Indirect Display Driver (IDD) miniport that creates virtual
monitors on Windows 10+. Each running instance is identified by:

- **Hardware ID:** `Root\Parsec\VDA`
- **Setup class GUID:** `{4d36e968-e325-11ce-bfc1-08002be10318}` (Display)
- **Device interface GUID:** `{00b41627-04c4-429e-a26e-0265cf50c8fa}`

A host creates a software device with `SwDeviceCreate`, then communicates
through the device interface GUID via `DeviceIoControl`.

**Key constraints**

- Maximum **16 virtual monitors** per adapter (driver-side tracking is a
  `DWORD[16]`, with `-1` marking empty slots).
- Maximum resolution clamped to **8192 × 4320** by the host.
- Default refresh rate is **60 Hz** when not specified.
- Custom preset resolutions live in the registry — up to 5 slots.

---

## 2. Device Lifecycle

```
MM_adapter_create  (SwDeviceCreate)
        ↓
MM_open_device     (SetupDiGetClassDevs + CreateFileW)
        ↓
MM_connect         (opens handle, spawns keep-alive thread)
        ↓
MM_device_add      (IOCTL 0x22E004) × N monitors
        ↓
MM_keepalive_thread (IOCTL 0x22A00C every ~200 ms — keeps displays alive)
        ↓
host_vdd_privacy_thread (optional — blanks physical displays)
        ↓  [on disconnect]
MM_disconnect      (IOCTL 0x22A008 × 16, close handle)
```

The device handle is opened **asynchronously** (`FILE_FLAG_OVERLAPPED`); every
IOCTL is issued with an `OVERLAPPED` structure and awaited via
`GetOverlappedResultEx`.

---

## 3. IOCTL Reference

All four IOCTLs use a **32-byte (`0x20`) zero-initialized input buffer**.

| IOCTL | Hex | Function | Direction | In | Out | Timeout | Purpose |
|-------|-----|----------|-----------|----|-----|---------|---------|
| `CTL_CODE(0x22, 0x802, 0, 0)` | `0x22E004` | `MM_device_add` | In/Out | 32 B | 4 B | 5000 ms | Create a virtual monitor. Output is the assigned monitor index (0–15). |
| `CTL_CODE(0x22, 0x802, 2, 0)` | `0x22A008` | `MM_disconnect` | In | 32 B | 0 | 1000 ms | Remove a single monitor. `InBuffer[1..4]` = monitor index. |
| `CTL_CODE(0x22, 0x803, 2, 0)` | `0x22A00C` | `MM_update_keepalive` | In | 32 B | 0 | 1000 ms | Watchdog ping. Must be sent every ~200 ms or the driver removes **all** monitors after ~1 s. |
| `CTL_CODE(0x22, 0x804, 0, 0)` | `0x22E010` | `MM_query_adapter_status` | In/Out | 32 B | 4 B | 1000 ms | Query the adapter. Output is a 4-byte status word (typically the driver minor version). |

### Buffer layout

```c
struct VDD_IOCTL_Input {
    uint8_t  reserved;        // [0]
    uint32_t monitor_index;   // [1..4]  — for remove/create
    uint8_t  padding[27];     // [5..31]
};
```

### IoControl pattern (important)

The driver can return `FALSE` from `DeviceIoControl` with a non-`ERROR_IO_PENDING`
status code **while still queuing** the operation. The robust pattern,
mirrored from the reference C library:

1. Call `DeviceIoControl` — **ignore its synchronous return**.
2. Call `GetOverlappedResultEx(handle, &ov, ..., timeout, FALSE)`.
3. If the wait fails (timeout / error), call `CancelIoEx` followed by a
   **blocking** `GetOverlappedResult(..., bWait = TRUE)` before returning.

Skipping step 3 lets the kernel write into the (now-defunct) stack frame
that held the `OVERLAPPED` / output buffer, which AVs the next caller on the
same thread.

---

## 4. Adapter Creation — `MM_adapter_create`

Registers a software device at runtime via `SwDeviceCreate`. Waits on a
creation-complete callback (signaled by `WaitForSingleObject`). Once
registered, the device interface is enumerated with `SetupDiGetClassDevs` and
opened via `CreateFileW` using `FILE_FLAG_OVERLAPPED | FILE_FLAG_WRITE_THROUGH
| FILE_FLAG_NO_BUFFERING`.

---

## 5. Monitor Create — `MM_device_add`

```c
// Find the first empty slot (value == -1) in the 16-entry array
for (i = 0; i < 16; i++) {
    if (context->monitors[i] == -1) break;
}
// Send IOCTL 0x22E004
DeviceIoControl(hDevice, 0x22E004, in_buf, 0x20, &out_index, 4, ...);
// Wait via GetOverlappedResultEx (timeout 5000 ms)
// out_index must be < 16; stored in context->monitors[i]
```

The returned monitor index becomes the **driver index** for that virtual
display. The UID seen later in monitor device-instance paths is
`0x100 + driver_index` (used to map back to a specific virtual monitor when
walking `EnumDisplayDevices` results).

---

## 6. Keep-Alive Watchdog — `MM_keepalive_thread`

Spawned by `MM_connect`. Loops continuously sending `IOCTL_UPDATE` at
~200 ms cadence:

```c
while (*alive_flag) {
    Sleep(100);  // 100 ms granularity
    QueryPerformanceCounter(&now);
    QueryPerformanceFrequency(&freq);
    elapsed_ms = (now - last) / (freq / 1000.0);
    if (elapsed_ms > 200.0) {
        MM_update_keepalive(context);  // IOCTL 0x22A00C
        last = now;
    }
}
```

**Critical behavior.** If pings stop, the driver removes **all** virtual
monitors after roughly 1 second. This is its built-in watchdog for cleaning
up after host processes that crashed without explicit disconnect. Any
re-implementation must keep up the cadence (a dedicated thread or 100 ms
timer is the standard pattern).

---

## 7. Display Enumeration — `MM_enumerate_displays`

Walks all displays using the Windows configuration-manager APIs:

1. `EnumDisplayDevicesW(NULL, i, ...)` — outer loop over adapters.
2. `EnumDisplayDevicesW(adapter, j, EDD_GET_DEVICE_INTERFACE_NAME)` — inner loop over monitors on that adapter.
3. `CM_Get_Device_Interface_PropertyW` → device node.
4. `CM_Locate_DevNodeW` + `CM_Get_DevNode_PropertyW` → hardware IDs.

Each entry is **836 bytes (209 DWORDs)**:

```
+0x004  adapter device ID      (128 wchars)
+0x108  display name           (32 wchars)
+0x148  monitor device ID      (128 wchars)
+0x248  interface path         (128 wchars)
Flags at DWORD offsets 4/5/6/7:
  [4] = active, [5] = primary, [6] = removable, [7] = is-parsec
```

The `is-parsec` flag is determined by matching the hardware ID against
`Root\Parsec\VDA` (with case-insensitive substring search).

> **Note for RDP / cloud-server hosts:** `EnumDisplayDevicesW` is bound to the
> calling thread's window station — it cannot see console-session displays
> from inside an RDP session. A supplemental `SetupDi` enumeration of the
> MONITOR class (GUID `{4d36e96e-e325-11ce-bfc1-08002be10318}`) walks the
> kernel device tree directly and is session-independent.

---

## 8. Custom Resolutions

Up to **5 custom resolutions** can be added at slot indices 0–4. The driver
reads them at adapter init from:

```
HKLM\SOFTWARE\Parsec\vdd\<slot>
    w   (REG_DWORD)   width
    h   (REG_DWORD)   height
    hz  (REG_DWORD)   refresh rate
```

Written via:

```c
RegCreateKeyExW(hkey, "SOFTWARE\\Parsec\\vdd\\<slot>", ..., KEY_READ | KEY_WRITE, ...);
RegSetValueExW(hkey, "w",  REG_DWORD, &width,    4);
RegSetValueExW(hkey, "h",  REG_DWORD, &height,   4);
RegSetValueExW(hkey, "hz", REG_DWORD, &refrate,  4);
```

To raise the 5-slot limit you'd have to patch the driver DLL itself
(`mm.dll`) — 5 is fixed in the driver-side parsing.

---

## 9. Privacy Mode — `host_vdd_privacy_thread`

Optional dedicated thread polling every 30 ms while the host wants to hide
physical displays from a connected client:

- `WTSQuerySessionInformationW` — check active session state.
- `EnumWindows` — detect whether any windows are still on real displays.
- `SetDisplayConfig(flags=0x44)` — `SDC_TOPOLOGY_INTERNAL`: blank/disable real displays.
- `SetDisplayConfig(flags=0x84)` — `SDC_TOPOLOGY_EXTEND`: restore real displays on disconnect.
- `MM_query_adapter_status` — poll for VDD adapter health.

When combined with `IDD_DISPLAY` only mode, this gives Parsec's "Privacy
Mode" — physical screens go dark while a virtual display drives the remote
client. **Note:** this conflicts with standalone managers like ParsecDisplay;
running both will fight over the topology. The README documents the
workaround (clear `HKLM\SYSTEM\CurrentControlSet\Control\GraphicsDrivers\Connectivity`).

---

## 10. Device Status Codes — `host_vdd_init`

Checks the device at `Root\Parsec\VDA` up to 4 times during startup.
Maps `CM_Get_DevNode_Status` flags to a numeric status:

| Status | Meaning |
|:------:|---------|
| 0 | UNKNOWN |
| 2 | OK |
| 3 | UNKNOWN PROBLEM |
| 4 | DISABLED |
| 5 | DRIVER ERROR |
| 6 | RESTART REQUIRED |
| 7 | DISABLED SERVICE |
| other | NOT INSTALLED |

These are surfaced to the user as informative dialogs in Parsec's host UI
("driver disabled — re-enable in Device Manager", "restart required after
install", etc.).

---

## 11. Adapter Recovery — `host_vdd_adapter_monitor`

A separate monitoring thread that handles driver disable/re-enable cycles
(e.g. the user updates the driver in Device Manager):

- Polls every 100 ms (idle) or 1000 ms (active).
- Re-attempts `MM_adapter_create` up to **3 times** when the device disappears.
- Bails out after **5 faults within 5 seconds** to avoid CPU spin on a
  permanently broken driver.
- Adapter is dynamically re-created when the driver returns to a healthy state.

This is what makes the Parsec host survive a driver upgrade without a
restart — and the same pattern is mirrored in ParsecDisplay's
`Vdd.Controller.StatusThread`.

---

## 12. Session Startup Ordering

The order in which a Parsec host initializes display-related machinery at
session start (excluding VUSB / gamepad / microphone code paths, which live
outside this driver):

1. **Custom resolutions** — `vdd_register_custom_resolution` × 3 (indices 2–4).
2. **Virtual displays** — `host_vdd_init` opens the device, spawns
   keep-alive + privacy threads, adds N monitors.
3. **Resolution clamping** — capped at 8192 × 4320 (configurable via session params).
4. **Screen blanking** — `PostMessageW(HWND_BROADCAST, WM_SYSCOMMAND, SC_MONITORPOWER, -1)`.
5. **Host video thread** — creates the capture queue + encoding thread.

---

## Function map

Behavioral labels used throughout this document. Names are conventional —
choose any that suit your re-implementation.

| Function | Purpose |
|----------|---------|
| `MM_adapter_create` | `SwDeviceCreate` registration of the IDD adapter. |
| `MM_open_device` | `SetupDiGetClassDevs` + `CreateFileW` to obtain the device handle. |
| `MM_connect` | High-level init: open device, spawn keep-alive thread. |
| `MM_disconnect` | Tear down: remove all monitors, close handle. |
| `MM_device_add` | `IOCTL 0x22E004` — create a virtual monitor. |
| `MM_query_adapter_status` | `IOCTL 0x22E010` — adapter health / version. |
| `MM_update_keepalive` | `IOCTL 0x22A00C` — watchdog ping. |
| `MM_keepalive_thread` | Loop sending keep-alive at ~200 ms cadence. |
| `MM_enumerate_displays` | Walk all displays via `EnumDisplayDevices` + CM APIs. |
| `vdd_register_custom_resolution` | Write a `(w, h, hz)` triple into the registry slot. |
| `host_vdd_init` | Driver-status check + open handle + add initial monitors. |
| `host_vdd_privacy_thread` | Optional: hide physical displays while a session is active. |
| `host_vdd_adapter_monitor` | Survive driver disable/re-enable cycles. |
