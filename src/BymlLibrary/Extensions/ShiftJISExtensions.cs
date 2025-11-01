using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace BymlLibrary.Extensions;

public static class ShiftJISExtensions
{
    static bool _registered = false;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe string ToShiftJISManaged(this Span<byte> jis)
    {
        fixed (byte* ptr = jis) {
            return ShiftJIS().GetString(ptr, jis.Length - 1);
        }
    }

    public static Encoding ShiftJIS()
    {
        TryInit();
        return Encoding.GetEncoding("shift_jis");
    }

    public static void TryInit()
    {
        if (!_registered) {
            _registered = true;
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }
    }
}
