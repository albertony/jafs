using System;
using System.Globalization;
using System.Xml.Serialization;

namespace JFSData
{
    //
    // Base class for all XML responses, containing attributes shared by all root elements.
    //
    [Serializable()]
    [XmlType(AnonymousType = true)]
    public partial class JFSRootObjectData
    {
        [XmlAttribute("host")]
        public string Host { get; set; }

        [XmlAttribute("time")]
        public string TimeString { get; set; }

        [XmlIgnore()]
        public DateTime Time
        {
            get { return JFSDataUtilities.ToDateTime(TimeString); }
            set { TimeString = JFSDataUtilities.FromDateTime(value); }
        }
    }

    public abstract class JFSNamedRootObjectData
    {
        [XmlIgnore()]
        public abstract string Name { get; }
    }

    public abstract class JFSNamedAndPathedRootObjectData : JFSNamedRootObjectData
    {
        [XmlIgnore()]
        public abstract string Path { get; }

        [XmlIgnore()]
        public abstract string OriginalPath { get; }

    }

    //
    // Abstract base class for common parts of folders and mount points, which are quite similar.
    //
    public abstract class JFSFolderBaseData : JFSNamedAndPathedRootObjectData
    {
        [XmlElement("metadata")]
        public JFSFolderMetaData Metadata { get; set; }

        [XmlArray("folders")]
        [XmlArrayItem("folder", IsNullable = false)]
        public JFSFolderData[] Folders { get; set; }

        [XmlArray("files")]
        [XmlArrayItem("file", IsNullable = false)]
        public JFSFileData[] Files { get; set; }
    }

    //
    // Helper class for elements containing a string value with xml:space attribute specified, typically "preserve" for preserving whitespace.
    //
    [Serializable()]
    [XmlType(AnonymousType = true)]
    public partial class JFSDataStringWithWhiteSpaceHandling
    {

        [XmlAttribute(AttributeName = "space", Form = System.Xml.Schema.XmlSchemaForm.Qualified, Namespace = "http://www.w3.org/XML/1998/namespace")]
        public string Space { get; set; }
        [XmlText()]
        public string String { get; set; }
        public override string ToString() { return String; }
    }

    //
    // Helper class for size elements which are unsigned integers.
    //
    [Serializable()]
    [XmlType(AnonymousType = true)]
    public partial class JFSDataSize
    {

        [XmlText()]
        public string String { get; set; }
        public override string ToString() { return JFSDataUtilities.HumanizeDataSize(Value); }
        public ulong Value { get { return ulong.Parse(String); } }
        static public implicit operator ulong(JFSDataSize o) { return o.Value; }
    }

    //
    // Helper class for size elements which are basically unsigned integers but can be -1 when unlimited or N/A.
    //
    [Serializable()]
    [XmlType(AnonymousType = true)]
    public partial class JFSDataCapacity
    {

        [XmlText()]
        public string String { get; set; }
        public override string ToString() { return Unlimited ? "Unlimited" : JFSDataUtilities.HumanizeDataSize(Value); }
        public bool Unlimited { get { return String.Trim() == "-1"; } }
        public ulong Value { get { return Unlimited ? ulong.MaxValue : ulong.Parse(String); } }
        static public implicit operator ulong(JFSDataCapacity o) { return o.Value; }
    }

    //
    // Helper class for elements containing date/time string.
    //
    [Serializable()]
    [XmlType(AnonymousType = true)]
    public partial class JFSDataDateTime
    {
        [XmlText()]
        public string String { get; set; }

        public DateTime? DateTime
        {
            get
            {
                if (String != null) { return JFSDataUtilities.ToDateTime(String); }
                else { return null; }
            }
        }
        static public implicit operator DateTime? (JFSDataDateTime o) { return o.DateTime; }
    }

    //
    // Helper class for elements containing GUID string.
    //
    [Serializable()]
    [XmlType(AnonymousType = true)]
    public partial class JFSDataGuid
    {
        [XmlText()]
        public string String { get; set; }

        [XmlIgnore()]
        public Guid Guid
        {
            get { return JFSDataUtilities.ToGuid(String); }
            set { this.String = JFSDataUtilities.FromGuid(value); }
        }
        static public implicit operator Guid (JFSDataGuid o) { return o.Guid; }
    }

    //
    // Helper class for elements containing MIME string.
    //
    [Serializable()]
    [XmlType(AnonymousType = true)]
    public partial class JFSDataMime
    {
        [XmlText()]
        public string String { get; set; }

        [XmlIgnore()]
        public System.Net.Mime.ContentType Mime
        {
            get { return JFSDataUtilities.ToMime(String); }
            set { this.String = JFSDataUtilities.FromMime(value); }
        }
        static public implicit operator System.Net.Mime.ContentType(JFSDataMime o) { return o.Mime; }
    }

    //
    // Utility class for serialization of some special types.
    // Used by the helper classes for elements below, but since those helper
    // classes cannot be used for XmlAttributes the workaround was to split
    // out the basic serialization implementations into a shared helper class here.
    //
    public class JFSDataUtilities
    {
        public const string DateTimeFormat = "yyyy'-'MM'-'dd-'T'HH':'mm':'ssK";
        public static string FromDateTime(DateTime value)
        {
            return value.ToUniversalTime().ToString(DateTimeFormat);
        }
        public static DateTime ToDateTime(string value)
        {
            return DateTime.ParseExact(value, DateTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);
        }
        public static Guid ToGuid(string value)
        {
            return System.Guid.Parse(value);
        }
        public static string FromGuid(Guid value)
        {
            return value.ToString();
        }
        public static System.Net.Mime.ContentType ToMime(string value)
        {
            return new System.Net.Mime.ContentType(value);
        }
        public static string FromMime(System.Net.Mime.ContentType value)
        {
            return value.MediaType;
        }
        public static string HumanizeDataSize(ulong size)
        {
            string[] sizes = { "B", "KiB", "MiB", "GiB", "TiB", "PiB", "EiB", "ZiB", "YiB" };
            ushort order = 0;
            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size = size / 1024;
            }
            // Adjust the format string to your preferences. For example "{0:0.#}{1}" would
            // show a single decimal place, and no space.
            return string.Format("{0:0.##} {1}", size, sizes[order]);
        }
    }
}
