namespace Jottacloud
{
    public sealed class API
    {
        public static string LibraryVersion { get { return System.Diagnostics.FileVersionInfo.GetVersionInfo(System.Reflection.Assembly.GetExecutingAssembly().Location).FileVersion; } }
        public static string UserAgentString { get { return "JottacloudFileSystem version " + LibraryVersion; } }
    }
}
