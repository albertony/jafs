using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml.Serialization;
using System.Net;

namespace JaFS
{
    public sealed class HttpMethod // Minimalistic variant of System.Net.Http.HttpMethod, avoiding dependency to the System.Net.Http assembly.
    {
        private string name;
        public override string ToString() { return name; }
        public static HttpMethod Get { get { return new HttpMethod() { name = "GET" }; } }
        public static HttpMethod Post { get { return new HttpMethod() { name = "POST" }; } }
    }

    //
    // Top level class for managing the Jottacloud File System.
    // 
    // Keeps the JFSUserData object as the top level object, the root of the file system.
    // Paths in the data objects are relative starting with the username, but in our
    // wrapper library here we use paths within a single username only.
    //
    public sealed class Jottacloud
    {
        private const string JFS_BASE_URL = "https://jfs.jottacloud.com/jfs"; // Previously: "https://www.jottacloud.com/jfs"
        private const string JFS_BASE_URL_UPLOAD = "https://up.jottacloud.com/jfs";
        private const string JFS_DATE_FORMAT = "yyyy'-'MM'-'dd-'T'HH':'mm':'ssK"; // Similar to the standard round-trip ("O", "o") format, but without decimals. Jottacloud requires this format, or else it will just ignore it and use current date/time!
        public const string BUILTIN_DEVICE_NAME = "Jotta";
        public string ApiVersion { get { return "2.4"; } } // API version. Previously 2.2, against www.jottacloud.com, per 06.03.2017, same as in havardgulldahl/jottalib since October 2014.
        public string LibraryVersion { get { return System.Diagnostics.FileVersionInfo.GetVersionInfo(System.Reflection.Assembly.GetExecutingAssembly().Location).FileVersion; } }
        private string RootName { get { return Credentials.UserName; } } // Cannot use Root.Name because the value here is needed for fetching that Root object!
        private NetworkCredential Credentials { get; set; }
        private ICollection<KeyValuePair<string, string>> RequestHeaders { get; }
        private JFSData.JFSUserData Data { get; set; }
        public bool AutoFetchCompleteData { get; set; } = false; // If false (default) then user must call FetchCompleteData on objects for operations where it is needed. If set to true this is done automatically whenever needed.
        public string Username { get { return Data.Username; } } // Same as RootName and Credentials.UserName.
        public string AccountType { get { return Data.AccountType; } } // If unlimited this is "unlimited", otherwise I don't know..
        public bool Locked { get { return Data.Locked; } }
        public bool ReadLocked { get { return Data.ReadLocked; } }
        public bool WriteLocked { get { return Data.WriteLocked; } }
        public bool QuotaWriteLocked { get { return Data.QuotaWriteLocked; } }
        public bool SyncEnabled { get { return Data.EnableSync; } }
        public ulong DeviceLimit { get { return Data.MaxDevices.Value; } }
        public bool DeviceLimitIsUnlimited { get { return Data.MaxDevices.Unlimited; } }
        public ulong MobileDeviceLimit { get { return Data.MaxMobileDevices.Value; } }
        public bool MobileDeviceLimitIsUnlimited { get { return Data.MaxMobileDevices.Unlimited; } }
        public bool CapacityIsUnlimited { get { return Data.Capacity.Unlimited; } }
        public ulong CapacityBytes { get { return Data.Capacity.Value; } }
        public string Capacity { get { return Data.Capacity.ToString(); } }
        public ulong UsageBytes { get { return Data.Usage.Value; } }
        public string Usage { get { return Data.Usage.ToString(); } }

        public Jottacloud(NetworkCredential credentials)
        {
            Credentials = credentials;
            RequestHeaders = new Dictionary<string, string>() {
                //{ "X-JottaAPIVersion", ApiVersion } // Old header name used with API version 2.2, against www.jottacloud.com, per 06.03.2017, same as in havardgulldahl/jottalib since October 2014. github.com/paaland/node-jfs does not use it, but github.com/havardgulldahl/jottalib do!
                { "x-jftp-version", ApiVersion } // New header name used for API version 2.4. Not required..
            };
            try
            {
                FetchDataObject();
            }
            catch (WebException we)
            {
                HttpWebResponse response = (HttpWebResponse)we.Response;
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    throw new UnauthorizedAccessException("Authorization with Jottacloud failed, please check that you are using the correct username and password, and try again!");
                }
                throw;
            }
        }
        public Jottacloud(string username, string password) : this(new NetworkCredential(username, password)) { }
        public void Refresh()
        {
            FetchDataObject();
        }
        private void FetchDataObject()
        {
            Data = RequestGetDataObject<JFSData.JFSUserData>(""); // Fetch the user object, which is our root level data object.
        }
        public string[] GetDeviceNames()
        {
            string[] names = new string[Data.Devices.Length];
            for (int i = 0; i < Data.Devices.Length; i++)
            {
                names[i] = Data.Devices[i].Name;
            }
            return names;
        }
        public JFSDevice GetDevice(string deviceName = null, string displayName = null, JFSData.JFSDataDeviceType? type = null, Guid? id = null)
        {
            // Get device based one or several criteria, using only the limited device information already
            // retrieved as part of the root (user) data.
            for (int i = 0; i < Data.Devices.Length; i++)
            {
                if ((deviceName == null || string.Equals(Data.Devices[i].Name, deviceName, StringComparison.OrdinalIgnoreCase)) // NB: Might as well ignore case since REST API is case insensitive!
                  && (displayName == null || string.Equals(Data.Devices[i].DisplayNameData.String, displayName, StringComparison.OrdinalIgnoreCase))
                  && (type == null || Data.Devices[i].Type == type)
                  && (id == null || Data.Devices[i].SID.Guid == id))
                {
                    return new JFSDevice(this, Data.Devices[i], false);
                }
            }
            return null;
        }
        public JFSDevice GetBuiltInDevice()
        {
            // Can use either the name ("Jotta") or device type ("JOTTA" in XML, we parse it into enum value JFSData.JFSDataDeviceType.BuiltIn) to decide.
            //return GetDevice(JFSDevice.BUILTIN_DEVICE_NAME);
            return GetDevice(type: JFSData.JFSDataDeviceType.BuiltIn);
        }
        public JFSMountPoint GetBuiltInArchiveMountPoint()
        {
            var device = GetBuiltInDevice();
            device.FetchCompleteData();
            return device.GetBuiltInMountPoint(JFSDevice.BuiltInMountPoints.Archive);
        }
        public JFSMountPoint GetBuiltInSyncMountPoint()
        {
            var device = GetBuiltInDevice();
            device.FetchCompleteData();
            return device.GetBuiltInMountPoint(JFSDevice.BuiltInMountPoints.Sync);
        }
        public JFSDevice[] GetDevices()
        {
            JFSDevice[] devices = new JFSDevice[Data.Devices.Length];
            for (int i = 0; i < Data.Devices.Length; i++)
            {
                devices[i] = new JFSDevice(this, Data.Devices[i], false);
            }
            return devices;
        }
        public JFSDevice NewDevice(string name, JFSData.JFSDataDeviceType deviceType = JFSData.JFSDataDeviceType.Workstation)
        {
            // Create a new device and return the new JFSDevice. All created devices, all devices without the built-in "Jotta",
            // will be shown in the backup feature of the Jottacloud Web UI. So any created devices are for the backup feature!
            // In addition to type, some devices also need additional information: At least the Android client also includes a
            // "cid" with is derived from the unique device id and encrypted with a public key in the apk. The field appears to
            // be optional.
            if (name == BUILTIN_DEVICE_NAME)
            {
                throw new InvalidOperationException("You cannot create a built-in device!");
            }
            var parameters = new Dictionary<string, string> { { "type", Enum.GetName(typeof(JFSData.JFSDataDeviceType), deviceType).ToUpper() } };
            // NB: Unlike with mount points we cannot create mount points and folder path in the same operation.
            var deviceData = RequestPost<JFSData.JFSDeviceData>("/" + name, parameters, ExpectedStatus: HttpStatusCode.Created); // Returns the new JFSDevice (seems to be incomplete?). NB: The POST for new device, like for mount points, returns Created state - but it always does this even if it already exists (for mount points we get OK if it already exists).
            FetchDataObject(); // Re-load the current (parent) user object also, so that it includes information about the new device!
            return new JFSDevice(this, deviceData, false);
        }
        public void DeleteDevicePermanently(string name)
        {
            if (name == BUILTIN_DEVICE_NAME)
            {
                throw new InvalidOperationException("You cannot delete the built-in device!");
            }
            // Delete device permanently, without possibility to restore from trash!
            var parameters = new Dictionary<string, string> { { "rm", "true" } };
            var fileData = RequestPost<JFSData.JFSUserData>("/" + name, parameters); // NB: Returns user data!
            // TODO: The request returns the updated user data object, but currently we reload by sending a normal get request
            FetchDataObject(); // Re-load the current (parent) user object, so that it gets the information that it is now deleted!
        }
        public void DeleteDevicePermanently(JFSDevice device)
        {
            if (device.Type == JFSData.JFSDataDeviceType.BuiltIn)
            {
                throw new InvalidOperationException("You cannot delete the built-in device!");
            }
            DeleteDevicePermanently(device.Name);
        }
        public JFSObject GetObject(string path, bool includeDeleted = false)
        {
            string deviceName, mountPointName, theName;
            string[] folderNames;
            ParsePath(path, out deviceName, out mountPointName, out folderNames, out theName);
            if (deviceName != "")
            {
                var device = GetDevice(deviceName);
                if (device == null)
                    throw new ArgumentException("Device \"" + deviceName + "\" not found!");
                device.FetchCompleteData();
                if (mountPointName != "")
                {
                    var mountPoint = device.GetMountPoint(mountPointName, includeDeleted);
                    if (mountPoint == null)
                        throw new ArgumentException("Mount point \"" + mountPointName + "\" not found!");
                    mountPoint.FetchCompleteData();
                    if (folderNames.LongLength > 0)
                    {
                        var folderName = folderNames[0];
                        var folder = mountPoint.GetFolder(folderName, includeDeleted);
                        if (folder == null)
                            throw new ArgumentException("Folder \"" + folderName + "\" not found!");
                        folder.FetchCompleteData();
                        for (long i = 1; i < folderNames.LongLength; i++)
                        {
                            folderName = folderNames[i];
                            folder = folder.GetFolder(folderName, includeDeleted);
                            if (folder == null)
                                throw new ArgumentException("Folder \"" + folderName + "\" not found!");
                            folder.FetchCompleteData();
                        }
                        if (theName != "")
                        {
                            // Could be a folder or a file
                            var file = folder.GetFile(theName, includeDeleted);
                            if (file == null)
                            {
                                folder = folder.GetFolder(theName, includeDeleted);
                                if (folder == null)
                                    throw new ArgumentException("File or folder \"" + theName + "\" not found!");
                                folder.FetchCompleteData();
                                return folder;
                            }
                            file.FetchCompleteData();
                            return file;
                        }
                        return folder;
                    }
                    if (theName != "")
                    {
                        // Could be a folder or a file
                        var file = mountPoint.GetFile(theName, includeDeleted);
                        if (file == null)
                        {
                            var folder = mountPoint.GetFolder(theName, includeDeleted);
                            if (folder == null)
                                throw new ArgumentException("File or folder \"" + theName + "\" not found!");
                            folder.FetchCompleteData();
                            return folder;
                        }
                        file.FetchCompleteData();
                        return file;
                    }
                    return mountPoint;
                }
                return device;
            }
            return null;
        }

        public JFSTrash GetTrash()
        {
            var path = "/" + BUILTIN_DEVICE_NAME + "/Trash";
            var trashData = RequestGetDataObject<JFSData.JFSMountPointData>(path);
            return new JFSTrash(this, trashData);
        }
        public JFSBasicFile[] GetSharedFiles()
        {
            // Return list of shared files.
            // TODO: The list currently includes any deleted shared files also..
            var path = "/" + BUILTIN_DEVICE_NAME + "/Links";
            var searchResultData = RequestGetDataObject<JFSData.JFSSearchResultData>(path);
            JFSBasicFile[] files = new JFSFile[searchResultData.Files.Length];
            for (int i = 0; i < searchResultData.Files.Length; i++)
            {
                // Creating file object using the FileBase factory method which will decide between JFSFile, JFSIncompleteFile or JFSCorruptFile.
                // Special case with path here: The data object (JFSSearchResultData) does not have a path that we can use as parent path for
                // files, but luckily the incomplete file objects contained in the data object have path member (which is the parent path) already.
                files[i] = JFSBasicFile.Manufacture(this, searchResultData.Files[i], false);
            }
            return files;
        }
        public JFSBasicFile[] GetRecentFiles(int maxResults = 10, string sortBy = "updated")
        {
            // Get a list of the n latest files (the server minimum default is 10), optionally sorted.
            // This is a special built-in object on mount point level, appearing as mount point with name "Label",
            // (but not behaving like a mount point), and only on the built-in "Jotta" device. Also we do not have
            // to keep the data object alive since we get complete information immediately and convert into our own
            // structure. Therefore it is implemented just as a method here on top level.
            var path = "/" + BUILTIN_DEVICE_NAME + "/Latest";
            var parameters = new Dictionary<string, string> {
                { "max", maxResults.ToString() },
                { "sort", sortBy },
                { "web", "true" }
            };
            var searchResultData = RequestGetDataObject<JFSData.JFSSearchResultData>(path, parameters);
            // NB: When limiting results with the "max" parameter then Metadata.NumberOfFiles show the same value as without this limit, so fewer files can be returned!
            JFSBasicFile[] files = new JFSFile[searchResultData.Files.Length];
            for (int i = 0; i < searchResultData.Files.Length; i++)
            {
                // Creating file object using the FileBase factory method which will decide between JFSFile, JFSIncompleteFile or JFSCorruptFile.
                // Special case with path here: The data object (JFSSearchResultData) does not have a path that we can use as parent path for
                // files, but luckily the incomplete file objects contained in the data object have path member (which is the parent path) already.
                files[i] = JFSBasicFile.Manufacture(this, searchResultData.Files[i], false);
            }
            return files;
        }
        public string ConvertFromDataPath(string dataPath)
        {
            if (dataPath == null) return null;
            return dataPath.Substring(RootName.Length + 1); // Paths in data objects start with username, paths in our JFS library has user object as root and keep paths relative to that, so we remove the "[username]/" prefix from the data path.
        }
        public string ConvertToDataPath(string path)
        {
            if (path == null) return null;
            if (string.IsNullOrEmpty(path) || path == "/")
                return "/" + RootName; // Fetching the root object itself
            else
                return "/" + RootName + (path.StartsWith("/") ? path : "/" + path);

        }
        private void ParsePath(string path, out string deviceName, out string mountPointName, out string[] folderNames, out string name)
        {
            // If path ends with a device ("/device"), device name will be returned in deviceName parameter,
            // and if path ends with mount point ("/device/mountpoint") then the mount point name will be
            // returned in the mountPointName parameter. Folders are a bit different: If the path ends with
            // a folder or a file directly on the mount point then that name will be returned in the name parameter.
            // If there is a sub-folder path then it is returned in folderNames array, but if the path ends with
            // a folder then the last folder name are still returned in the name parameter. If path ends with a
            // file then the file name are always in the name parameter.
            string pattern = @"^(?:\/)(?<device>[^\/]+)(?:(?:\/)(?<mountpoint>[^\/]+)(?:(?:\/)((?<folder>[^\/]+)(?:\/))*(?<name>[^\/]+))?)?$";
            var match = System.Text.RegularExpressions.Regex.Match(path, pattern);
            deviceName = match.Groups["device"].Value;
            mountPointName = match.Groups["mountpoint"].Value;
            var folderCaptures = match.Groups["folder"].Captures;
            folderNames = new string[folderCaptures.Count];
            for (int i = 0; i < folderCaptures.Count; i++)
                folderNames[i] = folderCaptures[i].Value;
            name = match.Groups["name"].Value;
        }
        private string GetDeviceNameFromPath(string path) { return path.Substring(0, path.IndexOf("/")); }
        private Uri CreateUri(string path, ICollection<KeyValuePair<string, string>> queryParameters = null, bool forUpload = false)
        {
            // Joining the file system's base URI with specified path, and adding any query parameters.
            string pathAndQuery = Uri.EscapeUriString(ConvertToDataPath(path)).Replace("+", "%2B").Replace("#", "%23"); // We need additional escaping for '+' and '#' characters to support that in file names! Uri escapes everything else for us (luckily it uses %20 for escaping space).
            if (queryParameters != null)
            {
                pathAndQuery += "?";
                foreach (var kv in queryParameters)
                {
                    pathAndQuery += Uri.EscapeDataString(kv.Key) + "=" + Uri.EscapeDataString(kv.Value) + "&";
                }
                pathAndQuery = pathAndQuery.Remove(pathAndQuery.Length - 1);
            }
            return new Uri((forUpload ? JFS_BASE_URL_UPLOAD : JFS_BASE_URL) + pathAndQuery);
        }
        private HttpWebRequest CreateRequest(HttpMethod method, Uri uri, ICollection<KeyValuePair<string, string>> additionalHeaders = null)
        {
            // Make a GET request for url
            var request = (HttpWebRequest)WebRequest.CreateHttp(uri);
            request.Method = method.ToString();
            request.Credentials = Credentials;
            //request.ContentType = contentType;
            request.KeepAlive = false;
            request.UserAgent = "JottacloudFileSystem version " + LibraryVersion;
            foreach (var kv in RequestHeaders)
            {
                request.Headers.Add(kv.Key, kv.Value);
            }
            if (additionalHeaders != null)
            {
                foreach (var kv in additionalHeaders)
                {
                    request.Headers.Add(kv.Key, kv.Value);
                }
            }
            //request.Timeout = TODO?
            return request;
        }
        public string RequestGet(string path, ICollection<KeyValuePair<string, string>> queryParameters = null, ICollection<KeyValuePair<string, string>> additionalHeaders = null)
        {
            // Make a GET request for url
            Uri uri = CreateUri(path, queryParameters);
            var request = CreateRequest(HttpMethod.Get, uri, additionalHeaders);
            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            {
                if (response.StatusCode != HttpStatusCode.OK) //if (response.StatusCode >= HttpStatusCode.InternalServerError)
                {
                    throw new JFSError(response.StatusDescription);
                }
                using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                {
                    return reader.ReadToEnd();
                }
            }
        }
        public DataObjectType RequestGetDataObject<DataObjectType>(string path, ICollection<KeyValuePair<string, string>> queryParameters = null, ICollection<KeyValuePair<string, string>> additionalHeaders = null)
        {
            // Make a GET request for url
            Uri uri = CreateUri(path, queryParameters);
            var request = CreateRequest(HttpMethod.Get, uri, additionalHeaders);
            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            {
                if (response.StatusCode != HttpStatusCode.OK) //if (response.StatusCode >= HttpStatusCode.InternalServerError)
                {
                    throw new JFSError(response.StatusDescription);
                }
                using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                {
                    try
                    {
                        XmlSerializer serializer = new XmlSerializer(typeof(DataObjectType));
                        return (DataObjectType)serializer.Deserialize(reader);
                    }
                    catch
                    {
#if DEBUG
                        // For debugging, read out the XML response without attempting to deserialize it.
                        // Since the response stream is not seek-able, we must re-send the original request!
                        string responseContent;
                        var debugRequest = CreateRequest(HttpMethod.Get, uri, additionalHeaders);
                        using (HttpWebResponse debugResponse = (HttpWebResponse)debugRequest.GetResponse())
                        using (StreamReader debugReader = new StreamReader(debugResponse.GetResponseStream()))
                        {
                            responseContent = debugReader.ReadToEnd();
                        }
#endif
                        throw;
                    }
                }
            }
        }
        public DataObjectType RequestPost<DataObjectType>(string path, ICollection<KeyValuePair<string, string>> queryParameters = null, ICollection<KeyValuePair<string, string>> additionalHeaders = null, ICollection<KeyValuePair<string, string>> data = null, HttpStatusCode ExpectedStatus = HttpStatusCode.OK)
        {
            // HTTP Post form data (string)
            Uri uri = CreateUri(path, queryParameters);
            var request = CreateRequest(HttpMethod.Post, uri, additionalHeaders);
            if (data != null)
            {
                StringBuilder postData = new StringBuilder();
                foreach (var kv in data)
                {
                    postData.Append(Uri.EscapeDataString(kv.Key) + "=" + Uri.EscapeDataString(kv.Value) + "&");
                }
                postData.Length--;
                byte[] byteArray = Encoding.UTF8.GetBytes(postData.ToString());
                request.ContentType = "application/octet-stream";
                request.ContentLength = byteArray.Length;
                using (Stream dataStream = request.GetRequestStream())
                {
                    dataStream.Write(byteArray, 0, byteArray.Length);
                }
            }
            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            {
                // Most post requests in JFS returns "OK", but some return "Created" when the object was created and "OK" if it already exists (possibly deleted but still in trash)!
                if (response.StatusCode != ExpectedStatus) //if (response.StatusCode >= HttpStatusCode.InternalServerError)
                {
                    throw new JFSError("Unexpected response code: " + response.StatusDescription);
                }
                using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                {
                    try
                    {
                        XmlSerializer serializer = new XmlSerializer(typeof(DataObjectType));
                        return (DataObjectType)serializer.Deserialize(reader);
                    }
                    catch
                    {
#if DEBUG
                        // For debugging, read out the XML response without attempting to deserialize it.
                        // Since the response stream is not seek-able, we must re-send the original request!
                        // NB: The original Post request was OK, only the response was not the expected XML,
                        // but if we re-send the request we might not get the same response!!
                        string responseContent;
                        var debugRequest = CreateRequest(HttpMethod.Post, uri, additionalHeaders);
                        using (HttpWebResponse debugResponse = (HttpWebResponse)debugRequest.GetResponse())
                        using (StreamReader debugReader = new StreamReader(debugResponse.GetResponseStream()))
                        {
                            responseContent = debugReader.ReadToEnd();
                        }
#endif
                        throw;
                    }
                }
            }
        }
        public void DownloadFile(string remotePath, FileInfo fileInfo)//, ulong from, ulong to)
        {
            // Make a GET request for url
            var queryParameters = new Dictionary<string, string> { { "mode", "bin" } };
            // For partial download:
            //    var additionalHeaders = new Dictionary<string, string> { { "Range", string.Format("bytes=%d-%d",from,to-1) } };
            Uri uri = CreateUri(remotePath, queryParameters);
            var request = CreateRequest(HttpMethod.Get, uri);//, additionalHeaders);
            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            {
                if (response.StatusCode != HttpStatusCode.OK) //if (response.StatusCode >= HttpStatusCode.InternalServerError)
                {
                    throw new JFSError(response.StatusDescription);
                }
                using (var inputStream = response.GetResponseStream())
                using (var outputStream = fileInfo.OpenWrite())
                {
                    inputStream.CopyTo(outputStream);
                }
            }
        }
        public JFSData.JFSFileData VerifyFile(string remotePath, FileInfo fileInfo, string md5Hash)
        {
            // Check if there is a valid file (not considering incomplete or corrupt revisions etc) at specified location with same name and MD5 hash as the specified local file!
            return VerifyFile(remotePath, fileInfo.Length.ToString(), fileInfo.CreationTimeUtc.ToString(JFS_DATE_FORMAT), fileInfo.LastWriteTimeUtc.ToString(JFS_DATE_FORMAT), md5Hash);
        }
        private JFSData.JFSFileData VerifyFile(string remotePath, string size, string timeCreated, string timeModified, string md5Hash)
        {
            // Check if there is a valid file (not considering incomplete or corrupt revisions etc) at specified location with same name, and MD5 hash as the specified local file!
            var queryParameters = new Dictionary<string, string> { { "cphash", "true" } };
            var additionalHeaders = new Dictionary<string, string> {
                { "JMd5", md5Hash },
                { "JSize", size },
                { "JCreated", timeCreated },
                { "JModified", timeModified },
            };
            Uri uri = CreateUri(remotePath, queryParameters, forUpload: false);
            var request = CreateRequest(HttpMethod.Post, uri, additionalHeaders);
            //request.ContentType = "application/octet-stream";
            request.ContentLength = 0;
            // Send request, and read response
            try
            {
                // Expecting status code 200 if match was found, and 404 if not. If 404 it will be thrown as exception so its not enough to test for HttpStatusCode.NotFound.
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                    if (response.StatusCode != HttpStatusCode.OK)
                        throw new JFSError(response.StatusDescription);

                    using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                    {
                        try
                        {
                            XmlSerializer serializer = new XmlSerializer(typeof(JFSData.JFSFileData));
#if DEBUG
                            string responseContent = reader.ReadToEnd();
                            using (var sreader = new StringReader(responseContent))
                                return (JFSData.JFSFileData)serializer.Deserialize(sreader);
#else
                            return (JFSData.JFSFileData)serializer.Deserialize(reader);
#endif
                        }
                        catch
                        {
#if DEBUG
                            // For debugging, read out the XML response without attempting to deserialize it.
                            // Since the response stream is not seek-able, we must re-send the original request!
                            // NB: The original Post request was OK, only the response was not the expected XML,
                            // but if we re-send the request we might not get the same response!!
                            string responseContent;
                            var debugRequest = CreateRequest(HttpMethod.Post, uri, additionalHeaders);
                            using (HttpWebResponse debugResponse = (HttpWebResponse)debugRequest.GetResponse())
                            using (StreamReader debugReader = new StreamReader(debugResponse.GetResponseStream()))
                            {
                                responseContent = debugReader.ReadToEnd();
                            }
#endif
                            throw;
                        }
                    }
                }
            }
            catch (WebException ex)
            {
                HttpWebResponse response = (HttpWebResponse)ex.Response;
                if (response.StatusCode == HttpStatusCode.NotFound)
                    return null; // Expected for the case when the file does not exist
                throw;
            }
        }
        public JFSData.JFSFileData UploadIfChanged(string remotePath, FileInfo fileInfo, out bool wasChanged, long offset = 0)
        {
            long fileSize = fileInfo.Length;
            string sizeString = fileSize.ToString();
            var timeCreated = fileInfo.CreationTimeUtc.ToString(JFS_DATE_FORMAT);
            var timeModified = fileInfo.LastWriteTimeUtc.ToString(JFS_DATE_FORMAT);
            if (offset < 0)
                throw new InvalidOperationException("Negative file offset is not valid");
            else if (offset > fileSize)
                throw new InvalidOperationException("Offset is larger than file size");
            string md5Hash;
            using (FileStream fileStream = fileInfo.OpenRead())
            {
                // Calculate MD5 hash
                md5Hash = CalculateMD5(fileStream);

                // Check if identical file already exists
                var fileData = VerifyFile(remotePath, sizeString, timeCreated, timeModified, md5Hash);
                if (fileData != null)
                // TODO: Do we also need to verify that the returned file is complete and not corrupt? This does not seem to be necessary,
                //       since the VerifyFile (cphash request) seem to fail even if trying to match the correct md5 of an incomplete revision,
                //       but not certain about this...
                //if (JFSBasicFile.NewestVersionIsCompleted(fileData)) // TODO: Is it necessary to do this, or is it enough to check if VerifyFile returned null due to HTTP 404 or some data when HTTP 200?
                {
                    wasChanged = false;
                    return fileData; // File already exists!
                }
                else
                {
                    // Need to upload it after all!
                    wasChanged = true;
                    // Move stream back to 0, or specified offset, after the MD5 calculation has used it.
                    fileStream.Seek(offset, SeekOrigin.Begin);
                    // Configure url, query parameters  and request headers
                    var queryParameters = new Dictionary<string, string> { { "umode", "nomultipart" } };
                    var additionalHeaders = new Dictionary<string, string> {
                        { "JMd5", md5Hash },
                        { "JSize", sizeString },
                        { "JCreated", timeCreated },
                        { "JModified", timeModified },
                    };
                    Uri uri = CreateUri(remotePath, queryParameters, forUpload: true);
                    var request = CreateRequest(HttpMethod.Post, uri, additionalHeaders);
                    request.ContentType = "application/octet-stream";
                    request.ContentLength = fileSize-offset;
                    // Write post data request
                    using (Stream requestStream = request.GetRequestStream())
                    {
                        fileStream.CopyTo(requestStream);
                    }
                    // Send request, and read response
                    using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                    {
                        if (response.StatusCode != HttpStatusCode.Created)
                        {
                            throw new JFSError(response.StatusDescription);
                        }
                        using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                        {
                            try
                            {
                                XmlSerializer serializer = new XmlSerializer(typeof(JFSData.JFSFileData));
#if DEBUG
                                string responseContent = reader.ReadToEnd();
                                using (var sreader = new StringReader(responseContent))
                                    return (JFSData.JFSFileData)serializer.Deserialize(sreader);
#else
                                return (JFSData.JFSFileData)serializer.Deserialize(reader);
#endif
                            }
                            catch
                            {
#if DEBUG
                                // For debugging, read out the XML response without attempting to deserialize it.
                                // Since the response stream is not seek-able, we must re-send the original request!
                                // NB: The original Post request was OK, only the response was not the expected XML,
                                // but if we re-send the request we might not get the same response!!
                                string responseContent;
                                var debugRequest = CreateRequest(HttpMethod.Post, uri, additionalHeaders);
                                using (HttpWebResponse debugResponse = (HttpWebResponse)debugRequest.GetResponse())
                                using (StreamReader debugReader = new StreamReader(debugResponse.GetResponseStream()))
                                {
                                    responseContent = debugReader.ReadToEnd();
                                }
#endif
                                throw;
                            }
                        }
                    }
                }
            }
        }
        public JFSData.JFSFileData UploadSimple(string path, FileInfo fileInfo, long offset = 0, bool noHashVerification = false, bool noFileTimes = false)
        {
            // Upload a fileobject to path, HTTP POST-ing to up.jottacloud.com, using the JottaCloud API.
            long fileSize = fileInfo.Length;
            if (offset < 0)
                throw new InvalidOperationException("Negative file offset is not valid");
            else if (offset > fileSize)
                throw new InvalidOperationException("Offset is larger than file size");
            using (FileStream fileStream = fileInfo.OpenRead())
            {
                // Configure url, query parameters  and request headers
                var additionalHeaders = new Dictionary<string, string> { { "JSize", fileSize.ToString() } }; // Required header!
                if (!noFileTimes)
                {
                    // Record the local file times when storing on server.
                    // If not the server will just register the current date time when it stores the file.
                    additionalHeaders.Add("JCreated", fileInfo.CreationTimeUtc.ToString(JFS_DATE_FORMAT));
                    additionalHeaders.Add("JModified", fileInfo.LastWriteTimeUtc.ToString(JFS_DATE_FORMAT));
                }
                if (!noHashVerification)
                {
                    // Calculate MD5 hash and put as header so that the server can verify it against received data.
                    // If not, the server will just accept whatever data we give it (as long as it matches the required header JSize), and it will return back the MD5 of it.
                    additionalHeaders.Add("JMd5", CalculateMD5(fileStream));
                }
                // Move stream back to 0, or specified offset, after the MD5 calculation has used it (and even if not we must move to any specified offset).
                fileStream.Seek(offset, SeekOrigin.Begin);
                var queryParameters = new Dictionary<string, string> { { "umode", "nomultipart" } };
                Uri uri = CreateUri(path, queryParameters, forUpload: true);
                var request = CreateRequest(HttpMethod.Post, uri, additionalHeaders);
                //request.ContentType = "application/octet-stream";
                request.ContentLength = fileSize-offset;
                // Write post data request
                using (Stream requestStream = request.GetRequestStream())
                {
                    fileStream.CopyTo(requestStream);
                }
                // Send request, and read response
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                    if (response.StatusCode != HttpStatusCode.Created)
                    {
                        throw new JFSError(response.StatusDescription);
                    }
                    using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                    {
                        try
                        {
                            XmlSerializer serializer = new XmlSerializer(typeof(JFSData.JFSFileData));
#if DEBUG
                            string responseContent = reader.ReadToEnd();
                            using (var sreader = new StringReader(responseContent))
                                return (JFSData.JFSFileData)serializer.Deserialize(sreader);
#else
                            return (JFSData.JFSFileData)serializer.Deserialize(reader);
#endif
                        }
                        catch
                        {
#if DEBUG
                            // For debugging, read out the XML response without attempting to deserialize it.
                            // Since the response stream is not seek-able, we must re-send the original request!
                            // NB: The original Post request was OK, only the response was not the expected XML,
                            // but if we re-send the request we might not get the same response!!
                            string responseContent;
                            var debugRequest = CreateRequest(HttpMethod.Post, uri, additionalHeaders);
                            using (HttpWebResponse debugResponse = (HttpWebResponse)debugRequest.GetResponse())
                            using (StreamReader debugReader = new StreamReader(debugResponse.GetResponseStream()))
                            {
                                responseContent = debugReader.ReadToEnd();
                            }
#endif
                            throw;
                        }
                    }

                }
            }
        }
        public JFSData.JFSFileData UploadMultipart(string path, FileInfo fileInfo, long offset = 0)
        {
            // Upload a fileobject to path, HTTP POST-ing to up.jottacloud.com, using the JottaCloud API.
            // If offset is specified we are resuming from a previous attempt.
            long fileSize = fileInfo.Length;
            if (offset < 0)
                throw new InvalidOperationException("Negative file offset is not valid");
            else if (offset > fileSize)
                throw new InvalidOperationException("Offset is larger than file size");
            string md5Hash;
            using (FileStream fileStream = fileInfo.OpenRead())
            {
                // Calculate MD5 hash
                md5Hash = CalculateMD5(fileStream);
                // Move stream back to 0, or specified offset, after the MD5 calculation has used it.
                fileStream.Seek(offset, SeekOrigin.Begin);
                // Configure url, query parameters  and request headers
                //var deviceName = GetDeviceNameFromPath(path);
                var timeCreated = fileInfo.CreationTimeUtc.ToString(JFS_DATE_FORMAT);
                var timeModified = fileInfo.LastWriteTimeUtc.ToString(JFS_DATE_FORMAT);
                var additionalHeaders = new Dictionary<string, string> {
                    { "JMd5", md5Hash },
                    { "JSize", fileSize.ToString() },
                    { "JCreated", timeCreated },
                    { "JModified", timeModified },
                    // The following are used by havardgulldahl/jottalib but does not seem to be mandatory.
                    //{ "X-Jfs-DeviceName", deviceName },
                    //{ "jx_csid", "" },
                    //{ "jx_lisence", "" },
                };
                // Prepare post data:
                // First three simple data sections: md5, modified time and created time.
                // Then a final section with the file contents. We prepare everything,
                // calculate the total size including the file, and then we write it
                // to the request. This way we can stream the file directly into the
                // request without copying the entire file into byte array first etc.
                string multipartBoundary = string.Format("----------{0:N}", Guid.NewGuid());
                byte[] multiPartContent = Encoding.UTF8.GetBytes(
                    CreateMultiPartItem("md5", md5Hash, multipartBoundary) + "\r\n"
                  + CreateMultiPartItem("modified", timeModified, multipartBoundary) + "\r\n"
                  + CreateMultiPartItem("created", timeCreated, multipartBoundary) + "\r\n"
                  + CreateMultiPartFileHeader("file", fileInfo.Name, null, multipartBoundary) + "\r\n");
                byte[] multipartTerminator = Encoding.UTF8.GetBytes("\r\n--" + multipartBoundary + "--\r\n");
                Uri uri = CreateUri(path, null, forUpload: true);
                var request = CreateRequest(HttpMethod.Post, uri, additionalHeaders);
                request.ContentType = "multipart/form-data; boundary=" + multipartBoundary;
                request.ContentLength = multiPartContent.Length + (fileSize-offset) + multipartTerminator.Length;
                // Write post data request
                using (Stream requestStream = request.GetRequestStream())
                {
                    requestStream.Write(multiPartContent, 0, multiPartContent.Length);
                    fileStream.CopyTo(requestStream);
                    requestStream.Write(multipartTerminator, 0, multipartTerminator.Length);
                }
                // Send request, and read response
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                    if (response.StatusCode != HttpStatusCode.Created)
                    {
                        throw new JFSError(response.StatusDescription);
                    }
                    using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                    {

                        try
                        {
                            XmlSerializer serializer = new XmlSerializer(typeof(JFSData.JFSFileData));
#if DEBUG
                            string responseContent = reader.ReadToEnd();
                            using (var sreader = new StringReader(responseContent))
                                return (JFSData.JFSFileData)serializer.Deserialize(sreader);
#else
                            return (JFSData.JFSFileData)serializer.Deserialize(reader);
#endif
                        }
                        catch
                        {
#if DEBUG
                            // For debugging, read out the XML response without attempting to deserialize it.
                            // Since the response stream is not seek-able, we must re-send the original request!
                            // NB: The original Post request was OK, only the response was not the expected XML,
                            // but if we re-send the request we might not get the same response!!
                            string responseContent;
                            var debugRequest = CreateRequest(HttpMethod.Post, uri, additionalHeaders);
                            using (HttpWebResponse debugResponse = (HttpWebResponse)debugRequest.GetResponse())
                            using (StreamReader debugReader = new StreamReader(debugResponse.GetResponseStream()))
                            {
                                responseContent = debugReader.ReadToEnd();
                            }
#endif
                            throw;
                        }
                    }

                }
            }
        }
        public static string CalculateMD5(FileInfo fileInfo)
        {
            using (FileStream fileStream = fileInfo.OpenRead())
            {
                return CalculateMD5(fileStream);
            }
        }
        public static string CalculateMD5(FileStream fileStream)
        {
            using (System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create())
            {
                return BitConverter.ToString(md5.ComputeHash(fileStream)).Replace("-", string.Empty);
            }
        }
        public static string CalculateMD5(byte[] fileData)
        {
            using (System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create())
            {
                return BitConverter.ToString(md5.ComputeHash(fileData)).Replace("-", string.Empty);
            }
        }
        public static string CalculateMD5(string fileContent)
        {
            return CalculateMD5(Encoding.UTF8.GetBytes(fileContent));
        }
        
        private string CreateMultiPartItem(string contentName, string contentValue, string boundary)
        {
            // Header and content. Append newline before next section, or footer section.
            return string.Format("--{0}\r\nContent-Disposition: form-data; name=\"{1}\"\r\n\r\n{2}",
                boundary,
                contentName,
                //System.Uri.EscapeDataString(contentName), //Alternative: URLEncod it. Using System.Uri.EscapeDataString or System.Web.HttpUtility.UrlEncode?
                contentValue);
            //System.Uri.EscapeDataString(contentValue)); //Alternative: URLEncod it. Using System.Uri.EscapeDataString or System.Web.HttpUtility.UrlEncode?
        }
        private string CreateMultiPartFileHeader(string contentName, string fileName, string fileType, string boundary)
        {
            // Header. Append newline and then file content.
            return string.Format("--{0}\r\nContent-Disposition: form-data; name=\"{1}\"; filename=\"{2}\";\r\nContent-Type: {3}\r\n",
                boundary,
                contentName,
                //System.Uri.EscapeDataString(contentName), //Alternative: URLEncod it. Using System.Uri.EscapeDataString or System.Web.HttpUtility.UrlEncode?
                fileName,
                //System.Uri.EscapeDataString(fileName)); //Alternative: URLEncod it. Using System.Uri.EscapeDataString or System.Web.HttpUtility.UrlEncode?
                fileType ?? "application/octet-stream");
        }
    }

    //
    // Jottacloud File System hierarchy, adding logic around corresponding data objects.
    //

    public abstract class JFSObject
    {
        protected Jottacloud FileSystem { get; }
        public JFSObject(Jottacloud fileSystem) { FileSystem = fileSystem; }
    }

    //
    // JFSObject is the base class for all objects in the JFS file system, wrapping a data object.
    //
    public abstract class JFSNamedObject<DataObjectType> : JFSObject where DataObjectType : JFSData.JFSNamedRootObjectData
    {
        private DataObjectType dataObject;
        protected DataObjectType Data { get { return dataObject; } }
        protected virtual void SetData(DataObjectType data, bool isComplete = false)
        {
            dataObject = data;
            CompleteData = isComplete;
            Name = Data.Name;
        }
        protected bool CompleteData { get; set; } = false;
        public string Name { get; protected set; } // Name of this object, without path. Value stored in this wrapper, but updated based on value from data object whenever (re-)loaded.
        public virtual string ParentPath { get { return "/"; } protected set { return; } } // Pathless objects are assumed to be at top level.
        public string FullName { get { return ParentPath + Name; } } // Full path of this object. Never ending with path separator.
        protected virtual string CreateChildPath(string childName) { return FullName + "/" + childName; }

        public JFSNamedObject(Jottacloud fileSystem, DataObjectType data, bool isCompleteData) : base(fileSystem)
        {
            SetData(data, isCompleteData);
        }
        public virtual void Refresh()
        {
            // Load, or re-load, complete data for the current object.
            FetchCompleteData(FullName);
        }
        public virtual void FetchCompleteData()
        {
            // Load, or re-load, complete data for the current object.
            FetchCompleteData(FullName);
        }
        protected void CheckCompleteData()
        {
            // Call this for operations where complete data is a requirement.
            // It will check if we have complete data, if not it will fetch it if option AutoFetchCompleteData
            // is set, or else throw an exception;
            if (!CompleteData)
            {
                if (FileSystem.AutoFetchCompleteData)
                {
                    FetchCompleteData();
                }
                else
                {
                    throw new InvalidOperationException("Complete data needed for this operation, call .FetchCompleteData() and try again!");
                }
            }
        }
        private void FetchCompleteData(string fullName)
        {
            // Variant for the case where we have no basic data object to fetch the name from, and it required as the last path of the uri.
            SetData(FileSystem.RequestGetDataObject<DataObjectType>(fullName), true); // NB: Always complete data!
        }
    }

    public abstract class JFSNamedAndPathedObject<DataObjectType> : JFSNamedObject<DataObjectType> where DataObjectType : JFSData.JFSNamedAndPathedRootObjectData
    {
        public override string ParentPath { get; protected set; } // Full path to the parent. Always starting and ending with path separator. Value stored in this wrapper, but updated based on value from data object whenever (re-)loaded.
        protected override void SetData(DataObjectType data, bool isComplete = false)
        {
            base.SetData(data, isComplete);
            if (isComplete)
            {
                ParentPath = FileSystem.ConvertFromDataPath(Data.Path) + "/";
            }
        }
        public JFSNamedAndPathedObject(Jottacloud fileSystem, DataObjectType dataWithpath, bool isCompleteData)
            : base(fileSystem, dataWithpath, isCompleteData)
        {
            ParentPath = FileSystem.ConvertFromDataPath(Data.Path) + "/";
        }
        public JFSNamedAndPathedObject(Jottacloud fileSystem, string parentFullName, DataObjectType incompleteDataWithoutPath)
            : base(fileSystem, incompleteDataWithoutPath, false)
        {
            ParentPath = parentFullName + "/"; // Since incomplete data it usually does not contain path yet.
        }
    }

    //
    // Device
    //
    public sealed class JFSDevice : JFSNamedObject<JFSData.JFSDeviceData>
    {
        public string DisplayName { get { return Data.DisplayNameData.String; } }
        public JFSData.JFSDataDeviceType Type { get { return Data.Type; } }
        public bool IsBuiltInDevice { get { return Name == Jottacloud.BUILTIN_DEVICE_NAME || Type == JFSData.JFSDataDeviceType.BuiltIn; } } // Testing both name and type, just to be on the safe side..
        public Guid ID { get { return Data.SID.Guid; } }
        public DateTime? Modified { get { return Data.Modified.DateTime; } }
        public ulong SizeInBytes { get { return Data.Size.Value; } }
        public string Size { get { return Data.Size.ToString(); } }
        public string User { get { return Data.User; } }

        // Some special considerations for mount points of the built-in device
        public enum BuiltInMountPoints { Archive, Sync } // All mount points that the Jottacloud's API treat as mount points, including the special ones, but excluding any user created mount points (which is only possible via API?)!
        public enum SpecialBuiltInMountPoints { Trash, Links, Shared, Latest } // These are returned as mount points in the API ("Trash" and "Links" are not listed as mount points on device object but exists when requesting them directly), but they are special in the sense that you for instance cannot create folders and upload files to them, so we handle them with special classes in our library and not as mount points!
        private bool IsMountPoint(string name) { return !IsSpecialBuiltinMountPoint(name); } // This is true for all that we in this library treat as mount points: The regular built-in mount points and any user created mount points, but not the built-in special mount points (those are handled as specific types in this library).
        private bool IsCustomMountPoint(string name) { return !IsBuiltInDevice || !IsBuiltinMountPoint(name); }
        private bool IsBuiltinMountPoint(string name)
            { return IsBuiltInDevice && (Array.FindIndex(Enum.GetNames(typeof(BuiltInMountPoints)), x => x.Equals(name, StringComparison.OrdinalIgnoreCase)) != -1
                                      || Array.FindIndex(Enum.GetNames(typeof(SpecialBuiltInMountPoints)), x => x.Equals(name, StringComparison.OrdinalIgnoreCase)) != -1); }
        private bool IsRegularBuiltinMountPoint(string name) { return IsBuiltInDevice && Array.FindIndex(Enum.GetNames(typeof(BuiltInMountPoints)), x => x.Equals(name, StringComparison.OrdinalIgnoreCase)) != -1; }
        private bool IsSpecialBuiltinMountPoint(string name) { return IsBuiltInDevice && Array.FindIndex(Enum.GetNames(typeof(SpecialBuiltInMountPoints)), x => x.Equals(name, StringComparison.OrdinalIgnoreCase)) != -1; }

        public ulong NumberOfObjects { get { return Data.Metadata.Total; } } // TODO: Is this not always the same as NumberOfMountPoints on devices??
        public ulong NumberOfApiMountPoints { get { return Data.Metadata.NumberOfMountPoints; } } // NB: Includes deleted and special mount points!
        public ulong NumberOfRegularMountPoints
        {
            get
            {
                // Filter out deleted mount points, and any mount points that are presented as that via the API but we do
                // not treat them as such in this library: The regular built-in mount points and any user created mount points,
                // but not the built-in special mount points (those are handled as specific types in this library).
                ulong counter = 0;
                for (ulong i = 0; i < Data.Metadata.NumberOfMountPoints; ++i)
                {
                    if (Data.MountPoints[i].Deleted == null && IsMountPoint(Data.MountPoints[i].Name))
                    {
                        counter++;
                    }
                }
                return counter;
            }
        }
        public JFSDevice(Jottacloud fileSystem, JFSData.JFSDeviceData data, bool isCompleteData) : base(fileSystem, data, isCompleteData) {}
        public void DeletePermanently()
        {
            throw new InvalidOperationException("Permanent deletion must be done from the parent object!");
        }
        public JFSDevice Rename(string newName)
        {
            /* TODO: Haven't found out how to rename devices yet..
            // Rename folder to a new name, or a path relative to the current folder in which case all intermediate folders along the way will also be created.
            var moveToPath = FileSystem.ConvertToDataPath(ParentPath + newName); // Same parent but different name, and convert to data path, appending the file system root name (username).
            var parameters = new Dictionary<string, string> { { "mv", moveToPath } };  // var parameters = new Dictionary<string, string> { { "mvDir", moveToPath } };
            var folderData = FileSystem.Post<JFSData.JFSDeviceData>(FullName, parameters); // Returns the new JFSFolderData (seems to be incomplete?)
            // TODO: It is a bit of a mess with the old and new folder structure, parent of old location
            // and parent of new location should be refreshed etc...
            //FetchCompleteData(); // TODO? Refresh data of the original now moved folder??
            return new JFSDevice(FileSystem, folderData);
            */
            throw new NotImplementedException();
        }
        public JFSMountPoint GetBuiltInMountPoint(BuiltInMountPoints mountPointType)
        {
            if (IsBuiltInDevice)
                return GetMountPoint(Enum.GetName(typeof(BuiltInMountPoints), mountPointType));
            else
                throw new InvalidOperationException("Only the built-in device have built-in mount points!");
        }
        public JFSMountPoint GetMountPoint(string mountPointName, bool includeDeleted = false, bool includeSpecialMountPoints = false)
        {
            // If argument includeSpecialMountPoints is true then we may also return special built-in mount points that we
            // have special classes for and not really handle by the mount point class in this library. 
            // Deleted mount points that are still in the trash are not returned by default, but argument includeDeleted can
            // be used to include them.
            if (!includeSpecialMountPoints && IsSpecialBuiltinMountPoint(mountPointName))
            {
                return null;
            }
            CheckCompleteData();
            for (ulong i = 0; i < Data.Metadata.NumberOfMountPoints; i++)
            {
                if (string.Equals(Data.MountPoints[i].Name, mountPointName, StringComparison.OrdinalIgnoreCase)) // NB: Might as well ignore case since REST API is case insensitive!
                {
                    if (includeDeleted || Data.MountPoints[i].Deleted == null)
                        return new JFSMountPoint(FileSystem, FullName, Data.MountPoints[i]);
                    else
                        return null;
                }
            }
            return null;
        }
        public string[] GetMountPointNames(bool includeDeleted = false, bool includeSpecialMountPoints = false)
        {
            // If argument includeSpecialMountPoints is true then we may also return special built-in mount points that we
            // have special classes for and not really handle by the mount point class in this library.
            // Deleted mount points that are still in the trash are not returned by default, but argument includeDeleted can
            // be used to include them.
            CheckCompleteData();
            string[] names = new string[Data.Metadata.NumberOfMountPoints];
            int counter = 0;
            for (ulong i = 0; i < Data.Metadata.NumberOfMountPoints; i++)
            {
                if ((includeDeleted || Data.MountPoints[i].Deleted == null)
                 && (includeSpecialMountPoints || !IsSpecialBuiltinMountPoint(Data.MountPoints[i].Name)))
                {
                    names[counter++] = Data.MountPoints[i].Name;
                }
            }
            if (counter != (int)Data.Metadata.NumberOfMountPoints)
                Array.Resize(ref names, counter);
            return names;
        }
        public JFSMountPoint[] GetMountPoints(bool includeDeleted = false, bool includeSpecialMountPoints = false)
        {
            // If argument includeSpecialMountPoints is true then we may also return special built-in mount points that we
            // have special classes for and not really handle by the mount point class in this library.
            // Deleted mount points that are still in the trash are not returned by default, but argument includeDeleted can
            // be used to include them.
            CheckCompleteData();
            JFSMountPoint[] mountPoints = new JFSMountPoint[Data.Metadata.NumberOfMountPoints];
            int counter = 0;
            for (ulong i = 0; i < Data.Metadata.NumberOfMountPoints; ++i)
            {
                if ((includeDeleted || Data.MountPoints[i].Deleted == null)
                 && (includeSpecialMountPoints || !IsSpecialBuiltinMountPoint(Data.MountPoints[i].Name)))
                {
                    mountPoints[counter++] = new JFSMountPoint(FileSystem, FullName, Data.MountPoints[i]);
                }
            }
            if (counter != (int)Data.Metadata.NumberOfMountPoints)
                Array.Resize(ref mountPoints, counter);
            return mountPoints;
        }
        public JFSMountPoint NewMountpoint(string name, bool allowCreatingOnBuiltinDevice = false)
        {
            // Create a new mount point and return the new JFSMountPoint.
            // NB: This is intended for custom devices (backup feature), where each backup configuration on the device gets
            // its own mount point. These mount points will be shown within the backup feature in the Jottacloud Web UI.
            // But you are actually allowed to create new mount ponits also for the built-in "Jotta" device. These will not
            // be shown in the Web interface so you can only work with them via this library (REST API), so you should
            // be a bit careful with doing this.
            // NB: Throws exception if mount point name already exists.
            if (!allowCreatingOnBuiltinDevice && IsBuiltInDevice)
            {
                // NB: Use with care! This is intended for custom devices (backup feature), where each backup configuration
                // on the device gets its own mount point. These mount points will be shown within the backup feature in 
                // the Jottacloud Web UI. But you are actually allowed to create new mount ponits also for the built-in "Jotta"
                // device. These will not be shown in the Web interface so you can only work with them via this library (REST API)!
                // NB: Throws exception if mount point name already exists.
                throw new InvalidOperationException("You are not allowed to create new mount points on the built-in device!");
            }
            if (IsBuiltinMountPoint(name))
            {
                throw new InvalidOperationException("You cannot create a built-in mount point!");
            }
            var url = FullName + "/" + name;
            // NB: Actually we can create a folder/subfolder path in the same operation by adding the "mkDir=true"
            // query parameter that we use when creating folder within mount points. A difference then is that the status
            // code for success is "OK" (like when creating folders) instead of "Created", and the returned object
            // is the last folder in the path. Could perhaps be a nice feature, but then if we must verify the mount point
            // name we must split the input argument etc..
            var newMountPointData = FileSystem.RequestPost<JFSData.JFSMountPointData>(url, ExpectedStatus: HttpStatusCode.Created); // Returns the new JFSMountPoint, which is complete (with path) except does not have the metadata element on it!! NB: The POST for new mount point returns Created state if success, and it returns OK if it already exists.
            if (FileSystem.AutoFetchCompleteData)
                FetchCompleteData(); // Re-load the current (parent) device also, so that it includes information about the new mount point?
            return new JFSMountPoint(FileSystem, newMountPointData, true); // NB: We indicate it is complete, although it does not have the metadata child element which it has in result from GET request!

        }
        public void DeleteMountPointPermanently(string name, bool allowDeletingFromBuiltinDevice = false)
        {
            if (!allowDeletingFromBuiltinDevice && IsBuiltInDevice)
            {
                throw new InvalidOperationException("You are not allowed to delete mount points from the built-in device!");
            }
            if (IsBuiltinMountPoint(name))
            {
                throw new InvalidOperationException("You cannot delete a built-in mount point!");
            }
            // Delete mount point permanently, without possibility to restore from trash!
            var parameters = new Dictionary<string, string> { { "rm", "true" } };
            var deviceData = FileSystem.RequestPost<JFSData.JFSDeviceData>(FullName + "/" + name, parameters); // NB: Returns in-complete device data!
            if (FileSystem.AutoFetchCompleteData)
                FetchCompleteData(); // Re-load the current (parent) folder also, so that it includes information about the new sub-folder?
        }
        public void DeleteMountPointPermanently(JFSMountPoint mountPoint)
        {
            DeleteMountPointPermanently(mountPoint.Name);
        }
    }

    //
    // Interface describing the shared logic between mount points and folders.
    //
    public abstract class JFSBasicFolder<DataObjectType> : JFSNamedAndPathedObject<DataObjectType> where DataObjectType : JFSData.JFSFolderBaseData
    {
        public string OriginalParentPath { get { return Data.OriginalPath != null ? FileSystem.ConvertFromDataPath(Data.OriginalPath) + "/" : null; } } // If deleted this contains the full path to the original parent, since ParentPath is the path within Trash. For deleted folders this is present also in incomplete data objects
        public string OriginalFullName { get { return OriginalParentPath == null ? null : OriginalParentPath + Name; } } // If deleted this contains the original Full path of this object. Never ending with path separator.
        public ulong NumberOfFilesAndFolders { get { return Data.Metadata != null ? Data.Metadata.Total : 0; } }
        public ulong NumberOfFolders { get { return Data.Metadata != null ? Data.Metadata.NumberOfFolders : 0; } }
        public ulong NumberOfFiles { get { return Data.Metadata != null ? Data.Metadata.NumberOfFiles : 0; } }

        public JFSBasicFolder(Jottacloud fileSystem, DataObjectType dataWithPath, bool isCompleteData) : base(fileSystem, dataWithPath, isCompleteData) { }
        public JFSBasicFolder(Jottacloud fileSystem, string parentFullName, DataObjectType incompleteDataWithoutPath) : base(fileSystem, parentFullName, incompleteDataWithoutPath) { }
        public string[] GetFileNames(bool includeDeleted = false)
        {
            // Deleted files that are still in the trash are not returned by default, but argument includeDeleted can be used to include them.
            string[] files = new string[Data.Metadata.NumberOfFiles];
            int counter = 0;
            for (ulong i = 0; i < Data.Metadata.NumberOfFiles; i++)
            {
                if (includeDeleted || Data.Files[i].Deleted == null)
                    files[counter++] = Data.Files[i].Name;
            }
            if (counter != (int)Data.Metadata.NumberOfFiles)
                Array.Resize(ref files, counter);
            return files;
        }
        public string[] GetFoldersNames(bool includeDeleted = false)
        {
            // Deleted folders that are still in the trash are not returned by default, but argument includeDeleted can be used to include them.
            string[] folders = new string[Data.Metadata.NumberOfFolders];
            int counter = 0;
            for (ulong i = 0; i < Data.Metadata.NumberOfFolders; i++)
            {
                if (includeDeleted || Data.Folders[i].Deleted == null)
                    folders[counter++] = Data.Folders[i].Name;
            }
            if (counter != (int)Data.Metadata.NumberOfFolders)
                Array.Resize(ref folders, counter);
            return folders;
        }
        public bool ContainsFile(string name, bool includeDeleted = false)
        {
            // Deleted files that are still in the trash are not returned by default, but argument includeDeleted can be used to include them.
            for (ulong i = 0; i < Data.Metadata.NumberOfFiles; i++)
            {
                if (string.Equals(Data.Files[i].Name, name, StringComparison.OrdinalIgnoreCase)) // NB: Might as well ignore case since REST API is case insensitive!
                {
                    if (includeDeleted || Data.Files[i].Deleted == null)
                        return true;
                    // else: return false? Meaning two files with same cannot exist at the same time, one being deleted and the other not??
                }
            }
            return false;
        }
        public bool ContainsFolder(string name, bool includeDeleted = false)
        {
            // Deleted folders that are still in the trash are not returned by default, but argument includeDeleted can be used to include them.
            for (ulong i = 0; i < Data.Metadata.NumberOfFolders; i++)
            {
                if (string.Equals(Data.Folders[i].Name, name, StringComparison.OrdinalIgnoreCase)) // NB: Might as well ignore case since REST API is case insensitive!
                {
                    if (includeDeleted || Data.Folders[i].Deleted == null)
                        return true;
                    // else: return false? Meaning two folder with same cannot exist at the same time, one being deleted and the other not??
                }
            }
            return false;
        }

        public JFSBasicFile[] GetFiles(bool includeDeleted = false)
        {
            // Deleted files that are still in the trash are not returned by default, but argument includeDeleted can be used to include them.
            CheckCompleteData();
            JFSBasicFile[] files = new JFSBasicFile[Data.Metadata.NumberOfFiles];
            int counter = 0;
            for (ulong i = 0; i < Data.Metadata.NumberOfFiles; i++)
            {
                if (includeDeleted || Data.Files[i].Deleted == null)
                    files[counter++] = JFSBasicFile.Manufacture(FileSystem, FullName, Data.Files[i]); // FileBase factory method will decide between JFSFile, JFSIncompleteFile or JFSCorruptFile!
            }
            if (counter != (int)Data.Metadata.NumberOfFiles)
                Array.Resize(ref files, counter);
            return files;
        }
        public JFSFolder[] GetFolders(bool includeDeleted = false)
        {
            // Deleted folders that are still in the trash are not returned by default, but argument includeDeleted can be used to include them.
            CheckCompleteData();
            JFSFolder[] folders = new JFSFolder[Data.Metadata.NumberOfFolders];
            int counter = 0;
            for (ulong i = 0; i < Data.Metadata.NumberOfFolders; i++)
            {
                if (includeDeleted || Data.Folders[i].Deleted == null)
                    folders[counter++] = new JFSFolder(FileSystem, FullName, Data.Folders[i]);
            }
            if (counter != (int)Data.Metadata.NumberOfFolders)
                Array.Resize(ref folders, counter);
            return folders;
        }
        public JFSBasicFile GetFile(string name, bool includeDeleted = false)
        {
            // Deleted files that are still in the trash are not returned by default, but argument includeDeleted can be used to include them.
            CheckCompleteData();
            for (ulong i = 0; i < Data.Metadata.NumberOfFiles; i++)
            {
                if (string.Equals(Data.Files[i].Name, name, StringComparison.OrdinalIgnoreCase)) // NB: Might as well ignore case since REST API is case insensitive!
                {
                    if (includeDeleted || Data.Files[i].Deleted == null)
                        return JFSBasicFile.Manufacture(FileSystem, FullName, Data.Files[i]); // FileBase factory method will decide between JFSFile, JFSIncompleteFile or JFSCorruptFile!
                    else
                        return null;
                }
            }
            return null;
        }
        public JFSFolder GetFolder(string name, bool includeDeleted = false)
        {
            // Deleted folders that are still in the trash are not returned by default, but argument includeDeleted can be used to include them.
            CheckCompleteData();
            for (ulong i = 0; i < Data.Metadata.NumberOfFolders; i++)
            {
                if (string.Equals(Data.Folders[i].Name, name, StringComparison.OrdinalIgnoreCase)) // NB: Might as well ignore case since REST API is case insensitive!
                {
                    if (includeDeleted || Data.Folders[i].Deleted == null)
                        return new JFSFolder(FileSystem, FullName, Data.Folders[i]);
                    else
                        return null;
                }
            }
            return null;
        }
        public List<KeyValuePair<string, JFSFileSystemInfo[]>> GetFileTree()
        {
            // For folder and mount point objects we can get a full folder list containing all folders and all files
            // from the specified level. It is not a regular folder tree structure like in Windows Explorer, but it is a
            // list of folders (including the specified root folder) with contents. This structure is retrieved as a special
            // data object FileDirList, which we get by adding the query parameter "mode=list" to the regular mount point or
            // folder query. Without this parameter we get the contents of the specified folder/mount point only (immediate
            // files and name of sub-folders).
            // Since we do not have a shared base object for mount points and folders, and also since we do not have
            // to keep the data object alive since we get complete information immediately and convert into our own
            // structure, this functionality is implemented just as a method here on top level.
            // Some notes:
            // The incomplete file data contains MD5 hash, but corrupt files does not have this set. Also incomplete files
            // does not have the size property set when fetched from FileDirList, must fetch complete data to get this.
            var queryParameters = new Dictionary<string, string> { { "mode", "list" } };
            var fileDirDataObject = FileSystem.RequestGetDataObject<JFSData.JFSFileDirListData>(FullName, queryParameters);
            // Build a simple file tree using basic information from the FileDirList.
            // Representing it as a map containing all files (no folders), where the path is the key and
            // a basic structure with {name, size, md5, uuid, state} is the value.
            // Size is the full size for complete files, partial uploaded size for incomplete files, and
            // it is null for corrupt files. MD5 hash is null for corrupt files (in this case).
            var fileTree = new List<KeyValuePair<string, JFSFileSystemInfo[]>>();
            for (int i = 0; i < fileDirDataObject.Folders.Length; i++)
            {
                var folderData = fileDirDataObject.Folders[i];
                string folderPath = folderData.Path + "/" + folderData.Name;
                long numberOfFiles = folderData.Files != null ? folderData.Files.LongLength : 0; // Important with null check: Empty folder is folder element without files children in XML leading to null when deserialized!
                var folderFiles = new JFSFileSystemInfo[numberOfFiles];
                for (int j = 0; j < numberOfFiles; j++)
                {
                    var fileData = folderData.Files[j];
                    if (fileData.CurrentRevision != null)
                    {
                        // A normal file: Get info from CurrentRevision.
                        folderFiles[j] = new JFSFileSystemInfo(fileData.Name, fileData.UUID, fileData.CurrentRevision.State, fileData.CurrentRevision.Size, fileData.CurrentRevision.MD5);
                    }
                    else if (fileData.LatestRevision != null)
                    {
                        if (fileData.LatestRevision.State == JFSData.JFSDataFileState.Incomplete)
                        {
                            // A incomplete file: Get info from LatestRevision. Size is missing when fetching data from JFSFileDirList.
                            folderFiles[j] = new JFSFileSystemInfo(fileData.Name, fileData.UUID, fileData.LatestRevision.State, null, fileData.LatestRevision.MD5);
                        }
                        else if (fileData.LatestRevision.State == JFSData.JFSDataFileState.Corrupt)
                        {
                            // A corrupt file: Get info from LatestRevision. Size and MD5 is missing.
                            folderFiles[j] = new JFSFileSystemInfo(fileData.Name, fileData.UUID, fileData.LatestRevision.State, null, null);
                        }
                        else
                        {
                            throw new NotImplementedException(String.Format("No JFSFile support for state %d. Please file a bug!", fileData.LatestRevision.State));
                        }
                    }
                    else
                    {
                        throw new NotImplementedException("Missing revision for JFSFile. Please file a bug!");
                    }
                }
                fileTree.Add(new KeyValuePair<string, JFSFileSystemInfo[]>(folderPath, folderFiles));
            }
            return fileTree;
        }
        public virtual JFSFolder NewFolder(string nameOrSubfolderPath)
        {
            // Create a new sub-folder and return the new JFSFolder.
            // NB: Input can be name of subfolder, or a path relative to the current folder in which case all intermediate folders along the way will also be created.
            // TODO: Does not check if it already exists, then the  existing folder will be returned.
            var parameters = new Dictionary<string, string> { { "mkDir", "true" } };
            var path = CreateChildPath(nameOrSubfolderPath);
            var newFolderData = FileSystem.RequestPost<JFSData.JFSFolderData>(path, parameters); // Returns the new JFSFolderData, which is complete (with path) except does not have the metadata element on it!! NB: The POST request for new folder returns OK if success but also if it already exists!
            if (FileSystem.AutoFetchCompleteData)
                FetchCompleteData(); // Re-load the current (parent) folder also, so that it includes information about the new sub-folder?
            newFolderData.Metadata = new JFSData.JFSFolderMetaData() { First = 0, Max = 0, Total = 0, NumberOfFiles = 0, NumberOfFolders = 0 }; // The data object is almost complete, but without metadata so we create it here!
            return new JFSFolder(FileSystem, newFolderData, true); // NB: We indicate it is complete, since we have created the metadata which it did not have in the result from GET request!
        }
        public abstract void Delete();
        public void Restore()
        {
            throw new NotImplementedException(); // TODO!
        }
        public virtual void DeleteFolderPermanently(string name)
        {
            // Delete filer or folder permanently, without possibility to restore from trash!
            var parameters = new Dictionary<string, string> { { "rmDir", "true" } };
            var mountPointData = FileSystem.RequestPost<JFSData.JFSMountPointData>(FullName + "/" + name, parameters); // NB: Returns (in-complete?) mount point data!
            if (FileSystem.AutoFetchCompleteData)
                FetchCompleteData(); // Re-load the current (parent) folder also, so that it includes information about the new sub-folder?
        }
        public void DeleteFolderPermanently(JFSFolder folder)
        {
            DeleteFolderPermanently(folder.Name);
        }
        public virtual void DeleteFilePermanently(string name)
        {
            // Delete filer or folder permanently, without possibility to restore from trash!
            var parameters = new Dictionary<string, string> { { "rm", "true" } };
            var newMountPointData = FileSystem.RequestPost<JFSData.JFSMountPointData>(FullName + "/" + name, parameters); // Returns user data
            if (FileSystem.AutoFetchCompleteData)
                FetchCompleteData(); // Re-load the current (parent) folder also, so that it includes information about the new sub-folder?
        }
        public void DeleteFilePermanently(JFSBasicFile file)
        {
            DeleteFilePermanently(file.Name);
        }
        public virtual JFSBasicFile UploadFile(string filePath)
        {
            // Upload a file to current folder and return the new JFSFile
            FileInfo fileInfo = new FileInfo(filePath);
            string jfsPath = FullName + "/" + fileInfo.Name;
            var newFileData = FileSystem.UploadSimple(jfsPath, fileInfo); // Returns the new JFSFileData, which is not complete because it often misses the path element event!?
            var fileObject = JFSBasicFile.Manufacture(FileSystem, FullName, newFileData);  // Cannot trust path being present in returned file data!
            if (FileSystem.AutoFetchCompleteData)
                FetchCompleteData(); // Re-load the current (parent) folder for the updated file to be considered (folders keep revision data about files)?
            return fileObject;
        }
        private void UploadFolder(DirectoryInfo localFolderInfo, bool recurseSubfolders, bool skipIdenticalFiles, string remoteParentPath, bool abortIfFolderExists, ref ulong numberOfFilesProcessed, ref ulong numberOfFilesUploaded, Action<string> logMessageCallback)
        {
            if (!localFolderInfo.Exists)
            {
                throw new InvalidOperationException("The specified local folder does not exists!");
            }
            CheckCompleteData();
            JFSFolder jfsFolder = null;
            if (ContainsFolder(localFolderInfo.Name))
            {
                if (abortIfFolderExists)
                    throw new InvalidOperationException("Folder with same name already exists!");
                jfsFolder = GetFolder(localFolderInfo.Name);
                jfsFolder.FetchCompleteData();
                if (logMessageCallback != null) logMessageCallback("Existing folder " + jfsFolder.FullName);
            }
            else
            {
                jfsFolder = NewFolder(localFolderInfo.Name);
                logMessageCallback?.Invoke("Created folder " + jfsFolder.FullName);
            }
            if (recurseSubfolders)
            {
                foreach (var subFolderInfo in localFolderInfo.GetDirectories())
                {
                    jfsFolder.UploadFolder(subFolderInfo, recurseSubfolders, skipIdenticalFiles, remoteParentPath + subFolderInfo.Name + "/", false, ref numberOfFilesProcessed, ref numberOfFilesUploaded, logMessageCallback); // Argument abortIfFolderExists makes no sense in the recursion
                }
            }
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            foreach (var fileInfo in localFolderInfo.GetFiles())
            {
                string jfsPath = jfsFolder.FullName + "/" + fileInfo.Name;
                JFSData.JFSFileData result = null;
                if (skipIdenticalFiles) {
                    bool wasChanged;
                    stopwatch.Restart();
                    result = FileSystem.UploadIfChanged(jfsPath, fileInfo, out wasChanged);
                    stopwatch.Stop();
                    if (wasChanged) {
                        numberOfFilesUploaded++;
                        logMessageCallback?.Invoke(string.Format("Uploaded new or changed file {0} [{1}] in {2}", jfsPath, JFSData.JFSDataUtilities.HumanizeDataSize((ulong)fileInfo.Length), stopwatch.Elapsed));
                    } else
                        logMessageCallback?.Invoke(string.Format("Verified existing file {0} [{1}] in {2}", jfsPath, JFSData.JFSDataUtilities.HumanizeDataSize((ulong)fileInfo.Length), stopwatch.Elapsed));
                }
                else {
                    stopwatch.Restart();
                    result = FileSystem.UploadSimple(jfsPath, fileInfo); // Returns the new JFSFileData, which is not complete because it often misses the path element event!?
                    stopwatch.Stop();
                    numberOfFilesUploaded++;
                    logMessageCallback?.Invoke(string.Format("Uploaded file {0} [{1}] in {2}", jfsPath, JFSData.JFSDataUtilities.HumanizeDataSize((ulong)fileInfo.Length), stopwatch.Elapsed));
                }
                if (!JFSBasicFile.NewestVersionIsCompleted(result))
                    throw new InvalidOperationException("Upload to " + jfsPath + " failed!");
                numberOfFilesProcessed++;
            }
        }
        public void UploadFolder(string localFolderPath, bool recurseSubfolders, bool skipIdenticalFiles, bool abortIfFolderExists, Action<string> logMessageCallback, ref ulong numberOfFilesProcessed, ref ulong numberOfFilesUploaded)
        {
            UploadFolder(new DirectoryInfo(localFolderPath), recurseSubfolders, skipIdenticalFiles, FullName + "/", abortIfFolderExists, ref numberOfFilesProcessed, ref numberOfFilesUploaded, logMessageCallback);
            if (FileSystem.AutoFetchCompleteData)
                FetchCompleteData(); // Re-load the current (parent) folder for the updated file to be considered (folders keep revision data about files)?
            logMessageCallback(string.Format("{0} files processed: {1} files uploaded / {2} files up to date", numberOfFilesProcessed, numberOfFilesUploaded, numberOfFilesProcessed - numberOfFilesUploaded));
        }
        public void UploadFolder(string localFolderPath, bool recurseSubfolders, bool skipIdenticalFiles, bool abortIfFolderExists)
        {
            ulong numberOfFilesProcessed = 0, numberOfFilesUploaded = 0;
            UploadFolder(new DirectoryInfo(localFolderPath), recurseSubfolders, skipIdenticalFiles, FullName + "/", abortIfFolderExists, ref numberOfFilesProcessed, ref numberOfFilesUploaded, null);
            if (FileSystem.AutoFetchCompleteData)
                FetchCompleteData(); // Re-load the current (parent) folder for the updated file to be considered (folders keep revision data about files)?
        }
        public void Download(DirectoryInfo localFolderInfo, bool recurseSubfolders, bool skipIdenticalFiles, bool abortIfFolderExists, ref ulong numberOfFilesProcessed, ref ulong numberOfFilesDownloaded, Action<string> logMessageCallback)
        {
            if (abortIfFolderExists && skipIdenticalFiles)
            {
                throw new ArgumentException("Cannot both check for existing files and abort if folder already exist!");
            }
            // Download the current folder into the specified local folder (so current folder will be child of the specified local folder!).
            CheckCompleteData();
            // Check if the local folder exists
            if (localFolderInfo.Exists)
            {
                if (abortIfFolderExists)
                    throw new InvalidOperationException("The specified local folder already exist!");
            }
            else
            {
                localFolderInfo.Create();
                logMessageCallback?.Invoke("Created folder " + localFolderInfo.FullName);
            }
            // Create the current folder
            localFolderInfo = new DirectoryInfo(localFolderInfo.FullName + "\\" + this.Name);
            if (!localFolderInfo.Exists)
            {
                localFolderInfo.Create();
                logMessageCallback?.Invoke("Created folder " + localFolderInfo.FullName);
            }
            // Recurse subfolders
            if (recurseSubfolders)
            {
                foreach (var jfsFolder in GetFolders())
                {
                    jfsFolder.FetchCompleteData();
                    jfsFolder.Download(localFolderInfo, recurseSubfolders, skipIdenticalFiles, false, ref numberOfFilesProcessed, ref numberOfFilesDownloaded, logMessageCallback); // Argument abortIfFolderExists makes no sense in the recursion
                }
            }
            // Process files
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            foreach (var jfsBasicFile in GetFiles())
            {
                // Ensure file has a valid version, and not only incomplete/corrupt versions.
                if (jfsBasicFile.IsValid) // if (jfsBasicFile is JFSFile)
                {
                    // Download the current valid version of the file.
                    var jfsFile = (JFSFile)jfsBasicFile;
                    jfsFile.FetchCompleteData();
                    FileInfo localFileInfo = new FileInfo(localFolderInfo.FullName + "\\" + jfsFile.Name);
                    if (localFileInfo.Exists && skipIdenticalFiles)
                    {
                        stopwatch.Restart();
                        bool wasChanged = jfsFile.UpdateLocal(localFileInfo);
                        stopwatch.Stop();
                        if (wasChanged)
                        {
                            numberOfFilesDownloaded++;
                            if (logMessageCallback != null)
                            {
                                localFileInfo.Refresh(); // Need to re-load information after file has been created to get the Length property!
                                logMessageCallback?.Invoke(string.Format("Downloaded new or changed file {0} from {1} [{2}] in {3}", localFileInfo.FullName, jfsFile.FullName, JFSData.JFSDataUtilities.HumanizeDataSize((ulong)localFileInfo.Length), stopwatch.Elapsed));
                            }
                        }
                        else
                            logMessageCallback?.Invoke(string.Format("Verified existing file {0} from {1} [{2}] in {3}", localFileInfo.FullName, jfsFile.FullName, JFSData.JFSDataUtilities.HumanizeDataSize((ulong)localFileInfo.Length), stopwatch.Elapsed));
                    }
                    else
                    {
                        stopwatch.Restart();
                        jfsFile.Download(localFileInfo, true);
                        stopwatch.Stop();
                        numberOfFilesDownloaded++;
                        if (logMessageCallback != null)
                        {
                            localFileInfo.Refresh(); // Need to re-load information after file has been created to get the Length property!
                            logMessageCallback.Invoke(string.Format("Downloaded file {0} from {1} [{2}] in {3}", localFileInfo.FullName, jfsFile.FullName, JFSData.JFSDataUtilities.HumanizeDataSize((ulong)localFileInfo.Length), stopwatch.Elapsed));
                        }
                    }
                    numberOfFilesProcessed++;
                }
                else
                {
                    logMessageCallback?.Invoke(string.Format("Skipping file {0} because remote file {1} it is not complete", jfsBasicFile.FullName, jfsBasicFile.FullName));
                }
            }
        }

        public void Download(string localFolderPath, bool recurseSubfolders, bool skipIdenticalFiles, bool abortIfFolderExists, ref ulong numberOfFilesProcessed, ref ulong numberOfFilesDownloaded, Action<string> logMessageCallback)
        {
            Download(new DirectoryInfo(localFolderPath), recurseSubfolders, skipIdenticalFiles, abortIfFolderExists, ref numberOfFilesProcessed, ref numberOfFilesDownloaded, logMessageCallback);
            logMessageCallback(string.Format("{0} files processed: {1} files uploaded / {2} files up to date", numberOfFilesProcessed, numberOfFilesDownloaded, numberOfFilesProcessed - numberOfFilesDownloaded));
        }
        public void Download(string localFolderPath, bool recurseSubfolders, bool skipIdenticalFiles, bool abortIfFolderExists)
        {
            ulong numberOfFilesProcessed = 0, numberOfFilesUploaded = 0;
            Download(new DirectoryInfo(localFolderPath), recurseSubfolders, skipIdenticalFiles, abortIfFolderExists, ref numberOfFilesProcessed, ref numberOfFilesUploaded, null);
        }
    }

    //
    // Mount point is more or less just a special kind of folder, but with some differences in the data structure so
    // it needs its own data object type. This class is for the mount points that support regular file operations:
    // The "Archive" and "Sync" mount points on the built-in "Jotta" device, and any mount points on user defined
    // devices (which is for the backup feature). The special mount points "Latest" and "Share" are handled by their
    // own specialized classes, since they cannot be used with regular file operations.
    //
    public sealed class JFSMountPoint : JFSBasicFolder<JFSData.JFSMountPointData>
    {
        public DateTime? Deleted { get { if (Data.Deleted != null) return Data.Deleted.DateTime; return null; } }
        public bool IsDeleted { get { return Deleted != null; } }
        public ulong SizeInBytes { get { return Data.Size.Value; } }
        public string Size { get { return Data.Size.ToString(); } }
        public DateTime? Modified { get { if (Data.Modified != null) return Data.Modified.DateTime; return null; } }
        public string DeviceName { get { return Data.Device; } } // Only in complete data
        public string UserName { get { return Data.User; } } // Only in complete data
        public JFSMountPoint(Jottacloud fileSystem, JFSData.JFSMountPointData dataWithPath, bool isCompleteData) : base(fileSystem, dataWithPath, isCompleteData) {}
        public JFSMountPoint(Jottacloud fileSystem, string parentFullName, JFSData.JFSMountPointData incompleteDataWithoutPath) : base(fileSystem, parentFullName, incompleteDataWithoutPath) { }
        public override void Delete()
        {
            // Delete this mount point and return a deleted JFSMountPoint.
            var parameters = new Dictionary<string, string> { { "dl", "true" } };
            var newMountPointData = FileSystem.RequestPost<JFSData.JFSMountPointData>(FullName, parameters); // Returns the deleted JFSMountPointData, which is complete (and now with a deleted timestamp on it)!
            SetData(newMountPointData); // Replace existing data object with the new one!
            // NB: Any parent objects (device), or child objects that have been deleted with this mount point, that the client keeps needs to be refreshed for the delete to be considered!
        }
        public void DeletePermanently()
        {
            throw new InvalidOperationException("Permanent deletion must be done from the parent object!");
        }
        // TODO: Rename, Move, Restore, etc? Don't know if it is possible on mount points. For move, tried both "mv" and "mvDir", but does not work (but when moving files/folders we can move them between mount points!)
    }

    //
    // Trash is a mount point object in the REST API, but it has some special aspects so we specialize it.
    // One thing is that when mount points are deleted they are moved to the trash mount point and listed
    // as folders, while when we request complete information about we get mount point data object!
    // Also delete/restore are special for the trash folder!
    //
    public sealed class JFSTrash : JFSNamedAndPathedObject<JFSData.JFSMountPointData>
    {
        public JFSTrash(Jottacloud fileSystem, JFSData.JFSMountPointData completeData) : base(fileSystem, completeData, true) {} // Since trash is not listed on device, we always have to fetch it directly - and then data is always complete!
        public JFSBasicFile[] GetFiles()
        {
            CheckCompleteData();
            JFSBasicFile[] files = new JFSBasicFile[Data.Metadata.NumberOfFiles];
            for (ulong i = 0; i < Data.Metadata.NumberOfFiles; i++)
            {
                files[i] = JFSBasicFile.Manufacture(FileSystem, FullName, Data.Files[i]); // FileBase factory method will decide between JFSFile, JFSIncompleteFile or JFSCorruptFile!
            }
            return files;
        }
        public JFSFolder[] GetFolders()
        {
            CheckCompleteData();
            JFSFolder[] folders = new JFSFolder[Data.Metadata.NumberOfFolders];
            int counter = 0;
            for (ulong i = 0; i < Data.Metadata.NumberOfFolders; i++)
            {
                if (!IsMountPointDataObject(Data.Folders[i])) // Skip objects listed in "folders" that are actually mount points!
                    folders[counter++] = new JFSFolder(FileSystem, FullName, Data.Folders[i]);
            }
            if (counter != (int)Data.Metadata.NumberOfFolders)
                Array.Resize(ref folders, counter);
            return folders;
        }
        public JFSMountPoint[] GetMountPoints()
        {
            CheckCompleteData();
            JFSMountPoint[] mountPoints = new JFSMountPoint[Data.Metadata.NumberOfFolders];
            int counter = 0;
            for (ulong i = 0; i < Data.Metadata.NumberOfFolders; i++)
            {
                if (IsMountPointDataObject(Data.Folders[i])) // Include only objects listed in "folders" that are actually mount points!
                    mountPoints[counter++] = new JFSMountPoint(FileSystem, FullName, ConvertToMountPointDataObject(Data.Folders[i]));
            }
            if (counter != (int)Data.Metadata.NumberOfFolders)
                Array.Resize(ref mountPoints, counter);
            return mountPoints;
        }
        public JFSBasicFile GetFile(string name)
        {
            // Deleted files that are still in the trash are not returned by default, but argument includeDeleted can be used to include them.
            CheckCompleteData();
            for (ulong i = 0; i < Data.Metadata.NumberOfFiles; i++)
            {
                if (string.Equals(Data.Files[i].Name, name, StringComparison.OrdinalIgnoreCase)) // NB: Might as well ignore case since REST API is case insensitive!
                {
                    return JFSBasicFile.Manufacture(FileSystem, FullName, Data.Files[i]); // FileBase factory method will decide between JFSFile, JFSIncompleteFile or JFSCorruptFile!
                }
            }
            return null;
        }
        public JFSFolder GetFolder(string name)
        {
            // Deleted folders that are still in the trash are not returned by default, but argument includeDeleted can be used to include them.
            CheckCompleteData();
            for (ulong i = 0; i < Data.Metadata.NumberOfFolders; i++)
            {
                if (string.Equals(Data.Folders[i].Name, name, StringComparison.OrdinalIgnoreCase)) // NB: Might as well ignore case since REST API is case insensitive!
                {
                    if (!IsMountPointDataObject(Data.Folders[i])) // Skip objects listed in "folders" that are actually mount points!
                        return new JFSFolder(FileSystem, FullName, Data.Folders[i]);
                    else
                        return null;
                }
            }
            return null;
        }
        public JFSMountPoint GetMountPoint(string name)
        {
            // Deleted folders that are still in the trash are not returned by default, but argument includeDeleted can be used to include them.
            CheckCompleteData();
            for (ulong i = 0; i < Data.Metadata.NumberOfFolders; i++)
            {
                if (string.Equals(Data.Folders[i].Name, name, StringComparison.OrdinalIgnoreCase)) // NB: Might as well ignore case since REST API is case insensitive!
                {
                    if (Data.Folders[i].OriginalPath == "/" + Data.User + "/" + Data.Device) // Include only objects listed in "folders" that are actually mount points!
                        new JFSMountPoint(FileSystem, FullName, ConvertToMountPointDataObject(Data.Folders[i]));
                    else
                        return null;
                }
            }
            return null;
        }
        private bool IsMountPointDataObject(JFSData.JFSFolderData folderData)
        {
            // Return true if abspath in data object is /[user]/[device] (and name is [mountpoint])
            return FileSystem.ConvertFromDataPath(folderData.OriginalPath).IndexOf("/", 1) == -1; // Trim off "/[user]" leaving "/[device]", and then check if there is more than the initial "/" present!
        }
        private JFSData.JFSMountPointData ConvertToMountPointDataObject(JFSData.JFSFolderData folderData)
        {
            // Convert from JFSFolderData to JFSMountPointData
            return new JFSData.JFSMountPointData()
            {
                NameData = new JFSData.JFSDataStringWithWhiteSpaceHandling() { Space = "preserve", String = folderData.Name },
                Deleted = new JFSData.JFSDataDateTime() { String = folderData.DeletedString },
                OriginalPathData = new JFSData.JFSDataStringWithWhiteSpaceHandling() { Space = folderData.OriginalPathData.Space, String = folderData.OriginalPathData.String }
            };
        }
    }

    public sealed class JFSFolder : JFSBasicFolder<JFSData.JFSFolderData>
    {
        public bool IsDeleted { get { return Data.Deleted != null; } }
        public JFSFolder(Jottacloud fileSystem, JFSData.JFSFolderData dataWithPath, bool isCompleteData) : base(fileSystem, dataWithPath, isCompleteData) { }
        public JFSFolder(Jottacloud fileSystem, string parentFullName, JFSData.JFSFolderData incompleteDataWithoutPath) : base(fileSystem, parentFullName, incompleteDataWithoutPath) { }
        public void Move(string newPath)
        {
            // Move/rename folder to a new name, possibly a whole new path - creating any intermediate folders along the way.
            // NB: The specified newPath must be a complete path, starting with the device: [device]/[mountpoint]/[new_path...].
            //     In the underlying API paths begin with [username], but here we perpend that automatically since we are not
            //     working across multiple users.
            // For example: To move and rename a folder "/OldParentName/OldFolderName" in mount point "Archive" on the
            // default "Jotta" device to path "/NewParentFolderName/NewFolderName" in the same mount point one has to specify
            // newPath as "/Jotta/Archive/NewParentFolderName/NewFolderName".
            // The operation is implemented by sending a POST request like the following:
            // "https://www.jotta.no/jfs/[username]/Jotta/Archive/OldFolderName?mvDir=/[username]/Jotta/Archive/NewParentFolderName/NewFolderName"
            // Returns the moved JFSFolder.
            var moveToPath = FileSystem.ConvertToDataPath(newPath); // Convert to data path, appending the file system root name (username).
            var parameters = new Dictionary<string, string> { { "mvDir", moveToPath } };
            var newFolderData = FileSystem.RequestPost<JFSData.JFSFolderData>(FullName, parameters); // Returns the new JFSFolderData, which is complete (and now with a deleted timestamp on it)!
            SetData(newFolderData); // Replace existing data object with the new one!
            // NB: Any parent objects (folder), or child objects that have been deleted with this folder, that the client keeps needs to be refreshed for the moved to be considered!
        }
        public void Rename(string newName)
        {
            // Rename folder to a new name, or a path relative to the current folder in which case all intermediate folders along the way will also be created.
            Move(ParentPath + newName); // Same parent but different name, and convert to data path, appending the file system root name (username).
        }
        public override void Delete()
        {
            if (IsDeleted)
            {
                throw new NotImplementedException("Deleting folders from Trash not supported yet!");
            }
            // Delete this folder and return a deleted JFSFolder.
            var parameters = new Dictionary<string, string> { { "dlDir", "true" } };
            var newFolderData = FileSystem.RequestPost<JFSData.JFSFolderData>(FullName, parameters); // Returns the deleted JFSFolderData, which is complete (and now with a deleted timestamp on it)!
            SetData(newFolderData); // Replace existing data object with the new one!
            // NB: Any parent objects (folder), or child objects that have been deleted with this folder, that the client keeps needs to be refreshed for the delete to be considered!
        }
        public void DeletePermanently()
        {
            throw new InvalidOperationException("Permanent deletion must be done from the parent object!");
        }
    }

    //
    // Basic file object, base for specialized file objects.
    //
    public abstract class JFSBasicFile : JFSNamedAndPathedObject<JFSData.JFSFileData>
    {
        public DateTime? Deleted { get { return Data.Deleted; } }
        public bool IsDeleted { get { return Deleted != null; } }
        public string SharedSecret { get { return Data.PublicURI; } }
        public bool IsShared { get { return SharedSecret != null; } }
        public string OriginalParentPath { get { return Data.OriginalPath != null ? FileSystem.ConvertFromDataPath(Data.OriginalPath) + "/" : null; } } // If deleted this contains the full path to the original parent, since ParentPath is the path within Trash. For deleted files this is present also in incomplete data objects
        public string OriginalFullName { get { return OriginalParentPath == null ? null : OriginalParentPath + Name; } } // If deleted this contains the original Full path of this object. Never ending with path separator.
        public Guid ID { get { return Data.UUID; } }
        public virtual bool IsValid { get { return Data.CurrentRevision != null; } } // If it is a JFSBasicFile then it has the potential to become a valid JFSFile if is has a valid version. But it might also have a have newer incomplete or corrupt version.
        protected override string CreateChildPath(string childName) { throw new InvalidOperationException(); } // Not supported for files!
        public JFSBasicFile(Jottacloud fileSystem, JFSData.JFSFileData dataWithPath, bool isCompleteData) : base(fileSystem, dataWithPath, isCompleteData) { }
        public JFSBasicFile(Jottacloud fileSystem, string parentFullName, JFSData.JFSFileData incompleteDataWithoutPath) : base(fileSystem, parentFullName, incompleteDataWithoutPath) { }
        public static bool NewestVersionIsCompleted(JFSData.JFSFileData data)
        {
            // Used for checking upload success.
            // If there is a LatestRevision it is a newer and not completed version than some previous complete version (if any at all).
            // I think that if there is a CurrentRevision then it is always complete, but we can test the state just to be on the safe side.
            // When uploading files we must verify that we get data for a valid, completed file back. Because for instance if there is
            // a hash mismatch the upload request succeeds but we get file data with a corrupt latest version back.
            return data.LatestRevision == null && data.CurrentRevision != null && data.CurrentRevision.State == JFSData.JFSDataFileState.Completed;
        }

        public static JFSBasicFile Manufacture(Jottacloud fileSystem, string parentFullName, JFSData.JFSFileData data)
        {
            // Class method to get the correct file class instantiated
            if (data.CurrentRevision != null)
            {
                // A valid file
                return ManufactureFromCurrentVersion(fileSystem, parentFullName, data);
            }
            else
            {
                // A corrupt or incomplete file
                return ManufactureFromLatestVersion(fileSystem, parentFullName, data);
            }
        }
        public static JFSBasicFile Manufacture(Jottacloud fileSystem, JFSData.JFSFileData dataWithPath, bool isCompleteData)
        {
            // Class method to get the correct file class instantiated
            if (dataWithPath.CurrentRevision != null)
            {
                // A valid file
                return ManufactureFromCurrentVersion(fileSystem, dataWithPath, isCompleteData);
            }
            else
            {
                // A corrupt or incomplete file
                return ManufactureFromLatestVersion(fileSystem, dataWithPath, isCompleteData);
            }
        }
        protected static JFSFile ManufactureFromCurrentVersion(Jottacloud fileSystem, JFSData.JFSFileData dataWithPath, bool isCompleteData)
        {
            return new JFSFile(fileSystem, dataWithPath, isCompleteData);
        }
        protected static JFSFile ManufactureFromCurrentVersion(Jottacloud fileSystem, string parentFullName, JFSData.JFSFileData data)
        {
            return new JFSFile(fileSystem, parentFullName, data);
        }
        protected static JFSCorruptFile ManufactureFromLatestVersion(Jottacloud fileSystem, JFSData.JFSFileData dataWithPath, bool isCompleteData)
        {
            if (dataWithPath.LatestRevision != null)
            {
                if (dataWithPath.LatestRevision.State == JFSData.JFSDataFileState.Incomplete)
                {
                    return new JFSIncompleteFile(fileSystem, dataWithPath, isCompleteData);
                }
                else if (dataWithPath.LatestRevision.State == JFSData.JFSDataFileState.Corrupt)
                {
                    return new JFSCorruptFile(fileSystem, dataWithPath, isCompleteData);
                }
                else
                {
                    throw new NotImplementedException(string.Format("No JFSFile support for state %d. Please file a bug!", dataWithPath.LatestRevision.State));
                }
            }
            else
            {
                throw new NotImplementedException("Missing revision for JFSFile. Please file a bug!");
            }
        }
        protected static JFSCorruptFile ManufactureFromLatestVersion(Jottacloud fileSystem, string parentFullName, JFSData.JFSFileData data)
        {
            if (data.LatestRevision != null)
            {
                if (data.LatestRevision.State == JFSData.JFSDataFileState.Incomplete)
                {
                    return new JFSIncompleteFile(fileSystem, parentFullName, data);
                }
                else if (data.LatestRevision.State == JFSData.JFSDataFileState.Corrupt)
                {
                    return new JFSCorruptFile(fileSystem, parentFullName, data);
                }
                else
                {
                    throw new NotImplementedException(String.Format("No JFSFile support for state %d. Please file a bug!", data.LatestRevision.State));
                }
            }
            else
            {
                throw new NotImplementedException("Missing revision for JFSFile. Please file a bug!");
            }
        }

        protected JFSBasicFile VerifyRemote(FileInfo fileInfo, string md5Hash)
        {
            // Check if the specified local file with a specified MD5 hash is matching the current version on server.
            var newFileData = FileSystem.VerifyFile(FullName, fileInfo, md5Hash);  // If match, it returns the JFSFileData of the matching item.
            if (newFileData != null)
            {
                // This means there was a match, and we have gotten back the data for the match.
                // TODO: Can we assume this has the same state as the current, and just update the current data (to get updated timestamps etc)?
                //       Or should we return a newly manufactured file object to make sure it is of the correct type?
                //SetData(newFileData); // Replace existing data object with the new one!
                return Manufacture(FileSystem, newFileData, true);
            }
            else
                return null; // No match!
        }
        public JFSBasicFile VerifyRemote(string filePath)
        {
            // Check if the specified local file is matching the current version on server.
            FileInfo fileInfo = new FileInfo(filePath);
            string md5Hash = Jottacloud.CalculateMD5(fileInfo);
            return VerifyRemote(fileInfo, md5Hash);
        }
        public JFSBasicFile VerifyRemote(string filePath, string md5Hash)
        {
            // Check if the specified local file with a specified MD5 hash is matching the current version on server.
            FileInfo fileInfo = new FileInfo(filePath);
            return VerifyRemote(fileInfo, md5Hash);
        }
        public JFSBasicFile UpdateRemote(string filePath)
        {
            // Put, possibly replace, file contents with (new) data - but check first if it matches the existing file!
            FileInfo fileInfo = new FileInfo(filePath);
            bool wasChanged;
            var newFileData = FileSystem.UploadIfChanged(FullName, fileInfo, out wasChanged);  // Returns the new JFSFileData, which is complete!
            // NB: We cannot just update the current data, since the new upload might be of a different state (e.g. uploaded an incomplete file and get a complete file back)!
            //     SetData(newFileData); // Replace existing data object with the new one!
            return Manufacture(FileSystem, newFileData, true); // Return entirely new object!
            // NB: Any parent objects (folder) that the client keeps needs to be refreshed for the updated file to be considered (folders keep revision data about files)!
        }
        public JFSBasicFile Upload(FileInfo fileInfo)
        {
            // Put, possibly replace, file contents with (new) data.
            var newFileData = FileSystem.UploadSimple(FullName, fileInfo);  // Returns the new JFSFileData, which is complete!
            // NB: We cannot just update the current data, since the new upload might be of a different state (e.g. uploaded an incomplete file and get a complete file back)!
            //     SetData(newFileData); // Replace existing data object with the new one!
            return Manufacture(FileSystem, newFileData, true); // Return entirely new object!
            // NB: Any parent objects (folder) that the client keeps needs to be refreshed for the updated file to be considered (folders keep revision data about files)!
        }
        public JFSBasicFile Upload(string filePath)
        {
            return Upload(new FileInfo(filePath));
        }
    }

    //
    // Corrupt file.
    // You will get a corrupt file if the server detects a hash mismatch between the MD5 and the content supplied.
    // Corrupt files have LatestRevision, possibly in addition to a CurrentRevision: The CurrentRevision is then the
    // most recent complete revision of the file, while the LatestRevision is the most recent upload which did not
    // complete. The revision number of the LatestVersion should always be larger than that of the CurrentRevision,
    // because when an upload successfully completes it will be stored as a new CurrentRevision and the existing
    // LatestRevision will be deleted (moved to the "revisions" list).
    // The LatestRevision should have state set to "Corrupted", it does not have a size attribute, and it may
    // or may not have an MD5 hash (currently it seems uploading with hash mismatch actually stores the supplied
    // MD5).
    //
    public class JFSCorruptFile : JFSBasicFile
    {
        public virtual ulong RevisionNumber { get { return Data.LatestRevision.Number; } }
        public virtual DateTime? Created { get { return Data.LatestRevision.Created.DateTime; } }
        public virtual DateTime? Modified { get { return Data.LatestRevision.Modified.DateTime; } }
        public virtual DateTime? Updated { get { return Data.LatestRevision.Updated.DateTime; } }
        public virtual string MD5 { get { return Data.LatestRevision.MD5; } }
        public virtual System.Net.Mime.ContentType Mime { get { return Data.LatestRevision.Mime.Mime; } }
        public virtual JFSData.JFSDataFileState State { get { return Data.LatestRevision.State; } }
        public override bool IsValid { get { return false; } } // If it is a JFSCorruptFile or JFSIncompleteFile then it is by definition an invalid file.
        public long NumberOfVersions { get { return (HasOlderValidVersion ? 2 : 1) + (Data.OldRevisions != null ? Data.OldRevisions.Length : 0); } } // This, the LatestRevision counts 1. If there is a previous completed version it counts +1. And if there are more old revisions they count as well.
        public bool HasOlderValidVersion { get { return Data.CurrentRevision != null; } }
        public JFSFile GetValidVersion() { return HasOlderValidVersion ? ManufactureFromCurrentVersion(FileSystem, Data, CompleteData) : null; }
        public JFSFile GetOldVersion(int steps = 1) { throw new NotImplementedException(); }
        public JFSCorruptFile(Jottacloud fileSystem, JFSData.JFSFileData dataWithPath, bool isCompleteData) : base(fileSystem, dataWithPath, isCompleteData) { }
        public JFSCorruptFile(Jottacloud fileSystem, string parentFullName, JFSData.JFSFileData incompleteDataWithoutPath) : base(fileSystem, parentFullName, incompleteDataWithoutPath) {}
        public bool VerifyLocal(string filePath) { return VerifyLocal(new FileInfo(filePath)); } // Check if the specified local file is matching the current item (not checking upstream).
        public bool VerifyLocal(FileInfo fileInfo) { return MD5 != null && Jottacloud.CalculateMD5(fileInfo) == MD5; } // Check if the specified local file is matching the current item (not checking upstream).
        protected JFSBasicFile VerifyRemoteExistingHash(FileInfo fileInfo) { if (MD5 == null) throw new InvalidOperationException("MD5 hash missing for current file item!"); return VerifyRemote(fileInfo, MD5); }
        private JFSBasicFile VerifyRemoteExistingHash(string filePath) { return VerifyRemoteExistingHash(new FileInfo(filePath)); }
    }

    //
    // Incomplete file.
    // You will get an incomplete file if the server detects a mismatch between the file size specified and the size of the content.
    // Like a corrupt file, but with MD5 hash and with a Size property keeping number of bytes transfered so far,
    // and with a Resume method for continuing the transfer.
    //
    public class JFSIncompleteFile : JFSCorruptFile
    {
        public virtual ulong SizeInBytes { get { return Data.LatestRevision.Size.Value; } } // Bytes uploaded of the file so far. Note that we only have the file size if the file was requested directly, not if it's part of a folder listing.
        public virtual string Size { get { return Data.LatestRevision.Size.ToString(); } }
        public JFSIncompleteFile(Jottacloud fileSystem, JFSData.JFSFileData dataWithPath, bool isCompleteData) : base(fileSystem, dataWithPath, isCompleteData) { }
        public JFSIncompleteFile(Jottacloud fileSystem, string parentFullName, JFSData.JFSFileData incompleteDataWithoutPath) : base(fileSystem, parentFullName, incompleteDataWithoutPath) { }
        public JFSBasicFile ResumeUpload(string filePath)
        {
            if (!CompleteData)
            {
                throw new InvalidOperationException("Complete data needed for this operation, call .FetchCompleteData() and try again!");
            }
            // Check if what we're asked to upload is actually the right file
            FileInfo fileInfo = new FileInfo(filePath);
            if (VerifyLocal(fileInfo))
            {
                // Upload from offset according to existing size
                var newFileData = FileSystem.UploadSimple(FullName, fileInfo, (long)SizeInBytes); // Returns the new JFSFileData, which is complete!
                // NB: We cannot just update the current data, since the new upload might be of a different state (e.g. uploaded an incomplete file and get a complete file back)!
                //     SetData(newFileData); // Replace existing data object with the new one!
                return Manufacture(FileSystem, newFileData, true); // Return entirely new object!
                // NB: Any parent objects (folder) that the client keeps needs to be refreshed for the updated file to be considered (folders keep revision data about files)!
            } else
                throw new JFSError("MD5 hashes don't match! Are you trying to resume with the wrong file?");
        }
    }

    // Normal, complete, file. Unlike incomplete and corrupt files it has a CurrentRevision.
    public sealed class JFSFile : JFSIncompleteFile
    {
        public enum ThumbSize
        {
            Small,
            Medium,
            Large,
            ExtraLarge,
        }
        public bool IsImage { get { return Mime.MediaType == "image"; } } // SUb-classes implements Mime
        public override ulong RevisionNumber { get { return Data.CurrentRevision.Number; } }
        public override DateTime? Created { get { return Data.CurrentRevision.Created.DateTime; } }
        public override DateTime? Modified { get { return Data.CurrentRevision.Modified.DateTime; } }
        public override DateTime? Updated { get { return Data.CurrentRevision.Updated.DateTime; } }
        public override ulong SizeInBytes { get { return Data.CurrentRevision.Size.Value; } }
        public override string Size { get { return Data.CurrentRevision.Size.ToString(); } }
        public override string MD5 { get { return Data.CurrentRevision.MD5; } }
        public override System.Net.Mime.ContentType Mime { get { return Data.CurrentRevision.Mime.Mime; } }
        public override JFSData.JFSDataFileState State { get { return Data.CurrentRevision.State; } }
        public override bool IsValid { get { return true; } } // If it is a JFSFile then it is by definition a valid file.
        public bool HasNewerInvalidVersion { get { return Data.LatestRevision != null; } } // Meaning incomplete or corrupt!
        public JFSCorruptFile GetLatestVersion() { return HasNewerInvalidVersion ? ManufactureFromLatestVersion(FileSystem, Data, CompleteData) : null; }
        public JFSFile(Jottacloud fileSystem, JFSData.JFSFileData dataWithPath, bool isCompleteData) : base(fileSystem, dataWithPath, isCompleteData) { }
        public JFSFile(Jottacloud fileSystem, string parentFullName, JFSData.JFSFileData incompleteDataWithoutPath) : base(fileSystem, parentFullName, incompleteDataWithoutPath) { }
        public string Read()
        {
            // Reading the file content by requesting the file url with parameter "mode=bin"
            var parameters = new Dictionary<string, string> { { "mode", "bin" } };
            return FileSystem.RequestGet(FullName, parameters);
        }
        public string ReadPartial(ulong from, ulong to)
        {
            // Reading the partial file content by requesting the file url with parameter "mode=bin",
            // and including the header "Range" with value "bytes=[from]-[to]".
            // Note that HTTP range requests are inclusive end while we use open ended range range.
            var parameters = new Dictionary<string, string> { { "mode", "bin" } };
            var additionalHeaders = new Dictionary<string, string> { { "Range", string.Format("bytes=%d-%d",from,to-1) } };
            return FileSystem.RequestGet(FullName, parameters, additionalHeaders);
        }
        public void Stream(ulong chunk_size=64*1024)
        {
            // TODO: Is this necessary, and easily possible, with the .NET WebResponse stream?
            throw new NotImplementedException(); // TODO!
        }
        public void Download(string localPath, bool overwrite)
        {
            Download(new FileInfo(localPath), overwrite);
        }
        public void Download(FileInfo localFile, bool overwrite)
        {
            if (localFile.Exists)
            {
                throw new InvalidOperationException(string.Format("Local file {0} already exists", localFile.FullName));
            }
            FileSystem.DownloadFile(FullName, localFile);
            // Set back the timestamps of the downloaded file according to information from server
            if (this.Created.HasValue)
                localFile.CreationTimeUtc = this.Created.Value;
            if (this.Modified.HasValue)
                localFile.LastWriteTimeUtc = this.Modified.Value;
            // What about Updated timestamp from server? And LastAccess timestamp on local file?
        }
        public bool UpdateLocal(FileInfo localFile)
        {
            if (!localFile.Exists)
            {
                throw new InvalidOperationException(string.Format("Local file {0} does not exists", localFile.FullName));
            }
            if (VerifyLocal(localFile) == false)
            {
                Download(localFile, true);
                return true;
            }
            else
            {
                return false;
            }
        }
        public void Share(bool enable = true)
        {
            // Enable public access at secret, share only uri, and return that uri.
            var parameters = new Dictionary<string, string> { { "mode", enable ? "enableShare" : "disableShare" } };
            var newFileData = FileSystem.RequestGetDataObject<JFSData.JFSFileData>(FullName, parameters); // Returns the new JFSFileData, which is complete (and now with or without publicURI on it)!
            SetData(newFileData); // Replace existing data object with the new one!
        }
        public void UnShare()
        {
            Share(false);
        }
        public void Restore()
        {
            // Restore the file.
            // TODO: See JFSFolder.Restore()
            throw new NotImplementedException(); // TODO!
        }
        public void Delete()
        {
            // Delete this file and return the new, deleted JFSFile.
            // See also JFSFolder.Delete()
            var parameters = new Dictionary<string, string> { { "dl", "true" } };
            var newFileData = FileSystem.RequestPost<JFSData.JFSFileData>(FullName, parameters); // Returns the new JFSFileData, which is complete (and now with a deleted timestamp on it)!
            SetData(newFileData); // Replace existing data object with the new one!
            // NB: Any parent objects (folder) that the client keeps needs to be refreshed for the delete to be considered!
        }
        public void DeletePermanently()
        {
            throw new InvalidOperationException("Permanent deletion must be done from the parent object!");
        }
        public void Move(string newPath)
        {
            // Move/rename file to a new name, possibly a whole new path - creating any intermediate folders along the way.
            // The specified newPath must be a complete path, starting with the device (so device, mount point, new path).
            // For example: To move and rename a file "/OldParentName/OldFolderName/Oldfile.txt" in mount point "Archive" on the
            // default "Jotta" device to path "/NewParentFolderName/NewFolderName/Newfile.txt" in the same mount point one has to specify
            // newPath as "/Jotta/Archive/NewParentFolderName/NewFolderName/Newfile.txt".
            // The operation is implemented by sending a POST request like the following:
            // "https://www.jotta.no/jfs/[username]/Jotta/Archive/OldFolderName/Oldfile.txt?mvDir=/[username]/Jotta/Archive/NewParentFolderName/NewFolderName/Newfile.txt"
            // Returns the moved JFSFile.
            // See also JFSFolder.MoveRename()
            var moveToPath = FileSystem.ConvertToDataPath(newPath); // Convert to data path, appending the file system root name (username).
            var parameters = new Dictionary<string, string> { { "mv", moveToPath } };
            var newFileData = FileSystem.RequestPost<JFSData.JFSFileData>(FullName, parameters); // Returns the new JFSFileData, which is complete (and with the new name/path)!
            SetData(newFileData); // Replace existing data object with the new one!
            // NB: Any parent objects (folder) that the client keeps needs to be refreshed for the moved file to be considered!
        }
        public void Rename(string newName)
        {
            // Rename file to a new name, or a path relative to the parent folder in which case all intermediate folders along the way will also be created.
            Move(ParentPath + newName); // Same parent but different name, and convert to data path, appending the file system root name (username).
        }
        public string Thumb(ThumbSize size = ThumbSize.Large)
        {
            // Get a thumbnail as string, or null if the file is not an image,
            // by requesting the file url with parameter "mode=thumb"
            if (!IsImage)
            {
                return null;
            }
            string sizeCode = "";
            switch (size)
            {
                case ThumbSize.Small: sizeCode = "WS"; break;
                case ThumbSize.Medium: sizeCode = "WM"; break;
                case ThumbSize.Large: sizeCode = "WL"; break;
                case ThumbSize.ExtraLarge: sizeCode = "WXL"; break;
                default: throw new JFSError(string.Format("Invalid thumbnail size %d for image %s", size, FullName));
            }
            var parameters = new Dictionary<string, string> { { "mode", "thumb" }, { "ts", sizeCode } };
            return FileSystem.RequestGet(FullName, parameters, null);
        }
    }

    //
    // Simple self-contained structure representing basic information about files,
    // something like System.IO.FileSystemInfo.
    //
    public sealed class JFSFileSystemInfo
    {
        public JFSFileSystemInfo(string name, Guid id, JFSData.JFSDataFileState state, ulong? size, string md5)
        {
            Name = name;
            ID = id;
            Size = size;
            MD5 = md5;
            switch (state)
            {
                case JFSData.JFSDataFileState.Incomplete: State = FileState.Incomplete; break;
                case JFSData.JFSDataFileState.Added: State = FileState.Added; break;
                case JFSData.JFSDataFileState.Processing: State = FileState.Processing; break;
                case JFSData.JFSDataFileState.Completed: State = FileState.Completed; break;
                case JFSData.JFSDataFileState.Corrupt: State = FileState.Corrupt; break;
            }
        }
        public enum FileState
        {
            Incomplete,
            Added,
            Processing,
            Completed,
            Corrupt
        }
        public string Name { get; }
        public Guid ID { get; }
        public FileState State { get; }
        public ulong? Size { get; } // Not set if state is corrupt or incomplete (since loaded from limited information in JFSFileDirList).
        public string MD5 { get; } // Not set if state is corrupt
    }

#if false
    public class JFSEnableSharing : JFSObject<JFSData.JFSEnableSharingData>
    {
        public override string Name { get { return "Latest"; } } // Special case: No name in data object!
        public override string ParentPath { get; } // Special case: No path in data object!
        public JFSEnableSharing(Jottacloud fileSystem, JFSData.JFSEnableSharingData data, bool completeData) : base(fileSystem, parentPath, data, null) {}
        public JFSEnableSharing(Jottacloud fileSystem, string name) : base(fileSystem, name, null) {}
        // TODO:
        //public JFSFileData[] GetFiles()
    }
#endif

    class JFSError : Exception
    {
        public JFSError(String message) : base(message) { }
#if false
        public static JFSError Create(System.Web.HttpException exception, string path)
        {
            switch (exception.GetHttpCode())
            {
                case 404:
                    return new JFSNotFoundError(String.Format("%s does not exist (%s)", path, exception.Message));
                case 401:
                    return new JFSCredentialsError(String.Format("Your credentials don't match for %s (%s) (probably incorrect password!)", path, exception.Message));
                case 403:
                    return new JFSAuthenticationError(String.Format("You don't have access to %s (%s)", path, exception.Message));
                case 416:
                    return new JFSRangeError(String.Format("Requested Range Not Satisfiable (%s)", path, exception.Message));
                case 500:
                    return new JFSServerError(String.Format("Internal server error: %s (%s)", path, exception.Message));
                case 400:
                    return new JFSBadRequestError(String.Format("Bad request: %s (%s)", path, exception.Message));
                default:
                    return new JFSError(String.Format("Error accessing %s (%s)", path, exception.Message));
            }
        }
#endif
    }
#if false
    class JFSBadRequestError : JFSError  // HTTP 400
    {
        public JFSBadRequestError(String message) : base(message) { }
    }
    class JFSCredentialsError : JFSError // HTTP 401
    {
        public JFSCredentialsError(String message) : base(message) { }
    }
    class JFSNotFoundError : JFSError //HTTP 404
    {
        public JFSNotFoundError(String message) : base(message) { }
    }
    class JFSAccessError : JFSError // ?
    {
        public JFSAccessError(String message) : base(message) { }
    }
    class JFSAuthenticationError : JFSError // HTTP 403
    {
        public JFSAuthenticationError(String message) : base(message) { }
    }
    class JFSRangeError : JFSError // HTTP 416
    {
        public JFSRangeError(String message) : base(message) { }
    }
    class JFSServerError : JFSError //HTTP 500
    {
        public JFSServerError(String message) : base(message) { }
    }
#endif
}
