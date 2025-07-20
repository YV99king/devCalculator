using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using NumberStyles = System.Globalization.NumberStyles;

namespace devCalculator
{
    public partial class MainForm : Form
    {
        #region topmost impl
        private static readonly IntPtr HWND_TOPMOST = new(-1);
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint TOPMOST_FLAGS = SWP_NOMOVE | SWP_NOSIZE;

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        private void MakeTopMost() =>
            SetWindowPos(this.Handle, HWND_TOPMOST, 0, 0, 0, 0, TOPMOST_FLAGS); // set window to top most
        #endregion

        private INumberHelper _calculator = new Calculator<ulong>();
        private object _modifyLock = new();

        public MainForm()
        {
            InitializeComponent();
        }

        private void OnChangedValue(ChangeSource source)
        {
            switch (_calculator)
            {
                case Calculator<byte> calc:
                    OnChangedValue(calc, source); break;
                case Calculator<sbyte> calc:
                    OnChangedValue(calc, source); break;
                case Calculator<short> calc:
                    OnChangedValue(calc, source); break;
                case Calculator<ushort> calc:
                    OnChangedValue(calc, source); break;
                case Calculator<int> calc:
                    OnChangedValue(calc, source); break;
                case Calculator<uint> calc:
                    OnChangedValue(calc, source); break;
                case Calculator<long> calc:
                    OnChangedValue(calc, source); break;
                case Calculator<ulong> calc:
                    OnChangedValue(calc, source); break;
                default:
                    Debug.Fail($"devCalculator.MainForm._calculator was thought to be a Calculator<T> where T is an internal integer type.");
                    throw new UnreachableException();
            }
        }
        private void OnChangedValue<T>(Calculator<T> calc, ChangeSource source)
            where T : struct, IBinaryInteger<T>
        {
            hexadecimalValue.Text = binaryValue.Text[..5] + calc.ToHexadecimalString().TrimStart('0');
            decimalValue.Text = decimalValue.Text[..5] + calc.ToDecimalString().TrimStart('0');
            octalValue.Text = octalValue.Text[..5] + calc.ToOctalString().TrimStart('0');
            binaryValue.Text = binaryValue.Text[..6] + GetBinStringFormatted(calc);
        }
        private static string GetBinStringFormatted<T>(Calculator<T> calc) where T : struct, IBinaryInteger<T>
        {
            var binValue = calc.ToBinaryString();
            int i;

            for (i = 0; i + 4 < binValue.Length; i += 4)
                if (binValue[i..(i + 4)] != "0000")
                    break;

            int parts = 1;
            StringBuilder sb = new(binValue[i..(i + 4)]);
            for (i += 4; i < binValue.Length; i += 4)
            {
                sb.Append([' ', .. binValue[i..(i + 4)]]);
                if (++parts == 8)
                    sb.Append("\n        ");
            }
            if (parts < 8)
                sb.Append('\n');

            return sb.ToString();
        }

        private static int ReadIndexFromName(Control control, int startIndex)
        {
            int index = 0;
            for (int i = startIndex; i < control.Name.Length; i++)
            {
                Debug.Assert(control.Name[i] is >= '0' and <= '9', $"invalid control name: {control.Name}");
                index *= 10;
                index += control.Name[i] - '0';
            }

            return index - 1; //fix indexing: from 1-based to 0-based
        }
        private unsafe ReadOnlySpan<byte> GetValueBytes() =>
            _calculator switch
            {
                Calculator<byte> calc =>   new ReadOnlySpan<byte>(ref calc._value),
                Calculator<sbyte> calc =>  new ReadOnlySpan<byte>(ref Unsafe.As<sbyte, byte>(ref calc._value)),
                Calculator<short> calc =>  new ReadOnlySpan<byte>(Unsafe.AsPointer(ref calc._value), sizeof(short)),
                Calculator<ushort> calc => new ReadOnlySpan<byte>(Unsafe.AsPointer(ref calc._value), sizeof(ushort)),
                Calculator<int> calc =>    new ReadOnlySpan<byte>(Unsafe.AsPointer(ref calc._value), sizeof(int)),
                Calculator<uint> calc =>   new ReadOnlySpan<byte>(Unsafe.AsPointer(ref calc._value), sizeof(uint)),
                Calculator<long> calc =>   new ReadOnlySpan<byte>(Unsafe.AsPointer(ref calc._value), sizeof(long)),
                Calculator<ulong> calc =>  new ReadOnlySpan<byte>(Unsafe.AsPointer(ref calc._value), sizeof(ulong)),
                _ => throw ThrowInvalidCalculator()
            };


        #region form events
        private void MainForm_Load(object sender, EventArgs e)
        {
            MakeTopMost();
        }

        private void OnBitClick(object sender, EventArgs e)
        {
            Debug.Assert(sender is Button { Name: ['b', 'i', 't', _, ..] }, $"invalid bit button name: {(sender as Button)?.Name}");

            Button button = (sender as Button)!;

            bool state = button.Text == "1";
            int index = ReadIndexFromName(button, 3);

            lock (_modifyLock)
            {
                state = !state;
                button.Text = state ? "1" : "0";
                _calculator[index] = state;
                OnChangedValue(ChangeSource.BitField);
            }
        }

        private void OnSizeChanged(object sender, EventArgs e)
        {
            bool isle = BitConverter.IsLittleEndian;
            lock (_modifyLock)
            {
                _calculator = (sizeInput.SelectedIndex, enableSign.Checked) switch
                {
                    (0, true) => new Calculator<sbyte>((sbyte)GetValueBytes()[isle ? 0 : ^1]),
                    (0, false) => new Calculator<byte>(GetValueBytes()[isle ? 0 : ^1]),
                    (1, true) => new Calculator<short>(BitConverter.ToInt16(ZeroExtend(GetValueBytes(), sizeof(short)))),
                    (1, false) => new Calculator<ushort>(BitConverter.ToUInt16(ZeroExtend(GetValueBytes(), sizeof(ushort)))),
                    (2, true) => new Calculator<int>(BitConverter.ToInt32(ZeroExtend(GetValueBytes(), sizeof(int)))),
                    (2, false) => new Calculator<uint>(BitConverter.ToUInt32(ZeroExtend(GetValueBytes(), sizeof(uint)))),
                    (3, true) => new Calculator<long>(BitConverter.ToInt64(ZeroExtend(GetValueBytes(), sizeof(long)))),
                    (3, false) => new Calculator<ulong>(BitConverter.ToUInt64(ZeroExtend(GetValueBytes(), sizeof(ulong)))),
                    _ => _calculator
                };
            }

            int bitGroupCount = (1 << sizeInput.SelectedIndex) * 2; // 0 => 2, 1 => 4, 2 => 8, 3 => 16
            foreach (GroupBox bitGroup in bitKeyboard.Controls)
            {
                Debug.Assert(bitGroup is GroupBox { Name: ['b', 'i', 't', 'G', 'r', 'o', 'u', 'p', _, ..] }, $"invalid bit button name: {(sender as GroupBox)?.Name}");
                var index = ReadIndexFromName(bitGroup, 8);

                foreach (Button button in bitGroup.Controls)
                    button.Enabled = index < bitGroupCount;
            }
            OnChangedValue(ChangeSource.None);

            static T[] ZeroExtend<T>(ReadOnlySpan<T> span, int toLength)
            {
                var output = new T[span.Length >= toLength ? span.Length : toLength];
                span.CopyTo(output);
                return output;
            }
        }

        private void inputValue_TextChanged(object sender, EventArgs e)
        {
            var numAsText = inputValue.Text;
            StringBuilder sb = new();
            for (int i = 0; i < numAsText.Length; i++)
            {
                
            }
            lock (_modifyLock)
            {
                var numStyle = NumberStyles.AllowBinarySpecifier;
                switch (_calculator)
                {
                    case Calculator<byte> calc:
                        calc.Value = byte.Parse(numAsText, numStyle); break;
                    case Calculator<sbyte> calc:
                        calc.Value = sbyte.Parse(numAsText, numStyle); break;
                    case Calculator<short> calc:
                        calc.Value = short.Parse(numAsText, numStyle); break;
                    case Calculator<ushort> calc:
                        calc.Value = ushort.Parse(numAsText, numStyle); break;
                    case Calculator<int> calc:
                        calc.Value = int.Parse(numAsText, numStyle); break;
                    case Calculator<uint> calc:
                        calc.Value = uint.Parse(numAsText, numStyle); break;
                    case Calculator<long> calc:
                        calc.Value = long.Parse(numAsText, numStyle); break;
                    case Calculator<ulong> calc:
                        calc.Value = ulong.Parse(numAsText, numStyle); break;
                    default:
                        break;
                }
                OnChangedValue(ChangeSource.KeyboardInput);
            }

            static T ParseText<T>(string text, out string formatted)
                where T : struct, IBinaryInteger<T>
            {

            }
        }

        #endregion

        [DoesNotReturn]
        private static Exception ThrowInvalidCalculator()
        {
            Debug.Fail($"devCalculator.MainForm._calculator was thought to be a Calculator<T> where T is an internal integer type.");
            throw new UnreachableException();
        }

        enum ChangeSource
        {
            None = 0,
            BitField = 1,
            KeyboardInput = 2
        }
        enum NumberFormat
        {
            Hex,
            Decimal,
            Octal,
            Binary
        }
    }
}
