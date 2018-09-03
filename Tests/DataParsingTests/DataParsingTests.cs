using System;
using System.IO;
using System.Net;
using System.Xml.Serialization;
using JFSData;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DataParsingTests
{
    [TestClass]
    public class DataParsingTests
    {
        private static string TestFilesDirectory = @"..\..\TestFiles";

        public static void Parse<DataObjectType>(string file)
        {
            string filePath = Path.Combine(TestFilesDirectory, file);
            if (!File.Exists(filePath))
                throw new System.ArgumentException(string.Format("Test file {0} does not exist", filePath));
            DataObjectType obj = default(DataObjectType);
            XmlSerializer serializer = new XmlSerializer(typeof(DataObjectType));
            using (StreamReader reader = new StreamReader(filePath))
                obj = (DataObjectType)serializer.Deserialize(reader); // Throws exception if XML cannot be parsed!
        }

        [TestMethod]
        public void TestParseAllXmlFiles()
        {
            // This test will fail if any of the parsing operations throws an exception!

            if (!Directory.Exists(TestFilesDirectory))
                throw new System.ArgumentException(string.Format("Test file directory {0} does not exist", TestFilesDirectory));
            Parse<JFSUserData>("User.xml");
            Parse<JFSDeviceData>("Device.xml");
            Parse<JFSMountPointData>("MountPoint.xml");
            Parse<JFSMountPointData>("MountPointCreatePostResponse.xml");
            Parse<JFSDeviceData>("MountPointDeletePermanentPostResponse.xml"); // Permanent delete of mount point returns the owner device object!

            Parse<JFSFolderData>("Folder.xml");
            Parse<JFSFolderData>("FolderDeleted.xml");
            Parse<JFSFolderData>("FolderWithCorruptAndIncompleteFiles.xml");

            Parse<JFSFileData>("File.xml");
            Parse<JFSFileData>("FileCorrupt.xml");
            Parse<JFSFileData>("FileIncomplete.xml");
            Parse<JFSFileData>("FileWithHistory.xml");
            Parse<JFSFileData>("FileDeleted.xml");
            Parse<JFSFileData>("FileDeletePostResponse.xml");
            Parse<JFSFileData>("FileEnableShare.xml");
            Parse<JFSFileData>("FileDisableShare.xml");
            Parse<JFSFileData>("FileMovePostResponse.xml");
            Parse<JFSFileData>("FileUploadPostResponse.xml");
            Parse<JFSFileData>("FileUploadPostResponseCorrupt.xml");
            Parse<JFSFileData>("FileUploadPostResponseIncomplete.xml");
            Parse<JFSFileData>("FileUploadPostResponseNewSuccess.xml");
            Parse<JFSFileData>("FileUploadPostResponseUpdateSuccess.xml");
            Parse<JFSFileData>("FileUploadPostResponseUpdateSuccess2.xml");

            Parse<JFSMountPointData>("Trash.xml");
            Parse<JFSFileDirListData>("FileDirList.xml");
            Parse<JFSSearchResultData>("SearchResult.xml");
            Parse<JFSSearchResultData>("Links.xml");
        }
    }
}