using System.Text;

namespace dBASE.NET.Encoders;

internal class MemoEncoder : IEncoder
{
    private static MemoEncoder _instance;

    private MemoEncoder() { }

    public static MemoEncoder Instance => _instance ??= new MemoEncoder();

    public const int BlockSize = 64;

    public byte[] Encode(DbfField field, object data, Encoding encoding)
    {
        if (data == null || string.IsNullOrEmpty(data.ToString()))
        {
            return BitConverter.GetBytes(0); // Null offset for empty values
        }

        if (field.Type != DbfFieldType.Memo)
        {
            throw new InvalidOperationException("Invalid field for memo data.");
        }

        var value = data.ToString();
        var encodedData = encoding.GetBytes(value);
        var totalSize = encodedData.Length + 8; // 8-byte header for FPT format
        var requiredBlocks = (totalSize + BlockSize - 1) / BlockSize;
        var dataSize = requiredBlocks * BlockSize;

        var memoBlock = new byte[dataSize];
        Array.Copy(BitConverter.GetBytes(1), memoBlock, 4); // Type 0x01 (text)
        Array.Copy(BitConverter.GetBytes(encodedData.Length), 0, memoBlock, 4, 4); // Length
        Array.Copy(encodedData, 0, memoBlock, 8, encodedData.Length);

        return memoBlock;
    }

    /// <inheritdoc />
    public object Decode(byte[] buffer, byte[] memoData, Encoding encoding)
    {
        /*
        var index = 0;
        // Memo fields of 5+ bytes in length store their index in text, e.g. "     39394"
        // Memo fields of 4 bytes store their index as an int.
        if (buffer.Length > 4)
        {
            var text = encoding.GetString(buffer).Trim();
            if (text.Length == 0) return null;
            index = Convert.ToInt32(text);
        }
        else
        {
            index = BitConverter.ToInt32(buffer, 0);
            if (index == 0) return null;
        }

        return FindMemo(index, memoData, encoding);
        */

        if (buffer == null || buffer.Length < 4)
        {
            return null; // No valid offset data
        }

        var offset = BitConverter.ToInt32(buffer, 0);
        if (offset <= 0)
        {
            return null; // No memo data
        }

        var start = offset * BlockSize;
        if (start + 8 > memoData.Length)
        {
            return null; // Not enough data for header
        }

        // Read memoType using big-endian conversion.
        var typeBytes = memoData.Skip(start).Take(4).ToArray();
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(typeBytes);
        }
        var memoType = BitConverter.ToInt32(typeBytes, 0);

        // Read memoLength using big-endian conversion.
        var lengthBytes = memoData.Skip(start + 4).Take(4).ToArray();
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(lengthBytes);
        }
        var memoLength = BitConverter.ToInt32(lengthBytes, 0);
        if (memoType != 1 || memoLength <= 0 || start + 8 + memoLength > memoData.Length)
        {
            return null; // Invalid memo block format
        }

        return encoding.GetString(memoData, start + 8, memoLength);
    }
}