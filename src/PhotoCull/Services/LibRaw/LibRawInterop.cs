using System.Runtime.InteropServices;

namespace PhotoCull.Services.LibRaw;

internal static class LibRawInterop
{
    private const string LibName = "libraw";

    [StructLayout(LayoutKind.Sequential)]
    public struct LibrawProcessedImage
    {
        public ushort Type;       // enum LibRaw_image_formats
        public ushort Height;
        public ushort Width;
        public ushort Colors;
        public ushort Bits;
        public int DataSize;
        // data follows immediately after this struct
    }

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr libraw_init(uint flags);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int libraw_open_file(IntPtr handle, string fileName);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int libraw_unpack_thumb(IntPtr handle);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr libraw_dcraw_make_mem_thumb(IntPtr handle, out int errorCode);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int libraw_unpack(IntPtr handle);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int libraw_dcraw_process(IntPtr handle);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr libraw_dcraw_make_mem_image(IntPtr handle, out int errorCode);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void libraw_dcraw_clear_mem(IntPtr processedImage);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void libraw_recycle(IntPtr handle);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void libraw_close(IntPtr handle);
}
