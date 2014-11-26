

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

		enum SyncType { No, A1, C2 }

		/// <summary>Bit used in the _decoderFlags for the Address Mark detector enabled</summary>
		const int SYNC_DETECTOR_ENABLED = BIT_0;
		/// <summary>Bit used in the _decoderFlags to indicates that DSR data are available</summary>
		const int DATA_READY = BIT_1;
		const int A1_FOUND = BIT_2;
		const int C2_FOUND = BIT_3;
		const int CRC_ENABLED = BIT_4;
		const int CRC_ACTIVE = BIT_5;
		const int IAM_POSITION = BIT_6;

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
		int _decoderFlags;

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
		/// <summary>This property indicates if the AM detection is enabled (true) or disabled (false)</summary>
		public bool SyncDetectEnable {
			get { return testBits(_decoderFlags, SYNC_DETECTOR_ENABLED); }
			set {
				if (value) setBits(ref _decoderFlags, SYNC_DETECTOR_ENABLED);
				else resetBits(ref _decoderFlags, SYNC_DETECTOR_ENABLED);
			}
		}

		/// <summary>
		/// This property is set to true if the last decoded character is an AM (A1 or C2)
		/// </summary>
		public bool SyncFound {
			get { return testBits(_decoderFlags, A1_FOUND, C2_FOUND); }
			// get { return (DataReady && ( ((MFM == 0x4489) && (Data == 0xA1)) || ((MFM == 0x5224)))); }
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
		/// This property indicates if a character is ready to be read from the DSR using the Data property
		/// </summary>
		public bool DataReady {
			get { return testBits(_decoderFlags, DATA_READY); }
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
		public ushort MFM {
			get { return (ushort)(_syncDetector >> 1); }
		}

		public ushort CRC {
			get { return _crc; }
		}

		public bool IAM {
			get {
				return testBits(_decoderFlags, IAM_POSITION);
			}
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


		public void shiftBitsIn(int bit) {
			// shift the provided bit in AM Detector LSB
			_syncDetector = (_syncDetector << 1) | bit;

			// decrement AM overlap count
			if (_syncOverlapCount != 0) _syncOverlapCount--;

			if (DataReady)
				resetBits(ref _decoderFlags, A1_FOUND, C2_FOUND);
			resetBits(ref _decoderFlags, DATA_READY);

			// Check for Address Mark only if detection enabled
			if (SyncDetectEnable) {
				SyncType bitsInDetector = SyncType.No;
				// Check if AM detector content is an Address Mark
				switch (_syncDetector & 0xFFFF) {
					case 0x4489:
						// detect A1 only if it does not overlap with a previous A1 AM
						if ((_syncOverlapCount == 0) || (_lastSyncFound != SyncType.A1)) {
							bitsInDetector = SyncType.A1;
							setBits(ref _decoderFlags, A1_FOUND);
							if (testBits(_decoderFlags, CRC_ENABLED)) {
								// if CRC not yet active - activate
								if (!testBits(_decoderFlags, CRC_ACTIVE)) {
									setBits(ref _decoderFlags, CRC_ACTIVE);
									_crc = 0xFFFF;		// reset CRC
									_crcCount = 16;
								}	// start CRC computation on first A1
							}	// CRC enabled
						}
						break;
					case 0x5224:
						bitsInDetector = SyncType.C2;
						setBits(ref _decoderFlags, C2_FOUND);
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
			if (testBits(_decoderFlags, CRC_ACTIVE)) {
				// process every 16 bits
				if ((_crcCount & 0xF) == 0) {
					if ((_crcCount > 48) || testBits(_decoderFlags, A1_FOUND)) {
						if (_crcCount == 48) {
							// TODO A1 is already active? do we need
							setBits(ref _decoderFlags);
							resetBits(ref _decoderFlags, SYNC_DETECTOR_ENABLED);
						}

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
						resetBits(ref _decoderFlags, CRC_ACTIVE);
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
						setBits(ref _decoderFlags, DATA_READY);
						if ((_syncSeq == 3) && (!SyncFound)) {
							_syncSeq = 0;
							setBits(ref _decoderFlags, IAM_POSITION);
						}
						else
							resetBits(ref _decoderFlags, IAM_POSITION);
					}
					else
						_dsrInvalid--;
				}	// one more byte
			}	// data bit
			else
				_dataDelay--;

		}	// end of dsrShiftBitIn()

		public void trackMode() {
			setBits(ref _decoderFlags, SYNC_DETECTOR_ENABLED);
			resetBits(ref _decoderFlags, CRC_ENABLED);
		}

		public void sectorMode() {
			setBits(ref _decoderFlags, SYNC_DETECTOR_ENABLED, CRC_ENABLED);
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
