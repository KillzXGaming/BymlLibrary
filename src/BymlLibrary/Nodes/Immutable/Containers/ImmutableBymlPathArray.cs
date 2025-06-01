using BymlLibrary.Nodes.Containers;
using BymlLibrary.Structures;
using LiteYaml.Emitter;
using Revrs;
using Revrs.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace BymlLibrary.Nodes.Immutable.Containers
{
    public readonly ref struct ImmutableBymlPathArray(Span<byte> data, int offset, int count)
    {
        /// <summary>
        /// Span of the BYMl data
        /// </summary>
        private readonly Span<byte> _data = data;

        /// <summary>
        /// The container offset (start of header)
        /// </summary>
        private readonly int _offset = offset;

        /// <summary>
        /// The container item count
        /// </summary>
        private readonly int _count = count;

        /// <summary>
        /// The path offsets in this container
        /// </summary>
        private readonly ReadOnlySpan<int> _offsets
            = data[(offset + BymlContainer.SIZE)..].ReadSpan<int>(++count);

        public readonly ImmutableBymlPath this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                int start = _offset + _offsets[index];
                int end = _offset + _offsets[++index];
                int count = (end - start) / Point.SIZE;
                // bytes for the path data
                return new ImmutableBymlPath(_data[start..end], count);
            }
        }


        public readonly ref struct ImmutableBymlPath(Span<byte> data, int count)
        {
            /// <summary>
            /// Span of the BYMl data
            /// </summary>
            private readonly Span<byte> _data = data;

            /// <summary>
            /// The path point count
            /// </summary>
            private readonly int _count = count;

            /// <summary>
            /// Path point entries
            /// </summary>
            private readonly Span<Point> _entries = count == 0 ? []
                : data[..].ReadSpan<Point>(count);

            public readonly Point this[int index]
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get
                {
                    int start = index * Point.SIZE;
                    int end = start + Point.SIZE;
                    // bytes for the path data
                    return _data[start..end].Read<Point>();
                }
            }

            public BymlPath ToMutable(in ImmutableByml root)
            {
                BymlPath path = new BymlPath() {
                    Points = new BymlPathPoint[_count], 
                };

                for (int i = 0; i < _count; i++)
                {
                    var point = this[i];
                    path.Points[i] = new BymlPathPoint()
                    {
                        Position = point.Position,
                        Normal = point.Normal,
                        Value = point.Value,
                    };
                }

                return path;
            }

            internal unsafe void EmitYaml(ref Utf8YamlEmitter emitter, in ImmutableByml root)
            {
                emitter.BeginSequence(SequenceStyle.Block);

                for (int i = 0; i < _count; i++) 
                {
                    var point = this[i];

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

                emitter.EndSequence();
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4, Size = SIZE)]
        public readonly struct Point()
        {
            public const int SIZE = 28;

            public readonly Vector3 Position;
            public readonly Vector3 Normal;
            public readonly uint Value;

            public class Reverser : IStructReverser
            {
                public static void Reverse(in Span<byte> slice)
                {
                    slice[0..4].Reverse();
                    slice[4..8].Reverse();
                    slice[8..12].Reverse();

                    slice[12..16].Reverse();
                    slice[16..20].Reverse();
                    slice[20..24].Reverse();

                    slice[24..28].Reverse();
                }
            }
        }
    }
}
