using System.Numerics;
using System.Text;

namespace devCalculator;

public interface INumberHelper
{
    bool this[int index] { get; set; }

    string ToHexadecimalString();
    string ToDecimalString();
    string ToOctalString();
    string ToBinaryString();
}

public interface ICalculator<T> : INumberHelper
    where T : struct, IBinaryInteger<T>
{
    T Value { get; set; }

    void Add(T other);
    void And(T other);
    void Divide(T other);
    void LeftShift(T count);
    void Modulus(T other);
    void Multiply(T other);
    void Negate();
    void Not();
    void Or(T other);
    void RightShift(T count);
    void Subtract(T other);
    void Xor(T other);
}

public class Calculator<T> : ICalculator<T> where T : struct, IBinaryInteger<T>
{
    internal T _value;

    public Calculator() { }
    public Calculator(T value) =>
        _value = value;


    public bool this[int index]
    {
        get
        {
            T mask = T.One << index;
            return (Value & mask) != T.Zero;
        }
        set
        {
            T bitToSet = T.One << index;
            if (value)
                Value |= bitToSet;
            else
                Value &= ~bitToSet;
        }
    }
    public T Value { get => _value; set => _value = value; }

    public void Add(T other) =>
        Value += other;
    public void Subtract(T other) =>
        Value -= other;
    public void Multiply(T other) =>
        Value *= other;
    public void Divide(T other) =>
        Value /= other;
    public void Modulus(T other) =>
        Value %= other;

    public void Negate() =>
        Value = -Value;

    public void LeftShift(T count) =>
        Value <<= GetAsInt(count);
    public void RightShift(T count) =>
        Value >>>= GetAsInt(count);

    public void Not() =>
        Value = ~Value;
    public void And(T other) =>
        Value &= other;
    public void Or(T other) =>
        Value |= other;
    public void Xor(T other) =>
        Value ^= other;

    public int GetBitValue(int index) =>
        this[index] ? 1 : 0;

    internal static int GetAsInt(T value) =>
        int.CreateTruncating(value);

    public string ToHexadecimalString()
    {
        Span<byte> bytes = stackalloc byte[Value.GetByteCount()];
        Value.WriteBigEndian(bytes);

        StringBuilder builder = new(bytes.Length * 2);
        foreach (var value in bytes)
        {
            int part1 = value / 16;
            if (part1 < 10)
                builder.Append(part1);
            else
                builder.Append((char)('A' + part1 - 10));

            int part2 = value % 16;
            if (part2 < 10)
                builder.Append(part2);
            else
                builder.Append((char)('A' + part2 - 10));
        }

        return builder.ToString();
    }
    public string ToDecimalString() =>
        Value.ToString(null, null);
    public string ToOctalString()
    {
        StringBuilder builder = new(Value.GetByteCount() * 8 / 3);
        int bitCount = Value.GetByteCount() * 8;

        if (bitCount % 3 == 2)
        	builder.Append(2 * GetBitValue(bitCount - 1)
                             + GetBitValue(bitCount - 2));
        else if (bitCount % 3 == 1)
        	builder.Append(GetBitValue(bitCount - 1));

        for (int i = bitCount - (bitCount % 3) - 1; i >= 0; i -= 3)
        {
            int nextDigit = 4 * GetBitValue(i)
                          + 2 * GetBitValue(i - 1)
                              + GetBitValue(i - 2);
            builder.Append(nextDigit);
        }
        return builder.ToString();
    }
    public string ToBinaryString()
    {
        StringBuilder builder = new(Value.GetByteCount());

        for (int i = Value.GetByteCount() * 8 - 1; i >= 0; i--)
            builder.Append(this[i] ? '1' : '0');

        return builder.ToString();
    }
}