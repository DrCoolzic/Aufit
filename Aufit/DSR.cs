/*!
@file dsr.cs
 * 
<summary>DSR Class: implements a shift register equivalent to the one used in the WD1772 FDC</summary>

<div class="jlg">Copyright (C) 2013-2014 Jean Louis-Guerin\n\n
This file is part of the Atari Universal FD Image Tool project.\n\n
The Atari Universal FD Image Tool project may be used and distributed without restriction provided
that this copyright statement is not removed from the file and that any
derivative work contains the original copyright notice and the associated
disclaimer.\n\n
The Atari Universal FD Image Tool project is free software; you can redistribute it
and/or modify  it under the terms of the GNU General Public License
as published by the Free Software Foundation; either version 3
of the License, or (at your option) any later version.\n\n
The Atari Universal FD Image Tool project is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.\n
See the GNU General Public License for more details.\n\n
You should have received a copy of the GNU General Public License
along with the Atari Universal FD Image Tool project; if not, write to the Free Software
Foundation, Inc., 51 Franklin St, Fifth Floor, Boston, MA  02110-1301  USA\n</div>

@author Jean Louis-Guerin
*/

using System.Diagnostics;

namespace WD1772Package {

/// <summary>Implement a DSR (Data Shift Register of 32 bits) to store incoming bits</summary>
///
/// <remarks>Something special about this DSR is that it can store a large number of null bits.
/// This is necessary for the long long spacing protection that convert to a large
/// number of bytes without data and clock bits!</remarks>
public class DSR {
	/// <summary>content of the 32 bits data shift register (clock+data)</summary>
	private int _content;
	/// <summary>number of accumulated bits in the DSR</summary>
	private int _bitsInDSR;
	/// <summary>decoded data (8 bits) after call to extract()</summary>
	private byte _decodedData;
	/// <summary>decoded clock (8 bits) after call to extract()</summary>
	private byte _decodedClock;
	/// <summary>Combined clock & data after call to extract()</summary>
	private ushort _mfmData;
	/// <summary>True when synchronization found in DSR</summary>
	private bool _syncFound;
	/// <summary>indicates number of bits to shift when sync found</summary> 
	private int _shiftToSync;
	private enum ByteType { a1, c2, data };
	private ByteType _lastByteType;
	private ByteType _beforeLast;


	/// <summary>Returned the decoded clock byte from the mfm data. Call extract() before</summary>
	public byte Clock {
		get { return _decodedClock; }
	}

	/// <summary>
	/// Returned the decoded data byte from the mfm data. Call extract() before.
	/// </summary>
	public byte Data {
		get { return _decodedData; }
	}

	/// <summary>
	/// Return the MFM data. Call extract() before.
	/// </summary>
	public ushort MFM {
		get { return _mfmData; }
	}

	/// <summary>Sync has been found </summary>
	/// <remarks>This property is set to true after calling searchSynch() if a sync byte has been found.
	/// It is also possible to force the flag to false if you do not want the shifting to be
	/// done by extract() ...</remarks>
	///
	public bool SyncFound {
		get { return _syncFound; }
		// set { _syncFound = value; }
	}


	/// <summary>The DSR Class Constructor</summary>
	/// We just reset variables
	public DSR() {
		_bitsInDSR = 0;
		_content = 0;
		_decodedClock = 0;
		_decodedData = 0;
		_mfmData = 0;
		_syncFound = false;
		_lastByteType = ByteType.data;
		_beforeLast = ByteType.data;
	}

	/// <summary>
	/// Reset the sync flag of the DSR
	/// </summary>
	public void resetSync() {
		_syncFound = false;
	}

	/// <summary>Shift by n bits the DSR</summary>
	/// <param name="nbits">the number of bits to shift in</param>
	///
	/// This procedure shift by n bits the content of the DSR then puts a logical one at the last position
	/// Internally it also increments the number of bits in the DSR.\n
	/// Due to huge flux transition (e.g. several ms! in Turrican) the number of bits to shift-in can be 
	/// larger than 16 bits. 
	public void shiftBitsIn(int nbits) {
		if (nbits > 16)
			// if n bits over 16 we complete with 0 the DSR
			_content = _content << (16 - _bitsInDSR);
		else
			// we shift by nbits and set the lsb
			_content = (_content << nbits) | 1;

		_bitsInDSR += nbits;
	}

	/// <summary>Shift one byte (8 bits) in the DSR</summary>
	/// <param name="val">byte to shift in</param>
	///
	/// This procedure shift by 8 bits the content of the DSR ored with the provided byte
	/// Internally it also increments the number of bits in the DSR by 8.
	public void shiftByteIn(byte val)
	{
		_content <<= 8;
		_content += val;
		_bitsInDSR += 8;
	}


	/// <summary>Extract the content of the DSR</summary>
	///
	/// This function should be called after searchSynch() function (if used) and before getting Data or
	/// Clock. The function takes care of shifting out by the right amount if a sync character has been found.
	/// If no sync character has been found then the leading 16 bits are used.\n
	/// As explained in shiftBitsIn() many "null bytes" can be pseudo shifted in the DSR. In this case each call
	/// return a null data byte ...
	public void extract() {
		int local;

		if (_syncFound)
			_bitsInDSR = _shiftToSync;
		else
			_bitsInDSR -= 16;

		if (_bitsInDSR > 16) {
			// n_bits > 16 we extract only the last 16 bits
			local = (_content >> 16) & 0xFFFF;
			if (_bitsInDSR > 32)
				// n_bits over 32 we set value to 0
				_content = 0;
			else
				// n_ bit between 16 & 32 set last bit
				_content = 1;
		}	// more than 16 bits
		else
			local = (_content >> (int)_bitsInDSR) & 0xFFFF;

		_mfmData = (ushort)local;
		_beforeLast = _lastByteType;
		_lastByteType = (_mfmData == 0x4489) ? ByteType.a1 :
						(_mfmData == 0x5224) ? ByteType.c2 : 
						ByteType.data;

		// extract clock and data
		for (int i = 0; i < 8; i++) {
			_decodedClock = (byte)((_decodedClock << 1) | (((local & 0x8000) != 0) ? 1 : 0));
			local = (local << 1);
			_decodedData = (byte)((_decodedData << 1) | (((local & 0x8000) != 0) ? 1 : 0));
			local = (local << 1);
		}
	}

	/// <summary>Search for a sync character in the DSR</summary>
	/// <returns>The number of bit to shift if a sync character has been found; otherwise return 0</returns>
	/// 
	/// Look in the DSR for a sync character (0x4489 or 0x5224). If a sync char is found it stores the
	/// necessary information so that extract() will do the right thing. It is possible to know that a
	/// sync character has been found by calling the sync() function. As mentioned the return value is
	/// only meaningful if a sync char has been found. 
	public int searchSync() {
	int SYNC1 = 0x4489;
	int SYNC2 = 0x5224;

		int i;
		_syncFound = false;

		for (i = 0; i < _bitsInDSR; i++) {
			if (((((_content >> i) & 0xFFFF) == SYNC1) && (_lastByteType != ByteType.c2) /* && (_beforeLast != ByteType.c2) */ ) || 
				 (((_content >> i) & 0xFFFF) == SYNC2)) {
					_syncFound = true;
					break;
			}
		} // we search all bits in DSR for sync byte
		if (_syncFound)
			_shiftToSync = i;
		else
			_shiftToSync = 0;
		return _shiftToSync;
	}

	/// <summary>Pre-synchronization of track start</summary>
	/// <returns>True if synchronization</returns>
	/// <remarks>Before the first synchronization the characters can be decoded any place.
	/// This function forces the chance of detecting either 00 or 4E.
	/// The char 4E is used in GAP and therefore will be found in most cases unless we have 
	/// a shifted track. The char 00 resulting of 0x8888 or 0x2222 is used to privilege 00
	/// over 55 (0x1111) or AA (0x4444) as it is sometimes used for protection</remarks>
	public bool preSync() {
		int i;
		bool syncFound = false;

		for (i = 0; i < _bitsInDSR; i++) {
			// preSync if 00 (8888) - 00 (2222) - 4E (9254)
			if ((((_content >> i) & 0xFFFF) == 0x8888) ||
				(((_content >> i) & 0xFFFF) == 0x2222) ||
				(((_content >> i) & 0xFFFF) == 0x9254)) {
				syncFound = true;
				break;
			} // 00 00 4E
		} // we search all bits in DSR for sync byte
		if (syncFound)
			_bitsInDSR = i;
		return syncFound;
	}

	/// @brief Find if a byte has been assembled in the DSR
	/// @return true when one or several bytes are in the DSR
	///
	/// This call can be done after the shiftBitsIn() call to check if one or more bytes have been
	/// assembled in the DSR. It must be done before the searchSynch() and/or extract(). Due
	/// to huge flux transition it is possible that many <b>bytes</b> are in the DSR. To get them
	/// all you must use this call in a loop
	public bool byteReady() {
		return (_bitsInDSR >= 16);
	}




	/// @}
}
}

