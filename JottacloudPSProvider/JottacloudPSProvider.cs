using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using System.Management.Automation;
using System.Management.Automation.Provider;
using System.Collections.ObjectModel;
using JaFS;
using System.Net;
using System.IO;
using System.Collections;


//
// New-PSDrive -Name JAFS -PSProvider JottacloudPSProvider -Root . -Credential (Get-Credential)
//
namespace JottacloudPSProvider
{
    #region Provider class (JottacloudPSProvider)

    [CmdletProvider("JottacloudPSProvider", ProviderCapabilities.ShouldProcess | ProviderCapabilities.Credentials)]
    public class JottacloudPSProvider : NavigationCmdletProvider, IContentCmdletProvider
    {
        #region DriveCmdletProvider

        //protected override Collection<PSDriveInfo> InitializeDefaultDrives()
        protected override PSDriveInfo NewDrive(PSDriveInfo drive)
        {
            JottacloudPSDriveInfo jedi = null;
            if (drive == null)
            {
                WriteError(new ErrorRecord(new ArgumentNullException("drive"), "NullDrive", ErrorCategory.InvalidArgument, null));
            }
            // Check if the drive root is not null or empty and if it is valid.
            /*else if (String.IsNullOrEmpty(drive.Root))
            {
                WriteError(new ErrorRecord(new ArgumentException("drive.Root"), "NoRoot", ErrorCategory.InvalidArgument, drive));
                return null;
            }*/
            else if (drive.Credential == null)
            {
                WriteError(new ErrorRecord(new ArgumentNullException("drive.Credential"), "NoCredential", ErrorCategory.InvalidArgument, drive));
            }
            else
            {
                // Create a new drive and create an ODBC connection to the new drive.
                jedi = new JottacloudPSDriveInfo(drive);
                Jottacloud jafs = new Jottacloud((NetworkCredential)drive.Credential);
                jafs.ClientMountRoot = jedi.Name;
                jafs.ClientMountPathSeparator = PSPathSeparator;
                jedi.JAFS = jafs;
            }
            return jedi;
        }
        //protected override object NewDriveDynamicParameters();
        protected override PSDriveInfo RemoveDrive(PSDriveInfo drive)
        {
            JottacloudPSDriveInfo jedi = null;
            if (drive == null)
            {
                WriteError(new ErrorRecord(new ArgumentNullException("drive"), "NullDrive", ErrorCategory.InvalidArgument, drive));
                return null;
            }
            else
            {
                // Close the JAFS connection to the drive.
                jedi = drive as JottacloudPSDriveInfo;
                if (jedi == null)
                {
                    WriteError(new ErrorRecord(new ArgumentException("Specified drive is not handled by JottacloudPSProvider"), "InvalidDrive", ErrorCategory.InvalidArgument, drive));
                    return null;
                }
                else
                {
                    //jedi.JAFS.Close();  // TODO: Currently no persistant connection!
                    return jedi;
                }
            }
        }

        #endregion

        #region ItemCmdletProvider

        //protected virtual void ClearItem(string path);
        //protected virtual object ClearItemDynamicParameters(string path);
        //protected virtual string[] ExpandPath(string path);
        protected override void GetItem(string path)
        {
            if (PathIsDrive(path))
            {
                WriteItemObject(this.PSDriveInfo, path, true);
            }
            else
            {
                Collection<JFSObject> pathObjects = JAFS.GetPathObjects(JAFSPath(path));
                JFSObject job = pathObjects.Count > 0 ? pathObjects[pathObjects.Count - 1] : null;
                bool isContainer = !(job is JFSBasicFile);
                WriteItemObject(job, path, isContainer);
            }
        }
        //protected virtual object GetItemDynamicParameters(string path);
        //protected virtual void InvokeDefaultAction(string path);
        //protected virtual object InvokeDefaultActionDynamicParameters(string path);
        protected override bool IsValidPath(string path)
        {
            if (PathIsDrive(path))
            {
                return false;
            }
            else
            {
                return JAFS.IsValidPath(path);
            }
        }
        protected override bool ItemExists(string path)
        {
            if (PathIsDrive(path))
            {
                return true;
            }
            else
            {
                JFSObject job = null;
                try
                {
                    Collection<JFSObject> pathObjects = JAFS.GetPathObjects(JAFSPath(path));
                    if (pathObjects.Count > 0)
                    {
                        job = pathObjects[pathObjects.Count - 1];
                    }
                }
                catch (ArgumentException)
                {
                    // Expecting JAFS.GetPathObjects to throw for path that does not exist, which is normal.
                }
                return job != null;
            }
        }

        #endregion
        //protected virtual object ItemExistsDynamicParameters(string path);

        // SetItem method of the item provider interface is not implemented, using SetContent
        // from content provider interface instead, same as the built-in FileSystem provider.
        //protected virtual void SetItem(string path, object value);
        //protected virtual object SetItemDynamicParameters(string path, object value);

        #region ContainerCmdletProvider

        //protected virtual bool ConvertPath(string path, string filter, ref string updatedPath, ref string updatedFilter);
        //protected virtual void CopyItem(string path, string copyPath, bool recurse);
        //protected virtual object CopyItemDynamicParameters(string path, string destination, bool recurse);
        protected override void GetChildItems(string path, bool recurse)
        {
            if (PathIsDrive(path))
            {
                foreach (var deviceName in JAFS.GetDeviceNames())
                {
                    WriteItemObject(deviceName, path, true);
                }
            }
            else
            {
                Collection<JFSObject> pathObjects = JAFS.GetPathObjects(JAFSPath(path));
                JFSObject job = pathObjects.Count > 0 ? pathObjects[pathObjects.Count - 1] : null;
                if (job is JFSDevice)
                {
                    JFSDevice dev = job as JFSDevice;
                    foreach (var mntName in dev.GetMountPointNames())
                    {
                        WriteItemObject(mntName, path, true);
                    }
                }
                else if (job is JFSMountPoint)
                {
                    JFSMountPoint mnt = job as JFSMountPoint;
                    foreach (var folName in mnt.GetFoldersNames())
                    {
                        WriteItemObject(folName, path, true);
                    }
                    foreach (var filName in mnt.GetFileNames())
                    {
                        WriteItemObject(filName, path, false);
                    }
                }
                else if (job is JFSFolder)
                {
                    JFSFolder fol = job as JFSFolder;
                    foreach (var subName in fol.GetFoldersNames())
                    {
                        WriteItemObject(subName, path, true);
                    }
                    foreach (var filName in fol.GetFileNames())
                    {
                        WriteItemObject(filName, path, false);
                    }
                }
                else if (job is JFSBasicFile)
                {
                    JFSBasicFile fil = job as JFSBasicFile;
                    WriteItemObject(fil.Name, path, false);
                }
                else
                {
                    throw new ArgumentException("Invalid path");
                }
            }
        }
        //protected virtual object GetChildItemsDynamicParameters(string path, bool recurse);
        //protected virtual void GetChildNames(string path, ReturnContainers returnContainers);
        //protected virtual object GetChildNamesDynamicParameters(string path);
        protected override bool HasChildItems(string path)
        {
            // This is part of Container provider and tells if the container contains any children.
            // Must be implemented for RemoveItem to work, to decide how to handle possible recursion.
            // See also IsItemContainer implemented for Navigation provider.
            if (PathIsDrive(path))
            {
                return true;
            }
            Collection<JFSObject> pathObjects = JAFS.GetPathObjects(JAFSPath(path));
            JFSObject job = pathObjects.Count > 0 ? pathObjects[pathObjects.Count - 1] : null;
            if (job != null)
            {
                if (job is JFSDevice)
                {
                    JFSDevice dev = job as JFSDevice;
                    return dev.NumberOfApiMountPoints > 0; // NB: Includes deleted and special mount points!
                }
                else if (job is JFSMountPoint)
                {
                    JFSMountPoint mnt = job as JFSMountPoint;
                    return mnt.NumberOfFilesAndFolders > 0;
                }
                else if (job is JFSFolder)
                {
                    JFSFolder fol = job as JFSFolder;
                    return fol.NumberOfFilesAndFolders > 0;
                }
                else if (job is JFSBasicFile)
                {
                    return false;
                }
                else
                {
                    throw new ArgumentException("Unexpected item type");
                }
            }
            else
            {
                return false; //?
            }
        }
        protected override void NewItem(string path, string itemTypeName, object newItemValue)
        {
            NewItemDynamicParameters parameters = DynamicParameters as NewItemDynamicParameters;
            // The itemTypeName is the type of path of the container:
            //  - If a drive is specified, then a device can be created under it.
            //  - If a device is specified a mount point can be created under it.
            //  - If mount point or folder a folder can be created under it (or file, but we currently does not support creating for instance empty file using the NewItem operation).
            string itemName = newItemValue as string;
            if (String.IsNullOrEmpty(itemName))
            {
                throw new ArgumentException("Value argument must be name of the item to be created");
            }
            if (String.Equals(itemTypeName, "device", StringComparison.OrdinalIgnoreCase))
            {
                if (PathIsDrive(path))
                {
                    if (ShouldProcess(itemName, "New device"))
                    {
                        WriteItemObject(JAFS.NewDevice(itemName, parameters.DeviceType), path + PSPathSeparator + itemName, true);
                    }
                }
                else
                {
                    throw new ArgumentException("A device can only be created at root level");
                }
            }
            else if (String.Equals(itemTypeName, "mountpoint", StringComparison.OrdinalIgnoreCase))
            {
                if (!PathIsDrive(path))
                {
                    Collection<JFSObject> pathObjects = JAFS.GetPathObjects(JAFSPath(path));
                    JFSDevice dev = pathObjects.Count > 0 ? pathObjects[pathObjects.Count - 1] as JFSDevice : null;
                    if (dev != null)
                    {
                        if (ShouldProcess(itemName, "New mount point"))
                        {
                            WriteItemObject(dev.NewMountpoint(itemName), path + PSPathSeparator + itemName, true);
                        }
                    }
                    else
                    {
                        throw new ArgumentException("A mountpoint can only be created from a device and the specified path does not represent one");
                    }
                }
                else
                {
                    throw new ArgumentException("A mountpoint can only be created from a device and the specified path does not represent one");
                }
            }
            else if (String.Equals(itemTypeName, "folder", StringComparison.OrdinalIgnoreCase))
            {
                if (!PathIsDrive(path))
                {
                    Collection<JFSObject> pathObjects = JAFS.GetPathObjects(JAFSPath(path));
                    JFSObject job = pathObjects.Count > 0 ? pathObjects[pathObjects.Count - 1] : null;
                    if (job is JFSMountPoint)
                    {
                        JFSMountPoint mnt = job as JFSMountPoint;
                        if (mnt != null)
                        {
                            if (ShouldProcess(itemName, "New folder"))
                            {
                                WriteItemObject(mnt.NewFolder(itemName), path + PSPathSeparator + itemName, true);
                            }
                        }
                    }
                    else if (job is JFSFolder)
                    {
                        JFSFolder pf = job as JFSFolder;
                        if (pf != null)
                        {
                            if (ShouldProcess(itemName, "New folder"))
                            {
                                WriteItemObject(pf.NewFolder(itemName), path + PSPathSeparator + itemName, true);
                            }
                        }

                    }
                    else
                    {
                        throw new ArgumentException("A folder can only be created from a mount point or folder and the specified path does not represent one");
                    }
                }
                else
                {
                    throw new ArgumentException("A folder can only be created from a mount point or folder and the specified path does not represent one");
                }
            }
            else if (String.Equals(itemTypeName, "file", StringComparison.OrdinalIgnoreCase))
            {
                if (!PathIsDrive(path))
                {
                    Collection<JFSObject> pathObjects = JAFS.GetPathObjects(JAFSPath(path));
                    JFSObject job = pathObjects.Count > 0 ? pathObjects[pathObjects.Count - 1] : null;
                    if (job is JFSMountPoint)
                    {
                        JFSMountPoint mnt = job as JFSMountPoint;
                        if (mnt != null)
                        {
                            if (ShouldProcess(itemName, "New file"))
                            {
                                WriteItemObject(mnt.NewFile(itemName, new byte[0]), path + PSPathSeparator + itemName, false);
                            }
                        }
                    }
                    else if (job is JFSFolder)
                    {
                        JFSFolder pf = job as JFSFolder;
                        if (pf != null)
                        {
                            if (ShouldProcess(itemName, "New file"))
                            {
                                WriteItemObject(pf.NewFile(itemName, new byte[0]), path + PSPathSeparator + itemName, false);
                            }
                        }

                    }
                    else
                    {
                        throw new ArgumentException("A file can only be created from a mount point or folder and the specified path does not represent one");
                    }
                }
                else
                {
                    throw new ArgumentException("A file can only be created from a mount point or folder and the specified path does not represent one");
                }
            }
            else
            {
                WriteError(new ErrorRecord(new ArgumentException("Type must be one of the following: device, mountpoint or folder"),
                                            "CannotCreateSpecifiedObject", ErrorCategory.InvalidArgument, path));
                throw new ArgumentException("This provider can only create items of types \"device\", \"mountpoint\" and \"folder\"");
            }
        }
        protected override object NewItemDynamicParameters(string path, string itemTypeName, object newItemValue)
        {
            return new NewItemDynamicParameters();
        }
        protected override void RemoveItem(string path, bool recurse)
        {
            RemoveItemDynamicParameters parameters = DynamicParameters as RemoveItemDynamicParameters;
            if (PathIsDrive(path))
            {
                throw new ArgumentException("Remove not supported for this item");
            }
            Collection<JFSObject> pathObjects = JAFS.GetPathObjects(JAFSPath(path));
            JFSObject job = pathObjects.Count > 0 ? pathObjects[pathObjects.Count - 1] : null;
            if (job is JFSDevice)
            {
                JFSDevice dev = job as JFSDevice;
                if (parameters.Permanent)
                {
                    if (ShouldProcess(path, "RemoveItem"))
                    {
                        JAFS.DeleteDevicePermanently(dev);
                    }
                }
                else
                {
                    throw new ArgumentException("Devices can only be removed permanently");
                }
            }
            else if (job is JFSMountPoint)
            {
                JFSMountPoint mnt = job as JFSMountPoint;
                if (parameters.Permanent)
                {
                    JFSDevice dev = pathObjects.Count > 1 ? pathObjects[pathObjects.Count - 2] as JFSDevice : null; // Parent is the device
                    if (dev == null)
                    {
                        throw new ArgumentException("Failed to find parent item needed for permanent remove");
                    }
                    if (ShouldProcess(path, "RemoveItem"))
                    {
                        dev.DeleteMountPointPermanently(mnt);
                    }
                }
                else
                {
                    if (ShouldProcess(path, "RemoveItem"))
                    {
                        mnt.Delete();
                    }
                }
            }
            else if (job is JFSFolder)
            {
                JFSFolder fol = job as JFSFolder;
                if (parameters.Permanent)
                {
                    JFSObject parent = pathObjects.Count > 1 ? pathObjects[pathObjects.Count - 2] : null;
                    if (parent is JFSMountPoint)
                    {
                        JFSMountPoint mnt = parent as JFSMountPoint;
                        if (mnt == null)
                        {
                            throw new ArgumentException("Failed to find parent item needed for permanent remove");
                        }
                        if (ShouldProcess(path, "RemoveItem"))
                        {
                            mnt.DeleteFolderPermanently(fol);
                        }
                    }
                    else if (parent is JFSFolder)
                    {
                        JFSFolder pf = parent as JFSFolder;
                        if (pf == null)
                        {
                            throw new ArgumentException("Failed to find parent item needed for permanent remove");
                        }
                        if (ShouldProcess(path, "RemoveItem"))
                        {
                            pf.DeleteFolderPermanently(fol);
                        }
                    }
                }
                else
                {
                    if (ShouldProcess(path, "RemoveItem"))
                    {
                        fol.Delete();
                    }
                }
            }
            else if (job is JFSFile)
            {
                JFSFile fil = job as JFSFile;
                if (parameters.Permanent)
                {
                    JFSObject parent = pathObjects.Count > 1 ? pathObjects[pathObjects.Count - 2] : null;
                    if (parent is JFSMountPoint)
                    {
                        JFSMountPoint mnt = parent as JFSMountPoint;
                        if (mnt == null)
                        {
                            throw new ArgumentException("Failed to find parent item needed for permanent remove");
                        }
                        if (ShouldProcess(path, "RemoveItem"))
                        {
                            mnt.DeleteFilePermanently(fil);
                        }
                    }
                    else if (parent is JFSFolder)
                    {
                        JFSFolder pf = parent as JFSFolder;
                        if (pf == null)
                        {
                            throw new ArgumentException("Failed to find parent item needed for permanent remove");
                        }
                        if (ShouldProcess(path, "RemoveItem"))
                        {
                            pf.DeleteFilePermanently(fil);
                        }
                    }
                }
                else
                {
                    if (ShouldProcess(path, "RemoveItem"))
                    {
                        fil.Delete();
                    }
                }
            }
            else
            {
                throw new ArgumentException("Remove not supported for this item");
            }
        }
        protected override object RemoveItemDynamicParameters(string path, bool recurse)
        {
            return new RemoveItemDynamicParameters();
        }
        protected override void RenameItem(string path, string newName)
        {
            if (PathIsDrive(path))
            {
                WriteItemObject(this.PSDriveInfo, path, true);
            }
            Collection<JFSObject> pathObjects = JAFS.GetPathObjects(JAFSPath(path));
            JFSObject job = pathObjects.Count > 0 ? pathObjects[pathObjects.Count - 1] : null;
            if (job is JFSFolder)
            {
                JFSFolder fol = job as JFSFolder;
                if (ShouldProcess(path, "RenameItem"))
                {
                    WriteItemObject(fol, path, false);
                    fol.Rename(newName);
                }
            }
            else if (job is JFSFile)
            {
                JFSFile fil = job as JFSFile;
                if (ShouldProcess(path, "RenameItem"))
                {
                    WriteItemObject(fil, path, false);
                    fil.Rename(newName);
                }
            }
            else
            {
                throw new ArgumentException("Rename not supported for this item");
            }
            WriteItemObject(job, path, true);
        }
        //protected virtual object RenameItemDynamicParameters(string path, string newName);

        #endregion

        #region NavigationCmdletProvider

        //protected virtual string GetChildName(string path);
        //protected virtual string GetParentPath(string path, string root);
        protected override bool IsItemContainer(string path)
        {
            if (PathIsDrive(path))
            {
                return true;
            }
            Collection<JFSObject> pathObjects = JAFS.GetPathObjects(JAFSPath(path));
            JFSObject job = pathObjects.Count > 0 ? pathObjects[pathObjects.Count - 1] : null;
            return job != null && !(job is JFSBasicFile);
        }
        //protected virtual string MakePath(string parent, string child);
        //protected string MakePath(string parent, string child, bool childIsLeaf);
        protected override void MoveItem(string path, string destination)
        {
            if (PathIsDrive(path))
            {
                WriteItemObject(this.PSDriveInfo, path, true);
            }
            Collection<JFSObject> pathObjects = JAFS.GetPathObjects(JAFSPath(path));
            JFSObject job = pathObjects.Count > 0 ? pathObjects[pathObjects.Count - 1] : null;
            if (job is JFSFolder)
            {
                JFSFolder fol = job as JFSFolder;
                if (ShouldProcess(path, "MoveItem"))
                {
                    WriteItemObject(fol, path, false);
                    fol.Move(JAFSPath(destination));
                }
            }
            else if (job is JFSFile)
            {
                JFSFile fil = job as JFSFile;
                if (ShouldProcess(path, "MoveItem"))
                {
                    WriteItemObject(fil, path, false);
                    fil.Move(JAFSPath(destination));
                }
            }
            else
            {
                ArgumentException e = new ArgumentException("Move not supported for this item");
                //WriteError(new ErrorRecord(e, "MoveNotSupported", ErrorCategory.InvalidArgument, path));
                throw e;
            }
            WriteItemObject(job, path, true);
        }
        //protected virtual object MoveItemDynamicParameters(string path, string destination);
        //protected virtual string NormalizeRelativePath(string path, string basePath);


        #region IContentCmdletProvider

        public void ClearContent(string path)
        {
            // TODO!
        }
        public object ClearContentDynamicParameters(string path)
        {
            return null;
        }
        public IContentReader GetContentReader(string path)
        {
            return new JottacloudPSContentReaderWriter(path, this);
        }
        public object GetContentReaderDynamicParameters(string path)
        {
            return null;
        }
        public IContentWriter GetContentWriter(string path)
        {
            return new JottacloudPSContentReaderWriter(path, this);
        }
        public object GetContentWriterDynamicParameters(string path)
        {
            return null;
        }

        #endregion

        #endregion

        #region Helper Methods

        private Jottacloud JAFS
        {
            get
            {
                JottacloudPSDriveInfo jedi = this.PSDriveInfo as JottacloudPSDriveInfo;
                if (jedi == null)
                {
                    return null;
                }
                else
                {
                    return jedi.JAFS;
                }
            }
        }

        // Adapts the path, making sure the correct path separator
        private string JAFSPath(string path)
        {
            string result = path;

            if (!String.IsNullOrEmpty(path))
            {
                result = path.Replace(PSPathSeparator, JAFSPathSeparator);
            }
            return result;
        }

        //Checks if a given path is actually a drive name.
        private bool PathIsDrive(string path)
        {
            // Remove the drive name and first path separator. If the path is reduced to nothing,
            // it is a drive. Also if its just a drive then there wont be any path separators
            return String.IsNullOrEmpty(path.Replace(this.PSDriveInfo.Root, "")) ||
                   String.IsNullOrEmpty(path.Replace(this.PSDriveInfo.Root + PSPathSeparator, ""));
        }

        public Collection<JFSObject> GetPathObjects(string path)
        {
            return JAFS.GetPathObjects(JAFSPath(path));
        }

        #region Private Properties

        private string PSPathSeparator = "\\";
        private string JAFSPathSeparator = "/"; // JAFS uses forward slashes, mapping them directly to REST API URLs

        #endregion Private Properties

        #endregion

    }

    #endregion

    #region Helper classes

    #region Drive info class

    internal class JottacloudPSDriveInfo : PSDriveInfo
    {
        Jottacloud jafs;
        public JottacloudPSDriveInfo(PSDriveInfo driveInfo) : base(driveInfo)
        {
        }
        public Jottacloud JAFS
        {
            get { return this.jafs; }
            set { this.jafs = value; }
        }
    }

    #endregion

    #region Dynamic parameter classes

    internal sealed class NewItemDynamicParameters
    {
        [Parameter()]
        public JFSData.JFSDataDeviceType DeviceType { get; set; }
    }
    internal sealed class RemoveItemDynamicParameters
    {
        [Parameter()]
        public SwitchParameter Permanent { get; set; }
    }

    #endregion

    #region Content reader/writer

    internal class JottacloudPSContentReaderWriter : IContentReader, IContentWriter
    {
        private JottacloudPSProvider provider;
        private string path;
        private ulong currentOffset;

        internal JottacloudPSContentReaderWriter(string path, JottacloudPSProvider provider)
        {
            this.path = path;
            this.provider = provider;
        }

        public IList Read(long readCount)
        {
            ArrayList blocks = new ArrayList();
            Collection<JFSObject> pathObjects = provider.GetPathObjects(path);
            JFSObject job = pathObjects.Count > 0 ? pathObjects[pathObjects.Count - 1] : null;
            if (job is JFSFile)
            {
                JFSFile fil = job as JFSFile;
                if (currentOffset > fil.SizeInBytes)
                {
                    return null;
                }
                if (currentOffset == 0 && readCount <= 0)
                {
                    // Read everything
                    blocks.AddRange(fil.Read().ToCharArray());
                }
                else
                {
                    ulong endPos = fil.SizeInBytes;
                    if (readCount > 0)
                    {
                        endPos = currentOffset + (ulong)readCount;
                        if (endPos > fil.SizeInBytes)
                        {
                            endPos = fil.SizeInBytes;
                        }
                    }
                    blocks.AddRange(fil.ReadPartial(currentOffset, endPos).ToCharArray());
                }
                return blocks;
            }
            else
            {
                return null;
            }
        }

        public IList Write(IList content)
        {
            Collection<JFSObject> pathObjects = provider.GetPathObjects(path);
            JFSObject job = pathObjects.Count > 0 ? pathObjects[pathObjects.Count - 1] : null;
            if (job is JFSFile)
            {
                JFSFile fil = job as JFSFile;
                Collection<byte> bytes = new Collection<byte>();
                foreach (var item in content)
                {
                    string line = (item as string);
                    if (line != null)
                    {
                        foreach (byte b in Encoding.UTF8.GetBytes(line))
                        {
                            bytes.Add(b);
                        }
                    }
                }
                byte[] rawBytes = new byte[bytes.Count];
                bytes.CopyTo(rawBytes, 0);
                fil.Write(rawBytes);
            }
            return null;
        }

        public void Seek(long offset, SeekOrigin origin)
        {
            if (origin == System.IO.SeekOrigin.Begin)
            {
                if (offset > 0)
                {
                    currentOffset = (ulong)(offset - 1);
                }
                else
                {
                    currentOffset = 0;
                }
            }
            else if (origin == System.IO.SeekOrigin.End)
            {
                Collection<JFSObject> pathObjects = provider.GetPathObjects(path);
                JFSObject job = pathObjects.Count > 0 ? pathObjects[pathObjects.Count - 1] : null;
                if (job is JFSFile)
                {
                    JFSFile fil = job as JFSFile;
                    if (fil.SizeInBytes <= 1)
                    {
                        currentOffset = 0;
                    }
                    else if (offset > 0)
                    {
                        if ((ulong)offset >= fil.SizeInBytes)
                        {
                            currentOffset = 0;
                        }
                        else
                        {
                            currentOffset = fil.SizeInBytes - 1 - (ulong)offset;
                        }
                    }
                    else
                    {
                        currentOffset = 0;
                    }
                }
                else
                {
                    currentOffset = 0;
                }
            }
            else
            {
                if (offset < 0)
                {
                    if ((ulong)(-1*offset) >= currentOffset)
                    {
                        currentOffset = 0;
                    }
                    else
                    {
                        currentOffset += (ulong)offset;
                    }
                }
                else
                {
                    currentOffset += (ulong)offset;
                }
            }
        }
        public void Close()
        {
            Dispose();
        }
        public void Dispose()
        {
            Seek(0, System.IO.SeekOrigin.Begin);
            GC.SuppressFinalize(this);
        }
    }

    #endregion


    #endregion
}
