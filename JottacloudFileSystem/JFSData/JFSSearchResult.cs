using System;
using System.Xml.Serialization;

namespace JFSData
{

    //
    // Wrapping the XML response from query "https://www.jotta.no/jfs/[username]/Jotta/Latest",
    // giving you a list of the n latest files (the server minimum default is 10), optionally sorted.
    // Parameters: {"sort":"updated", "max":n, "web":"true"}
    // Note that "Latest" is actually treated as a mount point, but it differs from the other mount points
    // in that it only has a list of files and not  properties of its own like name, size etc.
    // 

    [Serializable()]
    [XmlType(AnonymousType = true)]
    [XmlRoot(ElementName = "searchresult", Namespace = "", IsNullable = false)] // Root element in XML response from query "https://www.jotta.no/jfs/[username]/[device]", listing mount points.
    public partial class JFSSearchResultData : JFSRootObjectData
    {
        [XmlArray("files")]
        [XmlArrayItem("file", IsNullable = false)]
        public JFSFileData[] Files { get; set; }

        [XmlElement("metadata")]
        public JFSSearchResultMetaData Metadata { get; set; }
    }

    [Serializable()]
    [XmlType(AnonymousType = true)]
    public partial class JFSSearchResultMetaData
    {
        [XmlIgnore()] // Not in use? It is only an empty string, and that makes the parsing fail!
        [XmlAttribute(AttributeName = "first", DataType = "unsignedLong")]
        public ulong First { get; set; }

        [XmlIgnore()] // Not in use? It is only an empty string, and that makes the parsing fail!
        [XmlAttribute(AttributeName = "max", DataType = "unsignedLong")]
        public ulong Max { get; set; }

        [XmlAttribute(AttributeName = "total", DataType = "unsignedLong")]
        public ulong Total { get; set; }

        [XmlAttribute(AttributeName = "num_files", DataType = "unsignedLong")]
        public ulong NumberOfFiles { get; set; }
    }

}
