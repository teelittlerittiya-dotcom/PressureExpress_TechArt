using System.Text;

namespace PressureExpress.Network
{
    /// <summary>
    /// Room code generation, normalisation and the deterministic port mapping used by
    /// <see cref="NetworkMode.LocalLoopback"/>.
    ///
    /// The alphabet deliberately excludes 0/O, 1/I/L so a code read off a screen and typed on
    /// another machine cannot be mistyped through character confusion. Anything outside the
    /// alphabet is rejected rather than guessed at, so a wrong code fails loudly.
    /// </summary>
    public static class RoomCode
    {
        public const int Length = 6;

        private const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

        // TMP_InputField stores its content in a text component that carries a trailing zero width
        // space, and pasted codes routinely arrive with a BOM or stray whitespace attached.
        private const char ZeroWidthSpace = (char)0x200B;
        private const char ByteOrderMark = (char)0xFEFF;

        private static readonly System.Random Rng = new System.Random();

        public static string Generate()
        {
            var sb = new StringBuilder(Length);
            lock (Rng)
            {
                for (int i = 0; i < Length; i++)
                {
                    sb.Append(Alphabet[Rng.Next(Alphabet.Length)]);
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// Uppercases and strips separators/whitespace. Does NOT drop unknown characters, so that
        /// "ABC0EF" is reported as invalid instead of being silently shortened to a different code.
        /// </summary>
        public static string Normalize(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return string.Empty;

            var sb = new StringBuilder(raw.Length);
            foreach (char c in raw)
            {
                if (c == ZeroWidthSpace || c == ByteOrderMark) continue;
                if (c == ' ' || c == '\t' || c == '\r' || c == '\n') continue;
                if (c == '-' || c == '_') continue;

                sb.Append(char.ToUpperInvariant(c));
            }
            return sb.ToString();
        }

        public static bool IsValid(string normalized)
        {
            if (string.IsNullOrEmpty(normalized) || normalized.Length != Length) return false;
            foreach (char c in normalized)
            {
                if (Alphabet.IndexOf(c) < 0) return false;
            }
            return true;
        }

        /// <summary>
        /// Deterministic loopback port so that in the Editor a wrong code genuinely fails to
        /// connect, exercising the same failure UI the Steam path uses. FNV-1a, so it is stable
        /// across processes and runs (unlike string.GetHashCode, which is randomised per process).
        /// </summary>
        public static ushort ToLoopbackPort(string normalized)
        {
            unchecked
            {
                const uint offsetBasis = 2166136261;
                const uint prime = 16777619;

                uint hash = offsetBasis;
                if (!string.IsNullOrEmpty(normalized))
                {
                    foreach (char c in normalized)
                    {
                        hash ^= c;
                        hash *= prime;
                    }
                }
                // 7000..7999, clear of NGO's 7777 default and of most dev servers.
                return (ushort)(7000 + (hash % 1000));
            }
        }
    }
}
