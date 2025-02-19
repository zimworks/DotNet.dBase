﻿namespace dBASE.NET;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

/// <summary>
/// The Dbf class encapsulated a dBASE table (.dbf) file, allowing
/// reading from disk, writing to disk, enumerating fields and enumerating records.
/// </summary>
public class Dbf
{
    public DbfHeader header; //TOCHECK

    /// <summary>
    /// Initializes a new instance of the <see cref="Dbf" />.
    /// </summary>
    public Dbf()
    {
        header = DbfHeader.CreateHeader(DbfVersion.FoxBaseDBase3NoMemo);
        Fields = new List<DbfField>();
        Records = new List<DbfRecord>();
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
    //public Encoding Encoding { get; } = Encoding.ASCII;
    public Encoding Encoding { get; } = Encoding.GetEncoding(850);

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

        using var memoStream = File.Open(memoPath, FileMode.Open, FileAccess.Read,  FileShare.Read);
        Read(baseStream, memoStream);
    }

    /// <summary>
    /// Reads the contents of streams that initialize the current instance.
    /// </summary>
    /// <param name="baseStream">Stream with a database.</param>
    /// <param name="memoStream">Stream with a memo.</param>
    public void Read(Stream baseStream, Stream memoStream = null)
    {
        if (baseStream == null)
        {
            throw new ArgumentNullException(nameof(baseStream));
        }

        if (!baseStream.CanSeek)
        {
            throw new InvalidOperationException("The stream must provide positioning (support Seek method).");
        }

        baseStream.Seek(0, SeekOrigin.Begin);
        // using var reader = new BinaryReader(baseStream);
        //using var reader = new BinaryReader(baseStream, Encoding.ASCII); // ReadFields() use PeekChar to detect end flag=0D, default Encoding may be UTF8 then cause exception
        var encoding = Encoding.GetEncoding(850);
        using var reader = new BinaryReader(baseStream, encoding); // ReadFields() use PeekChar to detect end flag=0D, default Encoding may be UTF8 then cause exception
        ReadHeader(reader);
        var memoData = memoStream != null ? ReadMemos(memoStream) : null;
        ReadFields(reader);

        // After reading the fields, we move the read pointer to the beginning
        // of the records, as indicated by the "HeaderLength" value in the header.
        baseStream.Seek(header.HeaderLength, SeekOrigin.Begin);
        ReadRecords(reader, memoData);
    }

    /// <summary>
    /// Creates a new file, writes the current instance to the file, and then closes the file. If the target file already exists, it is overwritten.
    /// </summary>
    /// <param name="path">The file to read.</param>
    /// <param name="version">The version <see cref="DbfVersion" />. If unknown specified, use current header version.</param>
    /// <param name="mdxFlag"></param>
    /// <param name="languageDriver"></param>
    public void Write(string path, DbfVersion version = DbfVersion.VisualFoxPro
        , byte mdxFlag = 0x20, byte languageDriver = 0x20
    )
    {
        if (version != DbfVersion.Unknown)
        {
            header.Version = version;
            header = DbfHeader.CreateHeader(header.Version);
            header.MdxFlag = mdxFlag;
            header.LanguageDriver = languageDriver;
        }

        var memoPath = GetMemoPath(path, true);
        using var memoStream = new FileStream(memoPath, FileMode.Create, FileAccess.Write);
        using var stream = File.Open(path, FileMode.Create, FileAccess.Write);
        Write(stream, false, memoStream);
    }

    /// <summary>
    /// Creates writes the current instance to the specified stream.
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

        Write(stream, true, null);
    }

    private void Write(Stream stream, bool leaveOpen, Stream memoStream)
    {
        using var fptWriter = new BinaryWriter(memoStream);
        using var writer = new BinaryWriter(stream, Encoding, leaveOpen);
        header.Write(writer, Fields, Records);
        WriteFields(writer);
        WriteRecords(writer, fptWriter);
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

    private void WriteRecords(BinaryWriter writer, BinaryWriter fptWriter)
    {
        long? memoOffset = null;
        foreach (var record in Records)
        {
            record.Write(writer, Encoding, ref memoOffset, fptWriter);
        }

        // Write EOF character.
        writer.Write((byte) 0x1a);
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