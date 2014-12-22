/*!
@file DSR.cs
 * 
<summary>This file contains the structures and functions for the WD1772 DST, MFM, and CRC registers</summary>

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

 The shiftBitsIn function is a direct adaptation of the <b>FdcShiftBit</b> function provided
 as part of the SPS Decoder library from the Software Preservation Society http://www.softpres.org/.
 The original sources can be found at http://www.kryoflux.com/?page=download. The
 <b>FdcShiftBit</b> function is located in the CapsEFDC.cpp file.
 
 @author Jean Louis-Guerin & SPS People */

using GlobalPackage;

namespace WD1772Package {
	/// <summary>
	/// The DSR, Sync Mark Detector, and CRC modules of the WD1772
	/// </summary>
	public class DSR {

		enum SyncType { No, A1, C2 }

		/// <summary>Bit used in the _decoderFlags for the Address Mark detector enabled</summary>
		const uint SYNC_DETECTOR_ENABLED = Bit.BIT_01;

		/// <summary>Bit used in the _decoderFlags to indicates that DSR data are available</summary>
		const uint DATA_READY = Bit.BIT_02;

		/// <summary>Bit used in the _decoderFlags to indicates that A1 is found</summary>
		const uint A1_FOUND = Bit.BIT_03;

		/// <summary>Bit used in the _decoderFlags to indicates that C2 is found</summary>
		const uint C2_FOUND = Bit.BIT_04;
		const uint CRC_ENABLED = Bit.BIT_05;
		const uint CRC_ACTIVE = Bit.BIT_06;
		const uint IAM_POSITION = Bit.BIT_07;

		/// <summary>AM Detector</summary>
		/// <remarks>The _syncDetector is a 16 bits wide shift register that accumulates directly 
		/// all the decoded bits from the data separator (the DPLL).
		/// The bits are entered in the register through the dsrShiftBitIn() function. 
		/// This register is directly used to detect the A1 (0x4489) and C2 (0x5224) Address Marks. 
		/// it is also used as the source to shift the data bits the DSR and to compute the CRC</remarks>
		int _syncDetector;

		/// <summary>Data Shift register</summary>
		/// <remarks>The Data shift register is 8 bits wide and accumulates the decoded DATA bits.
		/// This register should be read when a byte has been assembled (this is indicated by 
		/// the DATA_AVAILABLE property</remarks>
		int _dsr;

		/// <summary>Stores the various decoder Flags</summary>
		uint _decoderFlags;

		/// <summary>Indicates the distance of the next data bit</summary>
		/// <remarks>A value of 0 indicates we just read a clock bit in the _amDetector and that
		/// we are ready to transfer the previous data bits in the DSR</remarks>
		int _dataDelay;

		/// <summary>Skip invalid data in DSR</summary>
		/// <remarks>A non null value indicates an invalid DSR content</remarks>
		int _dsrInvalid;

		/// <summary>Number of overlapping bits from last AM</summary>
		/// <remarks>A non null value indicates that we are still overlapping with the previous
		/// Address Mark</remarks>
		int _syncOverlapCount;

		/// <summary>Store the last AM detected</summary>
		SyncType _lastSyncFound;

		/// <summary>Number of bits assembled in the DSR</summary>
		int _dsrBitCount;

		int _syncSeq;

		ushort _crc;
		int _crcCount;

	

		#region properties
		/// <summary>This property indicates if the AM detection is enabled (true) or disabled (false)</summary>
		public bool SyncDetectEnable {
			get { return ((_decoderFlags & SYNC_DETECTOR_ENABLED) != 0); }
			set {
				if (value) _decoderFlags |= SYNC_DETECTOR_ENABLED;
				else _decoderFlags &= ~SYNC_DETECTOR_ENABLED;
			}
		}

		/// <summary>
		/// This property is set to true if the last decoded character is an AM (A1 or C2)
		/// </summary>
		public bool SyncFound {
			get { return ((_decoderFlags & (A1_FOUND + C2_FOUND)) != 0); }
		}

		/// <summary>The last decoded byte is an A1 Sync </summary>
		public bool A1Found {
			get { return ((_decoderFlags & A1_FOUND) != 0); }
		}

		/// <summary>The last decoded byte is an C2 Sync </summary>
		public bool C2Found {
			get { return ((_decoderFlags & C2_FOUND) != 0); }
		}

		/// <summary>
		/// This property indicates if a character is ready to be read from the DSR using the Data property
		/// </summary>
		public bool DataReady {
			get { return ((_decoderFlags & DATA_READY) != 0); }
		}

		/// <summary>
		/// When DsrReady is true this property is set to the decoded Data
		/// </summary>
		public byte Data {
			get { return (byte)_dsr; }
		}

		/// <summary>
		/// Content of the AM shift register when the AM was detected
		/// </summary>
		/// <remarks>As this is read one bit after the detection (data ready) the 
		/// content is shifted right by one</remarks>
		public ushort MFM {
			get { return (ushort)(_syncDetector >> 1); }
		}

		/// <summary>
		/// Returns the current CRC
		/// </summary>
		public ushort CRC {
			get { return _crc; }
		}

		/// <summary>
		/// Set to true for one byte after a sequence of three consecutive sync marks
		/// </summary>
		public bool IAM {
			get { return ((_decoderFlags & IAM_POSITION) != 0); }
		}

		/// <summary>
		/// Last Sync found was an A1 sync mark
		/// </summary>
		public bool LastSyncA1 {
			get { return (_lastSyncFound == SyncType.A1) ? true : false; }
		}

		/// <summary>
		/// Sync sequence in progress
		/// </summary>
		public bool inSyncSeq {
			get { return (_syncSeq > 0);  }
		}

		#endregion properties

		/// <summary>Initialize the DSR class</summary>
		public DSR() {
			_syncDetector = 0;
			_dsr = 0;
			_syncOverlapCount = 0;
			_decoderFlags = 0;
			_lastSyncFound = SyncType.No;
			_dataDelay = 0;
			_dsrInvalid = 0;
			_dsrBitCount = 0;
			_syncSeq = 0;
			_crc = 0;
			_crcCount = 0;
		}

		/// <summary>
		/// Shift one bit in the Address Mark Detector (MFM)
		/// </summary>
		/// <param name="bit">The bit to shift in 0 or 1</param>
		/// <remarks>
		/// One bit is shifted inside the MFM AM detector for detection of the
		/// Sync Marks. The bit shifted in are clock/data bits from a FDC stream
		/// in MFM format. The data bit is shifted in the Data Shift Register (DSR)
		/// and when a byte is ready it is made available. The distinction between
		/// clock and data bits is done by using the Sync Mark.\n
		/// This function is a direct adaptation of the source code from SPS people
		/// for more information read the header of this file.
		/// </remarks>
		public void shiftBitsIn(int bit) {
			// shift the provided bit in AM Detector LSB
			_syncDetector = (_syncDetector << 1) | bit;

			// decrement AM overlap count
			if (_syncOverlapCount != 0) _syncOverlapCount--;

			if ((_decoderFlags & DATA_READY) != 0)
				_decoderFlags &= ~(A1_FOUND | C2_FOUND);
			_decoderFlags &= ~DATA_READY;

			// Check for Address Mark only if detection enabled
			if ((_decoderFlags & SYNC_DETECTOR_ENABLED) != 0) {
				SyncType bitsInDetector = SyncType.No;
				// Check if AM detector content is an Address Mark
				switch (_syncDetector & 0xFFFF) {
					case 0x4489:
						// detect A1 only if it does not overlap with a previous A1 AM
						if ((_syncOverlapCount == 0) || (_lastSyncFound != SyncType.A1)) {
							bitsInDetector = SyncType.A1;
							_decoderFlags |= A1_FOUND;
							if ((_decoderFlags & CRC_ENABLED) != 0) {
								// if CRC not yet active - activate
								if ((_decoderFlags & CRC_ACTIVE) == 0) {
									_decoderFlags |= CRC_ACTIVE;
									_crc = 0xFFFF;		// reset CRC
									_crcCount = 16;
								}	// start CRC computation on first A1
							}	// CRC enabled
						}
						break;
					case 0x5224:
						bitsInDetector = SyncType.C2;
						_decoderFlags |= C2_FOUND;
						break;
				}	// detect AM

				// process Sync if detected
				if (bitsInDetector != SyncType.No) {
					_dataDelay = 1;	// we have just read the last data bit of an AM
					_syncSeq++;
					if (_lastSyncFound != bitsInDetector)
						_syncSeq = 1;

					// if AM overlap with different AM type
					if ((_syncOverlapCount != 0) && (_lastSyncFound != bitsInDetector)) {
						_dsrInvalid++;		// DSR value is invalid
						_dataDelay += 2;	// delay by one additional data bit (data & clock)
					}	// overlap

					// if DSR is empty it should not be flushed => DSR invalid
					if (_dsrBitCount == 0)
						_dsrInvalid++;

					_dsrBitCount = 7;		// force DSR to flush
					_syncOverlapCount = 16;	// 16 bits (8 clock + 8 data) must be read before next AM
					_lastSyncFound = bitsInDetector;
				}	// Sync found
			}	// Sync detection enabled

			// Process CRC if activated
			if ((_decoderFlags & CRC_ACTIVE) != 0) {
				// process every 16 bits
				if ((_crcCount & 0xF) == 0) {
					if ((_crcCount > 48) || ((_decoderFlags & A1_FOUND) != 0)) {
						if (_crcCount == 48) {
							_decoderFlags &= ~SYNC_DETECTOR_ENABLED;
						}	// after 3 A1 we disable AM detector

						// select data bits for CRC source byte value; needed without separate clock and data shifters
						byte value = 0;
						for (int mask = 0x4000; mask != 0; mask >>= 2) {
							value <<= 1;
							if ((_syncDetector & mask) != 0)
								value |= 1;
						}

						// update CRC
						_crc = updateCRC(value, _crc);

					}	// A1 or following bytes
					else
						// reset CRC process if less than 3 consecutive A1 detected
						_decoderFlags &= ~CRC_ACTIVE;
				}	// every 16 bits

				_crcCount++;
			}	// CRC active

			// wait for the data clock cycle plus the bit cell delay
			if (_dataDelay == 0) {
				_dataDelay = 1;

				// shift data bit into the DSR, we are at clock bit
				_dsr = ((_dsr << 1) | ((_syncDetector >> 1) & 1)) & 0xFF;

				// if 8 bits in DSR process
				if (++_dsrBitCount == 8) {
					_dsrBitCount = 0;	// reset count
					// set DSR flag if valid data
					if (_dsrInvalid == 0) {
						_decoderFlags |= DATA_READY;
						//if ((_syncSeq == 3) && (!SyncFound) && (_lastSyncFound == SyncType.A1)) {
						if ((_syncSeq == 3) && (!SyncFound)) {
							_syncSeq = 0;
							_decoderFlags |= IAM_POSITION;
							//if (_dsr < 0xF8) {
							//	_decoderFlags |= (SYNC_DETECTOR_ENABLED | CRC_ENABLED);
							//	_decoderFlags &= ~CRC_ACTIVE;
							//}
						}
						else
							_decoderFlags &= ~IAM_POSITION;
					}
					else
						_dsrInvalid--;
				}	// one more byte
			}	// data bit
			else
				_dataDelay--;

		}	// end of dsrShiftBitIn()

		/// <summary>
		/// Sets the right flags to read a track
		/// </summary>
		/// <remarks>
		/// In track mode the Sync Detector is always on and no CRC is computed
		/// </remarks>
		public void trackMode() {
			_decoderFlags |= SYNC_DETECTOR_ENABLED;
			_decoderFlags &= ~CRC_ENABLED;
		}

		/// <summary>
		/// Sets the right flags to read an ID or DATA segment of a sector
		/// </summary>
		/// <remarks>
		/// In sector mode the Sync Detector is on until 3 sync marks are detected.
		/// In sector mode the CRC is computed. This function must be called before
		/// reading each and every ID / DATA fields as the flags are resets after the sync
		/// </remarks>
		public void sectorMode() {
			_decoderFlags |= (SYNC_DETECTOR_ENABLED | CRC_ENABLED);
			_decoderFlags &= ~CRC_ACTIVE;
		}

		/// <summary>
		/// Update the CRC with the character provided
		/// </summary>
		/// <param name="c">Character used to update CRC</param>
		/// <param name="crc">Current CRC value</param>
		/// <returns>Updated CRC</returns>
		public static ushort updateCRC(byte c, ushort crc) {
			for (int i = 0; i < 8; i++)
				crc = (ushort)((crc << 1) ^ (((((crc >> 8) ^ (c << i)) & 0x0080) != 0) ? 0x1021 : 0));
			return crc;
		} // end of update_crc
	}
}
