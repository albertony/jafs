using System;
using System.Xml.Serialization;

namespace JFSData
{

    //
    // Wrapping the XML response from requesting a file to be shared, by giving public access to it
    // via a secret. The result is a file object similar to the regular file objects, with currentRevision
    // sub-element, but with a couple of differences: It does not have the path and abspath elements,
    // and it has an additional publicURI element containing the shared secret.
    //
    [Serializable()]
    [XmlType(AnonymousType = true)]
    [XmlRoot(ElementName = "enableSharing", Namespace = "", IsNullable = false)]
    public partial class JFSEnableSharingData : JFSRootObjectData
    {
        [XmlArray("files")]
        [XmlArrayItem("file", IsNullable = false)]
        public JFSFileData[] Files { get; set; }
    }
}
