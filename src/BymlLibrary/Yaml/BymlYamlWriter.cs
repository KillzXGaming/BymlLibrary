﻿using BymlLibrary.Nodes.Containers;
using BymlLibrary.Nodes.Immutable.Containers;
using LiteYaml.Emitter;
using System.Buffers;
using System.Globalization;
using System.Text;

namespace BymlLibrary.Yaml;

public static class BymlYamlWriter
{
    public static void Write(ref Utf8YamlEmitter emitter, in ImmutableByml byml, in ImmutableByml root)
    {
        byte[] formattedFloatRentedBuffer = ArrayPool<byte>.Shared.Rent(18);
        Span<byte> formattedFloatBuffer = formattedFloatRentedBuffer.AsSpan()[..18];

        byte[] formattedHexRentedBuffer = ArrayPool<byte>.Shared.Rent(18);
        Span<byte> formattedHexBuffer = formattedHexRentedBuffer.AsSpan()[..18];
        formattedHexBuffer[0] = (byte)'0';
        formattedHexBuffer[1] = (byte)'x';

        if (root.SupportsPaths && byml.Type == BymlNodeType.MK8PathIndex)
        {
            var pathIndex = byml.GetMK8PathIndex();
            root.GetMK8Path()[pathIndex].EmitYaml(ref emitter, root);
            return;
        }

        switch (byml.Type) {
            case BymlNodeType.HashMap32:
                byml.GetHashMap32().EmitYaml(ref emitter, root);
                break;
            case BymlNodeType.HashMap64:
                byml.GetHashMap64().EmitYaml(ref emitter, root);
                break;
            case BymlNodeType.String:
                WriteRawString(ref emitter, byml.GetStringIndex(), root.StringTable);
                break;
            case BymlNodeType.Binary:
                Span<byte> data = byml.GetBinary();
                WriteBinary(ref emitter, data);
                break;
            case BymlNodeType.BinaryAligned:
                Span<byte> dataAligned = byml.GetBinaryAligned(out int alignment);
                WriteBinaryAligned(ref emitter, dataAligned, alignment);
                break;
            case BymlNodeType.Array:
                byml.GetArray().EmitYaml(ref emitter, root);
                break;
            case BymlNodeType.Map:
                byml.GetMap().EmitYaml(ref emitter, root);
                break;
            case BymlNodeType.Bool:
                emitter.WriteBool(byml.GetBool());
                break;
            case BymlNodeType.Int:
                emitter.WriteInt32(byml.GetInt());
                break;
            case BymlNodeType.Float:
                WriteFloat(ref emitter, ref formattedFloatBuffer, byml.GetFloat());
                break;
            case BymlNodeType.UInt32:
                emitter.SetTag("!u");
                WriteUInt32(ref emitter, ref formattedHexBuffer, byml.GetUInt32());
                break;
            case BymlNodeType.Int64:
                emitter.SetTag("!l");
                emitter.WriteInt64(byml.GetInt64());
                break;
            case BymlNodeType.UInt64:
                emitter.SetTag("!ul");
                WriteUInt64(ref emitter, ref formattedHexBuffer, byml.GetUInt64());
                break;
            case BymlNodeType.Double:
                emitter.SetTag("!d");
                WriteDouble(ref emitter, ref formattedFloatBuffer, byml.GetDouble());
                break;
            case BymlNodeType.Null:
                emitter.WriteNull();
                break;
            default:
                throw new InvalidOperationException($"""
                    Invalid or unsupported node type '{byml.Type}'
                    """);
        }
    }

    public static void WriteRawString(ref Utf8YamlEmitter emitter, int index, in ImmutableBymlStringTable stringTable)
    {
        emitter.WriteString(Encoding.UTF8.GetString(stringTable[index][..^1]));
    }

    public static void WritePathPoint(ref Utf8YamlEmitter emitter, int index, in ImmutableBymlPath path)
    {
        var point = path[index];

        emitter.BeginMapping(MappingStyle.Flow);

        emitter.WriteString("X");
        emitter.WriteDouble(point.Position.X);
        emitter.WriteString("Y");
        emitter.WriteDouble(point.Position.Y);
        emitter.WriteString("Z");
        emitter.WriteDouble(point.Position.Z);

        emitter.WriteString("NX");
        emitter.WriteDouble(point.Normal.X);
        emitter.WriteString("NY");
        emitter.WriteDouble(point.Normal.Y);
        emitter.WriteString("NZ");
        emitter.WriteDouble(point.Normal.Z);

        emitter.WriteString("Value");
        emitter.WriteUInt32(point.Value);

        emitter.EndMapping();
    }

    public static void Write(ref Utf8YamlEmitter emitter, in Byml byml)
    {
        switch (byml.Value) {
            case IBymlNode node:
                node.EmitYaml(ref emitter);
                return;
        }

        byte[] formattedFloatRentedBuffer = ArrayPool<byte>.Shared.Rent(18);
        Span<byte> formattedFloatBuffer = formattedFloatRentedBuffer.AsSpan()[..18];

        byte[] formattedHexRentedBuffer = ArrayPool<byte>.Shared.Rent(18);
        Span<byte> formattedHexBuffer = formattedHexRentedBuffer.AsSpan()[..18];
        formattedHexBuffer[0] = (byte)'0';
        formattedHexBuffer[1] = (byte)'x';

        switch (byml.Type) {
            case BymlNodeType.String:
                emitter.WriteString(byml.GetString());
                break;
            case BymlNodeType.Binary:
                byte[] data = byml.GetBinary();
                WriteBinary(ref emitter, data);
                break;
            case BymlNodeType.BinaryAligned:
                (byte[] dataAligned, int alignment) = byml.GetBinaryAligned();
                WriteBinaryAligned(ref emitter, dataAligned, alignment);
                break;
            case BymlNodeType.Bool:
                emitter.WriteBool(byml.GetBool());
                break;
            case BymlNodeType.Int:
                emitter.WriteInt32(byml.GetInt());
                break;
            case BymlNodeType.Float:
                WriteFloat(ref emitter, ref formattedFloatBuffer, byml.GetFloat());
                break;
            case BymlNodeType.UInt32:
                emitter.SetTag("!u");
                WriteUInt32(ref emitter, ref formattedHexBuffer, byml.GetUInt32());
                break;
            case BymlNodeType.Int64:
                emitter.SetTag("!l");
                emitter.WriteInt64(byml.GetInt64());
                break;
            case BymlNodeType.UInt64:
                emitter.SetTag("!ul");
                WriteUInt64(ref emitter, ref formattedHexBuffer, byml.GetUInt64());
                break;
            case BymlNodeType.Double:
                emitter.SetTag("!d");
                WriteDouble(ref emitter, ref formattedFloatBuffer, byml.GetDouble());
                break;
            case BymlNodeType.Null:
                emitter.WriteNull();
                break;
            default:
                throw new InvalidOperationException($"""
                    Invalid or unsupported node type '{byml.Type}'
                    """);
        }
    }

    private static void WriteFloat(ref Utf8YamlEmitter emitter, ref Span<byte> formattedFloatBuffer, float value)
    {
        string formatted = (value % 1) switch {
            0 => string.Format(CultureInfo.InvariantCulture, "{0}.0", value),
            _ => value.ToString(CultureInfo.InvariantCulture.NumberFormat)
        };

        int bytesWritten = Encoding.UTF8.GetBytes(formatted, formattedFloatBuffer);
        emitter.WriteScalar(formattedFloatBuffer[..bytesWritten]);
    }

    private static void WriteDouble(ref Utf8YamlEmitter emitter, ref Span<byte> formattedDoubleBuffer, double value)
    {
        string formatted = (value % 1) switch {
            0 => string.Format(CultureInfo.InvariantCulture, "{0}.0", value),
            _ => value.ToString(CultureInfo.InvariantCulture.NumberFormat)
        };

        int bytesWritten = Encoding.UTF8.GetBytes(formatted, formattedDoubleBuffer);
        emitter.WriteScalar(formattedDoubleBuffer[..bytesWritten]);
    }

    private static void WriteUInt32(ref Utf8YamlEmitter emitter, ref Span<byte> formattedHexBuffer, uint value)
    {
        value.TryFormat(formattedHexBuffer[2..], out int written, "x8");
        emitter.WriteScalar(formattedHexBuffer[..(2 + written)]);
    }
    
    private static void WriteUInt64(ref Utf8YamlEmitter emitter, ref Span<byte> formattedHexBuffer, ulong value)
    {
        value.TryFormat(formattedHexBuffer[2..], out int written, "x16");
        emitter.WriteScalar(formattedHexBuffer[..(2 + written)]);
    }

    private static void WriteBinary(ref Utf8YamlEmitter emitter, Span<byte> data)
    {
        emitter.SetTag("!!binary");
        emitter.WriteString(
            Convert.ToBase64String(data)
        );
    }

    private static void WriteBinaryAligned(ref Utf8YamlEmitter emitter, in Span<byte> data, int alignment)
    {
        emitter.SetTag("!!file");
        emitter.BeginMapping(MappingStyle.Flow);
        emitter.WriteString("Alignment");
        emitter.WriteInt32(alignment);
        emitter.WriteString("Data");
        emitter.SetTag("!!binary");
        emitter.WriteString(Convert.ToBase64String(data));
        emitter.EndMapping();
    }
}
