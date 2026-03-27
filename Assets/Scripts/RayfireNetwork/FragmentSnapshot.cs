using Unity.Mathematics;
using Unity.Netcode;
using UnityEngine;

public struct FragmentSnapshot : INetworkSerializable
{
    public int Id;
    public Vector3 Position;
    public Quaternion Rotation;

    public FragmentSnapshot(int id, Vector3 position, Quaternion rotation)
    {
        Id = id;
        Position = position;
        Rotation = rotation;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Id);

        // Half-precision position (6 bytes instead of 12)
        if (serializer.IsWriter)
        {
            var writer = serializer.GetFastBufferWriter();
            writer.WriteValueSafe(FloatToHalf(SanitizeFloat(Position.x)));
            writer.WriteValueSafe(FloatToHalf(SanitizeFloat(Position.y)));
            writer.WriteValueSafe(FloatToHalf(SanitizeFloat(Position.z)));
        }
        else
        {
            var reader = serializer.GetFastBufferReader();
            reader.ReadValueSafe(out ushort hx);
            reader.ReadValueSafe(out ushort hy);
            reader.ReadValueSafe(out ushort hz);
            float fx = HalfToFloat(hx);
            float fy = HalfToFloat(hy);
            float fz = HalfToFloat(hz);
            // Guard against NaN from half-precision overflow/underflow
            Position = new Vector3(
                SanitizeFloat(fx),
                SanitizeFloat(fy),
                SanitizeFloat(fz));
        }

        // Smallest-three quaternion compression (7 bytes instead of 16)
        if (serializer.IsWriter)
        {
            var writer = serializer.GetFastBufferWriter();
            WriteCompressedQuaternion(writer, Rotation);
        }
        else
        {
            var reader = serializer.GetFastBufferReader();
            Rotation = ReadCompressedQuaternion(reader);
        }
    }

    /// <summary>
    /// Clamps to half-precision range and replaces NaN/Infinity with 0.
    /// </summary>
    private static float SanitizeFloat(float v)
    {
        if (float.IsNaN(v) || float.IsInfinity(v)) return 0f;
        // half-precision max is ~65504
        return Mathf.Clamp(v, -65500f, 65500f);
    }

    private static void WriteCompressedQuaternion(FastBufferWriter writer, Quaternion q)
    {
        float ax = Mathf.Abs(q.x), ay = Mathf.Abs(q.y), az = Mathf.Abs(q.z), aw = Mathf.Abs(q.w);
        byte largest = 0;
        float largestVal = ax;
        if (ay > largestVal) { largest = 1; largestVal = ay; }
        if (az > largestVal) { largest = 2; largestVal = az; }
        if (aw > largestVal) { largest = 3; }

        float sign = largest switch
        {
            0 => Mathf.Sign(q.x),
            1 => Mathf.Sign(q.y),
            2 => Mathf.Sign(q.z),
            _ => Mathf.Sign(q.w)
        };
        if (sign < 0) { q.x = -q.x; q.y = -q.y; q.z = -q.z; q.w = -q.w; }

        float a, b, c;
        switch (largest)
        {
            case 0: a = q.y; b = q.z; c = q.w; break;
            case 1: a = q.x; b = q.z; c = q.w; break;
            case 2: a = q.x; b = q.y; c = q.w; break;
            default: a = q.x; b = q.y; c = q.z; break;
        }

        writer.WriteValueSafe(largest);
        writer.WriteValueSafe(FloatToHalf(a));
        writer.WriteValueSafe(FloatToHalf(b));
        writer.WriteValueSafe(FloatToHalf(c));
    }

    private static Quaternion ReadCompressedQuaternion(FastBufferReader reader)
    {
        reader.ReadValueSafe(out byte largest);
        reader.ReadValueSafe(out ushort ha);
        reader.ReadValueSafe(out ushort hb);
        reader.ReadValueSafe(out ushort hc);
        float a = HalfToFloat(ha);
        float b = HalfToFloat(hb);
        float c = HalfToFloat(hc);

        // Guard against NaN
        if (float.IsNaN(a)) a = 0f;
        if (float.IsNaN(b)) b = 0f;
        if (float.IsNaN(c)) c = 0f;

        float missing = Mathf.Sqrt(Mathf.Max(0f, 1f - a * a - b * b - c * c));

        return largest switch
        {
            0 => new Quaternion(missing, a, b, c),
            1 => new Quaternion(a, missing, b, c),
            2 => new Quaternion(a, b, missing, c),
            _ => new Quaternion(a, b, c, missing)
        };
    }

    private static ushort FloatToHalf(float value) => (ushort)math.f32tof16(value);
    private static float HalfToFloat(ushort value) => math.f16tof32(value);
}
