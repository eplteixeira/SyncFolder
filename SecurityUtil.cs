namespace SyncFolder
{
    using System.Security.Cryptography;

    internal class SecurityUtil
    {
        public static string GetMD5(string filepPath)
        {
            using var md5 = MD5.Create();
            using var inputStream = File.OpenRead(filepPath);

            // Print the MD5 like "8C7DD9", instead of "8C-7D-D9"
            return BitConverter.ToString(md5.ComputeHash(inputStream)).Replace("-", string.Empty);
        }
    }
}
