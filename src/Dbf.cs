namespace dBASE.NET;

using Encoders;
using System;
using System.Buffers.Binary;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;

/// <summary>
/// The Dbf class encapsulated a dBASE table (.dbf) file, allowing
/// reading from disk, writing to disk, enumerating fields and enumerating records.
/// </summary>
public class Dbf
{
    private DbfHeader header;
    private FptHeader fptHeader;

    public const DbfVersion DefaultVersion = DbfVersion.VisualFoxPro;
    public const byte DefaultFlag = (byte)FoxProFlag.WithMemo;
    public const byte DefaultCodepage = (byte)FoxProCodepage.DOS_Multilingual;
    public const string DefaultEncodingName = "ibm850";

    public DbfVersion Version => header?.Version ?? DefaultVersion;
    public byte Flag => header?.Flag ?? DefaultFlag;
    public byte Codepage => header?.Codepage ?? DefaultCodepage;

    /// <summary>
    /// Initializes a new instance of the <see cref="Dbf" />.
    /// </summary>
    public Dbf()
    {
        header = DbfHeader.CreateHeader(DbfVersion.FoxBaseDBase3NoMemo);
        Fields = [];
        Records = [];
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Dbf" /> with custom encoding.
    /// </summary>
    /// <param name="encoding">Custom encoding.</param>
    public Dbf(Encoding encoding)
        : this()
    {
        Encoding = encoding ?? throw new ArgumentNullException(nameof(encoding));
    }

    /// <summary>
    /// The collection of <see cref="DbfField" /> that represent table header.
    /// </summary>
    public List<DbfField> Fields { get; }

    /// <summary>
    /// The collection of <see cref="DbfRecord" /> that contains table data.
    /// </summary>
    public List<DbfRecord> Records { get; }

    /// <summary>
    /// The <see cref="System.Text.Encoding" /> class that corresponds to the specified code page.
    /// </summary>
    public Encoding Encoding { get; } = Encoding.GetEncoding(DefaultEncodingName);

    /// <summary>
    /// Creates a new <see cref="DbfRecord" /> with the same schema as the table.
    /// </summary>
    /// <returns>A <see cref="DbfRecord" /> with the same schema as the <see cref="T:System.Data.DataTable" />.</returns>
    public DbfRecord CreateRecord()
    {
        var record = new DbfRecord(Fields);
        Records.Add(record);
        return record;
    }

    /// <summary>
    /// Opens a DBF file, reads the contents that initialize the current instance, and then closes the file.
    /// </summary>
    /// <param name="path">The file to read.</param>
    public void Read(string path)
    {
        // Open stream for reading.
        using var baseStream = File.Open(path, FileMode.Open, FileAccess.Read,  FileShare.Read);
        var memoPath = GetMemoPath(path);
        if (memoPath == null)
        {
            Read(baseStream);
            return;
        }

        using var fptStream = File.Open(memoPath, FileMode.Open, FileAccess.Read,  FileShare.Read);
        Read(baseStream, fptStream);
    }

    /// <summary>
    /// Reads the contents of streams that initialize the current instance.
    /// </summary>
    /// <param name="dbfStream">Stream with a database.</param>
    /// <param name="fptStream">Stream with a memo.</param>
    public void Read(Stream dbfStream, Stream fptStream = null)
    {
        if (dbfStream == null)
        {
            throw new ArgumentNullException(nameof(dbfStream));
        }

        if (!dbfStream.CanSeek)
        {
            throw new InvalidOperationException("The stream must provide positioning (support Seek method).");
        }

        dbfStream.Seek(0, SeekOrigin.Begin);
        using var reader = new BinaryReader(dbfStream, Encoding);
        ReadHeader(reader);
        ReadFields(reader);

        // After reading the fields, we move the read pointer to the beginning
        // of the records, as indicated by the "HeaderLength" value in the header.
        dbfStream.Seek(header.HeaderLength, SeekOrigin.Begin);
        var fptBytes = fptStream != null ? ReadMemos(fptStream) : null;
        ReadRecords(reader, fptBytes);

        if (fptStream != null)
        {
            fptStream.Seek(0, SeekOrigin.Begin);
            using var fptReader = new BinaryReader(fptStream, Encoding);
            fptHeader = FptHeader.Read(fptReader);
        }
    }

    /// <summary>
    /// Creates a new file, writes the current instance to the file, and then closes the file. If the target file already exists, it is overwritten.
    /// </summary>
    /// <param name="path">The file to read.</param>
    /// <param name="version">The version <see cref="DbfVersion" />. If unknown specified, use current header version.</param>
    /// <param name="flag"></param>
    /// <param name="codepage"></param>
    public void Write(string path, DbfVersion version = DbfVersion.VisualFoxPro
        , byte flag = DefaultFlag, byte codepage = DefaultCodepage
    )
    {
        if (version != DbfVersion.Unknown)
        {
            header.Version = version;
            header = DbfHeader.CreateHeader(header.Version);
            header.Flag = flag;
            header.Codepage = codepage;
        }

        using var stream = File.Open(path, FileMode.Create, FileAccess.Write);
        Write(stream, false);
    }

    /// <summary>
    /// Writes the current instance to the specified stream.
    /// </summary>
    /// <param name="stream">The output stream.</param>
    /// <param name="version">The version <see cref="DbfVersion" />. If unknown specified, use current header version.</param>
    public void Write(Stream stream, DbfVersion version = DbfVersion.Unknown)
    {
        if (version != DbfVersion.Unknown)
        {
            header.Version = version;
            header = DbfHeader.CreateHeader(header.Version);
        }

        Write(stream, true);
    }

    // Private Write method performs the combined DBF/FPT write.
    private void Write(Stream stream, bool leaveOpen)
    {
        using var writer = new BinaryWriter(stream, Encoding, leaveOpen);
        header.Write(writer, Fields, Records);
        WriteFields(writer);

        // Prepare the FPT file using the same base path as the DBF file.
        var fptPath = GetMemoPath(((FileStream)stream).Name, true);
        using var fptStream = new FileStream(fptPath, FileMode.Create, FileAccess.Write);
        using var fptWriter = new BinaryWriter(fptStream, Encoding);

        // Initialize the FPT header per Alaska's spec.
        fptHeader ??= new FptHeader();
        fptHeader.Write(fptWriter);

        // Start writing memo data immediately after the header block.
        long memoOffset = fptHeader.HeaderSize;

        foreach (var record in Records)
        {
            foreach (var field in Fields)
            {
                if (field.Type == DbfFieldType.Memo)
                {
                    var value = record[field.Name, true];

#if DEBUG
                    var encoder = MemoEncoder.Instance;
                    var memoData = encoder.Encode(field, value, Encoding);

                    var offset = (int)record[field.Name];
                    var start = offset * MemoEncoder.BlockSize;
                    fptWriter.Seek(start, SeekOrigin.Begin);
                    fptWriter.Write(memoData);
#else
                    if (string.IsNullOrEmpty(value?.ToString())) // Null/empty memo fields have a null (zero) offset!
                    {
                        record.SetMemoOffset(field, 0);
                    }
                    else
                    {
                        var encoder = MemoEncoder.Instance;
                        var memoData = encoder.Encode(field, value, Encoding);

                        //var currentMemoOffset = memoOffset / MemoEncoder.BlockSize;
                        var currentMemoOffset = (memoOffset - fptHeader.HeaderSize + fptHeader.BlockSize) / fptHeader.BlockSize;
                        record.SetMemoOffset(field, (int)currentMemoOffset);

                        fptWriter.Seek((int)memoOffset, SeekOrigin.Begin);
                        fptWriter.Write(memoData);

                        memoOffset += memoData.Length;
                    }
#endif
                }
            }
        }

        WriteRecords(writer);
    }

    private static byte[] ReadMemos(Stream stream)
    {
        if (stream == null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    private void ReadHeader(BinaryReader reader)
    {
        // Peek at version number, then try to read correct version header.
        var versionByte = reader.ReadByte();
        reader.BaseStream.Seek(0, SeekOrigin.Begin);
        var version = (DbfVersion) versionByte;
        header = DbfHeader.CreateHeader(version);
        header.Read(reader);
    }

    private void ReadFields(BinaryReader reader)
    {
        Fields.Clear();

        // Fields are terminated by 0x0d char.
        while (reader.PeekChar() != 0x0d)
        {
            Fields.Add(new DbfField(reader, Encoding));
        }

        // Read fields terminator.
        reader.ReadByte();
    }

    private void ReadRecords(BinaryReader reader, byte[] memoData)
    {
        Records.Clear();

        // Records are terminated by 0x1a char (officially), or EOF (also seen).
        while (reader.PeekChar() != 0x1a && reader.PeekChar() != -1)
        {
            try
            {
                Records.Add(new DbfRecord(reader, header, Fields, memoData, Encoding));
            }
            catch (EndOfStreamException)
            {
            }
        }
    }

    private void WriteFields(BinaryWriter writer)
    {
        foreach (var field in Fields)
        {
            field.Write(writer, Encoding);
        }

        // Write field descriptor array terminator.
        writer.Write((byte) 0x0d);

        // Write database container.
        for (var i = 0; i < 263; i++)
        {
            writer.Write((byte)0); // 263 reserved bytes.
        }
    }

    private void WriteRecords(BinaryWriter writer)
    {
        foreach (var record in Records)
        {
            record.Write(writer, Encoding);
        }

#if !DEBUG
        // Write EOF character.
        writer.Write((byte) 0x1a);
#endif
    }

    private static string GetMemoPath(string basePath, bool forceFpt = false)
    {
        var memoPath = Path.ChangeExtension(basePath, "fpt");
        if (forceFpt || File.Exists(memoPath))
        {
            return memoPath;
        }

        memoPath = Path.ChangeExtension(basePath, "dbt");
        return !File.Exists(memoPath) ? null : memoPath;
    }
}