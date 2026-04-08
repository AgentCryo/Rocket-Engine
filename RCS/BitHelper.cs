namespace RCS;

public static class BitHelper
{
    /// <summary>
    /// Write a single bit to 0 or 1.
    /// </summary>
    public static uint WriteBit(uint value, int bitIndex, bool bitValue)
    {
        return (value & ~(1u << bitIndex)) | ((bitValue ? 1u : 0u) << bitIndex);
    }

    /// <summary>
    /// Set a bit to 1.
    /// </summary>
    public static uint SetBit(uint value, int bitIndex)
    {
        return value | (1u << bitIndex);
    }

    /// <summary>
    /// Clear a bit to 0.
    /// </summary>
    public static uint ClearBit(uint value, int bitIndex)
    {
        return value & ~(1u << bitIndex);
    }

    /// <summary>
    /// Toggle (flip) a bit.
    /// </summary>
    public static uint ToggleBit(uint value, int bitIndex)
    {
        return value ^ (1u << bitIndex);
    }

    /// <summary>
    /// Check whether a bit is set (returns true if bit == 1).
    /// </summary>
    public static bool IsBitSet(uint value, int bitIndex)
    {
        return (value & (1u << bitIndex)) != 0;
    }

    /// <summary>
    /// Extract a bit as 0 or 1.
    /// </summary>
    public static uint ReadBit(uint value, int bitIndex)
    {
        return (value >> bitIndex) & 1u;
    }

    /// <summary>
    /// Write a range of bits (bitfield) into a value.
    /// </summary>
    public static uint WriteBits(uint value, int offset, int count, uint fieldValue)
    {
        uint mask = ((1u << count) - 1u) << offset;
        return (value & ~mask) | ((fieldValue << offset) & mask);
    }

    /// <summary>
    /// Read a range of bits (bitfield).
    /// </summary>
    public static uint ReadBits(uint value, int offset, int count)
    {
        uint mask = (1u << count) - 1u;
        return (value >> offset) & mask;
    }
    
    /// <summary>
    /// Write a single bit to 0 or 1.
    /// </summary>
    public static void WriteBit(ref uint value, int bitIndex, bool bitValue)
    {
        value = (value & ~(1u << bitIndex)) | ((bitValue ? 1u : 0u) << bitIndex);
    }

    /// <summary>
    /// Set a bit to 1.
    /// </summary>
    public static void SetBit(ref uint value, int bitIndex)
    {
        value |= (1u << bitIndex);
    }

    /// <summary>
    /// Clear a bit to 0.
    /// </summary>
    public static void ClearBit(ref uint value, int bitIndex)
    {
        value &= ~(1u << bitIndex);
    }

    /// <summary>
    /// Toggle (flip) a bit.
    /// </summary>
    public static void ToggleBit(ref uint value, int bitIndex)
    {
        value ^= (1u << bitIndex);
    }

    /// <summary>
    /// Extract a bit as 0 or 1.
    /// </summary>
    public static void ReadBit(ref uint value, int bitIndex)
    {
        value = (value >> bitIndex) & 1u;
    }

    /// <summary>
    /// Write a range of bits (bitfield) into a value.
    /// </summary>
    public static void WriteBits(ref uint value, int offset, int count, uint fieldValue)
    {
        uint mask = ((1u << count) - 1u) << offset;
        value = (value & ~mask) | ((fieldValue << offset) & mask);
    }

    /// <summary>
    /// Read a range of bits (bitfield).
    /// </summary>
    public static void ReadBits(ref uint value, int offset, int count)
    {
        uint mask = (1u << count) - 1u;
        value = (value >> offset) & mask;
    }
}