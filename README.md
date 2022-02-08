# HyperbolicDownloader

> A cross-platform P2P file sharing CLI client

## Commands

> Most commands have aliases (separated by `|`)\
> Some commands also have subcommands. `<command> subcommand` means that you can replace `<command>` with any version of the parent command.

### `exit` | `quit`

**Description:** Exits the application.\
**Parameter:** `none`


### `clear` | `cls`

**Description:** Clears the console.\
**Parameter:** `none`


### `info` | `inf`

**Description:** Displays the private and public IP address.\
**Parameter:** `none`


### `discover` | `disc`

**Description:** Tries to find other active hosts on the local network.\
**parameter:** `none`


### `check` | `status`

**Description:** Checks the status of known hosts.\
**parameter:** `none`


### `list` | `ls`

**Description:** Lists all files\
**parameter:** `none`

#### `<list> files`

**Description:** Lists all files\
**parameter:** `none`

#### `<list> hosts`

**description:** lists all hosts
**parameter:** `none`


### `add`

**Description:** Adds a file to the tracking list.\
**parameter:** `<file path>`

#### `<add> file`

**Description:** Adds a file to the tracking list\.\
**parameter:** `<filepath>`

#### `<add> host`

**Description:** Adds a host to the list of known hosts.\
**parameter:** `<IpAddress:port>`


### `remove` `rm`

**Description:** Removes a file from the tracking list.\
**parameter:** `<FileHash>`

#### `<remove> file`

**Description:** Removes a file from the tracking list.\
**parameter:** `<FileHash>`

#### `<remove> host`

**Description:** Removes a host from the list of known hosts.\
**parameter:** `<IpAddress:Port>`


### `get`

**Description:** Attempts to retrieve a file from another host using a hash.\
**parameter:** `<FileHash>`

#### `<get> from`

**Description:** Attempts to retrieve a file from another host using a .hyper file.\
**Parameter:** `<FilePathToHyperFile>`


### `generate` | `gen`

**Description:** Generates a .hyper file from a file hash.\
**Parameters:** `<FileHash>`

#### `<generate> noscan`

**Description:** Generates a .hyper file from a file hash without checking the known hosts. This adds only the local host to the file.\
**Parameter:** `<FileHash>`
