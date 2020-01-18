using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace LaunchDarkly.Client.Files
{
    internal sealed class FlagFileReader : IFileReader
    {
        private const int ReadFileRetryDelay = 200;
        private const int ReadFileRetryAttempts = 30000 / ReadFileRetryDelay;

        private FlagFileReader() { }
        public static readonly IFileReader Instance = new FlagFileReader();

        string IFileReader.ReadAllText(string path)
        {
            int delay = 0;
            for (int i = 0; ; i++)
            {
                try
                {
                    string content = File.ReadAllText(path);
                    return content;
                }
                catch (IOException e) when (IsFileLocked(e))
                {
                    // Retry for approximately 30 seconds before throwing
                    if (i > ReadFileRetryAttempts)
                    {
                        throw;
                    }
#if NETSTANDARD1_4 || NETSTANDARD1_6
                    Task.Delay(delay).Wait();
#else
                    Thread.Sleep(delay);
#endif
                    // Retry immediately the first time but 200ms thereafter
                    delay = ReadFileRetryDelay;
                }
            }
        }

        private static bool IsFileLocked(IOException exception)
        {
            // We cannot guarantee that these HResult values will be present on non-Windows OSes. However, this
            // logic is less important on other platforms, because in Unix-like OSes you can atomically replace a
            // file's contents (by creating a temporary file and then renaming it to overwrite the original file),
            // so FileDataSource will not try to read an incomplete update; that is not possibble in Windows.
            int errorCode = exception.HResult & 0xffff;
            switch (errorCode)
            {
                case 0x20: // ERROR_SHARING_VIOLATION
                case 0x21: // ERROR_LOCK_VIOLATION
                    return true;
                default:
                    return false;
            }
        }
    }
}
