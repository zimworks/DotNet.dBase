﻿using System.Text;

namespace dBASE.NET.Encoders;

internal class LogicalEncoder : IEncoder
{
    private static LogicalEncoder _instance;

    private LogicalEncoder() { }

    public static LogicalEncoder Instance => _instance ??= new LogicalEncoder();

    /// <inheritdoc />
    public byte[] Encode(DbfField field, object data, Encoding encoding)
    {
        // Convert boolean value to string.
        var text = "?";
        if (data != null)
        {
            text = (bool)data ? "Y" : "N";
        }

        // Grow string to fill field length.
        text = text.PadLeft(field.Length, ' ');

        // Convert string to byte array.
        return encoding.GetBytes(text);
    }

    /// <inheritdoc />
    public object Decode(byte[] buffer, byte[] memoData, Encoding encoding)
    {
        var text = encoding.GetString(buffer).Trim().ToUpper();
        if (text == "?")
        {
            return null;
        }
        return (text == "Y" || text == "T");
    }
}