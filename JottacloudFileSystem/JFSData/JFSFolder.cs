using System;
using System.Xml.Serialization;

namespace JFSData
{

    [Serializable()]
    [XmlType(AnonymousType = true)]
    [XmlRoot(ElementName = "folder", Namespace = "", IsNullable = false)] // Root element in XML response from query "https://www.jotta.no/jfs/[username]/[device]/[mountpoint]/[folder]".
    public partial class JFSFolderData : JFSFolderBaseData
    {
        [XmlAttribute("name")]
        public string NameData { get; set; }

        [XmlIgnore()]
        public override string Name { get { return NameData; } } // Implementation of JFSNamedRootObjectData

        [XmlAttribute("deleted")] // If and only if the folder is deleted it will have the timestamp in a "deleted" attribute.
        public string DeletedString { get; set; }

        [XmlIgnore()]
        public DateTime? Deleted
        {
            get
            {
                if (DeletedString != null) { return JFSDataUtilities.ToDateTime(DeletedString); }
                else { return null; }
            }
        }

        [XmlElement("path")]
        public JFSDataStringWithWhiteSpaceHandling PathData { get; set; }

        [XmlIgnore()]
        public override string Path { get { return PathData.String; } } // Implementation of JFSNamedAndPathedRootObjectData

        [XmlElement("abspath")]
        public JFSDataStringWithWhiteSpaceHandling AbsolutePathData { get; set; }
    }

    [Serializable()]
    [XmlType(AnonymousType = true)]
    public partial class JFSFolderMetaData
    {
        [XmlIgnore()] // Not in use? It is only an empty string, and that makes the parsing fail!
        [XmlAttribute(AttributeName = "first", DataType = "unsignedLong")]
        public ulong First { get; set; }

        [XmlIgnore()] // Not in use? It is only an empty string, and that makes the parsing fail!
        [XmlAttribute(AttributeName = "max", DataType = "unsignedLong")]
        public ulong Max { get; set; }

        [XmlAttribute(AttributeName = "total", DataType = "unsignedLong")]
        public ulong Total { get; set; }

        [XmlAttribute(AttributeName = "num_folders", DataType = "unsignedLong")]
        public ulong NumberOfFolders { get; set; }

        [XmlAttribute(AttributeName = "num_files", DataType = "unsignedLong")]
        public ulong NumberOfFiles { get; set; }
    }

}
