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

        [XmlElement("publicURI")]
        public string PublicURI { get; set; } // Only present when enabling sharing of a file (and then path and abspath are not present).

        [XmlElement("path")]
        public JFSDataStringWithWhiteSpaceHandling PathData { get; set; }

        [XmlIgnore()]
        public override string Path { get { return PathData.String; } } // Implementation of JFSNamedAndPathedRootObjectData

        [XmlElement("abspath")]
        public JFSDataStringWithWhiteSpaceHandling AbsolutePathData { get; set; }

        [XmlElement("currentRevision")] // Normal, completed files have this
        public JSFileRevisionData CurrentRevision { get; set; }

        [XmlElement("latestRevision")] // Incomplete or, possibly, corrupt files have this
        public JSFileRevisionData LatestRevision { get; set; }

        [XmlElement("revision")]
        public JSFileRevisionData Revision { get; set; } // Corrupt files have this

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
