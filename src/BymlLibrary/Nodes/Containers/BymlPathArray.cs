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
        public uint Value;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly int GetValueHashCode()
        {
            HashCode hashCode = new();
            hashCode.Add(Position.GetHashCode());
            hashCode.Add(Normal.GetHashCode());
            hashCode.Add(Value.GetHashCode());
            return hashCode.ToHashCode();
        }
    }
}
