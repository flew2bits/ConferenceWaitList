using System.Security.Cryptography;
using System.Text;

namespace ConferenceBooking;

public static class StringExtensions
{
    public static Guid ToGuidV5(this string value, string nameSpace = "ns://default")
    {
        var hash = SHA1.HashData(Encoding.UTF8.GetBytes(nameSpace + value));
        hash[7] = (byte)(hash[7] & 0x0f | 0x50);
        hash[8] = (byte)(hash[8] & 0x3f | 0x80);

        return new Guid(hash[..16]);
    }

    public static Guid ToUserGuid(this string userName)
        => $"user:{userName}".ToGuidV5(Constants.ApplicationNamespace);
}