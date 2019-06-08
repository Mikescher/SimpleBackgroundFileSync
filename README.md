SimpleBackgroundFileSync
========================

Simple windows traybar-program to sync files (one-way)

## Configuration

Create a config file in `%appdata%\SBFS\config.xml`  
~~~~~~~~~~~~~~~xml
<?xml version="1.0" encoding="UTF-8"?>
<config>
    <file source="..." target="..." compare="..." interval="..." source_not_found="..." copy_fail="..." fileevents="..." />
    <file source="..." target="..." compare="..." interval="..." source_not_found="..." copy_fail="..." fileevents="..." />
    <file source="..." target="..." compare="..." interval="..." source_not_found="..." copy_fail="..." fileevents="..." />
    <file source="..." target="..." compare="..." interval="..." source_not_found="..." copy_fail="..." fileevents="..." />
</config>
~~~~~~~~~~~~~~~

### Config value

 - `source` Source filepath
 - `target` Destination filepath
 - `compare` Used method to check if file needs to be copied (`always`, `checksum`, `filetime`)
 - `interval` Interval to check in seconds
 - `source_not_found` Action to do when source file is not found (`error`, `warn`, `ignore`)
 - `copy_fail` Action to do when the copy action fails (`error`, `warn`, `ignore`)
 - `fileevents` Use a Filewatcher to react faster on file changes (`true`, `false`)