using System;
using System.Xml.Serialization;

namespace JFSData
{
    //
    // Root element in XML response from query "https://www.jotta.no/jfs/[username]/[device]/[mountpoint]",
    // listing files and folders for a specified mount point.
    // 
    [Serializable()]
    [XmlType(AnonymousType = true)]
    [XmlRoot(ElementName = "mountPoint", Namespace = "", IsNullable = false)]
    public partial class JFSMountPointData : JFSFolderBaseData
    {

        // First the basic attributes that are also filled for devices referred to from the JFSUser object.

        [XmlElement("name")]
        public JFSDataStringWithWhiteSpaceHandling NameData { get; set; }

        [XmlIgnore()]
        public override string Name { get { return NameData.String; }  } // Implementation of JFSNamedRootObjectData

        [XmlElement("deleted")]
        public JFSDataDateTime Deleted { get; set; }

        [XmlElement("size")]
        public JFSDataSize Size { get; set; } // Return int of storage used in bytes.

        [XmlElement("modified")]
        public JFSDataDateTime Modified { get; set; }

        // Then the advanced attributes, only present in the specific device requests.

        [XmlElement("path")]
        public JFSDataStringWithWhiteSpaceHandling PathData { get; set; }

        [XmlIgnore()]
        public override string Path { get { return PathData.String; } } // Implementation of JFSNamedAndPathedRootObjectData

        [XmlElement("abspath")]
        public JFSDataStringWithWhiteSpaceHandling AbsolutePathData { get; set; }

        [XmlElement("user")]
        public string User { get; set; }

        [XmlElement("device")]
        public string Device { get; set; }
    }
}
