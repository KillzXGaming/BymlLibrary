using BymlLibrary.Structures;
using Revrs.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
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

        public readonly Span<byte> this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                int start = _offset + _offsets[index];
                int end = _offset + _offsets[++index];
                // bytes for the path data
                return _data[start..end];
            }
        }
    }
}
