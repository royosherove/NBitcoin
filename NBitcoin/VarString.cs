﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NBitcoin
{
	public class VarString : IBitcoinSerializable
	{
		public VarString()
		{

		}
		byte[] _Bytes = new byte[0];
		public int Length
		{
			get
			{
				return _Bytes.Length;
			}
		}
		public VarString(byte[] bytes)
		{
			if(bytes == null)
				throw new ArgumentNullException("bytes");
			_Bytes = bytes;
		}

		public byte[] GetString()
		{
			return _Bytes.ToArray();
		}
		#region IBitcoinSerializable Members

		public void ReadWrite(BitcoinStream stream)
		{
			 var len = new VarInt((ulong)_Bytes.Length);
			 stream.ReadWrite(ref len);
			if(!stream.Serializing)
				_Bytes = new byte[len.ToLong()];
			stream.ReadWrite(ref _Bytes);
		}

		#endregion
	}
}
