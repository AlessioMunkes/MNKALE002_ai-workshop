using System.Security.Cryptography;
using System.Text;

namespace AttendanceRegister.Services.Security;

/// <summary>
/// Six-character codes derived from a per-session secret and the wall clock,
/// in the manner of a TOTP authenticator.
///
/// The alternative was storing a code and rewriting it every 30 seconds from a
/// background job. That needs a hosted service, a database write per rotation
/// per open session, and it still races: a student who reads the screen at
/// second 29 and presses the button at second 31 submits a code the database
/// no longer holds.
///
/// Deriving instead makes validation a pure function of the moment it happens.
/// Nothing is written, nothing is scheduled, and the grace window below is a
/// deliberate parameter rather than an accident of timing.
/// </summary>
public static class RotatingCheckInCode
{
    /// <summary>How long one code lives before the next one replaces it.</summary>
    public const int StepSeconds = 30;

    public const int CodeLength = 6;

    /// <summary>
    /// No O, I or L, and no 0 or 1. A code read off a projector from the back
    /// of a lecture theatre cannot then be mistyped in the obvious ways.
    /// </summary>
    private const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    /// <summary>
    /// A fresh secret for a check-in window. Twelve characters of this alphabet
    /// is about 59 bits, which is far beyond anything worth attacking inside a
    /// window that closes in fifteen minutes — and it fits the existing column,
    /// so no schema change is needed.
    /// </summary>
    public static string NewSecret(int length = 12)
    {
        var buffer = new char[length];
        for (var i = 0; i < length; i++)
        {
            buffer[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }

        return new string(buffer);
    }

    public static string For(string secret, DateTimeOffset moment) =>
        Derive(secret, StepOf(moment));

    /// <summary>
    /// True when the candidate matches the current code, or one of the previous
    /// <paramref name="graceSteps"/> codes.
    ///
    /// One step of grace is deliberate: without it a code read at second 29 and
    /// submitted at second 31 fails, which would be indistinguishable to the
    /// student from typing it wrongly. One step means a code is usable for
    /// between 30 and 60 seconds — long enough to type, short enough that
    /// relaying it to someone off campus is a live coordination problem rather
    /// than a message.
    /// </summary>
    public static bool Matches(string secret, string? candidate, DateTimeOffset moment, int graceSteps = 1)
    {
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        var supplied = Encoding.UTF8.GetBytes(candidate.Trim().ToUpperInvariant());
        var step = StepOf(moment);
        var matched = false;

        // Every step is checked even after a hit, so the time taken does not
        // reveal which step matched.
        for (var back = 0; back <= graceSteps; back++)
        {
            var expected = Encoding.UTF8.GetBytes(Derive(secret, step - back));
            matched |= CryptographicOperations.FixedTimeEquals(expected, supplied);
        }

        return matched;
    }

    /// <summary>Seconds until the code on screen is replaced.</summary>
    public static int SecondsRemaining(DateTimeOffset moment) =>
        StepSeconds - (int)(moment.ToUnixTimeSeconds() % StepSeconds);

    private static long StepOf(DateTimeOffset moment) => moment.ToUnixTimeSeconds() / StepSeconds;

    private static string Derive(string secret, long step)
    {
        var counter = BitConverter.GetBytes(step);
        if (BitConverter.IsLittleEndian)
        {
            // Fixed byte order, so a code derived on one machine is the same
            // code on any other.
            Array.Reverse(counter);
        }

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(counter);

        var buffer = new char[CodeLength];
        for (var i = 0; i < CodeLength; i++)
        {
            // 256 is not a multiple of 31, so the first few letters are very
            // slightly likelier than the last few. It costs a fraction of a bit
            // across a six-character code that lives for half a minute, which is
            // not worth rejection sampling to remove.
            buffer[i] = Alphabet[hash[i] % Alphabet.Length];
        }

        return new string(buffer);
    }
}
