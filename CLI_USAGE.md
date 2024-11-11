# Command Line Interface

This is detailed help for CLI mode of the ParsecVdisplay app.

The CLI executable can be installed via setup installer can be invoked via command line environment. Try to run the command below to check if vdd is installed.

```
vdd help
```

## Usage

### `vdd add`

Add a virtual display.

### `vdd remove`

Remove the last added virtual display.

### `vdd remove all`

Remove one or all added virtual displays.

- #### `vdd remove`
  Remove the last added virtual display.

- #### `vdd remove X`
  Remove the added virtual display at index `X`.

- #### `vdd remove all`
  Remove all the added virtual displays.

### `vdd list`

List all added virtual displays.

### `vdd set`

Set display mode (aka resolutiion and refresh rate) for a virtual display.

- #### `vdd set X WxH`
  Set resolution `W x H` for the virtual display at index `X`.

- #### `vdd set X @R`
  Set refresh rate `R` for the virtual display at index `X`.

- #### `vdd set X WxH@R`
  Set full display mode, resolution `W x H` @ refresh rate `R` for the virtual display at index `X`.

### `vdd status`

Query the driver status.

### `vdd version`

Query the driver version.
