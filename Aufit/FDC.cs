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

		public GapField idGap;
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
		public GapField dataGap;
	}


	public class NonStdField {
		public byte IAM { get; set; }
		public BlockInfo info = new BlockInfo();
		//public TrackBuffer buffer = new TrackBuffer();
	}


	/// <summary>Store information about GAP fields of a sector</summary>
	public class GapField {
		public TrackBuffer buffer = new TrackBuffer();
		public BlockInfo info = new BlockInfo();
	}


	/// <summary>
	/// Sector information. Contains ID & Data fields and flags
	/// </summary>
	public class Sector {
		/// <summary>50% of bits above normal + 3.5%</summary>
		public const uint WIDTH_ABOVE = Bit.BIT_01;

		/// <summary>50% of bits below normal - 3.5%</summary>
		public const uint WIDTH_BELOW = Bit.BIT_02;

		/// <summary>Mix of bits below and above normal</summary>
		public const uint WIDTH_INTRA = Bit.BIT_03;

		/// <summary>IDAM not equal $FE</summary>
		public const uint NO_STD_IDAM = Bit.BIT_04;

		/// <summary>Wrong track number in ID</summary>
		public const uint BAD_TRK_NUM = Bit.BIT_05;

		/// <summary>Wrong head number in ID</summary>
		public const uint BAD_HEAD_NUM = Bit.BIT_06;

		/// <summary>Invalid sector number in ID</summary>
		public const uint BAD_SEC_NUM = Bit.BIT_07;

		/// <summary>Invalid sector length in ID</summary>
		public const uint BAD_SEC_LEN = Bit.BIT_08;

		/// <summary>Sector within sector (overlapping)</summary>
		public const uint SEC_IN_SEC = Bit.BIT_09;

		/// <summary>DAM not equal to $FB</summary>
		public const uint NO_STD_DAM = Bit.BIT_10;

		/// <summary>Two or more sector with same number</summary>
		public const uint DUP_SEC_NUM = Bit.BIT_11;

		/// <summary>Sector read time is 3% above normal</summary>
		public const uint LONG_SECT = Bit.BIT_12;

		/// <summary>Sector read time is 3% below normal</summary>
		public const uint SHORT_SECT = Bit.BIT_13;

		/// <summary>Sector has DM type of fuzzy byte</summary>
		public const uint DM_FUZZY = Bit.BIT_14;

		/// <summary>Sector data field but no ID field</summary>
		public const uint SEC_NO_ID = Bit.BIT_15;

		/// <summary>STruncated sector</summary>
		public const uint TRUNC_SECT = Bit.BIT_16;

		public const uint DM_HACK = Bit.BIT_30;
		public const uint SEC_EQ_TRACK = Bit.BIT_31;

		// BAD_ID_CRC => id.info.crc != 0
		// SECT_NO_DATA => data == null
		// BAD_DATA_CRC => data.info.crc != 0
		// FUZZY_SECT => data.fuzzy != null

		/// <summary>Pointer to the Id Field of a sector</summary>
		public IdField id;

		/// <summary>Pointer to the Data Field of the sector (can be null)</summary>
		public DataField data;

		public DataField dataNoId;

		/// <summary>Pointer to the Recovered Data Field of the sector (can be null)</summary>
		/// <remarks>If we find fuzzy bytes and good CRC we recover in this field</remarks>
		public DataField dataRecovered;

		public NonStdField nonStd;

		/// <summary>Sector's flags</summary>
		public uint flags;

		/// <summary>Same sector content than track at offset (Pasti optimize) </summary>
		public int trackOffset;
	}


	/// <summary>Track information</summary>
	public class Track {
		/// <summary>Track has less than 6062 bytes</summary>
		public const uint SHORT_TRACK = Bit.BIT_01;

		/// <summary>Track has more 6437 bytes</summary>
		public const uint LONG_TRACK = Bit.BIT_02;

		/// <summary>Data track detected</summary>
		public const uint DATA_TRACK = Bit.BIT_03;

		/// <summary>Track has more than 5 invalid bytes ($F5-F7) in GAP</summary>
		public const uint BAD_DATA_GAP = Bit.BIT_04;

		/// <summary>Sector numbered non sequentially</summary>
		public const uint SECTOR_SNS = Bit.BIT_05;

		/// <summary>Track contains sector with non standard size</summary>
		public const uint NOT_STD_SIZ = Bit.BIT_06;

		/// <summary>Track contains No Flux Area</summary>
		public const uint NO_FLUX_AREA = Bit.BIT_07;

		/// <summary>Shifted Track with data over index pulse</summary>
		public const uint DATA_OVER_IDX = Bit.BIT_08;

		/// <summary>Shifted Track with data after index pulse</summary>
		public const uint DATA_AFTER_IDX = Bit.BIT_09;

		/// <summary>Shifted Track with id over index pulse</summary>
		public const uint ID_OVER_IDX = Bit.BIT_10;

		/// <summary>Shifted Track with id after index pulse</summary>
		public const uint ID_AFTER_IDX = Bit.BIT_11;

		/// <summary>Hidden Data in track (DF & JMD)</summary>
		public const uint HIDDEN_DATA = Bit.BIT_12;

		/// <summary>Track has Sync sequence > 4</summary>
		public const uint INV_SYNC_SEQ = Bit.BIT_13;

		// partially formatted track
		// Fuzzy track ???

		/// <summary>buffer for all the byte read in the DATA field of the sector</summary>
		public TrackBuffer buffer = new TrackBuffer();

		/// <summary> Contains information about the field </summary>
		public BlockInfo info = new BlockInfo();

		/// <summary>Track's flags</summary>
		public uint flags;

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

	/// <summary>
	/// We raise an exception if a sector is not found in readSector()
	/// </summary>
	public class SectorNotFoundException : Exception {
		/// <summary>SectorNotFoundException Constructor</summary>
		public SectorNotFoundException() : base() { }
		/// <summary>SectorNotFoundException Constructor</summary>
		public SectorNotFoundException(string message) : base(message) { }
		/// <summary>SectorNotFoundException Constructor</summary>
		public SectorNotFoundException(string message, Exception e) : base(message, e) { }

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
							Global.Disk[track, side].track.flags |= Track.INV_SYNC_SEQ;

						// check NFA
						if ((dsr.MFM == 0) || (dsr.MFM == 0xFFFF))
							nfaCount++;
						else {
							if (nfaCount > 8)
								Global.Disk[track, side].track.flags |= Track.NO_FLUX_AREA;
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
									int s = (currentSector != -1) ? currentSector : dt.sectors.Count - 1;
									// ===================== TODO CHECK => optimize for pasti ====================================================
									dt.sectors[s].trackOffset = bti.ByteCount + 1;
									byteLeftInBlock = Global.sectSize(dt.sectors[s].id.Length) + 3; // AM + 2 CRC + Size
									phase = Phases.data;
									syncCount = 0;
									if (dt.sectors[s].data != null) {
										if (!dataNoId) dataNoId = true;
										else dataNoId = false;
									}   // sector has data
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
											dt.sectors[s].flags |= Sector.SEC_EQ_TRACK;
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
				Global.Disk[track, side].track.flags |= Track.BAD_DATA_GAP;
		}   // end of readTrack()


		/// <summary>Read and Store IAM info and store ID content for complete track</summary>
		/// <param name="track">The track index</param>
		/// <param name="side">The side index</param>
		/// <param name="rev">The revolution index</param>
		public static List<Sector> readIAM(int track, int side, int rev) {
			Global.DiskTrack dt = Global.Disk[track, side];
			int start = Global.firstIndex(track, side, rev);
			int stop = Global.lastIndex(track, side, rev);

			List<Sector> sectors = new List<Sector>();
			Sector sector = null;
			int byteLeftInBlock = 0;
			ushort crc = 0;
			int currentIndex;
			DataField saveData = null;
			Phases phase = Phases.postDataGap;

			DSR dsr = new DSR();
			dsr.sectorMode();
			BitTrackInfo bti = new BitTrackInfo(dsr);

			// We read from the beginning to end of a track looking only for all IAM in the current revolution.
			for (currentIndex = start; (currentIndex < stop) || (phase != Phases.postDataGap) || (dsr.SyncSeq > 0); currentIndex++) {
				if (currentIndex == (stop - 2))
					Global.Disk[track, side].bitCount = bti.BitCount;
				// break immediately if we are beyond buffer
				if (currentIndex >= dt.fluxData.fluxValue.Count()) {
					if ((phase == Phases.id) || (dsr.SyncSeq > 0)) {
						Global.Disk[track, side].hasIncompleteData = true;
						if (phase == Phases.id)
							bti.storeEndInfo(sector.id.info, false);
					}
					else if ((phase == Phases.postIdGap) && (sector.id.idGap != null)) {
						Global.Disk[track, side].hasIncompleteGap = true;
						bti.storeEndInfo(sector.id.idGap.info, false);
					}
					break;
				}

				int bitSpacing = bti.nextFlux(dt, currentIndex);
				while (bitSpacing-- > 0) {
					dsr.shiftIn(bitSpacing > 0 ? 0 : 1);
					bti.nextBit();

					if (dsr.DataReady) {
						bti.nextByte();

						if (phase != Phases.id) { // we are in GAP
							if ((phase == Phases.postIdGap) && (sector != null) && (sector.id.idGap != null))
								bti.addByte(sector.id.idGap.buffer, phase, false);

							if (dsr.IAM) {  // IAM position
								if ((phase == Phases.postIdGap) && (sector != null) && (sector.id.idGap != null)) {
									sector.id.idGap.buffer.removeLastBytes(4);
									bti.storeEndInfo(sector.id.idGap.info, true);
								}

								// test ID AM in range 0xFC - 0xFF
								if (Enumerable.Range(0xFC, 4).Contains(dsr.Data)) {
									if (currentIndex > stop) {
										phase = Phases.postDataGap;
										break;
									}
									sector = new Sector();
									sectors.Add(sector);
									sector.id = new IdField();
									byteLeftInBlock = 7;
									phase = Phases.id;
									bti.storeStartInfo(sector.id.info, true);
									bti.addByte(sector.id.buffer, phase, true);
								}   // IDAM

								// test Data AM in range 0xF8 - 0xFB
								else if (Enumerable.Range(0xF8, 4).Contains(dsr.Data)) {
									DataField data = new DataField();
									bti.storeStartInfo(data.info, true);
									if (sectors.Count == 0)
										saveData = data;
									else if (sector.data == null)
										sector.data = data;
									else
										sector.dataNoId = data;
									phase = Phases.postDataGap;
									dsr.sectorMode();
								}   // DAM

								else {
									//sector = new Sector();
									//sectors.Add(sector);
									//sector.nonStd = new NonStdField();
									//sector.nonStd.IAM = dsr.Data;
									//bti.storeStartInfo(sector.nonStd.info, true);

									phase = Phases.postDataGap;
									dsr.sectorMode();
								}   // non standard IAM
							}   // IAM position
						}   // we are in GAP

						else {  // we are in ID
							byteLeftInBlock--;
							if (byteLeftInBlock > 0) {
								bti.addByte(sector.id.buffer, phase, false);
								if (byteLeftInBlock == 1) crc = dsr.CRC;
							}   // not last byte
							else {
								dsr.sectorMode();   // we re-enable sync
								phase = Phases.postIdGap;
								sector.id.info.crc = crc;
								bti.storeEndInfo(sector.id.info, false);
								// if we have id and data back to back the first sync is not detected (vroom)
								if (dsr.Data == 0xA1) dsr.SyncSeq = 1;
								sector.id.idGap = new GapField();
								bti.storeStartInfo(sector.id.idGap.info, false);
								bti.addByte(sector.id.idGap.buffer, phase, false);
							}   // byteInBlock == 0
						} // we are in ID
					}   // one byte ready
				}   // shift all bits
			}   // for all index

			if (saveData != null) sector.data = saveData;

			// since we now continue when finding sync over index it is possible to have duplicate sector in first last position
			if ((sectors.Count > 0) && (sectors[0].id.Sector == sectors[sectors.Count - 1].id.Sector) &&
				(sectors[sectors.Count - 1].id.info.startTime > Global.Disk[track, side].fluxDataRev[rev].revolutionTime)) {
				sectors.RemoveAt(sectors.Count - 1);
				Debug.Assert(true, String.Format("Remove duplicate sector {0} (should not happen ignore)", sectors[0].id.Sector));
			}

			return sectors;
		}   // end of readAddr()


		public static uint readData(Global.DiskTrack dt, DataField data, int size) {
			Phases phase = Phases.postIdGap;
			DSR dsr = new DSR();
			dsr.sectorMode();
			BitTrackInfo bti = new BitTrackInfo(dsr);
			bool doLoop = true;
			int byteLeftInBlock = 0;
			int widthOver = 0;
			int widthUnder = 0;
			ushort crc = 0;
			uint flags = 0;
			bool alreadyFound = false;

			// due to random noise in front of the searched sector we may have variations
			int start = data.info.firstIndex - 150;
			if (start < 0) start = 0;

			for (int currentIndex = start; doLoop; currentIndex++) {
				if (currentIndex >= dt.fluxData.fluxValue.Count()) {
					if (phase == Phases.data) {
						//dt.hasIncompleteData = true;
						bti.storeEndInfo(data.info, false);
					}
					else if ((phase == Phases.postDataGap) && (data.dataGap != null)) {
						//dt.hasIncompleteGap = true;
						bti.storeEndInfo(data.dataGap.info, false);
					}
					break;
				}

				int bitSpacing = bti.nextFlux(dt, currentIndex);
				while (bitSpacing-- > 0) {
					dsr.shiftIn(bitSpacing > 0 ? 0 : 1);
					bti.nextBit();

					if (dsr.DataReady) {
						bti.nextByte();

						if (phase != Phases.data) { // we are in GAP
							if (phase == Phases.postDataGap)
								bti.addByte(data.dataGap.buffer, phase, false);

							if (dsr.IAM) {  // IAM position
								if (phase == Phases.postDataGap) {
									data.dataGap.buffer.removeLastBytes(4);
									bti.storeEndInfo(data.dataGap.info, true);
								}

								// test Data AM (range 0xF8 - 0xFB)
								if (Enumerable.Range(0xF8, 4).Contains(dsr.Data)) {
									if (!alreadyFound) {
										bti.setInfo(data.info, currentIndex);           // we are synchronized set running values
										bti.addByte(data.buffer, Phases.data, true);    // we add the sync seq
										byteLeftInBlock = size;
										phase = Phases.data;
										widthOver = widthUnder = 0;
										alreadyFound = true;
									}
									else
										doLoop = false; // for vroom
								}   // Data AM

								else {  // all other AM => stop
									if (data.dataGap != null)
										bti.storeEndInfo(data.dataGap.info, true);
									doLoop = false;
								}   // not Data AM

							}   // IAM
						}   // we are in GAP

						else {  // we are in DATA
							byteLeftInBlock--;
							switch (byteLeftInBlock) {
								case 1:
									bti.addByte(data.buffer, phase, false);
									crc = dsr.CRC;
									break;

								case 0: // end of DATA block
									data.info.crc = crc;
									bti.storeEndInfo(data.info, false);
									dsr.sectorMode();

									if ((widthOver > (size / 2)) || (widthUnder > (size / 2))) {
										if (widthOver > (size / 2)) flags |= Sector.WIDTH_ABOVE;
										if (widthUnder > (size / 2)) flags |= Sector.WIDTH_BELOW;
									}
									else if ((widthOver > 0) && (widthUnder > 0) && ((widthOver + widthUnder) > (size / 4)))
										flags |= Sector.WIDTH_INTRA;

									phase = Phases.postDataGap;
									// if we have id and data back to back the first sync is not detected (vroom)
									if (dsr.Data == 0xA1) dsr.SyncSeq = 1;
									data.dataGap = new GapField();
									bti.storeStartInfo(data.dataGap.info, false);
									bti.addByte(data.dataGap.buffer, phase, false);
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
		public static uint readSectorCompare(int track, int side, int rev, Sector sector, bool dataNoId) {
			Global.DiskTrack dt = Global.Disk[track, side];
			int size = Global.sectSize(sector.id.Length) + 3; // AM + 2 CRC + Size
			DataField data = dataNoId ? sector.dataNoId : sector.data;

			uint flags = readData(dt, data, size);
			int relativeFirstIndex = data.info.firstIndex - Global.firstIndex(track, side, rev);
			for (int j = 0; j < Global.Disk[track, side].fluxDataRev.Count(); j++) {
				if (j == rev) continue;
				DataField dataRev = new DataField();
				dataRev.info.firstIndex = Global.firstIndex(track, side, j) + relativeFirstIndex;
				readData(dt, dataRev, size);
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
			}
			return flags;
		}


		public static void processTrack(int track, int side, int rev) {
			if ((Global.Disk[track, side].sectors != null) &&
				(Global.Disk[track, side].sectors.Count > 0))
				return;
			if (Global.Disk[track, side].fluxData == null) return;

			// find all IAM
			Global.Disk[track, side].sectors = readIAM(track, side, rev);
			Global.DiskTrack dt = Global.Disk[track, side];

			for (int current = 0; current < dt.sectors.Count; current++) {
				Sector sector = dt.sectors[current];
				if (sector.data != null) {
					sector.flags |= readSectorCompare(track, side, rev, sector, false);
					int next = (current == (dt.sectors.Count - 1)) ? 0 : current + 1;
					int dataEnd = sector.data.info.startBit + sector.data.info.bitCount;
					int nextIdStart = dt.sectors[next].id.info.startBit;
					if (nextIdStart < sector.data.info.startBit)
						nextIdStart += dt.bitCount;
					if (dataEnd > nextIdStart) {
						sector.flags |= Sector.TRUNC_SECT;
						sector.data.truncBitSize = nextIdStart - sector.data.info.startBitIAM;
						sector.data.dataGap = null;
					}   // truncated
				}   // sector has data

				if (sector.dataNoId != null)
					readSectorCompare(track, side, rev, sector, true);
			}   // all sectors

			readTrack(track, side, rev);
			trackPositionAndSplice(track, side);
			Protections.checkProtection(track, side);

		}


		/// <summary>
		/// Find the first sector and position of track write splice
		/// </summary>
		/// <param name="track">Track number</param>
		/// <param name="side">Side number</param>
		/// <remarks>We search the first and last block of track and fill the firstSectIndex in Track structure.
		/// Then we find the write splice located at the end of the last block by searching the first non 4E char.
		/// This information is stored in the splicePosition field of Track structure. This information can be
		/// used to find if the track is shifted or not.</remarks>
		private static void trackPositionAndSplice(int track, int side) {
			Global.DiskTrack dt = Global.Disk[track, side];
			if ((dt.sectors == null) || (dt.sectors.Count == 0)) {
				Global.Disk[track, side].track.splicePosition = 0;
				return;
			}

			int maxPostSize = 0;
			int lastBlock = 0;
			int splicePosition = 0;
			Sector sector;

			//
			// we search the last block of the track
			//
			for (int i = 0; i < dt.sectors.Count; i++) {
				sector = dt.sectors[i];

				// if track has NFA and SWS the last truncated sect is the last block
				if ((dt.track.flags & Track.NO_FLUX_AREA) != 0) {
					for (lastBlock = dt.sectors.Count - 1; lastBlock >= 0; lastBlock--)
						if ((dt.sectors[lastBlock].flags & Sector.TRUNC_SECT) != 0)
							break;
					splicePosition = (dt.sectors[lastBlock].data.info.startBit + dt.sectors[lastBlock].data.truncBitSize - dt.track.info.bitCount) / 16;
					break;
				}   // NFA

				else {
					// for now we take the sector that have the largest post gap size
					if ((sector.data != null) && (sector.data.dataGap != null)) {
						if (sector.data.dataGap.buffer.DataByte.Count > maxPostSize) {
							maxPostSize = sector.data.dataGap.buffer.DataByte.Count;
							lastBlock = i;
						}
					}   // sector with data
					else {
						if (sector.id.idGap == null) return;
						if (sector.id.idGap.buffer.DataByte.Count > maxPostSize) {
							maxPostSize = sector.id.idGap.buffer.DataByte.Count;
							lastBlock = i;
						}
					}   // sector no data

					// search first non 4E byte
					sector = dt.sectors[lastBlock];
					GapField g = (sector.data != null) ? sector.data.dataGap : sector.id.idGap;
					if (g != null) {
						for (splicePosition = 1; splicePosition < g.buffer.DataByte.Count - 1; splicePosition++)
							if (g.buffer.DataByte[splicePosition] != 0x4E) break;
						if ((splicePosition >= (g.buffer.DataByte.Count - 1)) || (g.buffer.DataByte[splicePosition] == 0))
							splicePosition = (int)((double)g.buffer.DataByte.Count * 0.9);
						splicePosition += g.info.startByte - dt.track.buffer.DataByte.Count;
					}
				}   // not NFA

			}   // for all sectors

			int firstBlock = (lastBlock == (dt.sectors.Count - 1)) ? 0 : lastBlock + 1;
			Global.Disk[track, side].track.firstSectIndex = firstBlock;
			Global.Disk[track, side].track.splicePosition = splicePosition;
		}


		/// <summary>
		/// Release Track and Sector content
		/// </summary>
		/// <param name="track">The track number</param>
		/// <param name="side">The side number</param>
		public static void resetTrack(int track, int side) {
			Global.Disk[track, side].sectors = null;
			Global.Disk[track, side].track = null;
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
