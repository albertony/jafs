using System;
using System.Xml.Serialization;

namespace Jottacloud.JFSData
{
    //
    // Root element in XML response from query "https://www.jotta.no/jfs/[username]/[device]",
    // listing devices. There is a built-in device called "Jotta" handling built-in functionality
    // like file synchronization, and the backup feature of the Jottacloud software may have
    // added additional devices for each device it is configured on.
    // 
    [Serializable()]
    [XmlType(AnonymousType = true)]
    [XmlRoot(ElementName = "device", Namespace = "", IsNullable = false)] // Root element in XML response from query "https://www.jotta.no/jfs/[username]/[device]", listing mount points.
    public partial class JFSDeviceData : JFSNamedRootObjectData
    {
        // First the basic attributes that are also filled for devices referred to from the JFSUser object.

        [XmlElement("name")]
        public JFSDataStringWithWhiteSpaceHandling NameData { get; set; }

        [XmlIgnore()]
        public override string Name { get { return NameData.String; } } // Implementation of JFSNamedRootObjectData

        [XmlElement("display_name")]
        public JFSDataStringWithWhiteSpaceHandling DisplayNameData { get; set; }

        [XmlElement("type")]
        public JFSDataDeviceType Type { get; set; }

        [XmlElement("sid")]
        public JFSDataGuid SID { get; set; }

        [XmlElement("modified")]
        public JFSDataDateTime Modified { get; set; }

        [XmlElement("size")]
        public JFSDataSize Size { get; set; } // Return int of storage used in bytes.

        // Then the advanced attributes, only present in the specific device requests.

        [XmlElement("user")]
        public string User { get; set; }

        [XmlElement("metadata")]
        public JFSDeviceMetaData Metadata { get; set; }

        [XmlArray("mountPoints")]
        [XmlArrayItem("mountPoint", IsNullable = false)]
        public JFSMountPointData[] MountPoints { get; set; } // NB: Only a subset of the properties (name, size and modified) of the mount points are used from here, for all properties one will have to query the individual mount points specifically.

    }

    [Serializable()]
    [XmlType(AnonymousType = true)]
    public partial class JFSDeviceMetaData
    {
        [XmlIgnore()] // Not in use? It is only an empty string, and that makes the parsing fail!
        [XmlAttribute(AttributeName = "first", DataType = "unsignedLong")]
        public ulong First { get; set; }

        [XmlIgnore()] // Not in use? It is only an empty string, and that makes the parsing fail!
        [XmlAttribute(AttributeName = "max", DataType = "unsignedLong")]
        public ulong Max { get; set; }

        [XmlAttribute(AttributeName = "total", DataType = "unsignedLong")]
        public ulong Total { get; set; }

        [XmlAttribute(AttributeName = "num_mountpoints", DataType = "unsignedLong")]
        public ulong NumberOfMountPoints { get; set; }
    }

    [Serializable()]
    [XmlType(AnonymousType = true)]
    public enum JFSDataDeviceType
    {
        // The first is a built-in default device (containing built-in mount points archive, sync etc).
        [XmlEnum("JOTTA")]
        BuiltIn,
        // The rest is types of devices for backup.
        [XmlEnum("WORKSTATION")]
        Workstation,
        [XmlEnum("LAPTOP")]
        Laptop,
        [XmlEnum("IMAC")]
        Imac,
        [XmlEnum("MACBOOK")]
        Macbook,
        [XmlEnum("IPAD")]
        Ipad,
        [XmlEnum("ANDROID")]
        Android,
        [XmlEnum("IPHONE")]
        Iphone,
        [XmlEnum("WINDOWS_PHONE")]
        WindowsPhone,
    }
}
