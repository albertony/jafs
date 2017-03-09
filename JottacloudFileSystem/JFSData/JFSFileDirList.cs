using System;
using System.Xml.Serialization;

namespace JFSData
{

    //
    // Wrapping the XML response from query "https://www.jotta.no/jfs/[username]/[device]/[mountpoint]?mode=list",
    // or "https://www.jotta.no/jfs/[username]/[device]/[mountpoint]/[folder]?mode=list",
    // giving you the contents of all folders from this level. It is not a regular folder tree structure like
    // in Windows Explorer, but it is a list of folders (including the specified root folder) with contents.
    // Without the "mode=list" parameter you get the contents of the specified folder only (immediate files
    // and name of sub-folders).
    //

    [Serializable()]
    [XmlType(AnonymousType = true)]
    [XmlRoot(ElementName = "filedirlist", Namespace = "", IsNullable = false)]
    public partial class JFSFileDirListData : JFSRootObjectData
    {
        [XmlArray("folders")]
        [XmlArrayItem("folder", IsNullable = false)]
        public JFSFolderData[] Folders { get; set; }  // Folder objects without metadata and with only file, since all folders are represented individually..
    }
}
