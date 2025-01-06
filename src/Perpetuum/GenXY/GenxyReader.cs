using Perpetuum.Threading;
using Perpetuum.Zones;
using System.Drawing;
using System.Globalization;
using System.Text;

namespace Perpetuum.GenXY
{
    public class GenxyReader : Disposable
    {
        private readonly TextReader _reader;

        private static readonly NumberFormatInfo _numberFormatInfo;

        static GenxyReader()
        {
            _numberFormatInfo = (NumberFormatInfo)CultureInfo.InstalledUICulture.NumberFormat.Clone();
            _numberFormatInfo.NumberDecimalSeparator = ".";
        }

        public GenxyReader(TextReader reader)
        {
            _reader = reader;
        }

        private char _currentChar;

        private readonly char[] _charBuffer = new char[1024];
        private int _charReaded;
        private int _charPos;

        private bool Read()
        {
            if (_charPos >= _charReaded)
            {
                _charReaded = _reader.Read(_charBuffer, 0, _charBuffer.Length);
                if (_charReaded == 0)
                {
                    return false;
                }

                _charPos = 0;
            }

            _currentChar = _charBuffer[_charPos];
            return true;
        }

        private string ReadKey()
        {
            StringBuilder sb = new();

            while (Read())
            {
                if (_currentChar is '#' or '|')
                {
                    _charPos++;
                    continue;
                }

                if (_currentChar == '=')
                {
                    break;
                }

                if (!char.IsWhiteSpace(_currentChar))
                {
                    sb.Append(_currentChar);
                }

                _charPos++;
            }

            return sb.ToString();
        }

        public object? ReadValue()
        {
            while (Read())
            {
                if (char.IsWhiteSpace(_currentChar) || _currentChar == '=')
                {
                    _charPos++;
                    continue;
                }

                GenxyToken token = (GenxyToken)_currentChar;
                _charPos++;

                switch (token)
                {
                    case GenxyToken.String:
                        return ReadEscapedString();
                    case GenxyToken.StringArray:
                        return ReadEscapedStringArray();
                    case GenxyToken.Integer:
                        return ReadInt();
                    case GenxyToken.IntegerArray:
                        return ReadIntArray();
                    case GenxyToken.Long:
                        return ReadLong();
                    case GenxyToken.LongArray:
                        return ReadLongArray();
                    case GenxyToken.Decimal:
                        return ReadDecimal();
                    case GenxyToken.DecimalArray:
                        return ReadDecimalArray();
                    case GenxyToken.ByteArray:
                        return ReadByteArray();
                    case GenxyToken.DecimalLong:
                        return ReadDecimalLong();
                    case GenxyToken.Float:
                        return ReadFloat();
                    case GenxyToken.FloatBytes:
                        return ReadFloatBytes();
                    case GenxyToken.DoubleBytes:
                        return ReadDoubleBytes();
                    case GenxyToken.Date:
                        return ReadDate();
                    case GenxyToken.Color:
                        return ReadColor();
                    case GenxyToken.Area:
                        return ReadArea();
                    case GenxyToken.AreaArray:
                        return ReadAreaArray();
                    case GenxyToken.Point:
                        return ReadPoint();
                    case GenxyToken.Position:
                        return ReadPosition();
                    case GenxyToken.PositionArray:
                        return ReadPositionArray();
                    case GenxyToken.StartProp:
                        {
                            return ReadDictionary();
                        }
                }

                string v = ReadValueAsString();
                return v;
            }

            return null;
        }

        private string ReadEscapedString()
        {
            string v = ReadValueAsString();
            return ParseEscapedString(v);
        }

        private string[] ReadEscapedStringArray()
        {
            return ReadValueAsArray(ParseEscapedString);
        }

        private int ReadInt()
        {
            string valueString = ReadValueAsString();
            return ParseInt(valueString);
        }

        private int[] ReadIntArray()
        {
            return ReadValueAsArray(ParseInt);
        }

        private byte[] ReadByteArray()
        {
            string v = ReadValueAsString();

            int len = v.Length / 2;
            byte[] r = new byte[len];

            for (int i = 0; i < len; i++)
            {
                r[i] = ParseHexNumber(v.Substring(i * 2, 2), (s, num) => byte.Parse(num, NumberStyles.HexNumber, null));
            }

            return r;
        }

        private long ReadLong()
        {
            string v = ReadValueAsString();
            return ParseLong(v);
        }

        private long[] ReadLongArray()
        {
            return ReadValueAsArray(ParseLong);
        }

        private int ReadDecimal()
        {
            string v = ReadValueAsString();
            return int.Parse(v);
        }

        private int[] ReadDecimalArray()
        {
            return ReadValueAsArray(int.Parse);
        }

        private long ReadDecimalLong()
        {
            string v = ReadValueAsString();
            return long.Parse(v);
        }

        private double ReadFloat()
        {
            string v = ReadValueAsString();
            return double.Parse(v, _numberFormatInfo);
        }

        private double ReadFloatBytes()
        {
            string v = ReadValueAsString();

            byte[] b = new byte[4];
            for (int i = 0; i < 4; i++)
            {
                b[i] = Convert.ToByte(v.Substring(i * 2, 2), 16);
            }

            return BitConverter.ToSingle(b, 0);
        }

        private double ReadDoubleBytes()
        {
            string v = ReadValueAsString();

            byte[] b = new byte[8];
            for (int i = 0; i < 8; i++)
            {
                b[i] = Convert.ToByte(v.Substring(i * 2, 2), 16);
            }

            return BitConverter.ToDouble(b, 0);
        }

        private DateTime ReadDate()
        {
            int[] n = ReadValueAsArray(int.Parse, '.');
            return new DateTime(n[0], n[1], n[2], n[3], n[4], n[5]);
        }

        private Color ReadColor()
        {
            int[] n = ReadValueAsArray(ParseInt, '.');
            return Color.FromArgb(n[3], n[0], n[1], n[2]);
        }

        private Area ReadArea()
        {
            string v = ReadValueAsString();
            return ParseArea(v);
        }

        private Area[] ReadAreaArray()
        {
            return ReadValueAsArray(ParseArea);
        }

        private Point ReadPoint()
        {
            int[] n = ReadValueAsArray(ParseInt, '.');
            return new Point(n[0], n[1]);
        }

        private Position ReadPosition()
        {
            string v = ReadValueAsString();
            return ParsePosition(v);
        }

        private Position[] ReadPositionArray()
        {
            return ReadValueAsArray(ParsePosition);
        }


        public Dictionary<string, object> ReadDictionary()
        {
            Dictionary<string, object> d = new();

            while (Read())
            {
                if (char.IsWhiteSpace(_currentChar) || _currentChar == '[')
                {
                    _charPos++;
                    continue;
                }

                if (_currentChar == ']')
                {
                    _charPos++;
                    break;
                }

                string key = ReadKey();
                object value = ReadValue();
                d[key] = value;
            }

            return d;
        }


        private static Position ParsePosition(string positionString)
        {
            int[] n = ParseValueAsArray(positionString, ParseInt, '.');
            return new Position(n[0], n[1], n[2]);
        }

        private static Area ParseArea(string areaString)
        {
            int[] n = ParseValueAsArray(areaString, ParseInt, '.');
            return new Area(n[0], n[1], n[2], n[3]);
        }

        private static T ParseHexNumber<T>(string hexNumber, Func<int /* sign */, string, T> parser)
        {
            int sign = 1;
            if (hexNumber[0] == '-')
            {
                hexNumber = hexNumber[1..];
                sign = -1;
            }

            return parser(sign, hexNumber);
        }

        private static int ParseInt(string valueString)
        {
            return ParseHexNumber(valueString, (s, num) => int.Parse(num, NumberStyles.HexNumber, null) * s);
        }

        private static long ParseLong(string longString)
        {
            return ParseHexNumber(longString, (s, num) => long.Parse(num, NumberStyles.HexNumber, null) * s);
        }

        private static string ParseEscapedString(string s)
        {
            StringBuilder result = new();

            static int Decode(char d)
            {
                return (d & 0xf) + ((d & 0x40) >> 3) + ((d & 0x40) >> 6);
            }

            int i = 0;
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '\\')
                {
                    int hc = Decode(s[i++]);
                    int lc = Decode(s[i++]);
                    c = (char)((hc << 4) | lc);
                }
                result.Append(c);
            }

            return result.ToString();
        }

        private T[] ReadValueAsArray<T>(Func<string, T> valueAction, char separator = ',')
        {
            string v = ReadValueAsString();
            return ParseValueAsArray(v, valueAction, separator);
        }

        private static T[] ParseValueAsArray<T>(string value, Func<string, T> valueAction, char separator)
        {
            if (string.IsNullOrEmpty(value))
            {
                return new T[0];
            }

            string[] stringArray = value.Split(separator);
            T[] result = new T[stringArray.Length];
            for (int i = 0; i < stringArray.Length; i++)
            {
                result[i] = valueAction(stringArray[i]);
            }
            return result;
        }

        private string ReadValueAsString()
        {
            StringBuilder sb = new();
            while (Read())
            {
                if (_currentChar is '#' or
                    '|' or
                    ']')
                {
                    break;
                }

                sb.Append(_currentChar);
                _charPos++;
            }

            return sb.ToString();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _reader.Dispose();
            }
        }
    }
}