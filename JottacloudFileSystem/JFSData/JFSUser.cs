using System;
using System.Xml.Serialization;

namespace JFSData
{
    //
    // Wrapping the XML response from query "https://www.jotta.no/jfs/[username]",
    // listing all devices.
    //
    [Serializable()]
    [XmlType(AnonymousType = true)]
    [XmlRoot(ElementName = "user", Namespace = "", IsNullable = false)]
    public partial class JFSUserData : JFSNamedRootObjectData
    {
        [XmlElement("username")]
        public string Username { get; set; }

        [XmlIgnore()]
        public override string Name { get { return Username; } } // Implementation of JFSNamedRootObjectData

        [XmlElement("account-type")] // TODO: Make enum, but what are the possible values? "unlimited" is one of them.
        public string AccountType { get; set; }

        [XmlElement("enable-sync", DataType = "boolean")]
        public bool EnableSync{ get; set; }

        [XmlElement("locked", DataType = "boolean")]
        public bool Locked { get; set; }

        [XmlElement("read-locked", DataType = "boolean")]
        public bool ReadLocked { get; set; }

        [XmlElement("write-locked", DataType = "boolean")]
        public bool WriteLocked { get; set; }

        [XmlElement("quota-write-locked", DataType = "boolean")]
        public bool QuotaWriteLocked { get; set; }

        [XmlElement("max-devices")]
        public JFSDataCapacity MaxDevices { get; set; } // Number of devices, with special handling of -1 for meaning unlimited.

        [XmlElement("max-mobile-devices")]
        public JFSDataCapacity MaxMobileDevices { get; set; } // Number of mobile devices, with special handling of -1 for meaning unlimited.

        [XmlElement("capacity")]
        public JFSDataCapacity Capacity { get; set; } // Return storage capacity in bytes, with special handling of -1 for meaning unlimited.

        [XmlElement("usage")]
        public JFSDataSize Usage { get; set; } // Return storage used in bytes.

        [XmlArray("devices")]
        [XmlArrayItem("device", IsNullable = false)]
        public JFSDeviceData[] Devices { get; set; } // NB: Only a subset of the properties (name, display_name, type, sid, size and modified) of the devices are used from here, for all properties one will have to query the individual devices specifically.

    }
}
