# Command Line Interface

This is detailed help for CLI mode of the ParsecVDisplay app. The CLI executable
(`vdd`) can be installed via setup installer and can be invoked via command line
environment.

> Check out the [Releases page](https://github.com/nomi-san/parsec-vdd/releases)
> to get the setup installer (from v1.0) which has filename ends with
> `-setup.exe`.

## Usage

Run the command below to check if `vdd` is installed.

```sh
vdd -h
```

If success, you will get the help:

```sh
vdd command [args...]
    -a|add           - Add a virtual display
    -r|remove        - Remove the last added virtual display
            X        - Remove the virtual display at index X (number)
            all      - Remove all the added virtual displays
    -l|list          - Show all the added virtual displays and specs
    -s|set  X WxH    - Set resolution for a virtual display
                        where X is index number, WxH is size, e.g 1920x1080
            X @R     - Set only the refresh rate R, e.g @60, @120 (hz)
                        on Powershell, you should replace '@' with 'r'
            X WxH@R  - Set full display mode as above, e.g 1920x1080@144
    -v|version       - Query driver version and status
    -h|help          - Show this help
```

### Adding virtual display

Use command `-a` or `add` to add a virtual display.

```sh
vdd -a
```

The exit code is the index of added display, you can reuse this index to remove
the display. Less than 0 means error occurred.

### Removing virtual display

Use command `-r` or `remove` to remove the last added.

```sh
vdd -r
```

Remove the added virtual display at index `0`.

```sh
vdd -r 0
```

To remove all the added, replace the index with `all` or `*`.

```
vdd -r all
```

### Listing added displays

List all added virtual displays.

```sh
vdd -l
```

The exit code is the number of added virtual displays.

Example of output:

```
Index: 0
  - Device: \\.\DISPLAY37
  - Number: 2
  - Name: PSCCDD0
  - Mode: 1600 x 900 @ 60 Hz
  - Orientation: Landscape (0°)
```

### Setting display mode

A resolution is the display dimension (width x height) in pixels. A display mode
extends it plus a refresh rate such as 1920 x 1080 @ 60 Hz.

Set resolution for a virtual display at index 1.

```sh
vdd set 1 1920x1080
```

With full display mode, plus 120 Hz.

```sh
vdd set 1 1920x1080 @120
```

With only refresh rate.

```sh
vdd set 1 @144
```

On Powershell terminal, you should replace the symbol `@` with letter `r`.

```powershell
vdd set 1 1920x1080 r120
```

The command will fail and exit with non-zero if the display mode is invalid.

### Querying version

Query the driver status and version.

```sh
vdd -v
```

Example output:

```
Parsec Virtual Display Adapter
- Status: OK
- Version: 0.45
```

Here is the list of possible status and its code:

```js
0   OK                 - Ready to use
1   INACCESSIBLE       - Inaccessible
2   UNKNOWN            - Unknown status
3   UNKNOWN_PROBLEM    - Unknown problem
4   DISABLED           - Device is disabled
5   DRIVER_ERROR       - Device encountered error
6   RESTART_REQUIRED   - Must restart PC to use (could ignore but would have issue)
7   DISABLED_SERVICE   - Service is disabled
8   NOT_INSTALLED      - Driver is not installed
```

The status code is also the exit code of this command.
