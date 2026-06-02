using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class TtfTableReader
{
    private readonly BinaryReader reader;

    private readonly Dictionary<string, TableRecord> tables =
        new Dictionary<string, TableRecord>();

    private readonly Dictionary<uint, uint> unicodeToGlyph =
        new Dictionary<uint, uint>();

    private uint[] glyphOffsets;

    private ushort unitsPerEm;

    private short indexToLocFormat;

    private struct TableRecord
    {
        public uint offset;
        public uint length;
    }

    public TtfTableReader(byte[] fontBytes)
    {
        reader = new BinaryReader(new MemoryStream(fontBytes));

        ReadTableDirectory();

        ParseHead();

        ParseLoca();

        ParseCmap();
        Debug.Log("TTF Loaded");
        Debug.Log("unitsPerEm: " + unitsPerEm);
        Debug.Log("glyphOffsets count: " + glyphOffsets.Length);
        Debug.Log("cmap entries: " + unicodeToGlyph.Count);
    }

    public uint GetGlyphIndex(uint unicode)
    {
        Debug.Log("Looking up unicode: " + unicode + " (" + (char)unicode + ")");
        if (unicodeToGlyph.TryGetValue(unicode, out uint glyph))
        {
            Debug.Log("Glyph Index: " + glyph);
            return glyph;
        }

        Debug.Log("CMAP MISS");

        return 0;
    }

    public GlyphShape ReadGlyph(uint glyphIndex)
    {
        Debug.Log("READ GLYPH: " + glyphIndex);

        if (glyphIndex >= glyphOffsets.Length - 1)
        {
            Debug.Log("Glyph index out of range.");
            return null;
        }

        uint glyphOffset = glyphOffsets[glyphIndex];
        uint nextGlyphOffset = glyphOffsets[glyphIndex + 1];

        Debug.Log("Glyph Offset: " + glyphOffset);
        Debug.Log("Next Glyph Offset: " + nextGlyphOffset);

        if (glyphOffset == nextGlyphOffset)
        {
            Debug.Log("EMPTY GLYPH");
            return null;
        }

        TableRecord glyf = tables["glyf"];

        reader.BaseStream.Seek(glyf.offset + glyphOffset, SeekOrigin.Begin);

        short contourCount = ReadInt16();

        Debug.Log("Contour Count: " + contourCount);

        short xMin = ReadInt16();
        short yMin = ReadInt16();
        short xMax = ReadInt16();
        short yMax = ReadInt16();

        if (contourCount <= 0)
        {
            Debug.Log("Composite glyphs not implemented yet.");
            return null;
        }

        GlyphShape shape = new GlyphShape();

        ushort[] contourEndPoints = new ushort[contourCount];

        for (int i = 0; i < contourCount; i++)
        {
            contourEndPoints[i] = ReadUInt16();
        }

        ushort instructionLength = ReadUInt16();

        reader.BaseStream.Seek(instructionLength, SeekOrigin.Current);

        int pointCount = contourEndPoints[contourCount - 1] + 1;

        byte[] flags = new byte[pointCount];

        for (int i = 0; i < pointCount;)
        {
            byte flag = reader.ReadByte();

            flags[i++] = flag;

            if ((flag & 0x08) != 0)
            {
                byte repeatCount = reader.ReadByte();

                for (int r = 0; r < repeatCount; r++)
                {
                    flags[i++] = flag;
                }
            }
        }

        int[] xCoords = new int[pointCount];
        int currentX = 0;

        for (int i = 0; i < pointCount; i++)
        {
            byte flag = flags[i];

            bool xShort = (flag & 0x02) != 0;
            bool xSame = (flag & 0x10) != 0;

            int delta = 0;

            if (xShort)
            {
                byte value = reader.ReadByte();
                delta = xSame ? value : -value;
            }
            else if (!xSame)
            {
                delta = ReadInt16();
            }

            currentX += delta;

            xCoords[i] = currentX;
        }

        int[] yCoords = new int[pointCount];
        int currentY = 0;

        for (int i = 0; i < pointCount; i++)
        {
            byte flag = flags[i];

            bool yShort = (flag & 0x04) != 0;
            bool ySame = (flag & 0x20) != 0;

            int delta = 0;

            if (yShort)
            {
                byte value = reader.ReadByte();
                delta = ySame ? value : -value;
            }
            else if (!ySame)
            {
                delta = ReadInt16();
            }

            currentY += delta;

            yCoords[i] = currentY;
        }

        List<Vector2> points = new List<Vector2>(pointCount);
        List<bool> onCurve = new List<bool>(pointCount);

        for (int i = 0; i < pointCount; i++)
        {
            points.Add(new Vector2(xCoords[i], yCoords[i]));
            onCurve.Add((flags[i] & 0x01) != 0);
        }

        int contourStart = 0;

        for (int contourIndex = 0; contourIndex < contourCount; contourIndex++)
        {
            int contourEnd = contourEndPoints[contourIndex];

            GlyphContour contour = new GlyphContour();

            List<Vector2> contourPoints = new List<Vector2>();

            for (int i = contourStart; i <= contourEnd; i++)
            {
                contourPoints.Add(points[i]);
            }

            contour.points = contourPoints;

            float signedArea = ComputeSignedArea(contourPoints);

            contour.isHole = signedArea < 0f;

            shape.contours.Add(contour);

            contourStart = contourEnd + 1;
        }

        Debug.Log("Generated contours: " + shape.contours.Count);

        return shape;
    }

    private float ComputeSignedArea(List<Vector2> points)
    {
        float area = 0f;

        for (int i = 0; i < points.Count; i++)
        {
            Vector2 a = points[i];
            Vector2 b = points[(i + 1) % points.Count];

            area += (a.x * b.y) - (b.x * a.y);
        }

        return area * 0.5f;
    }

    private void ReadTableDirectory()
    {
        reader.BaseStream.Seek(4, SeekOrigin.Begin);

        ushort numTables = ReadUInt16();

        reader.BaseStream.Seek(6, SeekOrigin.Current);

        for (int i = 0; i < numTables; i++)
        {
            string tag = new string(reader.ReadChars(4));

            uint checksum = ReadUInt32();
            uint offset = ReadUInt32();
            uint length = ReadUInt32();

            tables.Add(tag, new TableRecord
            {
                offset = offset,
                length = length
            });
        }
    }

    private void ParseHead()
    {
        TableRecord head = tables["head"];

        reader.BaseStream.Seek(head.offset + 18, SeekOrigin.Begin);

        unitsPerEm = ReadUInt16();

        reader.BaseStream.Seek(head.offset + 50, SeekOrigin.Begin);

        indexToLocFormat = ReadInt16();
    }

    private void ParseLoca()
    {
        TableRecord loca = tables["loca"];

        reader.BaseStream.Seek(loca.offset, SeekOrigin.Begin);

        int count = (int)(loca.length / (indexToLocFormat == 0 ? 2 : 4));

        glyphOffsets = new uint[count];

        for (int i = 0; i < count; i++)
        {
            glyphOffsets[i] = indexToLocFormat == 0
                ? (uint)(ReadUInt16() * 2)
                : ReadUInt32();
        }
    }

    private void ParseCmap()
    {
        TableRecord cmap = tables["cmap"];

        reader.BaseStream.Seek(cmap.offset, SeekOrigin.Begin);

        ushort version = ReadUInt16();
        ushort numTables = ReadUInt16();

        uint subtableOffset = 0;

        // Find Unicode BMP encoding (platform 3, encoding 1 or 0)
        for (int i = 0; i < numTables; i++)
        {
            ushort platformID = ReadUInt16();
            ushort encodingID = ReadUInt16();
            uint offset = ReadUInt32();

            // Unicode BMP
            if (platformID == 3 && (encodingID == 1 || encodingID == 0))
            {
                subtableOffset = cmap.offset + offset;
                break;
            }
        }

        if (subtableOffset == 0)
        {
            Debug.LogError("No usable cmap subtable found");
            return;
        }

        reader.BaseStream.Seek(subtableOffset, SeekOrigin.Begin);

        ushort format = ReadUInt16();

        if (format != 4)
        {
            Debug.LogError("Only cmap format 4 supported right now. Found: " + format);
            return;
        }

        ReadUInt16(); // length
        ReadUInt16(); // language
        ushort segCountX2 = ReadUInt16();
        ushort segCount = (ushort)(segCountX2 / 2);

        ReadUInt16(); // searchRange
        ReadUInt16(); // entrySelector
        ReadUInt16(); // rangeShift

        ushort[] endCodes = new ushort[segCount];
        ushort[] startCodes = new ushort[segCount];
        short[] idDelta = new short[segCount];
        ushort[] idRangeOffset = new ushort[segCount];

        for (int i = 0; i < segCount; i++) endCodes[i] = ReadUInt16();
        ReadUInt16(); // reservedPad
        for (int i = 0; i < segCount; i++) startCodes[i] = ReadUInt16();
        for (int i = 0; i < segCount; i++) idDelta[i] = ReadInt16();
        for (int i = 0; i < segCount; i++) idRangeOffset[i] = ReadUInt16();

        uint glyphIdArrayStart = (uint)reader.BaseStream.Position;

        for (int i = 0; i < segCount; i++)
        {
            for (uint c = startCodes[i]; c <= endCodes[i]; c++)
            {
                if (c == 0xFFFF) continue;

                uint glyphIndex;

                if (idRangeOffset[i] == 0)
                {
                    glyphIndex = (uint)((c + idDelta[i]) & 0xFFFF);
                }
                else
                {
                    uint offset = (idRangeOffset[i] / 2U) + (c - startCodes[i]) + (uint)i;
                    reader.BaseStream.Seek(glyphIdArrayStart + offset * 2, SeekOrigin.Begin);
                    glyphIndex = ReadUInt16();

                    if (glyphIndex != 0)
                        glyphIndex = (uint)((glyphIndex + idDelta[i]) & 0xFFFF);
                }

                unicodeToGlyph[c] = glyphIndex;
            }
        }

        Debug.Log("cmap entries: " + unicodeToGlyph.Count);
    }

    private ushort ReadUInt16()
    {
        byte a = reader.ReadByte();
        byte b = reader.ReadByte();

        return (ushort)((a << 8) | b);
    }

    private short ReadInt16()
    {
        return unchecked((short)ReadUInt16());
    }

    private uint ReadUInt32()
    {
        byte a = reader.ReadByte();
        byte b = reader.ReadByte();
        byte c = reader.ReadByte();
        byte d = reader.ReadByte();

        return ((uint)a << 24) |
               ((uint)b << 16) |
               ((uint)c << 8) |
               d;
    }
}