using System.Text;

namespace dBASE.NET.Encoders;

internal class MemoEncoder : IEncoder
{
    private static MemoEncoder _instance;

    private MemoEncoder() { }

    public static MemoEncoder Instance => _instance ??= new MemoEncoder();

    public const int BlockSize = 64;

#if DEBUG
    public int Index { get; set; }
#endif

    #region MEMO
    public int WriteMemo(DbfField field, string memoString, Encoding encoding, long memoOffset, BinaryWriter fptWriter)
    {
        if (string.IsNullOrWhiteSpace(memoString))
        {
            return 0; // BitConverter.GetBytes(0); // Offset nullo per valori vuoti
        }

        if (field.Type != DbfFieldType.Memo)
        {
            throw new InvalidOperationException("Campo non valido per dati memo.");
        }

        var memoBytes = encoding.GetBytes(memoString);
        var totalSize = memoBytes.Length + 4; // 4-byte header per la lunghezza
        var requiredBlocks = (totalSize + BlockSize - 1) / BlockSize;
        var dataSize = requiredBlocks * BlockSize;

        var memoBlock = new byte[dataSize];
        Array.Copy(BitConverter.GetBytes(memoBytes.Length), memoBlock, 4);
        Array.Copy(memoBytes, 0, memoBlock, 4, memoBytes.Length);

        fptWriter!.Seek((int)memoOffset, SeekOrigin.Begin);
        fptWriter.Write(memoBlock);

        return dataSize;
    }
    #endregion

    public byte[] Encode(DbfField field, object data, Encoding encoding)
    {
        /* ChatGPT attempt
        var length = data?.ToString().Length ?? 0;
        if (length == 0)
        {
            return BitConverter.GetBytes(0);
        }
        if (length > 4)
        {
            return encoding.GetBytes(data.ToString());
        }
        */

        if (data == null) return BitConverter.GetBytes(0);
        if (field.Length > 4)
            return encoding.GetBytes(data.ToString());
        return BitConverter.GetBytes((int)data);
    }

    /// <inheritdoc />
    public object Decode(byte[] buffer, byte[] memoData, Encoding encoding)
    {
        var index = 0;
#if DEBUG
        Index = index;
#endif
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

#if DEBUG
        Index = index;
#endif
        return FindMemo(index, memoData, encoding);
    }

    private static string FindMemo(int index, byte[] memoData, Encoding encoding)
    {
        // This is the original implementation of findMemo. It was found that
        // the LINQ methods are orders of magnitude slower than using using array
        // offsets.

        /* UInt16 blockSize = BitConverter.ToUInt16(memoData.Skip(6).Take(2).Reverse().ToArray(), 0);
           int type = (int)BitConverter.ToUInt32(memoData.Skip(index * blockSize).Take(4).Reverse().ToArray(), 0);
           int length = (int)BitConverter.ToUInt32(memoData.Skip(index * blockSize + 4).Take(4).Reverse().ToArray(), 0);
           string text = encoding.GetString(memoData.Skip(index * blockSize + 8).Take(length).ToArray()).Trim();
           return text; */

        // The index is measured from the start of the file, even though the memo file header blocks takes
        // up the first few index positions.
        var blockSize = BitConverter.ToUInt16(new[] { memoData[7], memoData[6] }, 0);
        var length = (int)BitConverter.ToUInt32(
            new[]
            {
                memoData[index * blockSize + 4 + 3],
                memoData[index * blockSize + 4 + 2],
                memoData[index * blockSize + 4 + 1],
                memoData[index * blockSize + 4 + 0],
            },
            0);

        var memoBytes = new byte[length];
        var lengthToSkip = index * blockSize + 8;

        for (var i = lengthToSkip; i < lengthToSkip + length; ++i)
        {
            memoBytes[i - lengthToSkip] = memoData[i];
        }

        return encoding.GetString(memoBytes)
            //.Trim() //TOCHECK
        ;
    }
}