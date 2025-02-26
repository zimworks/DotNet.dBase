using dBASE.NET.Encoders;
using System.Buffers.Binary;
using System.Text;

namespace dBASE.NET;

/// <summary>
/// DbfRecord encapsulates a record in a .dbf file. It contains an array with
/// data (as an Object) for each field.
/// </summary>
public class DbfRecord
{
    private const string DefaultSeparator = ",";
    private const string DefaultMask = "{name}={value}";

    // ReSharper disable once InconsistentNaming
    private readonly List<DbfField> Fields;

    internal DbfRecord(BinaryReader reader, DbfHeader header, List<DbfField> fields, byte[] memoData, Encoding encoding)
    {
        this.Fields = fields;
        Data = [];
        Memo = [];

        // Read record marker.
        Marker = reader.ReadByte();

        // Read entire record as sequence of bytes.
        // Note that record length includes marker.
        var row = reader.ReadBytes(header.RecordLength - 1);
        if (row.Length == 0)
            throw new EndOfStreamException();
#if DEBUG
        //if (Marker == (int)DbfRecordMarker.Deleted) return;
#endif

        // Read data for each field.
        var offset = 0;
        foreach (var field in fields)
        {
            // Copy bytes from record buffer into field buffer.
            var buffer = new byte[field.Length];
            Array.Copy(row, offset, buffer, 0, field.Length);
            offset += field.Length;

            var encoder = EncoderFactory.GetEncoder(field.Type);
            var data = encoder.Decode(buffer, memoData, encoding);
            //data?.ToString() == "             12"
            if (field.Type == DbfFieldType.Memo)
            {
                Data.Add(BinaryPrimitives.ReadInt32LittleEndian(buffer.AsSpan(0, 4)));
                Memo.Add(data);
            }
            else
            {
                Data.Add(data);
                Memo.Add(null);
            }
        }
    }

    /// <summary>
    /// Create an empty record.
    /// </summary>
    internal DbfRecord(List<DbfField> fields)
    {
        this.Fields = fields;
        Data = [];
        foreach (var unused in fields)
        {
            Data.Add(null);
        }
    }
#pragma warning disable 1591
    public byte Marker { get; } = (byte)DbfRecordMarker.Valid;

    public List<object> Data { get; }

    public object this[int index] => Data[index];

    public object this[string name]
    {
        get
        {
            var index = Fields.FindIndex(x => x.Name.Equals(name));
            return index == -1 ? null : Data[index];
        }
        set
        {
            var index = Fields.FindIndex(x => x.Name.Equals(name));
            Data[index] = value;
        }
    }

    public object this[DbfField field]
    {
        get
        {
            var index = Fields.IndexOf(field);
            return index == -1 ? null : Data[index];
        }
    }

    public List<object> Memo { get; }

    public object this[string name, bool memoData]
    {
        get
        {
            var index = Fields.FindIndex(x => x.Name.Equals(name));
            return index == -1 ? null : Memo[index];
        }
    }
#pragma warning restore 1591

    /// <summary>
    /// Returns a string that represents the current object.
    /// </summary>
    /// <returns>A string that represents the current object.</returns>
    public override string ToString()
    {
        return ToString(DefaultSeparator, DefaultMask);
    }

    /// <summary>
    /// Returns a string that represents the current object with custom separator.
    /// </summary>
    /// <param name="separator">Custom separator.</param>
    /// <returns>A string that represents the current object with custom separator.</returns>
    public string ToString(string separator)
    {
        return ToString(separator, DefaultMask);
    }

    /// <summary>
    /// Returns a string that represents the current object with custom separator and mask.
    /// </summary>
    /// <param name="separator">Custom separator.</param>
    /// <param name="mask">
    /// Custom mask.
    /// <para>e.g., "{name}={value}", where {name} is the mask for the field name, and {value} is the mask for the value.</para>
    /// </param>
    /// <returns>A string that represents the current object with custom separator and mask.</returns>
    public string ToString(string separator, string mask)
    {
        separator ??= DefaultSeparator;
        mask = (mask ?? DefaultMask).Replace("{name}", "{0}").Replace("{value}", "{1}");

        return string.Join(separator, Fields.Select(z => string.Format(mask, z.Name, this[z])));
    }

    // Updates the memo offset in the record for the specified field.
    public void SetMemoOffset(DbfField field, int offset)
    {
        var index = Fields.FindIndex(f => f.Name == field.Name);
        if (index < 0)
            throw new KeyNotFoundException($"Field '{field.Name}' does not exist in the record.");

        Data[index] = offset;
    }

    // Writes all fields to the DBF record. For memo fields, it writes the offset value
    // as a string, padded to the field's defined length per xBASE specifications.
    internal void Write(BinaryWriter writer, Encoding encoding)
    {
        /*
        // Write marker (always "not deleted")
        writer.Write((byte)0x20);

        var index = 0;
        foreach (var field in fields)
        {
            var encoder = EncoderFactory.GetEncoder(field.Type);
            var buffer = encoder.Encode(field, Data[index], encoding);
            if (buffer != null)
            {
                if (buffer.Length > field.Length)
                {
                    throw new ArgumentOutOfRangeException(nameof(buffer.Length), buffer.Length, "Buffer length has exceeded length of the field.");
                }

                writer.Write(buffer);
            }
            index++;
        }
        */

        writer.Write(Marker);

        //Data[1]?.ToString() == "             12"
        var index = 0;
        foreach (var field in Fields)
        {
            if (field.Type == DbfFieldType.Memo)
            {
                // this is DBF+DBT bBASE spec, not DBF+FPT !
                // var offsetValue = this[field.Name];
                // var offsetStr = offsetValue.ToString().PadLeft(field.Length);
                // writer.Write(encoding.GetBytes(offsetStr));

                // this respects DBF+FPT spec
                var offset = Convert.ToInt32(this[field.Name]);
                writer.Write(BitConverter.GetBytes(offset));
            }
            else
            {
#if !DEBUG
                    var encoder = EncoderFactory.GetEncoder(field.Type);
                    var buffer = encoder.Encode(field, Data[index], encoding);
                    if (buffer != null)
                    {
                        if (buffer.Length > field.Length)
                        {
                            throw new ArgumentOutOfRangeException(nameof(buffer.Length), buffer.Length, "Buffer length has exceeded length of the field.");
                        }

                        writer.Write(buffer);
                    }
#else
                var value = this[field.Name];
                var encoder = EncoderFactory.GetEncoder(field.Type);
                var buffer = encoder.Encode(field, value, encoding);
                writer.Write(buffer);
#endif
            }
            index++;
        }
    }
}