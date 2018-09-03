JaFS
====

Jottacloud alternative File System (JaFS) is a C# library that wraps the HTTP REST API
available for the cloud storage service [Jottacloud](https://jottacloud.com) by the Norwegian
company Jotta AS.

Jotta AS is a Norwegian company that operates under Norwegian jurisdiction, safe from *USA PATRIOT Act*
and with a [privacy guarantee](https://blog.jottacloud.com/its-your-stuff-guaranteed-3f50359f72d).
This, together with their ulimited storage option, make them have an appealing offer in the crowded
market of cloud service providers.

Notice
======

This project was originally created back in 2017, and after getting it to a state where most of the features
I needed was working, it did not get much attention in the years to follow - until september 2021.
During this time Jottacloud slowly changed into token-based authentication (twice, actually - after the
first version, they changed to the version currently in use). Whitelabel products changed even slower,
some are not even using token-based
authentication yet.

In September 2021 I implemented basic support for token-based authentication. Usage example from PowerShell:
- First you must login to the Web GUI, and generate a "Personal Login Token", direct url: https://www.jottacloud.com/web/secure. Save the result as a PowerShell string variable:
    ```
    $personalLoginToken = 'INSERT_HERE'
    ```
- Initialize a PowerShell session, same way as before:
    ```
    Add-Type -Path .\JottacloudFileSystem.dll
    using namespace JaFS
    ```
- First time, create a token object from the personal login token, optionally save it into an **encrypted** file `Token.sec` for later re-use:
    ```
    $token = [Jottacloud]::CreateToken($personalLoginToken)
    $token | ConvertTo-Json | ConvertTo-SecureString -AsPlainText -Force | ConvertFrom-SecureString | Out-File -LiteralPath 'Token.sec' -NoNewline
    ```
- Second and later times, read and decrypt token from the file, and re-fresh it:
    ```
    $oldToken = [TokenObject](New-Object -TypeName System.Net.NetworkCredential -ArgumentList '', (Get-Content -LiteralPath 'Token.sec' -Raw | ConvertTo-SecureString) | Select-Object -ExpandProperty Password | ConvertFrom-Json)
    $token = [Jottacloud]::RefreshToken($oldToken)
    ```
- Initialize JaFS with token:
    ```
    $jafs = New-Object Jottacloud($token)
    ```
- From now on, everything is as before, e.g.:
    ```
    $jafs.AutoFetchCompleteData = $true
    $mnt = $jafs.GetBuiltInDevice().GetMountPoint("Archive")
    $mnt.GetFileTree()
    ```

Note:
- The access token is valid in 1 hour, before you must use the Refresh method to replace it with a new one.
- If the token expires, you will have to start over - generate a new personal login token from Web GUI etc.

In addition to new authentication, Jottacloud have also introduced a new API for file uploads,
referred to as the "Allocate API", which adds support for resume / de-duplication etc.
This project is still based on the original api, the "JFS API". Both APIs are in use by Jottacloud
official clients, for different parts of the functionality: JFS for basic navigation, Allocate for file uploads.
I have not completely tested what works and what does not in the current state of this project.

If you need a very good, maintained, command line tool for accessing Jottacloud,
and almost any other cloud storage provider, I suggest you take a look at [rclone](https://rclone.org/)!


Disclaimer
==========

**This is not an official product from Jotta:**

The REST API, called JFS (Jottacloud File System), is available at https://jfs.jottacloud.com/jfs
(and https://www.jottacloud.com/jfs).
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

Source code is in C#, with Visual Studio 2017 project targeting .NET framework version 4.6.1,
building as "Any CPU". Add as project reference, or build it and add assembly reference, to
your own project.


### PowerShell

#### Using the main C# library from PowerShell

One neat tip is to load the main C# library (`JottacloudFileSystem.dll`) into a Windows PowerShell
session by `Add-Type -Path <PathToLibrary>`,
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

#### Using the PowerShell Provider

Work has started on wrapping the main C# library (`JottacloudFileSystem.dll`) in a
PowerShell provider library (`JottacloudPSProvider.dll`), so that you can use the
familiar commands such as Get-Item, New-Item and Remove-Item to manipulate devices,
mount points and folders in Jottacloud:

```
[Reflection.Assembly]::LoadFrom("JottacloudPSProvider.dll") | Import-Module
New-PSDrive -Name JAFS -PSProvider JottacloudPSProvider -Root \ -Credential (Get-Credential)
Test-Path JAFS:\PC001
Get-Item JAFS:\PC001\Backup
Get-ChildItem JAFS:\PC001\Backup
New-item "JAFS:" -ItemType device -Value PSProviderTestDev -DeviceType LAPTOP
New-item "JAFS:\PSProviderTestDev" -ItemType mountpoint -Value PSProviderTestMount
New-item "JAFS:\PSProviderTestDev\PSProviderTestMount" -ItemType folder -Value PSProviderTestFolder
Remove-item JAFS:\PSProviderTestDev\PSProviderTestMount\PSProviderTestFolder
Remove-item JAFS:\PSProviderTestDev\PSProviderTestMount
Remove-item JAFS:\PSProviderTestDev -Permanent
```

Note that this is on an even earlier stage than the main C# library!

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
* `Trash` is a temporary storage for files that have been marked as deleted, but not permanently
erased yet. Jottacloud saves deleted files for a minimum 30 days before permanently deleting them.
Files and folders from the any of the other mount points will be present here, and also
deleted custom mount points.
* `Links` is a special mount point for sharing of files by giving public access using a secret.
This mount point is not behaving like folder and is therefore excluded by default from the JaFS
methods that are working with mount points (but can be included with an extra argument).
* `Shared` is an old mechanism for the sharing logic now in `Links`, i think.
* `Latest` is also a special mount point, this is for showing recent activity. Also not behaving
like a folder and excluded by default from the JaFS methods that are working with mount points.

When you in JaFS retrieves mount points on the built-in device you will get `Archive` and `Sync` back.
The special mount points `Trash`, `Links` and `Latest` are not by default exposed as mount points, the
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
new folders, rename/move them, delete them into trash or delete them permanently. There is also
a method for uploading a local folder, which will loop trough it and create folders and upload
files to the server location. There is also a corresponding method for downloading a remote folder.

#### Files

Files are stored in folders (or directly on mount points). You create files by uploading a local file
into a specified path. You can also rename/move files, delete them into trash or delete
them permanently.

Two important concepts of files in JFS is state and revisions. Jottacloud keeps track of the latest
successful/completed upload of a file and stores it in a revision named "current revision". Whenever
there is a unsuccessful upload, e.g. due to hash or size mismatch, it will store it in a revision
name "latest revision". Any older revisions are stored as "revision". The file revisions have a state
property telling if it was "Completed", "Incomplete", "Corrupt" (hash mismatch), etc. In JaFS the
file types are organized as a hierarchy: All files are JFSBasicFile, and then there is derived types
for corrupt files (JFSCorruptFile), incomplete files (JFSIncompleteFile) and complete files (JFSFile),
adding more and more features. For example only JFSFile has a `Read` method (you can only download
content of complete files, and only JFSIncompleteFile has a `resume` method (to continue upload where
a previous incomplete upload ended). But all files have `Write` method so that you can upload
new content for it. Note that when you retrieve file objects from containers (such as folders or mount
points) you will get them as JFSBasicFile, and you will need to cast it according to the type.
The JFSBasicFile class has properties for checking which type it really is.

Timestamps for when the file was first created and the time of the last update (write) are preserved,
but not timestamp for last read (access). Timestamps of folders are not preserved, as Jottacloud
does not store them (only for mount points).

Jottacloud includes functionality for sharing files, by generating a unique "secret" for it that can
be used in a special URL for anyone knowing the secret to have access to the file.

Limitations
===========

In general the main forcus so far has been on handling the data structure, with devices, mount points
and folders. File handling with uploading/downloading of files is also implemented, but there are
some issues.

Some of the things that have issues/are not completed/not started on yet:
* The uploading of folder with the "SkipIdenticalFiles" option, which is intended so save
traffic by checking MD5 of already uploaded files, seems to have issues (the cphash argument
in the API request).
* Robust handling of any kind of issues that can occur during file upload.
* Proper handling of file versions.
* Restore deleted items from trash (haven't found any API query that works).
* Rename mount points (haven't found any API query that works).
* Rename of devices (haven't found any API query that works).
* Sync-operation, e.g. delete files from remote folder which are no longer present in matching local folder.
Not sure if this will ever be part of this library though, probably better to build something on top of it
for these kind of features. Maybe implementing Jottacloud support in [rclone](https://github.com/ncw/rclone)
is a better option?

Side notes
==========

Check out [Duplicati](https://github.com/duplicati), *a free, open source, backup client that
securely stores encrypted, incremental, compressed backups on cloud storage services and remote file servers.*
I used my experience from the current project to contribute with a Jottacloud backend,
so that you can store unlimited amount of *encrypted, incremental and compressed* backup.

I also recommend [Syncthing](https://github.com/syncthing/syncthing), an open source, cross-platform, encrypted,
continuous file synchronization program for synchronizing files directly between devices.

Contributing
============

Help needed first of all with testing, and reporing any [issues](https://github.com/albertony/jafs/issues) found.

Then if you can figure out some of the missing pieces in the Jottacloud API (see the limitations section) it is
also highly appreciated.

History
=======

### Version 0.1.1

Added PowerShell Provider library.

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

