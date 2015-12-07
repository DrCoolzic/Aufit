/*!
@file FDC.cs
 * 
<summary>Emulate WD1772 FDC behavior</summary>

<div class="jlg">Copyright (C) 2013-2015 Jean Louis-Guerin\n\n
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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;

using GlobalPackage;
using Aufit;


namespace WD1772Package {
	/// <summary>Different phases of a sector: ID, Inner Gap, Data, Post GAP</summary>
	public enum Phases : byte {
		postDataGap,    ///<! Post Data GAP
		postIdGap,      ///!< GAP between ID and DATA
		id,             ///<! ID (Address) block
		data            ///<! DATA block
	}

	public enum IAMType { Id, Data, Other }

	/// <summary>Store information about a block of data</summary>
	public class BlockInfo {
		/// <summary>First byte of the block</summary> 
		public int startByte;

		/// <summary>First bit of the block</summary> 
		public int startBit;

		/// <summary>First IAM bit of the block</summary> 
		public int startBitIAM;

		/// <summary>Number of bits (clock + data) in the block</summary>
		public int bitCount;

		/// <summary>Starting Time of the block in nanoseconds from index</summary>
		public int startTime;
		public int startTimeIAM;

		/// <summary>Length of the Block in nanoseconds </summary>
		public int timeLength;

		public int firstIndex;
		public int lastIndex;

		/// <summary>CRC value at the end of reading an ID or DATA block.</summary>
		/// <remarks> Should be zero on a "good" block</remarks>
		public ushort crc;
	}


	/// <summary>Store information about the ID Field of a Sector</summary>
	public class IdField {
		/// <summary>Property that returns the Index Address Mark</summary> 
		public byte IDAM {
			get { return buffer.DataByte.Count > 3 ? buffer.DataByte[3] : (byte)0; }
		}

		/// <summary>Returns the track number</summary> 
		public byte Track {
			get { return buffer.DataByte.Count > 4 ? buffer.DataByte[4] : (byte)0; }
		}

		/// <summary>Returns the Head/Side number</summary> 
		public byte Head {
			get { return buffer.DataByte.Count > 5 ? buffer.DataByte[5] : (byte)0; }
		}

		/// <summary>Returns the sector number</summary> 
		public byte Sector {
			get { return buffer.DataByte.Count > 6 ? buffer.DataByte[6] : (byte)0; ; }
		}

		/// <summary>Returns the encoded data segment length</summary> 
		public byte Length {
			get { return buffer.DataByte.Count > 7 ? buffer.DataByte[7] : (byte)0; }
		}

		/// <summary>Returns the CRC</summary> 
		public ushort CRC {
			get { return buffer.DataByte.Count > 9 ? (ushort)((int)(buffer.DataByte[8]) + (int)(buffer.DataByte[9] << 8)) : (ushort)0; }
		}

		/// <summary>Store the ID Field content</summary>
		public TrackBuffer buffer = new TrackBuffer();

		/// <summary> Contains information about the Id field block</summary>
		public BlockInfo info = new BlockInfo();

		public GapField postIdGap;
	}


	/// <summary>Store information about the DATA Field of a Sector</summary>
	public class DataField {
		/// <summary>Returns the Data Address Mark</summary> 
		public byte DAM {
			get { return (buffer.DataByte.Count > 3) ? buffer.DataByte[3] : (byte)0; }
		}

		/// <summary>Returns the CRC</summary> 
		public ushort CRC {
			get {
				return (buffer.DataByte.Count > 3) ? (ushort)((int)(buffer.DataByte[buffer.DataByte.Count - 2]) +
					(int)(buffer.DataByte[buffer.DataByte.Count - 1] << 8)) : (ushort)0;
			}
		}

		/// <summary>buffer for all the byte read in the DATA field of the sector</summary>
		/// <remarks>The buffer contains the DAM and CRC. Therefore when writing sector data this must be removed</remarks>
		public TrackBuffer buffer = new TrackBuffer();

		/// <summary>Array of bytes to store fuzzy mask</summary> 
		public byte[] fuzzy;

		/// <summary>information about the data field </summary>
		public BlockInfo info = new BlockInfo();

		public int truncBitSize;
		public GapField postDataGap;
	}


	public class IAM {
		public IAMType Type { get; set; }
		public bool Error { get; set; }
		public byte Value { get; set; }
		public BlockInfo info = new BlockInfo();
	}


	/// <summary>Store information about GAP fields of a sector</summary>
	public class GapField {
		public TrackBuffer buffer = new TrackBuffer();
		public BlockInfo info = new BlockInfo();
	}

	[Flags]
	public enum SectorFlags : uint {
		None = 0,
		/// <summary>50% of bits cell width above normal (+3.5%)</summary>
		WIDTH_ABOVE = Bit.BIT_01,
		/// <summary>50% of bits cell width below normal (-3.5%)</summary>
		WIDTH_BELOW = Bit.BIT_02,
		/// <summary>50% of bits cell below and above normal</summary>
		WIDTH_VARIATION = Bit.BIT_03,
		/// <summary>IDAM not equal $FE</summary>
		NOT_STD_IDAM = Bit.BIT_04,
		/// <summary>Wrong track number in ID</summary>
		ID_BAD_TRK_NUM = Bit.BIT_05,
		/// <summary>Wrong head number in ID</summary>
		ID_BAD_HEAD_NUM = Bit.BIT_06,
		/// <summary>Invalid sector number in ID</summary>
		ID_BAD_SEC_NUM = Bit.BIT_07,
		/// <summary>Invalid sector length in ID</summary>
		ID_BAD_SEC_LEN = Bit.BIT_08,
		/// <summary>DAM not equal to $FB</summary>
		NOT_STD_DAM = Bit.BIT_09,
		/// <summary>Sector contains NFA region</summary>
		NFA_IN_SECT = Bit.BIT_10,
		/// <summary>Two or more sector with same number</summary>
		DUP_SEC_NUM = Bit.BIT_11,
		/// <summary>Sector read time is 3% above normal</summary>
		LONG_SECT = Bit.BIT_12,
		/// <summary>Sector read time is 3% below normal</summary>
		SHORT_SECT = Bit.BIT_13,
		/// <summary>Data field with no ID field</summary>
		DATA_NO_ID = Bit.BIT_14,
		/// <summary>Truncated sector</summary>
		TRUNC_SECT = Bit.BIT_15,
		/// <summary>Sector has DM type of fuzzy byte</summary>
		DM_FUZZY = Bit.BIT_16,

		DM_HACK = Bit.BIT_30,
		SEC_IN_TRACK = Bit.BIT_31

		// BAD_ID_CRC => id.info.crc != 0
		// SECT_NO_DATA => data == null
		// BAD_DATA_CRC => data.info.crc != 0
		// FUZZY_SECT => data.fuzzy != null
	}


	/// <summary>
	/// Sector information. Contains ID & Data fields and flags
	/// </summary>
	public class Sector {
		/// <summary>Pointer to the Id Field of a sector</summary>
		public IdField id;

		/// <summary>Pointer to the Data Field of the sector (can be null)</summary>
		public DataField data;

		public DataField dataNoId;

		/// <summary>Pointer to the Recovered Data Field of the sector (can be null)</summary>
		/// <remarks>If we find fuzzy bytes and good CRC we recover in this field</remarks>
		public DataField dataRecovered;

		/// <summary>Sector's flags</summary>
		public SectorFlags flags;

		/// <summary>Same sector content than track at offset (Pasti optimize) </summary>
		public int trackOffset;
	}


	[Flags]
	public enum TrackFlag : uint {
		None = 0,

		/// <summary>Track has less than 6062 bytes</summary>
		SHORT_TRACK = Bit.BIT_01,

		/// <summary>Track has more 6437 bytes</summary>
		LONG_TRACK = Bit.BIT_02,

		/// <summary>Data track detected</summary>
		DATA_TRACK = Bit.BIT_03,

		/// <summary>Track has more than 5 invalid bytes ($F5-F7) in GAP</summary>
		BAD_DATA_IN_GAP = Bit.BIT_04,

		/// <summary>Sector numbered non sequentially</summary>
		SECTORS_NOT_IN_SEQ = Bit.BIT_05,

		/// <summary>Hidden Data in track (DF & JMD)</summary>
		HIDDEN_DATA = Bit.BIT_06,

		/// <summary>Track has Sync sequence > 4</summary>
		INVALID_SYNC_SEQ = Bit.BIT_07,

		/// <summary>Track contains No Flux Area</summary>
		NFA_IN_TRACK = Bit.BIT_08,

		/// <summary>Track contains sector with non standard size</summary>
		SECTORS_NOT_STD_SIZE = Bit.BIT_09,

		DUP_SEC_IN_TRACK = Bit.BIT_10

	///// <summary>Shifted Track with data over index pulse</summary>
	//public const uint DATA_OVER_IDX = Bit.BIT_08;
	///// <summary>Shifted Track with data after index pulse</summary>
	//public const uint DATA_AFTER_IDX = Bit.BIT_09;
	///// <summary>Shifted Track with id over index pulse</summary>
	//public const uint ID_OVER_IDX = Bit.BIT_10;
	///// <summary>Shifted Track with id after index pulse</summary>
	//public const uint ID_AFTER_IDX = Bit.BIT_11;
	}


	/// <summary>Track information</summary>
	public class Track {
		/// <summary>buffer for all the byte read in the DATA field of the sector</summary>
		public TrackBuffer buffer = new TrackBuffer();

		/// <summary> Contains information about the field </summary>
		public BlockInfo info = new BlockInfo();

		/// <summary>Track's flags</summary>
		public TrackFlag flags;

		/// <summary>
		/// Location in byte of the first A1 sync before an IAM sequence
		/// </summary>
		public int firstSyncOffset = 0;

		/// <summary>Index of the first sector of the track</summary>
		public int firstSectIndex = 0;
		public int splicePosition = 0;

		/// <summary>Protection(s) detected for this track</summary>
		public bool hasProtections = false;
		public bool shiftedTrack = false;

	}

	/// <summary>We raise an exception to indicate bad error</summary>
	public class ImageErrorException : Exception {
		/// <summary>SectorNotFoundException Constructor</summary>
		public ImageErrorException() : base() { }
		/// <summary>SectorNotFoundException Constructor</summary>
		public ImageErrorException(string message) : base(message) { }
		/// <summary>SectorNotFoundException Constructor</summary>
		public ImageErrorException(string message, Exception e) : base(message, e) { }

		private static string _ExceptionMessage;

		/// <summary>
		/// Get/SEt the content of the Exception message
		/// </summary>
		public static string ExceptionMessage {
			get { return _ExceptionMessage; }
			set { _ExceptionMessage += value + "\n"; }
		}

		/// <summary>
		/// Reset content of exception message
		/// </summary>
		public static void resetException() {
			_ExceptionMessage = null;
		}
	}

	/// <summary>FDC read functions helper</summary>
	/// <remarks>We use ring buffers to handle the 4 last bytes information. 
	/// This allow to store the last three  Sync mark bytes in front of the ID and
	/// Data fields. The class has been design  to store information accurate at the
	/// bit level.</remarks>
	public class BitTrackInfo {
		private int _runningTime;
		private int _runningBit;
		private int _runningIndex;
		private int _byteCount;

		private int[] _currentIndex = new int[4];
		private int[] _currentTime = new int[4];
		private int[] _currentBit = new int[4];
		byte[] _dataBuffer = new byte[4];
		ushort[] _mfmBuffer = new ushort[4];
		int _ringIndex;

		private int _bitWidthSum;
		private int _bitWidthCount;
		private ushort _bitWidthAvg;
		private int _fluxTimeInc;
		private int _fluxRem;
		DSR _dsr;

		public int ByteCount {
			get { return _byteCount; }
		}

		public int BitCount {
			get { return _runningBit; }
		}

		public int BitWidthAvg {
			get { return _bitWidthAvg; }
		}

		/// <summary>Constructor</summary>
		/// <param name="dsr"></param>
		public BitTrackInfo(DSR dsr) {
			_dsr = dsr;
		}


		public void nextBit() {
			_runningBit++;
			_runningTime += _fluxTimeInc;
		}


		public void nextByte() {
			_byteCount++;
			_ringIndex = (_ringIndex + 1) % 4;
			_dataBuffer[_ringIndex] = _dsr.Data;
			_mfmBuffer[_ringIndex] = _dsr.MFM;
			_currentIndex[_ringIndex] = _runningIndex;
			_currentTime[_ringIndex] = _runningTime + _fluxRem;
			_currentBit[_ringIndex] = _runningBit;
			_bitWidthAvg = (_bitWidthCount == 0) ? (ushort)2000 : (ushort)(_bitWidthSum / _bitWidthCount);
			_bitWidthCount = _bitWidthSum = 0;
		}


		public int nextFlux(Global.DiskTrack dt, int currentIndex) {
			_runningIndex = currentIndex;
			int flux = dt.fluxData.fluxValue[currentIndex];
			int bitSpacing = dt.decodedData.bitSpacing[currentIndex];
			int bitWidth = dt.decodedData.bitWidth[currentIndex];
			_fluxTimeInc = flux / bitSpacing;
			_fluxRem = flux % bitSpacing;
			_bitWidthSum += bitWidth;
			_bitWidthCount++;
			return bitSpacing;
		}


		public void addByte(TrackBuffer b, Phases phase, bool addSync) {
			if (addSync) {
				for (int i = 1; i < 4; i++) {
					int x = (_ringIndex + i) % 4;
					b.addByte(_dataBuffer[x], _mfmBuffer[x], _bitWidthAvg, _currentTime[x], phase);
				}
			}   // flush sync seq
			b.addByte(_dsr.Data, _dsr.MFM, _bitWidthAvg, _currentTime[_ringIndex], phase);
		}


		public void storeStartInfo(BlockInfo bi, bool addSync) {
			int x;
			if (addSync) {
				x = (_ringIndex + 1) % 4;
				bi.startByte = _byteCount - 3;
				bi.startBitIAM = _currentBit[_ringIndex];
				bi.startTimeIAM = _currentTime[_ringIndex];
			}
			else {
				x = _ringIndex;
				bi.startByte = _byteCount;
			}
			bi.startBit = _currentBit[x];
			bi.startTime = _currentTime[x];
			bi.firstIndex = _currentIndex[x];
		}


		public void storeEndInfo(BlockInfo bi, bool removeSync) {
			int x = (removeSync) ? (_ringIndex + 1) % 4 : _ringIndex;
			bi.timeLength = _currentTime[x] - bi.startTime;
			bi.bitCount = _currentBit[x] - bi.startBit;
			bi.lastIndex = _currentIndex[x];
		}

		/// <summary>Used to set current value with saved info</summary>
		/// <param name="bi">Block info providing running values</param>
		/// <param name="currentIndex">current index</param>
		public void setInfo(BlockInfo bi, int currentIndex) {
			_runningIndex = currentIndex;
			_runningBit = bi.startBitIAM;
			_runningTime = bi.startTimeIAM;
			_byteCount = bi.startByte + 3;
		}

		public void resetInfo() {
			_runningBit = 0;
			_runningTime = 0;
			_byteCount = 0;
		}

	}   // class BitTrackInfo


	/// <summary>Emulate WD1772 FDC behavior</summary>
	public class FDC {
		// Bit cell width upper/lower values = 2000 +/- 3.5%
		private readonly static int WIDTH_UPPER_LIMIT = 2070;
		private readonly static int WIDTH_LOWER_LIMIT = 1930;

		public static bool isGapPhase(Phases p) {
			return ((p == Phases.postIdGap) || (p == Phases.postDataGap));
		}


		/// <summary>Read a track</summary>
		/// <param name="track">The track index</param>
		/// <param name="side">The side index</param>
		/// <param name="rev">The revolution index</param>
		/// <remarks>The data placed in the buffer are equivalent to what would 
		/// be read by a WD1772 decode the flux transitions. We store the
		/// phase of a byte based on information detected by readAddr and readSector.
		/// Therefore this function must be called last.</remarks>
		public static void readTrack(int track, int side, int rev) {
			if (Global.Disk[track, side].track != null) return;
			int start = Global.firstIndex(track, side, rev);
			int stop = Global.lastIndex(track, side, rev);

			Global.Disk[track, side].track = new Track();
			Global.DiskTrack dt = Global.Disk[track, side];
			DSR dsr = new DSR();
			dsr.trackMode();
			BitTrackInfo bti = new BitTrackInfo(dsr);

			int invalidByteInGap = 0;
			int nfaCount = 0;
			Phases phase = Phases.postDataGap;
			int currentSector = -1;
			int byteLeftInBlock = 0;
			int syncCount = 0;
			bool dataNoId = false;

			bti.storeStartInfo(Global.Disk[track, side].track.info, false);
			Global.Disk[track, side].track.info.firstIndex = start;

			// We read from the beginning to end of a track with AM decoder on all time
			for (int currentIndex = start; currentIndex < stop; currentIndex++) {
				int bitSpacing = bti.nextFlux(dt, currentIndex);

				while (bitSpacing-- > 0) {
					dsr.shiftIn(bitSpacing > 0 ? 0 : 1);
					bti.nextBit();

					if ((dsr.MFM == 0x4489) || (dsr.MFM == 0x5224))
						syncCount++;

					if (dsr.DataReady) {
						bti.nextByte();

						if (dsr.SyncSeq > 4)
							Global.Disk[track, side].track.flags |= TrackFlag.INVALID_SYNC_SEQ;

						// check NFA
						if ((dsr.MFM == 0) || (dsr.MFM == 0xFFFF))
							nfaCount++;
						else {
							if (nfaCount > 8)
								Global.Disk[track, side].track.flags |= TrackFlag.NFA_IN_TRACK;
							nfaCount = 0;
						}

						if (dsr.IAM)
							switch (dsr.Data) {
								// Case ID field //
								case 0xFC:
								case 0xFD:
								case 0xFE:
								case 0xFF:
									if (dt.track.firstSyncOffset == 0)
										Global.Disk[track, side].track.firstSyncOffset = bti.ByteCount - 3;
									currentSector++;
									byteLeftInBlock = 7;
									phase = Phases.id;
									dataNoId = false;
									break;

								// Case DATA field
								case 0xF8:
								case 0xF9:
								case 0xFA:
								case 0xFB:
									phase = Phases.data;
									byteLeftInBlock = 1;
									if ((dt.sectors != null) && (dt.sectors.Count > 0)) {
										int s = (currentSector != -1) ? currentSector : dt.sectors.Count - 1;
										// ===================== TODO CHECK => optimize for pasti ====================================================
										dt.sectors[s].trackOffset = bti.ByteCount + 1;
										byteLeftInBlock = Global.sectSize(dt.sectors[s].id.Length) + 3; // AM + 2 CRC + Size
										syncCount = 0;
										if (dt.sectors[s].data != null) {
											if (!dataNoId) dataNoId = true;
											else dataNoId = false;
										}   // sector has data
									}	// data but no sector => inconsistent
									break;

								// other = data track ?
								default:
									// TODO
									break;
							}   // switch on IAM

						else  // not IAM
							switch (phase) {
								case Phases.id:
									byteLeftInBlock--;
									if (byteLeftInBlock == 0)
										phase = Phases.postIdGap;
									break;

								case Phases.data:
									byteLeftInBlock--;
									if (byteLeftInBlock == 0) {
										int s = (currentSector != -1) ? currentSector : dt.sectors.Count - 1;
										phase = Phases.postDataGap;
										if (dataNoId && (syncCount == 0))
											dt.sectors[s].flags |= SectorFlags.SEC_IN_TRACK;
									}   // last byte
									break;

								default:    // gap
									if ((dsr.Data >= 0xF5) && (dsr.Data <= 0xF7))
										invalidByteInGap++;
									break;
							}   // not IAM

						bti.addByte(Global.Disk[track, side].track.buffer, phase, false);
					}   // data ready
				}   // shift bit in
			}   // for all bits

			bti.storeEndInfo(Global.Disk[track, side].track.info, false);

			if (invalidByteInGap > 5)
				Global.Disk[track, side].track.flags |= TrackFlag.BAD_DATA_IN_GAP;
		}   // end of readTrack()


		/// <summary>Read and Store IAM info for all revolutions of a track</summary>
		/// <param name="track">The track index</param>
		/// <param name="side">The side index</param>
		public static void readIAM(int track, int side) {
			Global.DiskTrack dt = Global.Disk[track, side];
			if (dt.iams != null) return;

			int start = Global.firstIndex(track, side, 0);
			//int stop = dt.fluxData.totalFluxCount;
			int stop = Global.lastIndex(track, side, dt.fluxDataRev.Length - 1);

			List<IAM>[] iams = new List<IAM>[dt.fluxDataRev.Length];
			for (int i = 0; i < dt.fluxDataRev.Length; i++) iams[i] = new List<IAM>();

			IAM iam = null;
			DSR dsr = new DSR();
			dsr.sectorMode();
			BitTrackInfo bti = new BitTrackInfo(dsr);
			int rev = 0;

			// We read from the first to last index looking only for all IAM
			for (int  currentIndex = start; currentIndex < stop; currentIndex++) {
				int last = Global.lastIndex(track, side, rev);
				if (currentIndex > last) {
					rev++;
					bti.resetInfo();
				}
				int bitSpacing = bti.nextFlux(dt, currentIndex);
				while (bitSpacing-- > 0) {
					dsr.shiftIn(bitSpacing > 0 ? 0 : 1);
					bti.nextBit();

					if (dsr.DataReady) {
						bti.nextByte();

						if (dsr.IAM) {  // IAM position
							if (iam != null)
								bti.storeEndInfo(iam.info, true);

							iam = new IAM();
							iams[rev].Add(iam);
							bti.storeStartInfo(iam.info, true);
							dsr.sectorMode();
							iam.Error = false;
							iam.Value = dsr.Data;

							if (Enumerable.Range(0xFC, 4).Contains(dsr.Data))	// ID AM
								iam.Type = IAMType.Id;
							else if (Enumerable.Range(0xF8, 4).Contains(dsr.Data))	// Data AM
								iam.Type = IAMType.Data;
							else
								iam.Type = IAMType.Other;
						}   // IAM position
					}   // one byte ready
				}   // shift all bits
			}   // for all index
			if (iam != null)
				bti.storeEndInfo(iam.info, true);
			Global.Disk[track, side].iams = iams;
		}   // end of readIAM()


		/// <summary>Read and Store IAM info and store ID content for complete track</summary>
		/// <param name="track">The track index</param>
		/// <param name="side">The side index</param>
		/// <param name="rev">The revolution index</param>
		//public static List<Sector> readId(int track, int side, int rev) {
		public static void readId(Global.DiskTrack dt, IdField id) {
			int firstIndex = (id.info.firstIndex - 16) < 0 ? 0 : id.info.firstIndex - 16;
			int lastIndex = id.info.lastIndex;
			int byteLeftInBlock = 0;
			ushort crc = 0;
			Phases phase = Phases.postDataGap;
			DSR dsr = new DSR();
			dsr.sectorMode();
			BitTrackInfo bti = new BitTrackInfo(dsr);

			for (int currentIndex = firstIndex; (currentIndex <= lastIndex) || (phase != Phases.postIdGap); currentIndex++) {
				if (currentIndex > dt.fluxData.fluxValue.Length - 1)
					break;

				int bitSpacing = bti.nextFlux(dt, currentIndex);
				while (bitSpacing-- > 0) {
					dsr.shiftIn(bitSpacing > 0 ? 0 : 1);
					bti.nextBit();
					if (dsr.DataReady) {
						bti.nextByte();

						if (phase != Phases.id) { // we are in GAP
							if (phase == Phases.postIdGap)
								bti.addByte(id.postIdGap.buffer, phase, false);

							if (dsr.IAM) {  // IAM position
								if (Enumerable.Range(0xFC, 4).Contains(dsr.Data)) { // ID AM
									byteLeftInBlock = 7;
									phase = Phases.id;
									bti.setInfo(id.info, currentIndex);
									bti.addByte(id.buffer, phase, true);
								}   // IDAM
								else {
									Debug.Assert(true, "Damned");
									phase = Phases.postDataGap;
									dsr.sectorMode();
								}   // non standard IAM
							}   // IAM position
						}   // we are in GAP

						else {  // we are in ID
							byteLeftInBlock--;
							if (byteLeftInBlock > 0) {
								bti.addByte(id.buffer, phase, false);
								if (byteLeftInBlock == 1) crc = dsr.CRC;
							}   // not last byte
							else {
								dsr.sectorMode();   // we re-enable sync
								id.info.crc = crc;
								bti.storeEndInfo(id.info, false);
								phase = Phases.postIdGap;

								id.postIdGap = new GapField();
								bti.storeStartInfo(id.postIdGap.info, false);
								bti.addByte(id.postIdGap.buffer, phase, false);
							}   // byteInBlock == 0
						} // we are in ID
					}   // one byte ready
				}   // shift all bits
			}   // for all index

			if (phase == Phases.postIdGap) {
				bti.storeEndInfo(id.postIdGap.info, false);
				id.postIdGap.buffer.removeLastBytes(1);
				if (id.postIdGap.buffer.DataByte.Count == 0)
					id.postIdGap = null;
			}
			if (phase == Phases.id)
				bti.storeEndInfo(id.info, false);
		}   // end of readId()


		public static SectorFlags readData(Global.DiskTrack dt, DataField data, int size, bool full) {
			int firstIndex = (data.info.firstIndex - 16) < 0 ? 0 : data.info.firstIndex - 16;
			int lastIndex = data.info.lastIndex;
			Phases phase = Phases.postIdGap;
			DSR dsr = new DSR();
			dsr.sectorMode();
			BitTrackInfo bti = new BitTrackInfo(dsr);

			int byteLeftInBlock = 0;
			int widthOver = 0;
			int widthUnder = 0;
			ushort crc = 0;
			SectorFlags flags = 0;
			int nfaCount = 0;

			for (int currentIndex = firstIndex; (currentIndex <= lastIndex) || (phase != Phases.postDataGap); currentIndex++) {
				if (currentIndex > dt.fluxData.fluxValue.Length - 1)
					break;
				if ((!full) && (phase == Phases.postDataGap))
					break;

				int bitSpacing = bti.nextFlux(dt, currentIndex);
				while (bitSpacing-- > 0) {
					dsr.shiftIn(bitSpacing > 0 ? 0 : 1);
					bti.nextBit();
					if (dsr.DataReady) {
						bti.nextByte();
						if (full) {
							if ((currentIndex > lastIndex) && !flags.HasFlag(SectorFlags.TRUNC_SECT)) {
								flags |= SectorFlags.TRUNC_SECT;
								data.truncBitSize = bti.ByteCount - data.info.startByte;
							}
							// check NFA
							if ((dsr.MFM == 0) || (dsr.MFM == 0xFFFF))
								nfaCount++;
							else {
								if (nfaCount > 8)
									flags |= SectorFlags.NFA_IN_SECT;
								nfaCount = 0;
							}
						}

						if (phase != Phases.data) { // we are in GAP
							if (phase == Phases.postDataGap)
								bti.addByte(data.postDataGap.buffer, phase, false);

							if (dsr.IAM) {  // IAM position
								if (Enumerable.Range(0xF8, 4).Contains(dsr.Data)) { // DATA AM
									bti.setInfo(data.info, currentIndex);           // we are synchronized set running values
									bti.addByte(data.buffer, Phases.data, true);    // we add the sync seq
									byteLeftInBlock = size;
									phase = Phases.data;
									widthOver = widthUnder = 0;
								}   // Data AM

								else {  // all other AM => stop
									Debug.Assert(true, "Damned");
									phase = Phases.postDataGap;
									dsr.sectorMode();
								}   // not Data AM
							}   // IAM Position
						}   // we are in GAP

						else {  // we are in DATA
							byteLeftInBlock--;
							switch (byteLeftInBlock) {
								case 1:
									bti.addByte(data.buffer, phase, false);
									crc = dsr.CRC;
									break;

								case 0: // end of DATA block
									dsr.sectorMode();
									data.info.crc = crc;
									bti.storeEndInfo(data.info, false);
									phase = Phases.postDataGap;

									if ((widthOver > (size / 2)) || (widthUnder > (size / 2))) {
										if (widthOver > (size / 2)) flags |= SectorFlags.WIDTH_ABOVE;
										if (widthUnder > (size / 2)) flags |= SectorFlags.WIDTH_BELOW;
									}
									else if ((widthOver > 0) && (widthUnder > 0) && ((widthOver + widthUnder) > (size / 4)))
										flags |= SectorFlags.WIDTH_VARIATION;

									data.postDataGap = new GapField();
									bti.storeStartInfo(data.postDataGap.info, false);
									bti.addByte(data.postDataGap.buffer, phase, false);
									break;

								default:    // other data bytes
									bti.addByte(data.buffer, phase, false);
									if (bti.BitWidthAvg > WIDTH_UPPER_LIMIT) widthOver++;
									if (bti.BitWidthAvg < WIDTH_LOWER_LIMIT) widthUnder++;
									break;
							}   // switch byte
						} // we are in DATA
					}   // one byte ready
				}   // shift in all bits;
			}   // for all index

			if ((phase == Phases.postDataGap) && (data.postDataGap != null)) {
				bti.storeEndInfo(data.postDataGap.info, false);
				data.postDataGap.buffer.removeLastBytes(1);
				if (data.postDataGap.buffer.DataByte.Count == 0)
					data.postDataGap = null;
			}
			if (phase == Phases.data)
				bti.storeEndInfo(data.info, false);
			return flags;
		}


		/// <summary>
		/// Reads and stores the content of a sector + compares content from other revolutions
		/// </summary>
		/// <param name="track"></param>
		/// <param name="side"></param>
		/// <param name="rev"></param>
		/// <param name="sector"></param>
		/// <param name="dataNoId"></param>
		/// <returns></returns>
		public static SectorFlags readDataCompare(int track, int side, int rev, Sector sector, int iamIndex, bool dataNoId) {
			Global.DiskTrack dt = Global.Disk[track, side];

			int size = Global.sectSize(sector.id.Length) + 3; // AM + 2 CRC + Size
			DataField data = dataNoId ? sector.dataNoId : sector.data;

			SectorFlags flags = readData(dt, data, size, true);


			for (int j = 0; j < Global.Disk[track, side].fluxDataRev.Count(); j++) {
				if (j == rev) continue;
				DataField dataRev = new DataField();
				transferInfo(dt.iams[j][iamIndex], dataRev.info);
				readData(dt, dataRev, size, false);
				// check fuzzy bytes if same size
				if (dataRev.buffer.DataByte.Count == data.buffer.DataByte.Count) {
					byte[] fuzzy = new byte[dataRev.buffer.DataByte.Count];
					bool fuzzyFound = false;

					for (int i = 3; i < dataRev.buffer.DataByte.Count; i++) {
						byte xor = (byte)(dataRev.buffer.DataByte[i] ^ data.buffer.DataByte[i]);
						if (xor != 0) {
							fuzzyFound = true;
							fuzzy[i] |= xor;
						}
					}   // compare all bytes

					if (fuzzyFound) {
						if (data.fuzzy == null) data.fuzzy = fuzzy;
						else
							for (int i = 3; i < dataRev.buffer.DataByte.Count; i++)
								data.fuzzy[i] |= fuzzy[i];
					}

					// if we have fuzzy sector and CRC is correct this indicates
					// that we need to keep the correctly read sector.
					if (fuzzyFound && (dataRev.info.crc == 0))
						sector.dataRecovered = dataRev;
				}   // same size
			}   // compare other rev
			return flags;
		}	// readDataCompare()


		public static void processTrack(int track, int side, int rev) {
			Global.DiskTrack dt = Global.Disk[track, side];
            if ((dt.sectors != null) && (dt.sectors.Count > 0)) return;
			if ((dt.fluxData == null) || (dt.decodedData == null)) return;
			Global.Disk[track, side].sectors = new List<Sector>();
			bool consistentIAM = true;

			readIAM(track, side);
			int maxSize = 0;
			for (int r = 0; r < Global.Disk[track, side].fluxDataRev.Count(); r++) {
				if (Global.Disk[track, side].iams[r].Count > maxSize)
					maxSize = Global.Disk[track, side].iams[r].Count;
 			}
			for (int r = 0; r < Global.Disk[track, side].fluxDataRev.Count(); r++) {
				int refRev = 0;
				int diff = maxSize - Global.Disk[track, side].iams[r].Count;
                if (diff > 0) {
					if (diff > 1) {
						try { throw new ImageErrorException(
								String.Format("*** T{0:D2}.{1:D1} Too many inconsistencies turning off sector detection ***", track, side));
						}
						catch (ImageErrorException e) { ImageErrorException.ExceptionMessage = e.Message; }
						consistentIAM = false;
                        break;
					}
					for (refRev = 0; refRev < Global.Disk[track, side].fluxDataRev.Count(); refRev++) {
						if (Global.Disk[track, side].iams[refRev].Count == maxSize) break;
					}   // first good
					for (int iamIndex = 0; iamIndex < Global.Disk[track, side].iams[r].Count; iamIndex++) {
						IAM iam = Global.Disk[track, side].iams[r][iamIndex];
						IAM refIAM = Global.Disk[track, side].iams[refRev][iamIndex];
						if (iam.info.startByte > (refIAM.info.startByte + 5)) {
							IAM repair = new IAM();
							repair.Type = refIAM.Type;
							repair.Error = true;
							transferInfo(refIAM, repair.info);
							Global.Disk[track, side].iams[r].Insert(iamIndex, repair);
							try {
								throw new ImageErrorException(
								  String.Format("*** T{0:D2}.{1:D1} {2} not found in Revolution {3} recovering from {4} ***",
								  track, side, refIAM.Type, r + 1, refRev + 1));
							}
							catch (ImageErrorException e) { ImageErrorException.ExceptionMessage = e.Message; }
							break;
						}   // found the guilty
						else
							Debug.Assert(true, "Damned IAM problem");
					}	// search first error
				}   // found problem
			}	// check all rev

			Sector sector = null;
			bool dataNoId = false;
			int maxPostSize = 0;
			int lastBlock = -1;
			int lastTruncNFABlock = -1;
			int splicePosition = 0;

			if (consistentIAM) {
				// read all index field
				foreach (IAM iam in Global.Disk[track, side].iams[rev]) {
					if (iam.Type == IAMType.Id) {
						sector = new Sector();
						Global.Disk[track, side].sectors.Add(sector);
						sector.id = new IdField();
						transferInfo(iam, sector.id.info);
						readId(Global.Disk[track, side], sector.id);
					}   // id
				}

				// read all data fields
				int idCount = -1;
				for (int iamIndex = 0; iamIndex < Global.Disk[track, side].iams[rev].Count; iamIndex++) {
					IAM iam = Global.Disk[track, side].iams[rev][iamIndex];
					if (iam.Type == IAMType.Id) idCount++;
					if ((iam.Type == IAMType.Data) && (Global.Disk[track, side].sectors.Count > 0)) {
						int secIndex = (idCount < 0) ? Global.Disk[track, side].sectors.Count - 1 : idCount;
						sector = Global.Disk[track, side].sectors[secIndex];
						if (sector.data != null) {
							sector.dataNoId = new DataField();
							transferInfo(iam, sector.dataNoId.info);
							dataNoId = true;
						}   // second data
						else {
							sector.data = new DataField();
							transferInfo(iam, sector.data.info);
							dataNoId = false;
						}   // normal data
						sector.flags |= readDataCompare(track, side, rev, sector, iamIndex, dataNoId);
					}   // data IAM
				}   // all IAM
			}	// consistent

			// read track
			readTrack(track, side, rev);

			// search lastBlock
			for (int secIndex = 0; secIndex < Global.Disk[track, side].sectors.Count; secIndex++) {
				sector = Global.Disk[track, side].sectors[secIndex];
				// search biggest post gap
				if ((sector.dataNoId != null) && (sector.dataNoId.postDataGap != null)) {
					if (sector.dataNoId.postDataGap.buffer.DataByte.Count > maxPostSize) {
						maxPostSize = sector.dataNoId.postDataGap.buffer.DataByte.Count;
						lastBlock = secIndex;
					}
				}	// extra sector
				else if ((sector.data != null) && (sector.data.postDataGap != null)) {
					if (sector.data.postDataGap.buffer.DataByte.Count > maxPostSize) {
						maxPostSize = sector.data.postDataGap.buffer.DataByte.Count;
						lastBlock = secIndex;
					}
				}   // sector with data
				else {
					if (sector.id.postIdGap == null) return;
					if (sector.id.postIdGap.buffer.DataByte.Count > maxPostSize) {
						maxPostSize = sector.id.postIdGap.buffer.DataByte.Count;
						lastBlock = secIndex;
					}
				}   // sector no data
				// store info if Truncated and NFA
				if (sector.flags.HasFlag(SectorFlags.NFA_IN_SECT) && sector.flags.HasFlag(SectorFlags.TRUNC_SECT))
					lastTruncNFABlock = Global.Disk[track, side].sectors.Count - 1;
			}

			// search splice
			if (lastTruncNFABlock != -1)	// NFA win over largest gap
				lastBlock = lastTruncNFABlock;
			else if (lastBlock != -1) {
				sector = Global.Disk[track, side].sectors[lastBlock];
				GapField g = (sector.dataNoId != null) ? sector.dataNoId.postDataGap : (sector.data != null) ? sector.data.postDataGap : sector.id.postIdGap;
				if (g != null) {
					for (splicePosition = 1; splicePosition < g.buffer.DataByte.Count - 1; splicePosition++)
						if (g.buffer.DataByte[splicePosition] != 0x4E) break;
					if ((splicePosition >= (g.buffer.DataByte.Count - 1)) || (g.buffer.DataByte[splicePosition] == 0))
						splicePosition = (int)(g.buffer.DataByte.Count * 0.9);
					splicePosition += g.info.startByte - Global.Disk[track, side].track.buffer.DataByte.Count;
				}	// gap exist
			}	// block found

			int firstBlock = (lastBlock == (Global.Disk[track, side].sectors.Count - 1)) ? 0 : lastBlock + 1;
			Global.Disk[track, side].track.firstSectIndex = firstBlock;
			Global.Disk[track, side].track.splicePosition = splicePosition;

			// check protections
			Protections.checkProtection(track, side);
		}	// processTrack()


		private static void transferInfo(IAM iam, BlockInfo info) {
			info.firstIndex = iam.info.firstIndex;
			info.lastIndex = iam.info.lastIndex;
			info.startBit = iam.info.startBit;
			info.startBitIAM = iam.info.startBitIAM;
			info.startByte = iam.info.startByte;
			info.startTime = iam.info.startTime;
			info.startTimeIAM = iam.info.startTimeIAM;
		}


		/// <summary>
		/// Release Track and Sector content
		/// </summary>
		/// <param name="track">The track number</param>
		/// <param name="side">The side number</param>
		public static void resetTrack(int track, int side) {
			Global.Disk[track, side].sectors = null;
			Global.Disk[track, side].track = null;
			Global.Disk[track, side].iams = null;
			Global.Disk[track, side].hasIncompleteData = false;
			Global.Disk[track, side].hasIncompleteGap = false;
		}


		/// <summary>
		/// Find a sector by its number in a list of sectors
		/// </summary>
		/// <param name="track">The track number</param>
		/// <param name="side">The side number</param>
		/// <param name="sec">The sector number</param>
		/// <param name="idBitPosition">Minimum bit position of the sector</param>
		/// <returns></returns>
		public static Sector findSector(int track, int side, int sec) {
			List<Sector> sectors = Global.Disk[track, side].sectors;
			foreach (Sector sector in sectors) {
				if (sector.id.Sector == sec)
					return sector;
			}
			return null;
		}
	}   // class FDC
}   // namespace
