using JFSData;
using System.IO;
using System.Xml.Serialization;

namespace JFSDataParsingTests
{
    class Program
    {
        static void Main(string[] args)
        {
            string testFilesPath = @"..\..\TestFiles";
            if (args.Length > 0)
                testFilesPath = args[0];
            {
                JFSUserData obj = null;
                string xmlFilePath = testFilesPath + @"\User.xml";
                XmlSerializer serializer = new XmlSerializer(typeof(JFSUserData));
                StreamReader reader = new StreamReader(xmlFilePath);
                obj = (JFSUserData)serializer.Deserialize(reader);
                reader.Close();
            }
            {
                JFSDeviceData obj = null;
                string xmlFilePath = testFilesPath + @"\Device.xml";
                XmlSerializer serializer = new XmlSerializer(typeof(JFSDeviceData));
                StreamReader reader = new StreamReader(xmlFilePath);
                obj = (JFSDeviceData)serializer.Deserialize(reader);
                reader.Close();
            }
            {
                JFSMountPointData obj = null;
                string xmlFilePath = testFilesPath + @"\MountPoint.xml";
                XmlSerializer serializer = new XmlSerializer(typeof(JFSMountPointData));
                StreamReader reader = new StreamReader(xmlFilePath);
                obj = (JFSMountPointData)serializer.Deserialize(reader);
                reader.Close();
            }
            {
                JFSFolderData obj = null;
                string xmlFilePath = testFilesPath + @"\Folder.xml";
                XmlSerializer serializer = new XmlSerializer(typeof(JFSFolderData));
                StreamReader reader = new StreamReader(xmlFilePath);
                obj = (JFSFolderData)serializer.Deserialize(reader);
                reader.Close();
            }
            {
                JFSFileData obj = null;
                string xmlFilePath = testFilesPath + @"\File.xml";
                XmlSerializer serializer = new XmlSerializer(typeof(JFSFileData));
                StreamReader reader = new StreamReader(xmlFilePath);
                obj = (JFSFileData)serializer.Deserialize(reader);
                reader.Close();
            }
            {
                JFSFileData obj = null;
                string xmlFilePath = testFilesPath + @"\FileIncomplete.xml";
                XmlSerializer serializer = new XmlSerializer(typeof(JFSFileData));
                StreamReader reader = new StreamReader(xmlFilePath);
                obj = (JFSFileData)serializer.Deserialize(reader);
                reader.Close();
            }
            {
                JFSFileData obj = null;
                string xmlFilePath = testFilesPath + @"\FileCorrupt.xml";
                XmlSerializer serializer = new XmlSerializer(typeof(JFSFileData));
                StreamReader reader = new StreamReader(xmlFilePath);
                obj = (JFSFileData)serializer.Deserialize(reader);
                reader.Close();
            }
            {
                JFSFileDirListData obj = null;
                string xmlFilePath = testFilesPath + @"\FileDirList.xml";
                XmlSerializer serializer = new XmlSerializer(typeof(JFSFileDirListData));
                StreamReader reader = new StreamReader(xmlFilePath);
                obj = (JFSFileDirListData)serializer.Deserialize(reader);
                reader.Close();
            }
            {
                JFSSearchResultData obj = null;
                string xmlFilePath = testFilesPath + @"\SearchResult.xml";
                XmlSerializer serializer = new XmlSerializer(typeof(JFSSearchResultData));
                StreamReader reader = new StreamReader(xmlFilePath);
                obj = (JFSSearchResultData)serializer.Deserialize(reader);
                reader.Close();
            }
            {
                JFSEnableSharingData obj = null;
                string xmlFilePath = testFilesPath + @"\EnableSharing.xml";
                XmlSerializer serializer = new XmlSerializer(typeof(JFSEnableSharingData));
                StreamReader reader = new StreamReader(xmlFilePath);
                obj = (JFSEnableSharingData)serializer.Deserialize(reader);
                reader.Close();
            }

            {
                JFSFileData obj = null;
                string xmlFilePath = testFilesPath + @"\FileWithHistory.xml";
                XmlSerializer serializer = new XmlSerializer(typeof(JFSFileData));
                StreamReader reader = new StreamReader(xmlFilePath);
                obj = (JFSFileData)serializer.Deserialize(reader);
                reader.Close();
            }

        }
    }
}
