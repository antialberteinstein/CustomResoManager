using System.Runtime.InteropServices;

namespace CustomResoManager.Models
{
    [StructLayout(LayoutKind.Explicit)]
    public struct DEVMODE
    {
        [FieldOffset(0)]
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmDeviceName;
        [FieldOffset(32)]
        public short dmSpecVersion;
        [FieldOffset(34)]
        public short dmDriverVersion;
        [FieldOffset(36)]
        public short dmSize;
        [FieldOffset(38)]
        public short dmDriverExtra;
        [FieldOffset(40)]
        public int dmFields;

        [FieldOffset(44)]
        public short dmOrientation;
        [FieldOffset(46)]
        public short dmPaperSize;
        [FieldOffset(48)]
        public short dmPaperLength;
        [FieldOffset(50)]
        public short dmPaperWidth;

        [FieldOffset(52)]
        public short dmScale;
        [FieldOffset(54)]
        public short dmCopies;
        [FieldOffset(56)]
        public short dmDefaultSource;
        [FieldOffset(58)]
        public short dmPrintQuality;

        [FieldOffset(60)]
        public short dmColor;
        [FieldOffset(62)]
        public short dmDuplex;
        [FieldOffset(64)]
        public short dmYResolution;
        [FieldOffset(66)]
        public short dmTTOption;
        [FieldOffset(68)]
        public short dmCollate;
        [FieldOffset(72)]
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmFormName;
        [FieldOffset(102)]
        public short dmLogPixels;
        [FieldOffset(104)]
        public int dmBitsPerPel;
        [FieldOffset(108)]
        public int dmPelsWidth;
        [FieldOffset(112)]
        public int dmPelsHeight;
        [FieldOffset(116)]
        public int dmDisplayFlags;
        [FieldOffset(116)]
        public int dmNup;
        [FieldOffset(120)]
        public int dmDisplayFrequency;
    }

    public class ResolutionModel
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public int RefreshRate { get; set; }
        public bool IsNative { get; set; }

        public override string ToString()
        {
            return $"{Width}x{Height} @ {RefreshRate}Hz";
        }
    }
}