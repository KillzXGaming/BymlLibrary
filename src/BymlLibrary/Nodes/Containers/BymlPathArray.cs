using BymlLibrary.Structures;
using LiteYaml.Emitter;
using Revrs;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace BymlLibrary.Nodes.Containers
{
    public struct BymlPathArray
    {
        public BymlPath[] Paths;

        public BymlPathArray(RevrsReader reader, BymlContainer container)
        {
            var start = reader.Position - 4;
            var offsets = reader.ReadSpan<uint>(container.Count + 1);

            Paths = new BymlPath[container.Count];
            for (var i = 0; i < container.Count; i++)
            {
                var count = (offsets[i + 1] - offsets[i]) / BymlPathPoint.SIZE;
                reader.Seek(start + (int)offsets[i]);
                Paths[i] = new BymlPath()
                {
                    Points = reader.ReadSpan<BymlPathPoint>((int)count).ToArray(),
                };
            }
        }

        // Emits per path
        internal unsafe void EmitYaml(ref Utf8YamlEmitter emitter, in ImmutableByml root, int pathIndex)
        {
            emitter.BeginSequence(SequenceStyle.Block);

            foreach (var point in Paths[pathIndex].Points)
            {
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
                emitter.WriteUInt32(point.Unk);

                emitter.EndMapping();
            }

            emitter.EndSequence();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetHashCode(BymlPathPoint[] points)
        {
            HashCode hashCode = new();
            for (int i = 0; i < points.Length; i++)
                hashCode.Add(points[i].GetValueHashCode());  
            return hashCode.ToHashCode();
        }
    }

    [StructLayout(LayoutKind.Sequential, Size = 0x1C)]
    public struct BymlPath
    {
        public BymlPathPoint[] Points;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetNodeHashCode()
        {
            HashCode hashCode = new();
            for (int i = 0; i < Points.Length; i++)
                hashCode.Add(Points[i].GetValueHashCode());
            return hashCode.ToHashCode();
        }
    }

    [StructLayout(LayoutKind.Sequential, Size = 0x1C)]
    public struct BymlPathPoint
    {
        public const int SIZE = 28;

        public Vector3 Position;
        public Vector3 Normal;
        public uint Unk;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly int GetValueHashCode()
        {
            HashCode hashCode = new();
            hashCode.Add(Position.GetHashCode());
            hashCode.Add(Normal.GetHashCode());
            hashCode.Add(Unk.GetHashCode());
            return hashCode.ToHashCode();
        }
    }
}
