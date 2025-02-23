using System.Buffers.Binary;

namespace dBASE.NET;

public class FptHeader
{
    public int NextAvailableBlock { get; set; } = 1;
    public int BlockSize { get; set; } = 64;
    public int HeaderSize { get; set; } = 512;

    public static FptHeader Read(BinaryReader reader)
    {
        var fptHeader =  new FptHeader
        {
            NextAvailableBlock = BinaryPrimitives.ReadInt32BigEndian(reader.ReadBytes(4)),
            BlockSize = BinaryPrimitives.ReadInt32BigEndian(reader.ReadBytes(4))
        };
        return fptHeader;
    }

    public void Write(BinaryWriter writer)
    {
        // Create a header block of the size defined in MemoEncoder.HeaderSize (e.g., 512 bytes)
        var headerBlock = new byte[Encoders.MemoEncoder.HeaderSize];
        // Write NextAvailableBlock (4 bytes, big-endian)
        BinaryPrimitives.WriteInt32BigEndian(headerBlock.AsSpan(0, 4), NextAvailableBlock);
        // Write BlockSize (4 bytes, big-endian)
        BinaryPrimitives.WriteInt32BigEndian(headerBlock.AsSpan(4, 4), BlockSize);
        // The remaining bytes remain zero (reserved)
        writer.Seek(0, SeekOrigin.Begin);
        writer.Write(headerBlock);
    }
}
