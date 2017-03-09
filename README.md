JaFS
====

Jottacloud alternative File System (JaFS) is a C# library that wraps the HTTP REST API
available for the cloud storage service [Jottacloud](https://jottacloud.com) by the Norwegian
company Jotta AS.

Jotta AS is a Norwegian company that operates under Norwegian jurisdiction, safe from *USA PATRIOT Act*
and with a [privacy guarantee](https://blog.jottacloud.com/its-your-stuff-guaranteed-3f50359f72d).
This, together with their ulimited storage option, make them have an appealing offer in the crowded
market of cloud service providers.

Disclaimer
==========

**This is not an official product from Jotta:**

The REST API, called JFS (Jottacloud File System), is available at (https://www.jottacloud.com/jfs).
It is not officially announced or supported, and no official documentation exist, but the company
founder has mentioned it and given some short instructions on how to use it for read operations
in the official [forum](http://forum.jotta.no/jotta/topics/api_http). Write support was not 
mentioned, but in a now deleted [forum thread](https://web.archive.org/web/20160306095137/http://forum.jotta.no/jotta/topics/jotta_api_for_remote_storage_fetch),
company staff endorsed attempts to use that too: *We won't ban anyone for tinkering with this,
we actually like that our users are showing this much interest in our service. As long as you
don't break any laws or disrupt the service, we would encourage you to keep testing stuff out.*

This library has been implemented based on the short instructions in the forum, by observing
(reverse engineering) what the official client does, looking into other open source projects
which has done related things (such as [JottaLib](https://github.com/havardgulldahl/jottalib)
and [node-jfs](https://github.com/paaland/node-jfs), and general trial and error.

**Use at your own risk:**

Use of this library, any parts of the source code, or any of my descriptions are entirely at
your own risk. I take no responsibility for any trouble you get yourself into, such as if you
loose all your data or get kicked out from Jottacloud. But of course, I am doing my best
to make sure that nothing bad happens.

As described above, this library is developed by reverse engineering and not based on official
documentation, and this may be a source of error. This also means that the functionality can
stop working without any notice, should Jottacloud decide to change the API.

Please add anything you find to the [bug tracker](https://github.com/albertony/jafs/issues).

**White label products:**

The REST API is shared with the various whitelabel products like [Get Sky](https://sky.get.no),
[Elkjøp Cloud](https://cloud.elkjop.no) etc so this library should also work for them.
Please let me know if you have tried on any such products, so I can gather a list of what
works and what doesn't (just add a [issue](https://github.com/albertony/jafs/issues) or something).

Installation
============

Source code is in C#, with Visual Studio 2015 project targeting .NET framework version 4.5.2,
building as "Any CPU". Add as project reference, or build it and add assembly reference, to
your own project.


### PowerShell

One neat tip is to load the library into a Windows PowerShell session by `Add-Type -Path <PathToLibrary>`,
and then you can use the functonality from an interactive shell. If you are on PowerShell 5.0
or newer you can execute `using namespace JaFS`

Example for PowerShell 5:
```
Add-Type -Path .\JottacloudFileSystem.dll
using namespace JaFS
$jafs = New-Object Jottacloud((Get-Credential).GetNetworkCredential())
$jafs.AutoFetchCompleteData = $true
$mnt = $jafs.GetBuiltInDevice().GetMountPoint("Archive")
$mnt.GetFileTree()
```

Usage
=====

Use in combination with the official web user interface is recommended, both to verify
changes but also to handle things not supported by this library yet.

The navigation in the file system structure (JFS) offered by the REST API is by use of URLs
`https://www.jotta.no/jfs/[username]/[device]/[mountpoint]/[folder]/[subfolder]/[file]`.
In the JaFS library you log on as a specific user, and all paths in the JaFS methods are relative
to this: `/[device]/[mountpoint]/[folder]/[subfolder]/[file]`.

The main object in the library is the file system object. From there you can get devices,
then from those you get to mount points, then folders and files. You can also find items
by path, and generate file tree lists from the file system object.

File system objects you get from the library, such as device, mountpoint, folder and file
objects, contain some minimum of information about child objects. For each child object
an additional request needs to be sent to the REST API to get all details, for instance
children of that again. To reduce the traffic this is by default not done automatically,
and you must call the method `FetchCompleteData` on the objects to do it. There is
a property `AutoFetchCompleteData` on the main `Jottacloud` file system object which
you can enable to make this happen automatically: The detailed information are then
retrieved when you call a method which needs it, but not before.

### Concepts

#### Devices

At the top level in the JaFS structure is what is called devices.
There is always a built-in device with name `Jotta`, reserved for the built-in functionality offered
through the official web user interface and the official desktop client, like the file synchronization,
archive, file sharing etc. If your account supports it, additional devices can be created from the
official client software using its backup feature. Any devices other than the built-in device
will show up in the backup section in the official web interface. Whether you can have multiple
devies depends on your account type. Unlimited accounts can have an unlimited number of devices.

JaFS support creation of new devices and deleting devices. Note that deleting a device is always
permanent, it will not be moved to trash with the possibility for restoring it later. Renaming
of devices are not supported yet.

#### Mount points

Devices contain mount points. Mount points are more or less just a kind of root folder. The built-in
device `Jotta` contains some built-in mount points which are used from the official software:
* `Archive` are a generic file store accessible through in the web user interface only.
* `Sync` is where the files synchronization with the official desktop client are kept.
* `Shared` is a special mount point for sharing of files by giving public access using a secret.
This mount point is not behaving like folder and is therefore excluded by default from the JaFS
methods that are working with mount points (but can be included with an extra argument).
* `Latest` is also a special mount point, this is for showing recent activity. Also not behaving
like a folder and excluded by default from the JaFS methods that are working with mount points.

When you in JaFS retrieves mount points on the built-in device you will get `Archive` and `Sync` back.
The special mount points `Shared` and `Latest` are not by default exposed as mount points, the
corresponding functionality are handled via dedicated methods instead.

The backup functionality in the official desktop client creates a device specific for your client,
and then on that device one mount point for each backup set (folder) that you configure.

JaFS allow you to create new mount points, but by default only for custom devices. These will
show up in the backup section of the official web user interface. With a special option you are
also allowed to create new mount points also for the built-in device, but these will
not be shown in the official web user interface so you must be a bit careful with this.

Custom mount points can be deleted, and will then be moved to trash (will look like a folder
in the web user interface). You can also delete them permanently. Rename/move is not supported yet.

As mentioned above; mount points are more or less just folders (except the special ones),
and they contain lists of folders (sub-folders) and files (files can be stored directly on
mount points and does not have to be in files). Mount points have a size property
counting the total storage used by the mount point, this information is not on regular folders.

#### Folders

Folders contain lists of folders (sub-folders) and files. Not very surprising. You can create
new folders, rename/move them, delete them into trash or delete them permanently.

#### Files

Files are stored in folders (or mount points). You create files by uploading a local file
into a specified path. You can also rename/move files, delete them into trash or delete
them permanently.

Limitations
===========

Some of the functions that are not implemented yet:
* Correct handling of any kind of issues that can occur during file upload.
* Handling of file versions.
* Resume upload of incomplete files.
* Restore deleted items from trash.
* Rename mount points.
* Rename of devices.
* Sharing of files by giving public access using a secret.

Side notes
==========

Check out [Duplicati](https://github.com/duplicati), *a free, open source, backup client that
securely stores encrypted, incremental, compressed backups on cloud storage services and remote file servers.*
I used my experience from the current project to contribute with a Jottacloud backend,
so that you can store *encrypted, incremental and compressed* backup in your unlimited.

I also recommend [Syncthing](https://github.com/syncthing/syncthing), an open source, cross-platform, encrypted,
continuous file synchronization program for synchronizing files directly between devices.

Contributing
============

Written entirely by me as of now, although under heavy influence by the projects mentioned in credits.

History
=======

### Version 0.1

The initial version published on GitHub. The main structure is place, management of
devices, mount points and folders should work with the limitations mentioned in text above.
Main concern now is to finish the file upload handling.

Credits
=======

I got much help in understanding the REST API by looking at the Python utility [JottaLib](https://github.com/havardgulldahl/jottalib)
from @havardgulldahl, and also the Node.js utility [node-jfs](https://github.com/paaland/node-jfs) by @paaland
was helpful.

License
=======

All code is licensed under GNU Lesser General Public License version 3 (LGPLv3).
