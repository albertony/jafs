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


//
// New-PSDrive -Name JAFS -PSProvider JottacloudPSProvider -Root . -Credential (Get-Credential)
//
namespace JottacloudPSProvider
{
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
    [CmdletProvider("JottacloudPSProvider", ProviderCapabilities.ShouldProcess | ProviderCapabilities.Credentials)]
    public class JottacloudPSProvider : NavigationCmdletProvider
    {
        #region DriveCmdletProvider

        //protected override Collection<PSDriveInfo> InitializeDefaultDrives()
        protected override PSDriveInfo NewDrive(PSDriveInfo drive)
        {
            // Check if the drive object is null.
            if (drive == null)
            {
                WriteError(new ErrorRecord(new ArgumentNullException("drive"), "NullDrive", ErrorCategory.InvalidArgument, null));
                return null;
            }
            // Check if the drive root is not null or empty and if it is valid.
            /*if (String.IsNullOrEmpty(drive.Root))
            {
                WriteError(new ErrorRecord(new ArgumentException("drive.Root"), "NoRoot", ErrorCategory.InvalidArgument, drive));
                return null;
            }*/
            // Check if credentials are specified
            if (drive.Credential == null)
            {
                WriteError(new ErrorRecord(new ArgumentException("drive.Credential"), "NoCredential", ErrorCategory.InvalidArgument, drive));
                return null;
            }
            // Create a new drive and create an ODBC connection to the new drive.
            JottacloudPSDriveInfo jedi = new JottacloudPSDriveInfo(drive);
            Jottacloud jafs = new Jottacloud((NetworkCredential)drive.Credential);
            jedi.JAFS = jafs;
            return jedi;
        }
        //protected override object NewDriveDynamicParameters();
        protected override PSDriveInfo RemoveDrive(PSDriveInfo drive)
        {
            // Check if drive object is null.
            if (drive == null)
            {
                WriteError(new ErrorRecord(new ArgumentNullException("drive"), "NullDrive", ErrorCategory.InvalidArgument, drive));
                return null;
            }
            // Close the JAFS connection to the drive.
            JottacloudPSDriveInfo jedi = drive as JottacloudPSDriveInfo;
            if (jedi == null)
            {
                return null;
            }
            //jedi.JAFS.Close();  // TODO: Currently no persistant connection!
            return jedi;
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
            JottacloudPSDriveInfo jedi = this.PSDriveInfo as JottacloudPSDriveInfo;
            try
            {
                Collection<JFSObject> pathObjects = jedi.JAFS.GetPathObjects(JAFSPath(path));
                JFSObject job = pathObjects.Count > 0 ? pathObjects[pathObjects.Count - 1] : null;
                bool isContainer = !(job is JFSBasicFile);
                WriteItemObject(job, path, isContainer);
            }
            catch (ArgumentException ex)
            {
                throw ex;
            }
        }
        //protected virtual object GetItemDynamicParameters(string path);
        //protected virtual void InvokeDefaultAction(string path);
        //protected virtual object InvokeDefaultActionDynamicParameters(string path);
        protected override bool IsValidPath(string path)
        {
            return true;
        }
        protected override bool ItemExists(string path)
        {
            if (PathIsDrive(path))
            {
                return true;
            }
            JottacloudPSDriveInfo jedi = this.PSDriveInfo as JottacloudPSDriveInfo;
            try
            {
                Collection<JFSObject> pathObjects = jedi.JAFS.GetPathObjects(JAFSPath(path));
                JFSObject job = pathObjects.Count > 0 ? pathObjects[pathObjects.Count - 1] : null;
                return job != null;
            }
            catch (ArgumentException ex)
            {
                return false;
            }
        }

        #endregion

        //protected virtual object ItemExistsDynamicParameters(string path);
        //protected virtual void SetItem(string path, object value);
        //protected virtual object SetItemDynamicParameters(string path, object value);

        #region ContainerCmdletProvider

        //protected virtual bool ConvertPath(string path, string filter, ref string updatedPath, ref string updatedFilter);
        //protected virtual void CopyItem(string path, string copyPath, bool recurse);
        //protected virtual object CopyItemDynamicParameters(string path, string destination, bool recurse);
        protected override void GetChildItems(string path, bool recurse)
        {
            JottacloudPSDriveInfo jedi = this.PSDriveInfo as JottacloudPSDriveInfo;
            if (PathIsDrive(path))
            {
                foreach (var deviceName in jedi.JAFS.GetDeviceNames())
                {
                    WriteItemObject(deviceName, path, true);
                }
            }
            try
            {
                Collection<JFSObject> pathObjects = jedi.JAFS.GetPathObjects(JAFSPath(path));
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
                        WriteItemObject(filName, path, true);
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
                        WriteItemObject(filName, path, true);
                    }
                }
                else if (job is JFSBasicFile)
                {
                    JFSBasicFile fil = job as JFSBasicFile;
                    WriteItemObject(fil.Name, path, true);
                }
                else
                {
                    throw new ArgumentException("Invalid path");
                }
            }
            catch (ArgumentException ex)
            {
                throw ex;
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
            JottacloudPSDriveInfo jedi = this.PSDriveInfo as JottacloudPSDriveInfo;
            Collection<JFSObject> pathObjects = jedi.JAFS.GetPathObjects(JAFSPath(path));
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
            JottacloudPSDriveInfo jedi = this.PSDriveInfo as JottacloudPSDriveInfo;
            if (String.Equals(itemTypeName, "device", StringComparison.OrdinalIgnoreCase))
            {
                if (PathIsDrive(path))
                {
                    if (ShouldProcess(itemName, "New device"))
                    {
                        jedi.JAFS.NewDevice(itemName, parameters.DeviceType);
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
                    Collection<JFSObject> pathObjects = jedi.JAFS.GetPathObjects(JAFSPath(path));
                    JFSDevice dev = pathObjects.Count > 0 ? pathObjects[pathObjects.Count - 1] as JFSDevice : null;
                    if (dev != null)
                    {
                        if (ShouldProcess(itemName, "New mount point"))
                        {
                            dev.NewMountpoint(itemName);
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
                    Collection<JFSObject> pathObjects = jedi.JAFS.GetPathObjects(JAFSPath(path));
                    JFSObject job = pathObjects.Count > 0 ? pathObjects[pathObjects.Count - 1] : null;
                    if (job is JFSMountPoint)
                    {
                        JFSMountPoint mnt = job as JFSMountPoint;
                        if (mnt != null)
                        {
                            if (ShouldProcess(itemName, "New folder"))
                            {
                                mnt.NewFolder(itemName);
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
                                pf.NewFolder(itemName);
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
                WriteItemObject(this.PSDriveInfo, path, true);
            }
            JottacloudPSDriveInfo jedi = this.PSDriveInfo as JottacloudPSDriveInfo;
            try
            {
                Collection<JFSObject> pathObjects = jedi.JAFS.GetPathObjects(JAFSPath(path));
                JFSObject job = pathObjects.Count > 0 ? pathObjects[pathObjects.Count - 1] : null;
                if (job is JFSDevice)
                {
                    JFSDevice dev = job as JFSDevice;
                    if (parameters.Permanent)
                    {
                        if (ShouldProcess(path, "RemoveItem"))
                        {
                            WriteItemObject(dev, path, false);
                            jedi.JAFS.DeleteDevicePermanently(dev);
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
                            WriteItemObject(mnt, path, false);
                            dev.DeleteMountPointPermanently(mnt);
                        }
                    }
                    else
                    {
                        if (ShouldProcess(path, "RemoveItem"))
                        {
                            WriteItemObject(mnt, path, false);
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
                                WriteItemObject(fol, path, false);
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
                                WriteItemObject(fol, path, false);
                                pf.DeleteFolderPermanently(fol);
                            }
                        }
                    }
                    else
                    {
                        if (ShouldProcess(path, "RemoveItem"))
                        {
                            WriteItemObject(fol, path, false);
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
                                WriteItemObject(fil, path, false);
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
                                WriteItemObject(fil, path, false);
                                pf.DeleteFilePermanently(fil);
                            }
                        }
                    }
                    else
                    {
                        if (ShouldProcess(path, "RemoveItem"))
                        {
                            WriteItemObject(fil, path, false);
                            fil.Delete();
                        }
                    }
                }
                else
                {
                    ArgumentException e = new ArgumentException("Rename not supported for this item");
                    //WriteError(new ErrorRecord(e, "MoveNotSupported", ErrorCategory.InvalidArgument, path));
                    throw e;
                }
            }
            catch (ArgumentException ex)
            {
                throw ex;
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
            JottacloudPSDriveInfo jedi = this.PSDriveInfo as JottacloudPSDriveInfo;
            try
            {
                Collection<JFSObject> pathObjects = jedi.JAFS.GetPathObjects(JAFSPath(path));
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
                    ArgumentException e = new ArgumentException("Rename not supported for this item");
                    //WriteError(new ErrorRecord(e, "MoveNotSupported", ErrorCategory.InvalidArgument, path));
                    throw e;
                }
                WriteItemObject(job, path, true);
            }
            catch (ArgumentException ex)
            {
                throw ex;
            }
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
            JottacloudPSDriveInfo jedi = this.PSDriveInfo as JottacloudPSDriveInfo;
            Collection<JFSObject> pathObjects = jedi.JAFS.GetPathObjects(JAFSPath(path));
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
            JottacloudPSDriveInfo jedi = this.PSDriveInfo as JottacloudPSDriveInfo;
            try
            {
                Collection<JFSObject> pathObjects = jedi.JAFS.GetPathObjects(JAFSPath(path));
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
            catch (ArgumentException ex)
            {
                throw ex;
            }
        }
        //protected virtual object MoveItemDynamicParameters(string path, string destination);
        //protected virtual string NormalizeRelativePath(string path, string basePath);

        #endregion

        #region Helper Methods

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
        } // PathIsDrive

        #region Private Properties

        private string PSPathSeparator = "\\";
        private string JAFSPathSeparator = "/"; // JAFS uses forward slashes, mapping them directly to REST API URLs

        #endregion Private Properties

        #endregion

    }
}
