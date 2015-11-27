/*!
@file FDC.cs
 * 
<summary>Emulate WD1772 FDC commands: read track, read address, read sector and more</summary>

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
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Windows.Media;

using KFStreamPackage;
using GlobalPackage;
using Aufit;
using System.Windows.Controls;
using System.Windows;

namespace WD1772Package {
	/// <summary>Different phases of a sector: ID, Inner Gap, Data, Post GAP</summary>
	public enum Phases : byte {
		postDataGap,	///<! Post Data GAP
		postIdGap,		///!< GAP between ID and DATA
		id,				///<! ID (Address) block
		data			///<! DATA block
	}


	/// <summary>Store information about a block of data</summary>
	public class BlockInfo {
		/// <summary>First byte of the block</summary> 
		public int startByte;

		/// <summary>First bit of the block</summary> 
		public int startBit;
		public int startBitIAM;
		public int startTimeIAM;

		/// <summary>Number of bits (clock + data) in the block</summary>
		public int bitCount;

		/// <summary>Starting Time of the block in nanoseconds from index</summary>
		public int startTime;

		/// <summary>Length of the Block in nanoseconds </summary>
		public int timeLength;

		public int startIndex;
		public int lastIndex;

		/// <summary>CRC value at the end of reading an ID or DATA block.</summary>
		/// <remarks> Should be zero on a "good" block</remarks>
		public ushort crc;
	}


	/// <summary>Store information about the ID Field of a Sector</summary>
	public class IdField {
		/// <summary>Returns the Index Address Mark</summary> 
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

		public GapField gap;
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
					(int)(buffer.DataByte[buffer.DataByte.Count - 1] << 8)) : (ushort)0 ;
			}
		}

		/// <summary>buffer for all the byte read in the DATA field of the sector</summary>
		/// <remarks>The buffer contains the DAM and CRC. Therefore when writing sector data this must be removed</remarks>
		public TrackBuffer buffer = new TrackBuffer();

		/// <summary>Array of bytes to store fuzzy mask</summary> 
		public byte[] fuzzy;

		/// <summary>information about the data field </summary>
		public BlockInfo info = new BlockInfo();

		public int truncDataBitSize;
		public GapField gap;
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
		public const uint SEC_PROCESSED = Bit.BIT_32;
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

		/// <summary>the content of the sector start at this track offset</summary>
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

		/// <summary>
		/// Index of the first sector of the track
		/// </summary>
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

	/// <summary>
	/// Handles repetitive actions during FDC read functions
	/// </summary>
	/// <remarks>We use ring buffers to handle the 4 last bytes with start information. 
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

		private int _bitWidthSum;
		private int _bitWidthCount;
		private ushort _bitWidthAvg;
		private int _fluxTimeInc;
		private int _fluxRem;
		DSR _dsr;

		byte[] _dataBuffer = new byte[4];
		ushort[] _mfmBuffer = new ushort[4];
		int _ringIndex;

		public int ByteCount {
			get { return _byteCount; }
		}

		public int BitCount {
			get { return _runningBit; }
		}

		public int BitWidthAvg {
			get { return _bitWidthAvg; }
		}

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
			// count can be null due to NFA - TODO check ???
			if (addSync) {
				for (int i = 1; i < 4; i++) {
					int x = (_ringIndex + i) % 4;
					b.addByte(_dataBuffer[x], _mfmBuffer[x], _bitWidthAvg, _currentTime[x], phase);
				}
			}
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
			bi.startIndex = _currentIndex[x];
		}

		public void storeEndInfo(BlockInfo bi, bool removeSync) {
			int x = (removeSync) ? (_ringIndex + 1) % 4 : _ringIndex;
			bi.timeLength = _currentTime[x] - bi.startTime;
			bi.bitCount = _currentBit[x] - bi.startBit;
			bi.lastIndex = _currentIndex[x];
		}

		public void setInfo(BlockInfo bi, int currentIndex) {
			_runningIndex = currentIndex;
			_runningBit = bi.startBitIAM;
			_runningTime = bi.startTimeIAM;
		}
	}


	/// <summary>Emulate some WD1772 FDC Commands</summary>
	public class FDC {
		// 2000 +/- 3.5%
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
			Phases currentPhase = Phases.postDataGap;
			int currentSector = -1;
			int byteLeftInBlock = 0;
			int syncCount = 0;
			bool data2 = false;
			bti.storeStartInfo(Global.Disk[track, side].track.info, false);
			Global.Disk[track, side].track.info.startIndex = start;

			// We read from the beginning to end of a track looking with AM decoder on all time
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
								case 0xFC: case 0xFD: case 0xFE: case 0xFF:
									if (dt.track.firstSyncOffset == 0)
										Global.Disk[track, side].track.firstSyncOffset = bti.ByteCount - 3;
									currentSector++;
									byteLeftInBlock = 7;
									currentPhase = Phases.id;
									data2 = false;
									break;

								// Case DATA field
								case 0xF8: case 0xF9: case 0xFA: case 0xFB:
									int s = (currentSector != -1) ? currentSector : dt.sectors.Count - 1;
									byteLeftInBlock = Global.sectSize(dt.sectors[s].id.Length) + 3; // AM + 2 CRC + Size
									currentPhase = Phases.data;
									syncCount = 0;
									if (dt.sectors[s].data != null) {
										if (!data2) data2 = true;
										else data2 = false;
									}   // sector has data
									break;

								// other = data track ?
								default:
									// TODO
									break;
							}   // switch on IAM

						else  // not IAM
							switch (currentPhase) {
								case Phases.id:
									byteLeftInBlock--;
									if (byteLeftInBlock == 0) 
										currentPhase = Phases.postIdGap;
									break;

								case Phases.data:
									byteLeftInBlock--;
									if (byteLeftInBlock == 0) {
										int s = (currentSector != -1) ? currentSector : dt.sectors.Count - 1;
										currentPhase = Phases.postDataGap;
										if (data2 && (syncCount == 0))
											dt.sectors[s].flags |= Sector.SEC_EQ_TRACK;
									}	// last byte
									break;

								default:    // gap
									if ((dsr.Data >= 0xF5) && (dsr.Data <= 0xF7))
										invalidByteInGap++;
									break;
							}   // not IAM

						bti.addByte(Global.Disk[track, side].track.buffer, currentPhase, false);
					}   // data ready
				}   // shift bit in
			}   // for all bits

			bti.storeEndInfo(Global.Disk[track, side].track.info, false);

			if (invalidByteInGap > 5)
				Global.Disk[track, side].track.flags |= Track.BAD_DATA_GAP;
		}   // end of readTrack()


		public static void readTemplate(int track, int side, int rev) {
			Global.DiskTrack dt = Global.Disk[track, side];
			int start = Global.firstIndex(track, side, rev);
			int stop = Global.lastIndex(track, side, rev);

			Phases phase = Phases.postDataGap;
			DSR dsr = new DSR();
			dsr.sectorMode();

			BitTrackInfo bti = new BitTrackInfo(dsr);

			for (int currentIndex = start; (currentIndex < stop); currentIndex++) {

				int bitSpacing = bti.nextFlux(dt, currentIndex);

				while (bitSpacing-- > 0) {
					dsr.shiftIn(bitSpacing > 0 ? 0 : 1);
					bti.nextBit();

					if (dsr.DataReady) {
						bti.nextByte();

						IdField id = new IdField();
						bti.storeStartInfo(id.info, false);
						bti.addByte(id.buffer, phase, false);
						bti.storeEndInfo(id.info, false);
					}   // one byte ready

				}   // shift in all bits;
			}   // for all index
		}


		/// <summary>Read all the IAM fields of a track</summary>
		/// <param name="track">The track index</param>
		/// <param name="side">The side index</param>
		/// <param name="rev">The revolution index</param>
		/// <remarks>Reads all the IAM of a track</remarks>
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

			// We read from the beginning to end of a track looking only for all IAM in current revolution.
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
					else if ((phase == Phases.postIdGap) && (sector.id.gap != null)) {
						Global.Disk[track, side].hasIncompleteGap = true;
						bti.storeEndInfo(sector.id.gap.info, false);
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
							if ((phase == Phases.postIdGap) && (sector != null) && (sector.id.gap != null))
								bti.addByte(sector.id.gap.buffer, phase, false);

							if (dsr.IAM) {	// IAM position
								if ((phase == Phases.postIdGap) && (sector != null) && (sector.id.gap != null)) {
									sector.id.gap.buffer.removeLastBytes(4);
									bti.storeEndInfo(sector.id.gap.info, true);
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
								}	// DAM

								else {
									//sector = new Sector();
									//sectors.Add(sector);
									//sector.nonStd = new NonStdField();
									//sector.nonStd.IAM = dsr.Data;
									//bti.storeStartInfo(sector.nonStd.info, true);

									phase = Phases.postDataGap;
									dsr.sectorMode();
								}	// non standard IAM
							}   // IAM position
						}   // we are in GAP

						else {	// we are in ID
							byteLeftInBlock--;
							if (byteLeftInBlock > 0) {
								bti.addByte(sector.id.buffer, phase, false);
								if (byteLeftInBlock == 1) crc = dsr.CRC;
							}	// not last byte
							else {
								dsr.sectorMode();   // we re-enable sync
								phase = Phases.postIdGap;
								sector.id.info.crc = crc;
								bti.storeEndInfo(sector.id.info, false);
								// if we have id and data back to back the first sync is not detected (vroom)
								if (dsr.Data == 0xA1) dsr.SyncSeq = 1;
								sector.id.gap = new GapField();
								bti.storeStartInfo(sector.id.gap.info, false);
								bti.addByte(sector.id.gap.buffer, phase, false);
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
				Debug.Assert(true, String.Format("Remove duplicate sector {0} (ignore)", sectors[0].id.Sector));
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

			int start = data.info.startIndex - 20;
			if (start < 0) start = 0;

            for (int currentIndex = start; doLoop; currentIndex++) { 
				int bitSpacing = bti.nextFlux(dt, currentIndex);

				while (bitSpacing-- > 0) {
					dsr.shiftIn(bitSpacing > 0 ? 0 : 1);
					bti.nextBit();

					if (dsr.DataReady) {
						bti.nextByte();

						if (phase != Phases.data) { // we are in GAP
							if (phase == Phases.postDataGap)
								bti.addByte(data.gap.buffer, phase, false);

							if (dsr.IAM) {  // IAM position
								if (phase == Phases.postDataGap) {
									data.gap.buffer.removeLastBytes(4);
									bti.storeEndInfo(data.gap.info, true);
								}

								// test Data AM (range 0xF8 - 0xFB)
								if (Enumerable.Range(0xF8, 4).Contains(dsr.Data)) {
									if (!alreadyFound) {										
										bti.setInfo(data.info, currentIndex);			// we are synchronized set running values
										bti.addByte(data.buffer, Phases.data, true);	// we add the sync seq
										byteLeftInBlock = size;
										phase = Phases.data;
										widthOver = widthUnder = 0;
										alreadyFound = true;
									}
									else
										doLoop = false;
								}	// Data IAM
								else {  // all other AM => stop
									if (data.gap != null)
										bti.storeEndInfo(data.gap.info, true);
									doLoop = false;
								}   // not DAM
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

									if ( (widthOver > (size / 2)) || (widthUnder > (size / 2)) ) {
										if (widthOver > (size / 2))		flags |= Sector.WIDTH_ABOVE;
										if (widthUnder > (size / 2))	flags |= Sector.WIDTH_BELOW;
									}
									else if ((widthOver > 0) && (widthUnder > 0) && ((widthOver + widthUnder) > (size / 4)))
										flags |= Sector.WIDTH_INTRA;

									phase = Phases.postDataGap;
									// if we have id and data back to back the first sync is not detected (vroom)
									if (dsr.Data == 0xA1) dsr.SyncSeq = 1;
									data.gap = new GapField();
									bti.storeStartInfo(data.gap.info, false);
									bti.addByte(data.gap.buffer, phase, false);
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




		public static uint readSectorCompare (int track, int side, int rev, Sector sector, bool dataNoId) {
			Global.DiskTrack dt = Global.Disk[track, side];
			int size = Global.sectSize(sector.id.Length) + 3; // AM + 2 CRC + Size
			DataField data = dataNoId ? sector.dataNoId : sector.data;

			uint flags = readData(dt, data, size);
			for (int j = 0; j < Global.Disk[track, side].fluxDataRev.Count(); j++) {
				if (j == rev) continue;
				DataField dataRev = new DataField();
				int indexIncrement = Global.firstIndex(track, side, rev + 1) - Global.firstIndex(track, side, rev);
				dataRev.info.startIndex = data.info.startIndex + indexIncrement;
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
				}	// same size
			}
			return flags;		
		}


		/// <summary>
		/// Reads the DATA field of a specified sector
		/// </summary>
		/// <param name="track">Track</param>
		/// <param name="side">Side</param>
		/// <param name="rev">Revolution value</param>
		/// <param name="sectToRead">Sector number to read (only used when idBitPosition is not negative.</param>
		/// <param name="idBitPosition">If positive value the sector to find must be located just after 
		/// the specified bit position. If negative this means we try to read all sector at once</param>
		/// <param name="compare">If set to true we are in compare mode, else we are in read mode</param>
		/// <remarks>The position parameter is necessary to read the right data of a duplicated sector
		/// (i.e. we have several sectors with same number). Otherwise the data for the first encountered
		/// sector would always be returned (equivalent to position==0). \n
		/// To speedup reading sectors this parameter can be set to a negative value. In this case we try
		/// to read all sectors in one revolution. But as we may have sector within sector it is possible 
		/// that some of the sectors are not read in one pass. This can be checked with the SEC_PROCESSED flag. 
		/// For all sectors not read the function must be called again this time using a positive idBitPosition value.\n
		/// When compare is false we are reading the sector normally and we fill the Sector structure. 
		/// But if compare==true we are in "compare" mode. In that case we read the same sector using the rev parameter 
		/// and we compare the data buffer read with the "reference" data buffer to fill the fuzzy structure.\n
		/// NEW for being able to retrieve master information: We are now storing the GAP bytes between ID and DATA 
		/// (inner gap) and beyond DATA (post gap). These values are useful to write the master information. 
		/// With the WD1772 it is not possible to read this kind of data because during a read track the data 
		/// are often shifted due to presence of false 0x5224 sync mark. /// </remarks>
		public static void readSector(int track, int side, int rev, byte sectToRead, int idBitPosition, bool compare) {
			Global.DiskTrack dt = Global.Disk[track, side];
			int start = Global.firstIndex(track, side, rev);
			int stop = Global.lastIndex(track, side, rev);

			int byteLeftInBlock = 0;
			//int currentTime = 0;
			//int currentBit = 0;
			//int byteRead = 0;
			ushort crc = 0;
			bool idFound = false;
			int widthOver = 0;
			int widthUnder = 0;
			Phases phase = Phases.postDataGap;
			uint flags = 0;
			int idCount = 0;
			//int bitWidthSum = 0;
			//int bitWidthCount = 0;
			//int startIndex = 0;

			Sector sector = null;
			if (idBitPosition >= 0) {
				sector = findSector(track, side, sectToRead, idBitPosition);
				Debug.Assert(sector != null, "Sector not found in readSector",
					String.Format("Track {0} Side {1} sector {2}", track, side, sectToRead));
			}   // one sector read mode

			DSR dsr = new DSR();
			dsr.sectorMode();
			BitTrackInfo bti = new BitTrackInfo(dsr);
			DataField data = null;

			// in the new loop scheme to read gap data we start from startBit
			// To read the gap data of the last sector correctly we need to read past the end of
			// the current revolution until we find the next ID. If we reach the end of data before
			// we set the hasIncompleteGap to true and terminate the GapField content.
			//
			// * in single sector mode: we search the ID field for the sector to read, we skip the id 
			//	content, we read the inner gap, and if we find a data field we read it and we also 
			//	read the post gap data. In either case we terminate the Gap field (inner or post)
			//	and we terminate the loop when we find the next ID field.
			//	
			// * in multiple sector mode: we detect the first ID and find the corresponding sector, 
			//	we skip the id content, we read the inner gap, and if we find a data field we read it 
			//	as well as the post gap data. When we find the next ID we terminate the Gap field 
			//	(inner or post) and we process the next sector as described above for the firs sector.
			//	We continue the loop until the detected ID is beyond the end of current revolution.
			bool breakLoop = false;
			bool data2 = false;
			for (int currentIndex = start; !breakLoop; currentIndex++) {
				//if (startIndex == 0) startIndex = i;
				if (currentIndex >= dt.fluxData.fluxValue.Count()) {
					// We have reach the end of the buffer
					if (!compare) {
						if ((phase == Phases.postDataGap) || (phase == Phases.postIdGap)) {
							Global.Disk[track, side].hasIncompleteGap = true;
							if ((phase == Phases.postDataGap) && (sector.dataNoId.gap != null))
								bti.storeEndInfo(sector.dataNoId.gap.info, false);
							else if ((phase == Phases.postDataGap) && (sector.data.gap != null))
								bti.storeEndInfo(sector.data.gap.info, false);
							else
								bti.storeEndInfo(sector.id.gap.info, false);
						}
						else
							Global.Disk[track, side].hasIncompleteData = true;
					}
					break;
				}   // end of buffer

				int bitSpacing = bti.nextFlux(dt, currentIndex);

				while (bitSpacing-- > 0) {
					dsr.shiftIn(bitSpacing > 0 ? 0 : 1);
					bti.nextBit();

					if (dsr.DataReady) {
						bti.nextByte();

						if ((phase != Phases.id) && (phase != Phases.data)) {
							//////////////////
							// We are in a GAP
							//////////////////

							// if we have found at least an ID we accumulate the GAP data
							if ((phase == Phases.postIdGap) && (idCount > 0) && (sector != null) && (sector.id.gap != null) && !compare)
								bti.addByte(sector.id.gap.buffer, phase, false);
							else if ((idCount > 0) && (sector != null) && (sector.dataNoId != null) && (sector.dataNoId.gap != null) && !compare)
								bti.addByte(sector.dataNoId.gap.buffer, phase, false);
							else if ((idCount > 0) && (sector != null) && (sector.data != null) && (sector.data.gap != null) && !compare)
								bti.addByte(sector.data.gap.buffer, phase, false);

							if (dsr.IAM) {
								// here we remove the three sync and the IAM
								if ((phase == Phases.postIdGap) && (idCount > 0) && (sector != null) && (sector.id.gap != null) && !compare)
									sector.id.gap.buffer.removeLastBytes(4);
								else if ((idCount > 0) && (sector != null) && (sector.dataNoId != null) && (sector.dataNoId.gap != null) && !compare)
									sector.dataNoId.gap.buffer.removeLastBytes(4);
								else if ((idCount > 0) && (sector != null) && (sector.data != null) && (sector.data.gap != null) && !compare)
									sector.data.gap.buffer.removeLastBytes(4);

								switch (dsr.Data) {
									// Case ID field //		
									case 0xFC: case 0xFD: case 0xFE: case 0xFF:								
										if ((sector != null) && !compare) {	// terminate gap block
											if ((sector.dataNoId != null) && (sector.dataNoId.gap != null) && (phase == Phases.postDataGap))												
												bti.storeEndInfo(sector.dataNoId.gap.info, true);
											else if ((sector.data != null) && (sector.data.gap != null) && (phase == Phases.postDataGap)) 
												bti.storeEndInfo(sector.data.gap.info, true);
											else if ((sector.id.gap != null))
												bti.storeEndInfo(sector.id.gap.info, true);
										}   // terminate gap

										if (idBitPosition < 0) {	// read multiple sector mode
											// if not found and past index we have no data (except if very close to index)									
											if ((currentIndex - 8) > stop) {    // ================> TODO < ====================== check
											//if ((runningTime - Global.Disk[track, side].fluxDataRev[rev].revolutionTime) > 32000) {
												breakLoop = true;
												break;  // break switch statement
											}

											// search the corresponding sector -- start position can vary from rev to rev
											sector = null;
											foreach (Sector s in Global.Disk[track, side].sectors)
												if (Math.Abs(bti.BitCount - s.id.info.startBitIAM) < 300) {
													sector = s;
													break;  // break loop
												}

											if (sector == null) {
												throw new SectorNotFoundException(
													String.Format("*** Sector not found exception T{0:D2}.{1:D1} @{2} Sect count={3} *** switching to safe mode",
													track, side, bti.ByteCount, Global.Disk[track, side].sectors.Count));
											}

											sector.flags |= Sector.SEC_PROCESSED;
											flags = 0;

											// ready to read the ID block
											byteLeftInBlock = 7;
											phase = Phases.id;
										}   // read multiple sectors mode

										else {	// read one sector mode											
											if (idFound) {	// if we have already found the requested ID we break
												phase = Phases.id;
												breakLoop = true;   // don't need to continue
												break;
											}

											// if we have not yet reach the requested position we ignore
											if ((bti.BitCount + 16) < idBitPosition) { 
												dsr.sectorMode();
												phase = Phases.postDataGap;
												break;
											}

											// otherwise we need to read the ID block
											byteLeftInBlock = 7;
											phase = Phases.id;
											sector.flags |= Sector.SEC_PROCESSED;
											flags = 0;
										}   // read one sector mode
										break;	

									// case DATA field
									case 0xF8: case 0xF9: case 0xFA: case 0xFB:
										// If we have found the right ID field we check that this Data field is less than 
										// 43 bytes apart from the Id. Id field is already 7 bytes 43 + 7 = 50
										if (idFound && ((bti.ByteCount - sector.id.info.startByte) <= 60)) { 
											data2 = false;
											data = new DataField();
											byteLeftInBlock = Global.sectSize(sector.id.Length) + 3; // AM + 2 CRC + Size
											phase = Phases.data;
											bti.addByte(data.buffer, phase, true);      // we add the sync seq
											widthOver = 0;
											widthUnder = 0;

											if (!compare)
												bti.storeEndInfo(sector.id.gap.info, true);
											bti.storeStartInfo(data.info, true);
										}   // normal data block

										else if ((idCount > 0) && (sector.data != null) && (SectorNotFoundException.ExceptionMessage == null) ) {
											data2 = true;
											sector.flags |= Sector.SEC_NO_ID;
											data = new DataField();
											byteLeftInBlock = Global.sectSize(sector.id.Length) + 3; // AM + 2 CRC + Size
											phase = Phases.data;
											bti.addByte(data.buffer, phase, true);     // we add the sync seq
											widthOver = 0;
											widthUnder = 0;

											if (!compare)
												bti.storeEndInfo(sector.id.gap.info, true);
											bti.storeStartInfo(data.info, true);
										}   //	second data sector (sector with no id)
										else {
											dsr.sectorMode();
											phase = Phases.postDataGap;
										}	// no data
										break;

									default:
										// other AM re-enable sync search
										dsr.sectorMode();
										break;
								}   // switch AM
							}   // IAM
						}   // GAP

						else {
							//////////////////////////////////
							// We are in an ID or DATA segment
							//////////////////////////////////
							byteLeftInBlock--;
							if (phase == Phases.id)
								// ID segment
								switch (byteLeftInBlock) {
									case 4:
										if (idBitPosition < 0) {
											idFound = true; // used to read data
										}   // read multiple sectors
										else {
											// we only check if we have the right sector number
											if (sectToRead != dsr.Data) {
												idFound = false;
												dsr.sectorMode();
											}	// not the right id
											else
												idFound = true;
										}   // read one sector
										break;
									case 0: // end of ID block
										dsr.sectorMode();
										// if we have id and data back to back the first sync is not detected (vroom)
										if (dsr.Data == 0xA1) dsr.SyncSeq = 1;
										idCount++;
										phase = Phases.postIdGap;

										if (!compare) {
											sector.id.gap = new GapField();
											bti.storeStartInfo(sector.id.gap.info, false);
											bti.addByte(sector.id.gap.buffer, phase, false);
										}

										break;
									default:
										break;
								}   // ID Segment
							else {	// DATA SEGMENT
								switch (byteLeftInBlock) {
									case 1:
										bti.addByte(data.buffer, phase, false);
										crc = dsr.CRC;
										break;
									case 0: // end of DATA block
										data.info.crc = crc;
										bti.storeEndInfo(data.info, false);
										dsr.sectorMode();
										// if we have id and data back to back the first sync is not detected (vroom)
										if (dsr.Data == 0xA1) dsr.SyncSeq = 1;
										idFound = false;

										if ((widthOver > (Global.sectSize(sector.id.Length) / 2)) || (widthUnder > (Global.sectSize(sector.id.Length) / 2))) {
											if (widthOver > (Global.sectSize(sector.id.Length) / 2))
												flags |= Sector.WIDTH_ABOVE;
											if (widthUnder > (Global.sectSize(sector.id.Length) / 2))
												flags |= Sector.WIDTH_BELOW;
										}
										else if ((widthOver > 0) && (widthUnder > 0) && ((widthOver + widthUnder) > (Global.sectSize(sector.id.Length) / 4)))
											flags |= Sector.WIDTH_INTRA;

										phase = Phases.postDataGap;
										if (!compare) {
											if (sector.data.gap == null) {
												sector.data.gap = new GapField();
												bti.storeStartInfo(sector.data.gap.info, false);
												bti.addByte(sector.data.gap.buffer, phase, false);
											}
											else if (sector.dataNoId.gap == null) {
												sector.dataNoId.gap = new GapField();
												bti.storeStartInfo(sector.dataNoId.gap.info, false);
												bti.addByte(sector.dataNoId.gap.buffer, phase, false);
											}
										}   // not compare start post gap

										if (idBitPosition >= 0)
											breakLoop = true;   // TODO ================= now we need to capture post gap data

										sector.flags |= flags;  // We OR the flags from all revolutions for max detection

										if (!compare) {
											if (data2)
												sector.dataNoId = data;
											else
												sector.data = data;
										}   // not compare mode

										else if ((!data2 && (sector.data != null)) || (data2 && (sector.dataNoId != null))) {
											DataField sdata;
											if (data2)
												sdata = sector.dataNoId;
											else
												sdata = sector.data;

											int size = sdata.buffer.DataByte.Count;
											// We check that size is same otherwise we probably have a shifted track
											if (data.buffer.DataByte.Count < size) {
												// TODO ???
												// to check fuzzy bits on shifted track we need at least 3 rev
												//if (Global.Disk[track, side].packedFluxDataRev.Count() < 3)
												//	Global.DataIncomplete = true;
												size = data.buffer.DataByte.Count;
											}
											//	//Debug.Assert(sref.buffer.Bytes.Count == sect.buffer.Bytes.Count, "Possible truncation",
											//	//	String.Format("buffers size s1={0} s2={1}\n", sref.buffer.Bytes.Count, sect.buffer.Bytes.Count));

											byte[] fuzzy = new byte[size];
											bool fuzzyFound = false;

											for (int j = 3; j < size; j++) {
												byte xor = (byte)(data.buffer.DataByte[j] ^ sdata.buffer.DataByte[j]);
												if (xor != 0) {
													fuzzyFound = true;
													fuzzy[j] |= xor;
												}
											}   // compare all bytes

											if (fuzzyFound) {
												if (sdata.fuzzy == null)
													sdata.fuzzy = fuzzy;
												else
													for (int j = 3; j < size; j++)
														sdata.fuzzy[j] |= fuzzy[j];
											}

											// if we have fuzzy sector and CRC is correct this indicates
											// that we need to keep the correctly read sector.
											if (fuzzyFound && (data.info.crc == 0) && (sdata == sector.dataNoId))
												sector.dataRecovered = data;

										} // compare mode with data
										break;

									default:	// data bytes
										bti.addByte(data.buffer, phase, false);
										//int segment = 3 - ((byteLeftInBlock - 3) / (Global.sectSize(sector.id.size) / 4));
										if (bti.BitWidthAvg > WIDTH_UPPER_LIMIT) widthOver++;
										if (bti.BitWidthAvg < WIDTH_LOWER_LIMIT) widthUnder++;
										break;
								}   // switch
							}   // DATA Segment

						} // ID or DATA segment
						//startIndex = 0;
					}   // Data ready
				}   // shift bit in
			}   // For all bits

			if (phase == Phases.data)
				bti.storeEndInfo(data.info, false);
		}   // readSector()


		public static void processTrack(int track, int side, int rev) {
			if ((Global.Disk[track, side].sectors != null) && (Global.Disk[track, side].sectors.Count > 0))
				return;
			// non existing track
			if (Global.Disk[track, side].fluxData == null) return;


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
						sector.data.truncDataBitSize = nextIdStart - sector.data.info.startBitIAM;
						sector.data.gap = null;
					}   // truncated
				}   // sector has data

				if (sector.dataNoId != null)
					readSectorCompare(track, side, rev, sector, true);
			}   // all sectors

			readTrack(track, side, rev);
			//findTruncatedSector(track, side);
			trackPositionAndSplice(track, side);
			Protections.checkProtection(track, side);

		}

		///// <summary>
		///// This function calls readAddr(), readSector(), readTrack(), and checkProtection()
		///// </summary>
		///// <param name="track">The track to read</param>
		///// <param name="side">The side to read</param>
		///// <param name="rev">The revolution to use</param>
		//public static void processTrack(int track, int side, int rev) {
		//	if ((Global.Disk[track, side].sectors != null) && (Global.Disk[track, side].sectors.Count > 0))
		//		return;

		//	// non existing track
		//	if (Global.Disk[track, side].fluxData == null) return;

		//	//if (Global.Disk[track, side].sectors == null)
		//	//	Global.Disk[track, side].sectors = new List<Sector>();

		//	//readTemplate(track, side, rev);
		//	Global.Disk[track, side].sectors = readIAM(track, side, rev);
		//	readAllSectors(track, side, rev);

		//	//if (Global.Disk[track, side].sectors.Count > 12)
		//	//	readOneByOne(track, side, rev);
		//	//else
		//	//	try {
		//	//		// we try to read all in one pass
		//	//		if (Global.Disk[track, side].sectors.Count > 0) {
		//	//			readSector(track, side, rev, 0, -1, false);
		//	//			for (int j = 0; j < Global.Disk[track, side].fluxDataRev.Count(); j++) {
		//	//				if (j == rev) continue;
		//	//				readSector(track, side, j, 0, -1, true);
		//	//			}
		//	//			// check SWS
		//	//			// TODO if included sector is at start of track not detected
		//	//			foreach (Sector sector in Global.Disk[track, side].sectors) {
		//	//				if ((sector.flags & Sector.SEC_PROCESSED) == 0) {
		//	//					sector.flags |= Sector.SEC_IN_SEC;
		//	//					byte sectorNumber = sector.id.Sector;
		//	//					int idBitPosition = sector.id.info.startBit;
		//	//					readSector(track, side, rev, sectorNumber, idBitPosition, false);
		//	//					// now multiple readSector() for fuzzy bits
		//	//					for (int j = 0; j < Global.Disk[track, side].fluxDataRev.Count(); j++) {
		//	//						if (j == rev) continue;
		//	//						readSector(track, side, j, sectorNumber, idBitPosition, true);
		//	//					}
		//	//				}   // not processed = SWS
		//	//			}   // check all sectors
		//	//		}   // non empty
		//	//	}
		//	//	catch (SectorNotFoundException e) {
		//	//		SectorNotFoundException.ExceptionMessage = e.Message;
		//	//		readOneByOne(track, side, rev);
		//	//	}

		//	//// remove post GAP TODO does not work if last
		//	//for (int i = 0; i < Global.Disk[track, side].sectors.Count - 1; i++) {
		//	//	Sector s = Global.Disk[track, side].sectors[i];
		//	//	Sector sp1 = Global.Disk[track, side].sectors[i + 1];
		//	//	int endCurrentData = 0;
		//	//	if (s.data != null)
		//	//		endCurrentData = s.data.info.startByte + s.data.buffer.DataByte.Count;
		//	//	int startNextId = sp1.id.info.startByte;
		//	//	if (endCurrentData > startNextId)
		//	//		s.data.gap = null;
		//	//}

		//	readTrack(track, side, rev);
		//	//findTruncatedSector(track, side);
		//	trackPositionAndSplice(track, side);
		//	Protections.checkProtection(track, side);
		//}

		//private static void readOneByOne(int track, int side, int rev) {
		//	for (int i = 0; i < Global.Disk[track, side].sectors.Count; i++) {
		//		// in recover we delete things we might have already found
		//		Global.Disk[track, side].sectors[i].data = null;
		//		Global.Disk[track, side].sectors[i].data2 = null;
		//		Global.Disk[track, side].sectors[i].recovered_data = null;
		//		Global.Disk[track, side].sectors[i].flags = 0;


		//		byte sectorNumber = Global.Disk[track, side].sectors[i].id.Sector;
		//		int idBitPosition = Global.Disk[track, side].sectors[i].id.info.startBit;
		//		readSector(track, side, rev, sectorNumber, idBitPosition, false);

		//		// now multiple readSector() for fuzzy bits
		//		for (int j = 0; j < Global.Disk[track, side].fluxDataRev.Count(); j++) {
		//			if (j == rev) continue;
		//			readSector(track, side, j, sectorNumber, idBitPosition, true);
		//		}
		//	}
		//}

		//private static void findTruncatedSector(int track, int side) {
		//	Global.DiskTrack dt = Global.Disk[track, side];
		//	if ((dt.sectors == null) || (dt.sectors.Count == 0))
		//		return;

		//	for (int current = 0; current < dt.sectors.Count; current++) {
		//		Sector s = dt.sectors[current];
		//		if (s.data == null) continue;
		//		int next = (current == (dt.sectors.Count - 1)) ? 0 : current + 1;
		//		int dataEnd = s.data.info.startBit + s.data.info.bitCount;
		//		int idStart = dt.sectors[next].id.info.startBit;
		//		if (idStart < s.data.info.startBit)
		//			idStart += dt.track.info.bitCount;
		//		if (dataEnd > idStart) {
		//			s.flags |= Sector.TRUNC_SECT;
		//			s.data.truncDataBitSize = idStart - s.data.info.startBitIAM;
		//		}   // truncated
		//	}   // for all sectors

		//}


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
			Sector s;

			for (int i = 0; i < dt.sectors.Count; i++) {
				s = dt.sectors[i];
				//
				// we search the last block of the track
				//
				// if track has NFA and SWS the last truncated sect is the last block
				if ((dt.track.flags & Track.NO_FLUX_AREA) != 0) {
					for (lastBlock = dt.sectors.Count - 1; lastBlock >= 0; lastBlock--)
						if ((dt.sectors[lastBlock].flags & Sector.TRUNC_SECT) != 0)
							break;
					splicePosition = (dt.sectors[lastBlock].data.info.startBit + dt.sectors[lastBlock].data.truncDataBitSize - dt.track.info.bitCount) / 16;
					break;
				}   // NFA
				else {
					// for now we take the sector that have the largest post gap size
					if ((s.data != null) && (s.data.gap != null)) {
						if (s.data.gap.buffer.DataByte.Count > maxPostSize) {
							maxPostSize = s.data.gap.buffer.DataByte.Count;
							lastBlock = i;
						}
					}   // sector with data
					else {
						if (s.id.gap == null) return;
						if (s.id.gap.buffer.DataByte.Count > maxPostSize) {
							maxPostSize = s.id.gap.buffer.DataByte.Count;
							lastBlock = i;
						}
					}   // sector no data
						// search first non 4E byte
					s = dt.sectors[lastBlock];
					GapField g = (s.data != null) ? s.data.gap : s.id.gap;
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
		public static Sector findSector(int track, int side, int sec, int idBitPosition) {
			List<Sector> sectors = Global.Disk[track, side].sectors;
			foreach (Sector sector in sectors) {
				if (idBitPosition < 0) {
					if (sector.id.Sector == sec)
						return sector;
				}
				else {
					if ((sector.id.Sector == sec) && (sector.id.info.startBit == idBitPosition))
						return sector;
				}
			}
			return null;
		}
	}
}
