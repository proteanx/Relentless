using System;
using System.Collections.Generic;

namespace Loom.ZombieBattleground
{
    public struct ItemPosition : IEquatable<ItemPosition>, IComparable<ItemPosition>
    {
        public static readonly ItemPosition Start = new ItemPosition(true, -1);
        public static readonly ItemPosition End = new ItemPosition(true, -2);

        private int Index { get; }

        private ItemPosition(bool skipChecks, int index)
        {
            if (!skipChecks && index < 0)
                throw new ArgumentOutOfRangeException(nameof(index));

            Index = index;
        }

        public ItemPosition(int index) : this(false, index)
        {
        }

        public int GetIndex<T>(ICollection<T> collection)
        {
            if (collection == null)
                throw new ArgumentNullException(nameof(collection));

            return GetIndex(collection.Count);
        }

        public int GetIndex<T>(IReadOnlyCollection<T> collection)
        {
            if (collection == null)
                throw new ArgumentNullException(nameof(collection));

            return GetIndex(collection.Count);
        }

        public int GetIndex(int count)
        {
            if (this == Start)
                return 0;

            if (this == End)
                return count;

            return Index;
        }

        public bool Equals(ItemPosition other)
        {
            return Index == other.Index;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is ItemPosition other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Index;
        }

        public static bool operator ==(ItemPosition left, ItemPosition right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ItemPosition left, ItemPosition right)
        {
            return !left.Equals(right);
        }

        public int CompareTo(ItemPosition other)
        {
            return Index.CompareTo(other.Index);
        }

        public override string ToString()
        {
            string val;
            if (this == Start)
            {
                val = "Start";
            }
            else if (this == End)
            {
                val = "End";
            }
            else
            {
                val = Index.ToString();
            }

            return $"(Index: {val})";
        }

        public static explicit operator ItemPosition(int index)
        {
            return new ItemPosition(index);
        }
    }
}
