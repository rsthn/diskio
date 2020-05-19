# Direct Disk I/O Utility

Tool to read/write raw sectors from any drive. Binaries can be found on `bin/Debug` folder, or you can build them yourself. We used SharpDevelop.

### Syntax:
```sh
diskio [options] <driveName|driveNumber> fileName
```

### Options:
```
   -[r]ead n     Reads the specified amount of bytes from the drive.
   -[w]rite      Writes all data from the input file to the drive.
   -[s]tart n    Sets the starting offset of the drive for reading/writing (in bytes).
   -[e]num       Enumerates all available physical drives.
   -q            Turn off the prompt for confirmation of data write.
```

### Examples:

- Read 2 MB from X: starting at sector 6.
```sh
diskio -s 6s -r 2m X: output
```

- Writes boot.bin to drive Z:
```sh
diskio -w Z: boot.bin
```

- Writes disk_image.img to enumerated drive 0 (use `diskio -e` to list available drives).
```sh
diskio -w 0 disk_image.img
```

### Troubleshooting

If you get permission errors, ensure you run the tool from an elevated command prompt, direct disk access is not for the faint of heart.

Before any write operation is done, you will be prompted if you're sure to continue. The tool has to make sure you're sure.
