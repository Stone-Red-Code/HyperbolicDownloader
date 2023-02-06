# HyperbolicDownloader

> A cross-platform P2P file sharing CLI client

## Usage

1. Download one of the [releases](https://github.com/Stone-Red-Code/HyperbolicDownloader/releases)
1. Exacute the `HyperbolicDownloader` file
1. Wait until the setup process is finished
1. Do one of the below
    - Add a host to your client using the [add](#add) command and use the [get](#get) command tho retrive a file
    - Use the [generate](https://github.com/Stone-Red-Code/HyperbolicDownloader#generate) command to generate a `.hyper` file and the [get from](https://github.com/Stone-Red-Code/HyperbolicDownloader#getfrom) command to retrive a file trough the generated `.hyper` file

## How it works

When you start the program, it first tries to open a public port via UPnP/NAT-PMP. If it succeeds, you can communicate with the client using the public IP address and port.
If it is unable to find a UPnP/NAT-PMP device, you will need to manually set up port forwarding on your client's IP address to port "3055". The public port has to be between 1000 and 6000 here.
# HyperbolicDownloader

> A cross-platform P2P file sharing CLI client

## Usage

1. Download one of the [releases](https://github.com/Stone-Red-Code/HyperbolicDownloader/releases)
1. Execute the `HyperbolicDownloader` file
1. Wait until the setup process is finished
1. Do one of the below
    - Add a host to your client using the [add](#add) command and use the [get](#get) command to retrieve a file
    - Use the [generate](https://github.com/Stone-Red-Code/HyperbolicDownloader#generate) command to generate a `.hyper` file and the [get from](https://github.com/Stone-Red-Code/HyperbolicDownloader#getfrom) command to retrieve a file through the generated `.hyper` file.

## How it works

When you start the program, it first tries to open a public port via UPnP/NAT-PMP. If it succeeds, you can communicate with the client using the public IP address and port.
If it is unable to find a UPnP/NAT-PMP device, you will need to manually set up port forwarding on your client's IP address to port "3055". The public port has to be between 1000 and 6000 here.

HyperbolicDownloader can retrieve files from other computers (aka hosts) using an SHA 512 hash.
The client checks all known hosts to see if it could find the requested file. If one of the hosts has the requested file, it immediately downloads it.
After the file is completely downloaded, it is validated by comparing the hash entered with that of the file received. If the hash does not match, you will receive a warning message, and you can download the file again if needed.\
This makes it very difficult to tamper with requested files, as long as the source from which you obtain the hash/`.hpyer` file is trusted.

You can generate `.hyper` files with your client using the [generate](https://github.com/Stone-Red-Code/HyperbolicDownloader#generate). Command\
These files contain the hash value of the actual file and the hosts that should have the requested file.
You can use the [get from](https://github.com/Stone-Red-Code/HyperbolicDownloader#getfrom) command to retrieve the file or if you are using Windows you can right-click the `.hyper` file, select `open with` and select the HyperbolicDownloader executable.

## Disclaimer

Keep in mind that all network traffic is not encrypted. So, do not send sensitive information with HyperbolicDownloader.
Since none of the files are stored anywhere centralized, the quality of the files cannot be controlled. Therefore, pay attention to what you download.

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

**Description:** Lists all files.\
**parameter:** `none`

#### `<list> hosts`

**description:** Lists all hosts.\
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

### `remove` | `rm`

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

<a name="#getfrom"></a>

#### `<get> from`

**Description:** Attempts to retrieve a file from another host using a .hyper file.\
**Parameter:** `<FilePathToHyperFile>`

<a name="#generate"></a>

### `generate` | `gen`

**Description:** Generates a .hyper file from a file hash.\
**Parameters:** `<FileHash>`

#### `<generate> noscan`

**Description:** Generates a .hyper file from a file hash without checking the known hosts. This adds only the local host to the file.\
**Parameter:** `<FileHash>`
HyperbolicDownloader can retrieve files from other computers (aka hosts) using a SHA 512 hash.
The client checks all known hosts to see if it could find the requested file. If one of the hosts has the requested file, it immediately downloads it.
After the file is completely downloaded, it is validated by comparing the hash entered with that of the file received. If the hash does not match, you will receive a warning message and you can download the file again if needed.\
This makes it very difficult to tamper with requested files, as long as the source from which you obtain the hash/`.hpyer` file is trusted.

You can generate `.hyper` files with your client using the [generate](https://github.com/Stone-Red-Code/HyperbolicDownloader#generate). command\
These files contain the hash value of the actual file and the hosts that should have the requested file.
You can use the [get from](https://github.com/Stone-Red-Code/HyperbolicDownloader#getfrom) command to retrieve the file or if you are using Windows you can right-click the `.hyper` file, select `open with` and select the HyperbolicDownloader executable.

## Disclaimer

Keep in mind that all network traffic is not encrypted. So, do not send sensitive information with HyperbolicDownloader.
Since none of the files are stored anywhere centralized, the quality of the files cannot be controlled. Therefore, pay attention to what you download.

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

**Description:** Lists all files.\
**parameter:** `none`

#### `<list> hosts`

**description:** Lists all hosts.\
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

### `remove` | `rm`

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

<a name="#getfrom"></a>

#### `<get> from`

**Description:** Attempts to retrieve a file from another host using a .hyper file.\
**Parameter:** `<FilePathToHyperFile>`

<a name="#generate"></a>

### `generate` | `gen`

**Description:** Generates a .hyper file from a file hash.\
**Parameters:** `<FileHash>`

#### `<generate> noscan`

**Description:** Generates a .hyper file from a file hash without checking the known hosts. This adds only the local host to the file.\
**Parameter:** `<FileHash>`
