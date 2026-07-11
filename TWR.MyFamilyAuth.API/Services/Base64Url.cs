namespace TWR.MyFamilyAuth.API.Services;

// WebAuthn/FIDO2 credential IDs and public keys travel as unpadded base64url per the spec —
// Fido2NetLib doesn't expose its internal converter's encode/decode as a public helper.
public static class Base64Url
{
    public static string Encode(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static byte[] Decode(string value)
    {
        var s = value.Replace('-', '+').Replace('_', '/');
        switch (s.Length % 4)
        {
            case 2: s += "=="; break;
            case 3: s += "=";  break;
        }
        return Convert.FromBase64String(s);
    }
}
