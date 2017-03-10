using System;
using System.Xml.Serialization;

namespace JFSData
{
    [Serializable()]
    [XmlType(AnonymousType = true)]
    [XmlRoot(ElementName = "file", Namespace = "", IsNullable = false)] // Root element in XML response from query "https://www.jotta.no/jfs/[username]/[device]/[mountpoint]/[folderpath.../][file]".
    public partial class JFSFileData : JFSNamedAndPathedRootObjectData
    {
        [XmlAttribute("name")]
        public string NameData { get; set; }

        [XmlIgnore()]
        public override string Name { get { return NameData; } } // Implementation of JFSNamedRootObjectData

        [XmlAttribute("uuid")]
        public string UUIDString { get; set; }

        [XmlIgnore()]
        public Guid UUID
        {
            get { return JFSDataUtilities.ToGuid(UUIDString); }
            set { UUIDString = JFSDataUtilities.FromGuid(value); }
        }

        [XmlAttribute("deleted")] // If and only if the file is deleted it will have the timestamp in a "deleted" attribute.
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

        [XmlAttribute("contextType")] // When it is deleted but is in trash it will have attribute contextType="TRASH"
        public string ContextType { get; set; }

        [XmlElement("publicURI")]
        public string PublicURI { get; set; } // Only present when enabling sharing of a file (and then path and abspath are not present).

        [XmlElement("path")]
        public JFSDataStringWithWhiteSpaceHandling PathData { get; set; }

        [XmlIgnore()]
        public override string Path { get { return PathData != null ? PathData.String : null; } } // Implementation of JFSNamedAndPathedRootObjectData

        [XmlElement("abspath")] // For deleted folders that are in trash this will contain the original location, while path is the location within trash. For files not in trash this is identical to path.
        public JFSDataStringWithWhiteSpaceHandling OriginalPathData { get; set; }

        [XmlIgnore()]
        public override string OriginalPath { get { return OriginalPathData != null ? OriginalPathData.String : null; } } // Implementation of JFSNamedAndPathedRootObjectData

        [XmlElement("currentRevision")] // The latest successful, completed revision of the file.
        public JSFileRevisionData CurrentRevision { get; set; }

        [XmlElement("latestRevision")] // Incomplete and corrupt files have this, and it represents the latest unsuccessful upload. There might also be a CurrentRevision representing an older successful upload.
        public JSFileRevisionData LatestRevision { get; set; }

        [XmlArray("revisions")]
        [XmlArrayItem("revision", IsNullable = false)]
        public JSFileRevisionData[] OldRevisions { get; set; }

    }

    [Serializable()]
    [XmlType(AnonymousType = true)]
    public partial class JSFileRevisionData
    {
        [XmlElement("number", DataType = "unsignedLong")]
        public ulong Number { get; set; }

        [XmlElement("state")]
        public JFSDataFileState State { get; set; }

        [XmlElement("created")]
        public JFSDataDateTime Created { get; set; }

        [XmlElement("modified")]
        public JFSDataDateTime Modified { get; set; }

        [XmlElement("updated")]
        public JFSDataDateTime Updated { get; set; }

        [XmlElement("mime")]
        public JFSDataMime Mime { get; set; }

        [XmlElement("mstyle")]
        public JFSDataMime MimeStyle { get; set; }

        [XmlElement("size")]
        public JFSDataSize Size { get; set; }

        [XmlElement("md5")]
        public string MD5 { get; set; }
    }

    [Serializable()]
    [XmlType(AnonymousType = true)]
    public enum JFSDataFileState
    {
        [XmlEnum("INCOMPLETE")]
        Incomplete,
        [XmlEnum("ADDED")]
        Added,
        [XmlEnum("PROCESSING")]
        Processing,
        [XmlEnum("COMPLETED")]
        Completed,
        [XmlEnum("CORRUPT")]
        Corrupt,
    }
}
