using System.Security.Cryptography;
using System.Text;

namespace AdminLicencias.Api.Security;

internal static class SecureCompare
{
    public static bool EqualsConstantTime(string? a, string? b)
    {
        if (a is null || b is null) return false;
        var left = Encoding.UTF8.GetBytes(a.Trim());
        var right = Encoding.UTF8.GetBytes(b.Trim());
        if (left.Length != right.Length)
        {
            // Comparar contra sí mismo para no filtrar longitud por timing obvio.
            CryptographicOperations.FixedTimeEquals(left, left);
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(left, right);
    }
}
