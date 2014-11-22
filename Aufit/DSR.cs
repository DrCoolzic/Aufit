using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WD1772Package {
	public class DSR {
		const int BIT_0 = 0x0001;
		const int BIT_1 = 0x0002;
		const int BIT_2 = 0x0004;
		const int BIT_3 = 0x0008;
		const int BIT_4 = 0x0010;
		const int BIT_5 = 0x0020;
		const int BIT_6 = 0x0040;
		const int BIT_7 = 0x0080;
		const int BIT_8 = 0x0100;

		/// <summary>Byte Type</summary>
		enum ByteType { NotSync, A1Sync, C2Sync }

		/// <summary>
		/// Bit used in the _decoderFlags to indicate that the Sync Address Mark detector is enabled
		/// </summary>
		const int SYNC_DETECTOR_ENABLED =	BIT_0;

		/// <summary>Bit used in the _decoderFlags to indicates that data have been assembled</summary>
		const int DATA_READY =	BIT_1;

		/// <summary>Found the pattern 0x4489 in the AM detector</summary>
		const int A1_FOUND = BIT_2;

		/// <summary>Found the pattern 0x5224 in the AM detector</summary>
		const int C2_FOUND = BIT_3;

		/// <summary>AM Shift Detector</summary>
		/// <remarks>A 16 bits wide shift register that accumulates directly 
		/// all the decoded bits from the data separator (the DPLL).
		/// The bits are entered in the register through the dsrShiftBitIn() function. 
		/// This register is directly used to detect the A1 (0x4489) and C2 (0x5224) Sync marks. 
		/// it is also used as the source to shift the data bits the DSR and to compute the CRC</remarks>
		int _amShitfRegister;

		/// <summary>Data Shift register</summary>
		/// <remarks>The Data shift register is 8 bits wide and accumulates the decoded DATA bits.
		/// This register should be read when a byte has been assembled (this is indicated by 
		/// the DATA_READY property</remarks>
		int _dsr;

		/// <summary>Stores the various decoder Flags</summary>
		int _decoderFlags;

		/// <summary>Indicates the distance of the next data bit</summary>
		/// <remarks>A value of 0 indicates we just read a clock bit in the _amShitfRegister and that
		/// we are ready to transfer the previous data bits in the DSR</remarks>
		int _dataDelay;

		/// <summary>Skip invalid data in DSR</summary>
		/// <remarks>A non null value indicates an invalid DSR content</remarks>
		int _dsrInvalid;

		/// <summary>Number of overlapping bits from last Sync</summary>
		/// <remarks>A non null value indicates that we are still overlapping with the previous
		/// Address Mark</remarks>
		int _syncOverlapCount;

		/// <summary>Store the type of the last sync found</summary>
		ByteType _lastSyncFound;

		/// <summary>Number of bits assembled in the DSR</summary>
		int _dsrBitCount;

		#region flag_helper_functions
		/// <summary>Set the specified bit(s) in the flag</summary>
		/// <param name="flag">The flag to update</param>
		/// <param name="bits">An array of bits to set in the flag</param>
		void setBits(ref int flag, params int[] bits) {
			for (int i = 0; i < bits.Length; i++)
				flag |= bits[i];
		}

		/// <summary>Reset the specified bit(s) in the flag</summary>
		/// <param name="flag">The flag to update</param>
		/// <param name="bits">An array of bits to reset in the flag</param>		
		void resetBits(ref int flag, params int[] bits) {
			for (int i = 0; i < bits.Length; i++)
				flag &= ~bits[i];
		}

		/// <summary>Test the specified bit(s) in the flag</summary>
		/// <param name="flag">The flag to test</param>
		/// <param name="bits">An array of bits to test in the flag</param>
		/// <returns>True if any of the tested bits is set</returns>
		bool testBits(int flag, params int[] bits) {
			bool result = false;
			for (int i = 0; i < bits.Length; i++)
				result |= ((flag & bits[i]) != 0);
			return result;
		}
		#endregion flag_helper_functions


		#region properties
		/// <summary>This property indicates if the Sync detection is enabled (true) or disabled (false)</summary>
		public bool SyncDetectEnable {
			get { return testBits(_decoderFlags, SYNC_DETECTOR_ENABLED); }
			set {
				if (value) setBits(ref _decoderFlags, SYNC_DETECTOR_ENABLED);
				else resetBits(ref _decoderFlags, SYNC_DETECTOR_ENABLED);
			}
		}

		/// <summary>This property is set to true if the last decoded character is an AM (A1 or C2)</summary>
		public bool SyncFound {
			get { return testBits(_decoderFlags, A1_FOUND, C2_FOUND); }
		}

		/// <summary>The last decoded byte is an A1 Sync </summary>
		public bool A1Found {
			get { return testBits(_decoderFlags, A1_FOUND); }
		}

		/// <summary>The last decoded byte is an C2 Sync </summary>
		public bool C2Found {
			get { return testBits(_decoderFlags, C2_FOUND); }
		}

		/// <summary>
		/// This property is true when a character is ready to be read from the DSR
		/// </summary>
		public bool DataReady {
			get { return testBits(_decoderFlags, DATA_READY); }
		}

		/// <summary>When DataReady is true this property returns the decoded Data</summary>
		public byte Data {
			get { return (byte)_dsr; }
		}

		/// <summary>Content of the AM decoder register</summary>
		public ushort MFM {
			get { return (ushort)(_amShitfRegister >> 1); }
		}

		#endregion properties

		/// <summary>Initialize the DSR class</summary>
		public DSR() {
			_amShitfRegister = 0;
			_dsr = 0;
			_syncOverlapCount = 0;
			_decoderFlags = 0;
			_lastSyncFound = ByteType.NotSync;
			_dataDelay = 0;
			_dsrInvalid = 0;
			_dsrBitCount = 0;
		}


		public void shiftBitsIn(int bit) {
			// shift the provided bit in AM Detector LSB
			_amShitfRegister = (_amShitfRegister << 1) | bit;

			// decrement AM overlap count
			if (_syncOverlapCount != 0) _syncOverlapCount--;

			resetBits(ref _decoderFlags, DATA_READY);
			//if (_dataDelay == 1)
				resetBits(ref _decoderFlags, A1_FOUND, C2_FOUND);
			
			// Check for Sync only if detection enabled
			if (SyncDetectEnable) {
				ByteType bitsInDetector = ByteType.NotSync;
				// Check if am detector content is a sync
				switch (_amShitfRegister & 0xFFFF) {
					case 0x4489:
						// detect A1 only if it does not overlap with a previous A1
						if ((_syncOverlapCount == 0) || (_lastSyncFound != ByteType.A1Sync)) {
							bitsInDetector = ByteType.A1Sync;
							setBits(ref _decoderFlags, A1_FOUND);
						}
						break;
					case 0x5224:
						bitsInDetector = ByteType.C2Sync;
						setBits(ref _decoderFlags, C2_FOUND);
						break;
				}	// detect AM

				// process Sync if detected
				if (bitsInDetector != ByteType.NotSync) {
					_dataDelay = 1;	// we have just read the last data bit of a sync

					// if Sync overlap with different Sync type
					if ((_syncOverlapCount != 0) && (_lastSyncFound != bitsInDetector)) {
						_dsrInvalid++;		// DSR value is invalid
						_dataDelay += 2;	// delay by one additional data bit (data & clock)
						Debug.Assert(_lastSyncFound != ByteType.A1Sync, "Overlapping A1 Sync is not possible");
					}	// overlap

					// if DSR is empty it should not be flushed
					if (_dsrBitCount == 0) 
						_dsrInvalid++;

					_dsrBitCount = 7;		// force DSR to flush
					_syncOverlapCount = 16;	// 16 bits (8 clock + 8 data) must be read before next AM
					_lastSyncFound = bitsInDetector;
				}	// Sync found
			}	// Sync detection enabled

			// wait for the data clock cycle plus the bit cell delay
			if (_dataDelay == 0) {
				_dataDelay = 1;

				// shift data bit into the DSR, we are at clock bit
				_dsr = ((_dsr << 1) | ((_amShitfRegister >> 1) & 1)) & 0xFF;

				// if 8 bits in DSR process
				if (++_dsrBitCount == 8) {
					_dsrBitCount = 0;	// reset count
					// set DATA_READY flag if valid data
					if (_dsrInvalid == 0)
						setBits(ref _decoderFlags, DATA_READY);
					else
						_dsrInvalid--;
				}	// one more byte
			}	// data bit
			else
				_dataDelay--;

		}	// end of dsrShiftBitIn()

        public bool byteReady() { return DataReady; }

	}
}
