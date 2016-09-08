﻿#region License

// The MIT License
//
// Copyright (c) 2006-2008 DevDefined Limited.
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Security.Permissions;
using System.Text;

namespace DevDefined.OAuth.KeyInterop
{
	[Serializable]
	public sealed class BerDecodeException : Exception
	{
		public BerDecodeException()
		{
		}

		public BerDecodeException(string message)
			: base(message)
		{
		}

		public BerDecodeException(string message, Exception ex)
			: base(message, ex)
		{
		}

		public BerDecodeException(string message, int position)
			: base(message)
		{
			Position = position;
		}

		public BerDecodeException(string message, int position, Exception ex)
			: base(message, ex)
		{
			Position = position;
		}

		private BerDecodeException(SerializationInfo info, StreamingContext context)
			: base(info, context)
		{
			Position = info.GetInt32("Position");
		}

		public int Position { get; }

		public override string Message
		{
			get
			{
				var sb = new StringBuilder(base.Message);

				sb.AppendFormat(" (Position {0}){1}",
					Position, Environment.NewLine);

				return sb.ToString();
			}
		}

		[SecurityPermission(SecurityAction.Demand, SerializationFormatter = true)]
		public override void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			base.GetObjectData(info, context);
			info.AddValue("Position", Position);
		}
	}

	public class AsnKeyParser
	{
		private readonly AsnParser parser;

		public AsnKeyParser(byte[] contents)
		{
			parser = new AsnParser(contents);
		}

		public static byte[] TrimLeadingZero(byte[] values)
		{
			byte[] r;
			if ((0x00 == values[0]) && (values.Length > 1))
			{
				r = new byte[values.Length - 1];
				Array.Copy(values, 1, r, 0, values.Length - 1);
			}
			else
			{
				r = new byte[values.Length];
				Array.Copy(values, r, values.Length);
			}

			return r;
		}

		public static bool EqualOid(byte[] first, byte[] second)
		{
			if (first.Length != second.Length)
			{
				return false;
			}

			for (var i = 0; i < first.Length; i++)
			{
				if (first[i] != second[i])
				{
					return false;
				}
			}

			return true;
		}

		public RSAParameters ParseRSAPrivateKey()
		{
			var parameters = new RSAParameters();

			// Checkpoint
			var position = parser.CurrentPosition();


			// Sanity Check
			var length = parser.NextSequence();
			// Ignore Sequence - PrivateKeyInfo
			if (length != parser.RemainingBytes())
			{
				var sb = new StringBuilder("Incorrect Sequence Size. ");
				sb.AppendFormat("Specified: {0}, Remaining: {1}",
					length.ToString(CultureInfo.InvariantCulture),
					parser.RemainingBytes().ToString(CultureInfo.InvariantCulture));
				throw new BerDecodeException(sb.ToString(), position);
			}

			// Checkpoint
			position = parser.CurrentPosition();
			// Version
			var value = parser.NextInteger();
			if (0x00 != value[0])
			{
				var sb = new StringBuilder("Incorrect PrivateKeyInfo Version. ");
				var v = new BigInteger(value);
				sb.AppendFormat("Expected: 0, Specified: {0}", v.ToString());
				throw new BerDecodeException(sb.ToString(), position);
			}

			// Checkpoint
			position = parser.CurrentPosition();

			// Ignore Sequence - AlgorithmIdentifier
			length = parser.NextSequence();
			if (length > parser.RemainingBytes())
			{
				var sb = new StringBuilder("Incorrect AlgorithmIdentifier Size. ");
				sb.AppendFormat("Specified: {0}, Remaining: {1}",
					length.ToString(CultureInfo.InvariantCulture),
					parser.RemainingBytes().ToString(CultureInfo.InvariantCulture));
				throw new BerDecodeException(sb.ToString(), position);
			}

			// Checkpoint
			position = parser.CurrentPosition();

			// Grab the OID
			value = parser.NextOID();
			byte[] oid = { 0x2a, 0x86, 0x48, 0x86, 0xf7, 0x0d, 0x01, 0x01, 0x01 };
			if (!EqualOid(value, oid))
			{
				throw new BerDecodeException("Expected OID 1.2.840.113549.1.1.1", position);
			}

			// Optional Parameters
			if (parser.IsNextNull())
			{
				parser.NextNull();
			}
			else
			{
				// Gracefully skip the optional data
				parser.Next();
			}

			// Checkpoint
			position = parser.CurrentPosition();

			// Ignore OctetString - PrivateKey
			length = parser.NextOctetString();
			if (length > parser.RemainingBytes())
			{
				var sb = new StringBuilder("Incorrect PrivateKey Size. ");
				sb.AppendFormat("Specified: {0}, Remaining: {1}",
					length.ToString(CultureInfo.InvariantCulture),
					parser.RemainingBytes().ToString(CultureInfo.InvariantCulture));
				throw new BerDecodeException(sb.ToString(), position);
			}

			// Checkpoint
			position = parser.CurrentPosition();

			// Ignore Sequence - RSAPrivateKey
			length = parser.NextSequence();
			if (length < parser.RemainingBytes())
			{
				var sb = new StringBuilder("Incorrect RSAPrivateKey Size. ");
				sb.AppendFormat("Specified: {0}, Remaining: {1}",
					length.ToString(CultureInfo.InvariantCulture),
					parser.RemainingBytes().ToString(CultureInfo.InvariantCulture));
				throw new BerDecodeException(sb.ToString(), position);
			}

			// Checkpoint
			position = parser.CurrentPosition();
			// Version
			value = parser.NextInteger();
			if (0x00 != value[0])
			{
				var sb = new StringBuilder("Incorrect RSAPrivateKey Version. ");
				var v = new BigInteger(value);
				sb.AppendFormat("Expected: 0, Specified: {0}", v);
				throw new BerDecodeException(sb.ToString(), position);
			}

			parameters.Modulus = TrimLeadingZero(parser.NextInteger());
			parameters.Exponent = TrimLeadingZero(parser.NextInteger());
			parameters.D = TrimLeadingZero(parser.NextInteger());
			parameters.P = TrimLeadingZero(parser.NextInteger());
			parameters.Q = TrimLeadingZero(parser.NextInteger());
			parameters.DP = TrimLeadingZero(parser.NextInteger());
			parameters.DQ = TrimLeadingZero(parser.NextInteger());
			parameters.InverseQ = TrimLeadingZero(parser.NextInteger());

			Debug.Assert(0 == parser.RemainingBytes());

			return parameters;
		}

	}

	internal class AsnParser
	{
		private readonly int _initialCount;
		private readonly List<byte> _octets;

		public AsnParser(byte[] values)
		{
			_octets = new List<byte>(values.Length);
			_octets.AddRange(values);

			_initialCount = _octets.Count;
		}

		public int CurrentPosition()
		{
			return _initialCount - _octets.Count;
		}

		public int RemainingBytes()
		{
			return _octets.Count;
		}

		private int GetLength()
		{
			var length = 0;

			// Checkpoint
			var position = CurrentPosition();

			try
			{
				var b = GetNextOctet();

				if (b == (b & 0x7f))
				{
					return b;
				}
				var i = b & 0x7f;

				if (i > 4)
				{
					var sb = new StringBuilder("Invalid Length Encoding. ");
					sb.AppendFormat("Length uses {0} octets",
						i.ToString(CultureInfo.InvariantCulture));
					throw new BerDecodeException(sb.ToString(), position);
				}

				while (0 != i--)
				{
					// shift left
					length <<= 8;

					length |= GetNextOctet();
				}
			}
			catch (ArgumentOutOfRangeException ex)
			{
				throw new BerDecodeException("Error Parsing Key", position, ex);
			}

			return length;
		}

		public byte[] Next()
		{
			var position = CurrentPosition();

			try
			{
				GetNextOctet();

				var length = GetLength();
				if (length > RemainingBytes())
				{
					var sb = new StringBuilder("Incorrect Size. ");
					sb.AppendFormat("Specified: {0}, Remaining: {1}",
						length.ToString(CultureInfo.InvariantCulture),
						RemainingBytes().ToString(CultureInfo.InvariantCulture));
					throw new BerDecodeException(sb.ToString(), position);
				}

				return GetOctets(length);
			}

			catch (ArgumentOutOfRangeException ex)
			{
				throw new BerDecodeException("Error Parsing Key", position, ex);
			}
		}

		public byte GetNextOctet()
		{
			var position = CurrentPosition();

			if (0 == RemainingBytes())
			{
				var sb = new StringBuilder("Incorrect Size. ");
				sb.AppendFormat("Specified: {0}, Remaining: {1}",
					1.ToString(CultureInfo.InvariantCulture),
					RemainingBytes().ToString(CultureInfo.InvariantCulture));
				throw new BerDecodeException(sb.ToString(), position);
			}

			var b = GetOctets(1)[0];

			return b;
		}

		public byte[] GetOctets(int octetCount)
		{
			var position = CurrentPosition();

			if (octetCount > RemainingBytes())
			{
				var sb = new StringBuilder("Incorrect Size. ");
				sb.AppendFormat("Specified: {0}, Remaining: {1}",
					octetCount.ToString(CultureInfo.InvariantCulture),
					RemainingBytes().ToString(CultureInfo.InvariantCulture));
				throw new BerDecodeException(sb.ToString(), position);
			}

			var values = new byte[octetCount];

			try
			{
				_octets.CopyTo(0, values, 0, octetCount);
				_octets.RemoveRange(0, octetCount);
			}

			catch (ArgumentOutOfRangeException ex)
			{
				throw new BerDecodeException("Error Parsing Key", position, ex);
			}

			return values;
		}

		public bool IsNextNull()
		{
			return 0x05 == _octets[0];
		}

		public int NextNull()
		{
			var position = CurrentPosition();

			try
			{
				var b = GetNextOctet();
				if (0x05 != b)
				{
					var sb = new StringBuilder("Expected Null. ");
					sb.AppendFormat("Specified Identifier: {0}", b.ToString(CultureInfo.InvariantCulture));
					throw new BerDecodeException(sb.ToString(), position);
				}

				// Next octet must be 0
				b = GetNextOctet();
				if (0x00 != b)
				{
					var sb = new StringBuilder("Null has non-zero size. ");
					sb.AppendFormat("Size: {0}", b.ToString(CultureInfo.InvariantCulture));
					throw new BerDecodeException(sb.ToString(), position);
				}

				return 0;
			}

			catch (ArgumentOutOfRangeException ex)
			{
				throw new BerDecodeException("Error Parsing Key", position, ex);
			}
		}

		public bool IsNextSequence()
		{
			return 0x30 == _octets[0];
		}

		public int NextSequence()
		{
			var position = CurrentPosition();

			try
			{
				var b = GetNextOctet();
				if (0x30 != b)
				{
					var sb = new StringBuilder("Expected Sequence. ");
					sb.AppendFormat("Specified Identifier: {0}",
						b.ToString(CultureInfo.InvariantCulture));
					throw new BerDecodeException(sb.ToString(), position);
				}

				var length = GetLength();
				if (length > RemainingBytes())
				{
					var sb = new StringBuilder("Incorrect Sequence Size. ");
					sb.AppendFormat("Specified: {0}, Remaining: {1}",
						length.ToString(CultureInfo.InvariantCulture),
						RemainingBytes().ToString(CultureInfo.InvariantCulture));
					throw new BerDecodeException(sb.ToString(), position);
				}

				return length;
			}

			catch (ArgumentOutOfRangeException ex)
			{
				throw new BerDecodeException("Error Parsing Key", position, ex);
			}
		}

		public int NextOctetString()
		{
			var position = CurrentPosition();

			try
			{
				var b = GetNextOctet();
				if (0x04 != b)
				{
					var sb = new StringBuilder("Expected Octet String. ");
					sb.AppendFormat("Specified Identifier: {0}", b.ToString(CultureInfo.InvariantCulture));
					throw new BerDecodeException(sb.ToString(), position);
				}

				var length = GetLength();
				if (length > RemainingBytes())
				{
					var sb = new StringBuilder("Incorrect Octet String Size. ");
					sb.AppendFormat("Specified: {0}, Remaining: {1}",
						length.ToString(CultureInfo.InvariantCulture),
						RemainingBytes().ToString(CultureInfo.InvariantCulture));
					throw new BerDecodeException(sb.ToString(), position);
				}

				return length;
			}

			catch (ArgumentOutOfRangeException ex)
			{
				throw new BerDecodeException("Error Parsing Key", position, ex);
			}
		}

		public byte[] NextInteger()
		{
			var position = CurrentPosition();

			try
			{
				var b = GetNextOctet();
				if (0x02 != b)
				{
					var sb = new StringBuilder("Expected Integer. ");
					sb.AppendFormat("Specified Identifier: {0}", b.ToString(CultureInfo.InvariantCulture));
					throw new BerDecodeException(sb.ToString(), position);
				}

				var length = GetLength();
				if (length > RemainingBytes())
				{
					var sb = new StringBuilder("Incorrect Integer Size. ");
					sb.AppendFormat("Specified: {0}, Remaining: {1}",
						length.ToString(CultureInfo.InvariantCulture),
						RemainingBytes().ToString(CultureInfo.InvariantCulture));
					throw new BerDecodeException(sb.ToString(), position);
				}

				return GetOctets(length);
			}

			catch (ArgumentOutOfRangeException ex)
			{
				throw new BerDecodeException("Error Parsing Key", position, ex);
			}
		}

		public byte[] NextOID()
		{
			var position = CurrentPosition();

			try
			{
				var b = GetNextOctet();
				if (0x06 != b)
				{
					var sb = new StringBuilder("Expected Object Identifier. ");
					sb.AppendFormat("Specified Identifier: {0}",
						b.ToString(CultureInfo.InvariantCulture));
					throw new BerDecodeException(sb.ToString(), position);
				}

				var length = GetLength();
				if (length > RemainingBytes())
				{
					var sb = new StringBuilder("Incorrect Object Identifier Size. ");
					sb.AppendFormat("Specified: {0}, Remaining: {1}",
						length.ToString(CultureInfo.InvariantCulture),
						RemainingBytes().ToString(CultureInfo.InvariantCulture));
					throw new BerDecodeException(sb.ToString(), position);
				}

				var values = new byte[length];

				for (var i = 0; i < length; i++)
				{
					values[i] = _octets[0];
					_octets.RemoveAt(0);
				}

				return values;
			}

			catch (ArgumentOutOfRangeException ex)
			{
				throw new BerDecodeException("Error Parsing Key", position, ex);
			}
		}
	}
}