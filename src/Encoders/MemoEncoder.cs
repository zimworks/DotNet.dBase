using System.Buffers.Binary;
using System.Text;

namespace dBASE.NET.Encoders;

internal class MemoEncoder : IEncoder
{
    private static MemoEncoder _instance;

    private MemoEncoder() { }

    public static MemoEncoder Instance => _instance ??= new MemoEncoder();

    public const int HeaderSize = 512;
    public const int BlockSize = 64;

    public byte[] Encode(DbfField field, object data, Encoding encoding)
    {
        if (data == null || string.IsNullOrEmpty(data.ToString()))
        {
            return BitConverter.GetBytes(0); // Return null offset for empty memo values.
        }

        if (field.Type != DbfFieldType.Memo)
        {
            throw new InvalidOperationException("Invalid field for memo data.");
        }

        var value = data.ToString();
        var encodedData = encoding.GetBytes(value);
        var totalSize = encodedData.Length + 8; // 8-byte header: 4 bytes for type and 4 for length.
        var requiredBlocks = (totalSize + BlockSize - 1) / BlockSize;
        var dataSize = requiredBlocks * BlockSize;

        var memoBlock = new byte[dataSize];
        // Write memo header using big-endian:
        BinaryPrimitives.WriteInt32BigEndian(memoBlock.AsSpan(0, 4), 1); // Memo type 1 (text)
        BinaryPrimitives.WriteInt32BigEndian(memoBlock.AsSpan(4, 4), encodedData.Length); // Length of memo content.
        Array.Copy(encodedData, 0, memoBlock, 8, encodedData.Length);

        return memoBlock;
    }

    /// <inheritdoc />
    public object Decode(byte[] buffer, byte[] memoData, Encoding encoding)
    {
        if (buffer == null || buffer.Length < 4)
        {
            return null; // No valid offset data.
        }

        var offset = BinaryPrimitives.ReadInt32LittleEndian(buffer.AsSpan(0, 4));
        if (offset <= 0)
        {
            return null; // No memo data.
        }

        if (memoData == null)
        {
            return null; // No memo read.
        }

        var start = offset * BlockSize;
        if (start + 8 > memoData.Length)
        {
            return null; // Not enough data for header.
        }

        var memoType = BinaryPrimitives.ReadInt32BigEndian(memoData.AsSpan(start, 4));
        var memoLength = BinaryPrimitives.ReadInt32BigEndian(memoData.AsSpan(start + 4, 4));

        if (memoType != 1 || memoLength <= 0 || start + 8 + memoLength > memoData.Length)
        {
            return null; // Invalid memo block format.
        }

        return encoding.GetString(memoData, start + 8, memoLength);
    }
}