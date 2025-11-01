using BymlLibrary.Extensions;
using BymlLibrary.Nodes.Containers;
using BymlLibrary.Structures;
using Revrs;
using System.Runtime.CompilerServices;
using System.Text;

namespace BymlLibrary.Writers;

internal class BymlWriter
{
    private readonly Byml _root;
    private readonly ushort _version;
    private readonly bool _supportPaths;

    private readonly BymlNodeCache _nodeCache = new();

    private Dictionary<string, int> _keys = [];
    private Dictionary<string, int> _strings = [];
    private Dictionary<int, (int, BymlPath)> _paths = [];

    private Encoding _encoding;

    public RevrsWriter Writer { get; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public BymlWriter(Byml byml, in Stream stream, Endianness endianness,
        ushort version, bool supportPaths, bool useShiftJIS = false)
    {
        Writer = new(stream, endianness);
        _version = version;
        _supportPaths = supportPaths;
        _encoding = useShiftJIS ? Encoding.UTF8 : Encoding.UTF8;

        Collect(_root = byml);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write()
    {
        Writer.Seek(BymlHeader.SIZE + (_supportPaths ? 4 : 0));

        int keyTableOffset = WriteStringTable(ref _keys);
        int stringTableOffset = WriteStringTable(ref _strings);
        int pathNodeOffset = WritePathTable(ref _paths);

        int rootNodeOffset = (int)Writer.Position;
        Write(_root);

        Writer.Seek(0);
        Writer.Write<BymlHeader, BymlHeader.Reverser>(new(
            magic: Byml.BYML_MAGIC,
            version: _version,
            keyTableOffset,
            stringTableOffset,
            rootNodeOffset
        ));

        // Write path then root if paths are supported
        if (_supportPaths)
        {
            Writer.Seek(12);
            Writer.Write(pathNodeOffset);
            Writer.Write(rootNodeOffset);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(in Byml byml)
    {
        if (byml.Value is IBymlNode container) {
            WriteContainer(container);
        }
        else if (byml.Type.IsSpecialValueType()) {
            WriteSpecial(byml);
        }
        else {
            WriteValue(byml);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void WriteContainer(IBymlNode container)
    {
        List<(long, Byml)> staged = [];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void WriteNode(Byml byml)
        {
            if (byml.Type.IsValueType() || byml.Value is BymlPath) {
                WriteValue(byml);
            }
            else {
                staged.Add((Writer.Position, byml));
                Writer.Write(0u);
            }
        }

        container.Write(this, WriteNode);

        foreach ((long offset, Byml node) in staged) {
            int currentPosition = (int)Writer.Position;
            if (_nodeCache.Lookup(node, out int hash, out int bucket) is int cachedOffset) {
                Writer.Seek(offset);
                Writer.Write(cachedOffset);
                Writer.Seek(currentPosition);
            }
            else {
                Writer.Seek(offset);

                if (node.Value is (byte[] _, int alignment)) {
                    currentPosition += (currentPosition + 8).AlignUp(alignment);
                }

                Writer.Write(currentPosition);
                Writer.Seek(currentPosition);
                Write(node);
                _nodeCache.UpdateOffset(hash, bucket, node, currentPosition);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WriteValue(in Byml byml)
    {
        // Path nodes are used over BymlNodeType.Binary for v1
        // So check by value rather than type
        if (byml.Value is BymlPath path)
        {
            // Path index which is stored under the _paths during Collect()
            var p = _paths[path.GetNodeHashCode()];
            Writer.Write(p.Item1);
            return;
        }

        switch (byml.Type) {
            case BymlNodeType.String: {
                Writer.Write(_strings[byml.GetString()]);
                break;
            }
            case BymlNodeType.Bool: {
                Writer.Write(byml.GetBool() ? 1 : 0);
                break;
            }
            case BymlNodeType.Int: {
                Writer.Write(byml.GetInt());
                break;
            }
            case BymlNodeType.UInt32: {
                Writer.Write(byml.GetUInt32());
                break;
            }
            case BymlNodeType.Float: {
                Writer.Write(byml.GetFloat());
                break;
            }
            case BymlNodeType.Null: {
                Writer.Write(0);
                break;
            }
            default: {
                throw new NotSupportedException($"""
                    The value type node '{byml.Type}' is not supported.
                    """);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WriteSpecial(in Byml byml)
    {
        switch (byml.Type) {
            case BymlNodeType.Binary: {
                byte[] data = byml.GetBinary();
                Writer.Write(data.Length);
                Writer.Write(data);
                Writer.Align(4);
                break;
            }
            case BymlNodeType.BinaryAligned: {
                (byte[] data, int alignment) = byml.GetBinaryAligned();
                Writer.Write(data.Length);
                Writer.Write(alignment);
                Writer.Write(data);
                break;
            }
            case BymlNodeType.Int64: {
                Writer.Write(byml.GetInt64());
                break;
            }
            case BymlNodeType.UInt64: {
                Writer.Write(byml.GetUInt64());
                break;
            }
            case BymlNodeType.Double: {
                Writer.Write(byml.GetDouble());
                break;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteContainerHeader(BymlNodeType type, int count)
    {
        BymlContainer container = new(type, count);
        Writer.Write<BymlContainer, BymlContainer.Reverser>(container);
    }

    public int WritePathTable(ref Dictionary<int, (int, BymlPath)> paths)
    {
        if (paths.Count <= 0)
            return 0;

        int tableOffset = (int)Writer.Position;
        WriteContainerHeader(BymlNodeType.MK8PathArray, paths.Count);

        // Write path offsets
        int previousStringOffset = ((paths.Count + 1) * sizeof(uint)) + BymlContainer.SIZE;

        Writer.Write(previousStringOffset);
        foreach (var path in paths.Values) {
            Writer.Write(previousStringOffset += BymlPathPoint.SIZE * path.Item2.Points.Length);
        }

        foreach (var point in paths.Values.SelectMany(x => x.Item2.Points)) {
            Writer.Write(point.Position);
            Writer.Write(point.Normal);
            Writer.Write(point.Value);
        }

        Writer.Align(4);
        return tableOffset;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int WriteStringTable(ref Dictionary<string, int> strings)
    {
        if (strings.Count <= 0) {
            return 0;
        }

        strings = strings
            .OrderBy(x => x.Key, StringComparer.Ordinal)
            .Select((x, i) => (x.Key, Index: i))
            .ToDictionary(x => x.Key, x => x.Index);

        int tableOffset = (int)Writer.Position;

        WriteContainerHeader(BymlNodeType.StringTable, strings.Count);

        int previousStringOffset = ((strings.Count + 1) * sizeof(uint)) + BymlContainer.SIZE;
        Writer.Write(previousStringOffset);
        foreach (var str in strings.Keys) {
            Writer.Write(previousStringOffset += _encoding.GetByteCount(str) + 1);
        }

        foreach (var str in strings.Keys) {
            Writer.WriteStringUtf8(str);
            Writer.Write<byte>(0);
        }

        Writer.Align(4);
        return tableOffset;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Collect(in Byml byml)
    {
        if (byml.Value is IBymlNode container) {
            int hash = container.Collect(this);
            _nodeCache[byml] = hash;
            return hash;
        }
        else if (byml.Value is string str) {
            _strings.TryAdd(str, 0);
            return Byml.ValueEqualityComparer.GetValueNodeHashCode(byml);
        }
        else if (byml.Value is BymlPath path)
        {
            if (!_supportPaths)
                throw new Exception($"Format is not set to save with paths but path data is included!");

            int hash = path.GetNodeHashCode();
            _paths.Add(hash, (_paths.Count, path));
            _nodeCache[byml] = hash;
            return hash;
        }
        else if (byml.Value is byte[] data) {
            int hash = Byml.ValueEqualityComparer.GetBinaryNodeHashCode((data, null), byml.Type);
            _nodeCache[byml] = hash;
            return hash;
        }
        else if (byml.Type == BymlNodeType.BinaryAligned) {
            int hash = Byml.ValueEqualityComparer.GetBinaryNodeHashCode(byml.GetBinaryAligned(), byml.Type);
            _nodeCache[byml] = hash;
            return hash;
        }
        else if (byml.Type.IsSpecialValueType()) {
            int hash = Byml.ValueEqualityComparer.GetValueNodeHashCode(byml);
            _nodeCache[byml] = hash;
            return hash;
        }
        else {
            return Byml.ValueEqualityComparer.GetValueNodeHashCode(byml);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddKey(string value) => _keys[value] = 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetKeyIndex(string key) => _keys[key];
}
