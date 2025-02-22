namespace dBASE.NET
{
    public enum FoxProCodepage : byte
    {
        DOS_USA                    = 0x01, //code page 437
        DOS_Multilingual           = 0x02, //code page 850
        Windows_ANSI               = 0x03, //code page 1252
        Standard_Macintosh         = 0x04, 
        EE_MSDOS                   = 0x64, //code page 852
        Nordic_MSDOS               = 0x65, //code page 865
        Russian_MSDOS              = 0x66, //code page 866
        Icelandic_MSDOS            = 0x67, 
        Kamenicky_Czech_MSDOS      = 0x68, 
        Mazovia_Polish_MSDOS       = 0x69, 
        Greek_MSDOS_437G           = 0x6A, 
        Turkish_MSDOS              = 0x6B, 
        Russian_Macintosh          = 0x96, 
        Eastern_European_Macintosh = 0x97, 
        Greek_Macintosh            = 0x98, 
        Windows_EE                 = 0xC8, //code page 1250
        Russian_Windows            = 0xC9, 
        Turkish_Windows            = 0xCA, 
        Greek_Windows              = 0xCB
    }
}
