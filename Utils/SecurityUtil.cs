using System.Security.Cryptography;
using System.Text;

namespace SyncFolder.Utils;

internal class SecurityUtil
{
    private static MD5? MD5;

    private static MD5 GetMD5()
    {
        MD5 ??= MD5.Create();
        return MD5;
    }

    /// <summary>
    /// Return hash code for file content.
    /// </summary>
    /// <param name="filepPath">The location of file path.</param>
    /// <returns>The hash code.</returns>
    public static string GetMD5HashFileContent(string filepPath)
    {
        using var inputStream = File.OpenRead(filepPath);

        // Print the MD5 like "8C7DD9", instead of "8C-7D-D9"
        return BitConverter.ToString(GetMD5().ComputeHash(inputStream)).Replace("-", string.Empty);
    }

    /// <summary>
    /// Return hash code for any string data.
    /// </summary>
    /// <param name="data">The string to generate hash.</param>
    /// <returns></returns>
    public static string GetMD5Hash(string data)
    {
        using var md5 = MD5.Create();
        var byteArray = Encoding.UTF8.GetBytes(data);

        // Print the MD5 like "8C7DD9", instead of "8C-7D-D9"
        return BitConverter.ToString(GetMD5().ComputeHash(byteArray)).Replace("-", string.Empty);
    }
}
