using System.Text;

namespace SavefileManager.Services;

/// <summary>
/// Reads the family name out of a .ttf / .otf / .ttc file (the "name" table), so the program knows
/// what to call a font file the user adds without needing any platform API.
/// </summary>
public static class FontNameReader
{
    public static string? ReadFamilyName(string path)
    {
        try { return Read(File.ReadAllBytes(path)); }
        catch { return null; }
    }

    private static int U16(byte[] d, int o) => (d[o] << 8) | d[o + 1];
    private static uint U32(byte[] d, int o) => ((uint)d[o] << 24) | ((uint)d[o + 1] << 16) | ((uint)d[o + 2] << 8) | d[o + 3];

    public static string? Read(byte[] d)
    {
        try
        {
            if (d.Length < 12) return null;
            int baseOff = 0;
            if (d[0] == 't' && d[1] == 't' && d[2] == 'c' && d[3] == 'f')   // font collection: use the first font
                baseOff = (int)U32(d, 12);

            int numTables = U16(d, baseOff + 4);
            int nameOff = -1;
            for (int i = 0; i < numTables; i++)
            {
                int rec = baseOff + 12 + 16 * i;
                if (d[rec] == 'n' && d[rec + 1] == 'a' && d[rec + 2] == 'm' && d[rec + 3] == 'e')
                {
                    nameOff = (int)U32(d, rec + 8);
                    break;
                }
            }
            if (nameOff < 0) return null;

            int count = U16(d, nameOff + 2);
            int strOff = nameOff + U16(d, nameOff + 4);
            string? family = null, typographic = null;
            int familyScore = -1, typoScore = -1;

            for (int i = 0; i < count; i++)
            {
                int rec = nameOff + 6 + 12 * i;
                int platform = U16(d, rec), encoding = U16(d, rec + 2), lang = U16(d, rec + 4);
                int id = U16(d, rec + 6), len = U16(d, rec + 8), off = U16(d, rec + 10);
                if (id != 1 && id != 16) continue;

                string text;
                int score;
                if (platform == 3 || platform == 0)
                {
                    text = Encoding.BigEndianUnicode.GetString(d, strOff + off, len);
                    score = platform == 3 && lang == 0x0409 ? 3 : 2;
                }
                else if (platform == 1 && encoding == 0)
                {
                    text = Encoding.Latin1.GetString(d, strOff + off, len);
                    score = 1;
                }
                else continue;

                text = text.Trim();
                if (text.Length == 0) continue;
                if (id == 1 && score > familyScore) { family = text; familyScore = score; }
                if (id == 16 && score > typoScore) { typographic = text; typoScore = score; }
            }
            // "Family" (id 1) is the name programs normally look fonts up by.
            return family ?? typographic;
        }
        catch
        {
            return null;
        }
    }
}
