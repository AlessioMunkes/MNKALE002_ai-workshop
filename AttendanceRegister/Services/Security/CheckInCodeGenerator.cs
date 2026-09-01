using System.Security.Cryptography;

namespace AttendanceRegister.Services.Security;

/// <summary>
/// Six-character session codes. The alphabet leaves out 0/O and 1/I/L so a code
/// read off a lecture-hall projector cannot be mistyped in the obvious ways.
/// </summary>
public static class CheckInCodeGenerator
{
    private const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    public static string Generate(int length = 6)
    {
        var buffer = new char[length];
        for (var i = 0; i < length; i++)
        {
            buffer[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }

        return new string(buffer);
    }
}
