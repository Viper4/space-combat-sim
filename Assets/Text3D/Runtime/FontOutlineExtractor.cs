using System.Collections.Generic;
using UnityEngine;

public class FontOutlineExtractor
{
    private readonly TtfTableReader reader;

    private readonly Dictionary<uint, GlyphShape> glyphCache =
        new Dictionary<uint, GlyphShape>();

    public FontOutlineExtractor(byte[] fontBytes)
    {
        reader = new TtfTableReader(fontBytes);
    }

    public GlyphShape GetGlyphShape(uint unicode)
    {
        Debug.Log("REQUEST GLYPH FOR: " + (char)unicode + " (" + unicode + ")");
        if (glyphCache.TryGetValue(unicode, out GlyphShape cached))
        {
            return cached;
        }

        uint glyphIndex = reader.GetGlyphIndex(unicode);

        GlyphShape shape = reader.ReadGlyph(glyphIndex);

        glyphCache.Add(unicode, shape);

        return shape;
    }
}