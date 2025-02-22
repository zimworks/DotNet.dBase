namespace dBASE.NET
{
    public enum FoxProFlag : byte
    {
     WithMemo = 0x02,
     DBC = 0x04, // DatabaseContainer
     DBCWithMemo = 0x07 // incl. memo & indexes
    }
}
