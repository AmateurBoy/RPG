using Unity.Collections;

namespace TestedField
{
    /// <summary>Безопасное преобразование строк в FixedString (длинные строки обрезаются, а не бросают исключение).</summary>
    public static class NetStrings
    {
        public static FixedString32Bytes To32(string s)
        {
            var fs = new FixedString32Bytes();
            if (!string.IsNullOrEmpty(s))
                fs.CopyFromTruncated(s);
            return fs;
        }

        public static FixedString64Bytes To64(string s)
        {
            var fs = new FixedString64Bytes();
            if (!string.IsNullOrEmpty(s))
                fs.CopyFromTruncated(s);
            return fs;
        }

        public static FixedString512Bytes To512(string s)
        {
            var fs = new FixedString512Bytes();
            if (!string.IsNullOrEmpty(s))
                fs.CopyFromTruncated(s);
            return fs;
        }

        public static FixedString128Bytes To128(string s)
        {
            var fs = new FixedString128Bytes();
            if (!string.IsNullOrEmpty(s))
                fs.CopyFromTruncated(s);
            return fs;
        }
    }
}
