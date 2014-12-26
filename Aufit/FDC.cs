/*!
@file FDC.cs
 * 
<summary>Emulate WD1772 FDC commands: read track, read address, read sector</summary>

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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Windows.Media;

using KFReaderPackage;
using GlobalPackage;
using Aufit;

namespace WD1772Package {
	/// <summary>Different phases of a sector: ID, Gap, Data</summary>
	public enum Phases : byte {
		p_gap = 0,	///<! GAP
		p_id,		///<! ID (Address) block
		p_data,		///<! DATA block
	}


	/// <summary>
	/// Store information about a block of data read from track.
	/// /// </summary>
	public struct BlockInfo {
		/// <summary>Number of bytes in the block</summary> 
		public int byteCount;

		/// <summary>First byte of the block</summary> 
		public int startByte;

		/// <summary>First bit of the block</summary> 
		public int startBit;

		/// <summary>Number of bits (clock + data) in the block</summary>
		public int bitCount;

		/// <summary>Starting Time of the block in nanoseconds from index</summary>
		public int startTime;

		/// <summary>Length of the Block in nanoseconds </summary>
		public int timeLength;

		/// <summary>CRC value at the end of reading an ID or DATA block. Should be zero on a "good" block</summary>
		public ushort crc;
	}


	/// <summary>
	/// Store information about the ID Field of a Sector
	/// </summary>
	public class IdField {
		/// <summary>Store the Index Address Mark</summary> 
		public byte iam;

		/// <summary>Store the track number</summary> 
		public byte track;

		/// <summary>Store the Head/Side number</summary> 
		public byte head;

		/// <summary>Store the sector number</summary> 
		public byte number;

		/// <summary>Store the encoded data segment size</summary> 
		public byte size;

		/// <summary>Store the CRC on two bytes</summary> 
		public ushort crc;

		/// <summary> Contains information about the Id field block</summary>
		public BlockInfo info = new BlockInfo();
	}


	/// <summary>
	/// Store information about the DATA Field of a Sector
	/// </summary>
	public class DataField {
		/// <summary>Store the Index Address Mark</summary> 
		public byte iam;

		/// <summary>buffer for all the byte read in the DATA field of the sector</summary>
		public TrackBuffer buffer;

		/// <summary>Store the CRC on two bytes</summary> 
		public ushort crc;

		/// <summary>Array of bytes to store fuzzy mask</summary> 
		public byte[] fuzzy;

		/// <summary>information about the data field </summary>
		public BlockInfo info = new BlockInfo();
	}


	/// <summary>
	/// Sector information. Contains ID & Data fields and flags
	/// </summary>
	public class Sector {
		public const uint WIDTH_ABOVE = Bit.BIT_01;
		public const uint WIDTH_BELOW = Bit.BIT_02;
		public const uint WIDTH_INTRA = Bit.BIT_03;
		public const uint NO_STD_IDAM = Bit.BIT_04;
		public const uint BAD_TRK_NUM = Bit.BIT_05;
		public const uint BAD_HEAD_NUM = Bit.BIT_06;
		public const uint BAD_SEC_NUM = Bit.BIT_07;
		public const uint BAD_SEC_LEN = Bit.BIT_08;
		public const uint SEC_IN_SEC = Bit.BIT_09;
		public const uint NO_STD_DAM = Bit.BIT_10;
		public const uint DUP_SEC_NUM = Bit.BIT_11;
		public const uint LONG_SECT = Bit.BIT_12;
		public const uint SHORT_SECT = Bit.BIT_13;
		public const uint SECTOR_SEQ = Bit.BIT_14;

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

		/// <summary>Sector's flags</summary>
		public uint flags;

		/// <summary>the content of the sector start at this track offset</summary>
		public int trackOffset;
	}


	/// <summary>Track information</summary>
	public class Track {
		public const uint SHORT_TRACK = Bit.BIT_01;
		public const uint LONG_TRACK = Bit.BIT_02;
		public const uint DATA_TRACK = Bit.BIT_03;
		public const uint BAD_DATA_GAP = Bit.BIT_04;

		public const uint NO_FLUX_AREA = Bit.BIT_07;
		public const uint DATA_OVER_IDX = Bit.BIT_08;
		public const uint DATA_AFTER_IDX = Bit.BIT_09;
		public const uint ID_OVER_IDX = Bit.BIT_10;
		public const uint ID_AFTER_IDX = Bit.BIT_11;
		// SYNC_IN_DATA ???
		// hidden data in track ?
		// invalid sync seq
		// partially formatted track
		// Fuzzy track ???

		/// <summary>buffer for all the byte read in the DATA field of the sector</summary>
		public TrackBuffer buffer;

		/// <summary> Contains information about the field </summary>
		public BlockInfo info = new BlockInfo();

		/// <summary>Track's flags</summary>
		public uint flags;

		/// <summary>
		/// Location in byte of the first A1 sync before an IAM sequence
		/// </summary>
		public int firstSyncOffset = 0;

		/// <summary>
		/// Protection(s) detected for this track
		/// </summary>
		public bool hasProtections = false;

	}


	/// <summary>Emulate some WD1772 FDC Commands</summary>
	public class FDC {
		private readonly static int widthUpperLimit = 2070;
		private readonly static int widthLowerLimit = 1930;

		/// <summary>Read a track</summary>
		/// <remarks>The data placed in the buffer are equivalent to what would 
		/// be read by a WD1772 decode the flux transitions. We store the
		/// phase for a byte based on information detected by readAddr and readSector.
		/// Therefore this function must be called last.</remarks>
		public static void readTrack(int track, int side, int rev) {
			if (Global.Disk[track, side].track != null) return;

			Global.Disk[track, side].track = new Track();
			Global.Disk[track, side].track.buffer = new TrackBuffer();
			Global.Disk[track, side].track.info.startTime = 0;
			Global.Disk[track, side].track.info.startByte = 0;
			Global.Disk[track, side].track.info.startBit = 0;

			Global.DiskTrack dt = Global.Disk[track, side];
			int startBit = dt.packedFluxDataRev[rev].firstFluxBit;
			int stopBit = startBit + dt.packedFluxDataRev[rev].bitCount;

			DSR dsr = new DSR();
			dsr.trackMode();

			int currentTime = 0;
			int byteCount = 0;

			int sectorNumber = -1;
			int sectorCount = dt.sectors.Count;
			Phases phase = Phases.p_gap;
			int invalidByteInGap = 0;
			int nfaCount = 0;
			//int nfaStart;
			bool compute = true;
			int nextIdStartTime = int.MaxValue;
			int nextIdStopTime = int.MaxValue;
			int nextDataStartTime = int.MaxValue;
			int nextDataStopTime = int.MaxValue;
			bool hasData = false;
			int byteInSector = 0;
			bool compareSector = false;


			// We read from the beginning to end of a track looking with AM decoder on all time
			for (int currentBit = startBit; (currentBit < stopBit); currentBit++) {
				int currentByte = currentBit / 8;
				int bitInByte = currentBit % 8;
				byte bits = dt.packedFluxData.packedBits[currentByte];
				int bit = (bits >> bitInByte) & 0x1;
				int bitWidth = dt.packedFluxData.bitWidth[currentByte];

				//currentTime += bitWidth;
				if (bit != 0) {
					int flux = Global.fluxWidth(currentBit, track, side);
					currentTime += flux;
				}

				dsr.shiftBitsIn(bit);

				// here we set phase if sectors found in readAddr
				if ((dt.sectors != null) && (dt.sectors.Count > 0)) {
					if (compute) {
						// here we test if last sector has data that wraps
						if ((sectorNumber == -1) && (dt.sectors[dt.sectors.Count - 1].data != null)) {
							int dataStartTime = dt.sectors[dt.sectors.Count - 1].data.info.startTime;
							int dataStopTime = dataStartTime + dt.sectors[dt.sectors.Count - 1].data.info.timeLength;
							if ((dataStartTime > dt.packedFluxDataRev[rev].revolutionTime) ||
								(dataStopTime > dt.packedFluxDataRev[rev].revolutionTime)) {

								if (dataStartTime > dt.packedFluxDataRev[rev].revolutionTime)
									nextDataStartTime = dataStartTime - dt.packedFluxDataRev[rev].revolutionTime;
								else
									nextDataStartTime = currentTime;
								nextDataStopTime = dataStopTime - dt.packedFluxDataRev[rev].revolutionTime;
								// check SWS
								int IdStartTime = dt.sectors[0].id.info.startTime;
								if (nextDataStopTime > IdStartTime)
									nextDataStopTime = IdStartTime;
								hasData = true;
								compute = false;
							}	// last sector has data past index - shifted track
							else
								sectorNumber = 0;
						}	// test if last sector is wrapped

						if ((sectorNumber == -1) && (dt.sectors[dt.sectors.Count - 1].data == null)) 
							sectorNumber = 0;
						
						if (sectorNumber != -1) {
							Sector sector = dt.sectors[sectorNumber];
							nextIdStartTime = sector.id.info.startTime;
							nextIdStopTime = nextIdStartTime + sector.id.info.timeLength;
							nextDataStartTime = int.MaxValue;
							nextDataStopTime = int.MaxValue;
							if (sector.data != null) {
								nextDataStartTime = sector.data.info.startTime;
								nextDataStopTime = nextDataStartTime + sector.data.info.timeLength;
								// check SWS
								if ((sectorNumber + 1) < dt.sectors.Count) {
									int IdStartTime = dt.sectors[sectorNumber + 1].id.info.startTime;
									if (nextDataStopTime > IdStartTime)
										nextDataStopTime = IdStartTime;
								}
								hasData = true;
							}	// sector has data
							else hasData = false;
							compute = false;						
						}
					} // need to compute values

					// set phase
					if (currentTime >= nextIdStartTime)
						phase = Phases.p_id;
					if (currentTime >= nextIdStopTime) {
						if (hasData == false) {
							sectorNumber = (sectorNumber + 1) % sectorCount;
							compute = true;
						}
						phase = Phases.p_gap;
					}
					if (currentTime >= nextDataStartTime)
						phase = Phases.p_data;
					if (currentTime >= nextDataStopTime) {
						phase = Phases.p_gap;
						sectorNumber = (sectorNumber + 1) % sectorCount;
						compute = true;
					}
				}	// we use sectors info

				if (dsr.DataReady) {
					byteCount++;

					if (dsr.IAM && (dt.track.firstSyncOffset == 0)) {
						Global.Disk[track, side].track.firstSyncOffset = byteCount - 3;
					}
					if ((dsr.Data >= 0xF5) && (dsr.Data <= 0xF7) && (phase == Phases.p_gap))
						invalidByteInGap++;

					// check NFA
					if ((dsr.MFM == 0) || (dsr.MFM == 0xFFFF)) {
						//if (nfaCount == 0) {
						//	nfaStart = currentTime;
						//}
						nfaCount++;
					}
					else {
						if (nfaCount > 8)
							Global.Disk[track, side].track.flags |= Track.NO_FLUX_AREA;
						nfaCount = 0;
					}

					if (compareSector && (dt.sectors[sectorNumber].data != null)) {
						if (dsr.Data != dt.sectors[sectorNumber].data.buffer.Bytes[byteInSector].dataByte) {
							compareSector = false;
						}
						else {
							if (byteInSector == (dt.sectors[sectorNumber].data.buffer.Bytes.Count -1)) {
								dt.sectors[sectorNumber].flags |= Sector.SEC_EQ_TRACK;
								compareSector = false;
							}
							byteInSector++;
						}
					}

					// check if sector content is same as track
					if (!compareSector && (sectorNumber >= 0) && (phase == Phases.p_data)) {
						if (dsr.Data == dt.sectors[sectorNumber].data.iam) {
							dt.sectors[sectorNumber].trackOffset = byteCount + 1;
							byteInSector = 0;
							compareSector = true;
						}
					}


					Global.Disk[track, side].track.buffer.addByte(dsr.Data, dsr.MFM, (ushort)bitWidth, currentTime, phase);
				}	// data ready

			}	// for all bits

			Global.Disk[track, side].track.info.byteCount = byteCount;
			Global.Disk[track, side].track.info.timeLength = currentTime;

			if (invalidByteInGap > 10) {
				Global.Disk[track, side].track.flags |= Track.BAD_DATA_GAP;
			}

		}	// end of readTrack()


	
		/// <summary>
		/// Read all the ID fields of a track
		/// </summary>
		/// <param name="track">The track index</param>
		/// <param name="side">The side index</param>
		/// <param name="rev">The revolution index</param>
		/// <remarks>Interpret the flux transition as the WD1772 would do with
		/// a read address command. For each new ID a sector is added to the Disk
		/// data structure.</remarks>
		public static void readAddr(int track, int side, int rev) {
			int startBit = Global.Disk[track, side].packedFluxDataRev[rev].firstFluxBit;
			int stopBit = startBit + Global.Disk[track, side].packedFluxDataRev[rev].bitCount;
			int lastBye = Global.Disk[track, side].packedFluxData.packedBits.Count();
			int sectorCount = 0;
			int byteLeftInBlock = 0;
			int currentTime = 0;
			int byteCount = 0;
			ushort crc = 0;

			Phases phase = Phases.p_gap;
			DSR dsr = new DSR();
			dsr.sectorMode();

			// We read from the beginning to end of a track looking only for ID field (in current revolution).
			// If the end of track happen in the middle of an ID field we continue with data from the next revolution.
			for (int currentBit = startBit; (currentBit < stopBit) || (phase == Phases.p_id) || dsr.inSyncSeq; currentBit++) {
				int currentByte = currentBit / 8;
				int bitInByte = currentBit % 8;

				if (currentByte == Global.Disk[track, side].packedFluxData.packedBits.Count()) {
					// We have reach the end of the buffer while reading an ID
					if (Global.Disk[track, side].packedFluxDataRev.Count() == 1)
						// we have a shifted track with only 1 rev !
						Global.DataIncomplete = true;
					break;
				}

				byte bits = Global.Disk[track, side].packedFluxData.packedBits[currentByte];
				int bit = (bits >> bitInByte) & 0x1;

				if (bit != 0) {
					int flux = Global.fluxWidth(currentBit, track, side);
					currentTime += flux;
				}
				//currentTime += Global.Disk[track, side].packedFluxData.bitWidth[currentByte];

				dsr.shiftBitsIn(bit);

				if (dsr.DataReady) {
					byteCount++;

					if (phase != Phases.p_id) {	// not in ID						
						if (dsr.IAM) {
							switch (dsr.Data) {
								case 0xFC:	// IBM IAM
								case 0xFD:
								case 0xFE:	// IDAM - start of an ID block
								case 0xFF:
									// Found an ID Field so we create a new sector that we add to the list
									Sector sector = new Sector();
									Global.Disk[track, side].sectors.Add(sector);
									// and a new id
									sector.id = new IdField();
									sectorCount++;
									sector.id.iam = dsr.Data;
									sector.id.info.startBit = currentBit - startBit;
									sector.id.info.startByte = byteCount;
									sector.id.info.startTime = currentTime;
									byteLeftInBlock = 7;
									phase = Phases.p_id;
									break;
								default:
									// other AM re-enable sync search
									dsr.sectorMode();
									break;
							}	// switch AM
						}	// IAM
						//else dsr.sectorMode();
					}	// not in ID
					else {	// We are in an ID
						byteLeftInBlock--;
						switch (byteLeftInBlock) {
							case 6: Global.Disk[track, side].sectors[sectorCount - 1].id.track = dsr.Data; break;
							case 5: Global.Disk[track, side].sectors[sectorCount - 1].id.head = dsr.Data; break;
							case 4: Global.Disk[track, side].sectors[sectorCount - 1].id.number = dsr.Data; break;
							case 3: Global.Disk[track, side].sectors[sectorCount - 1].id.size = dsr.Data; break;
							case 2: Global.Disk[track, side].sectors[sectorCount - 1].id.crc = dsr.Data; break;
							case 1: Global.Disk[track, side].sectors[sectorCount - 1].id.crc += (ushort)(dsr.Data << 8);
								crc = dsr.CRC;
								break;
							case 0:	// end of ID block
								Global.Disk[track, side].sectors[sectorCount - 1].id.info.byteCount = byteCount - Global.Disk[track, side].sectors[sectorCount - 1].id.info.startByte;
								Global.Disk[track, side].sectors[sectorCount - 1].id.info.crc = crc;
								Global.Disk[track, side].sectors[sectorCount - 1].id.info.timeLength = currentTime - Global.Disk[track, side].sectors[sectorCount - 1].id.info.startTime;
								Global.Disk[track, side].sectors[sectorCount - 1].id.info.bitCount = currentBit - Global.Disk[track, side].sectors[sectorCount - 1].id.info.startBit - startBit;
								phase = Phases.p_gap;
								dsr.sectorMode();	// we re-enable sync
								break;
						}					
					} // we are in ID
				}	// one byte ready
			}	// for all bit

			// We are still in ID phase due to end of file 
			if (phase == Phases.p_id) {
				Global.Disk[track, side].sectors[sectorCount - 1].id.info.byteCount = byteCount - Global.Disk[track, side].sectors[sectorCount - 1].id.info.startByte;
				Global.Disk[track, side].sectors[sectorCount - 1].id.info.crc = crc;
				Global.Disk[track, side].sectors[sectorCount - 1].id.info.timeLength = currentTime - Global.Disk[track, side].sectors[sectorCount - 1].id.info.startTime;
			}

			// since we now continue when finding sync over index it is possible to have duplicate sector in first last position
			if ((Global.Disk[track, side].sectors.Count > 0) && 
				(Global.Disk[track, side].sectors[0].id.number == Global.Disk[track, side].sectors[Global.Disk[track, side].sectors.Count - 1].id.number) &&
				(Global.Disk[track, side].sectors[Global.Disk[track, side].sectors.Count - 1].id.info.startTime > Global.Disk[track, side].packedFluxDataRev[rev].revolutionTime))
					Global.Disk[track, side].sectors.RemoveAt(Global.Disk[track, side].sectors.Count - 1);
		}	// end of readAddr()


		/// <summary>
		/// Reads the DATA field of a specified sector
		/// </summary>
		/// <param name="track">Track</param>
		/// <param name="side">Side</param>
		/// <param name="rev">Revolution value</param>
		/// <param name="sectToRead">Sector number to read</param>
		/// <param name="idBitPosition">If positive the sector to find must be located after this specified 
		/// position in nanosecond. If negative this means we read all sector at once</param>
		/// <param name="compare">If set to true we are in compare mode, else we are in read mode</param>
		/// <remarks>The position parameter is necessary to read the data of a sector multiply  defined 
		/// (index.e. we have several sectors with same number). Otherwise the data for the first encountered
		/// sector would always be returned (equivalent to position==0). \n
		/// However to speedup things this parameter can be set to a negative value. In this case 
		/// we read all sector in one revolution. But as we may have sector within sector it is possible 
		/// that some of the sector are not read in one pass this can be checked with the SEC_PROCESSED flag. 
		/// For all sector not read the function must be called again this time using a positive idBitPosition value.\n
		/// When compare is false we are reading the sector normally and we fill the SectorContent structure. 
		/// But if compare==true we are in "compare" mode. In that case we read the same sector using the rev parameter 
		/// and we compare the data buffer read with the "reference" data buffer to fill the fuzzy structure.</remarks>
		public static void readSector(int track, int side, int rev, byte sectToRead, int idBitPosition, bool compare) {
			int startBit = Global.Disk[track, side].packedFluxDataRev[rev].firstFluxBit;
			int stopBit = startBit + Global.Disk[track, side].packedFluxDataRev[rev].bitCount;
			int lastBye = Global.Disk[track, side].packedFluxData.packedBits.Count();
			int byteLeftInBlock = 0;
			int currentTime = 0;
			int byteCount = 0;
			ushort crc = 0;
			bool idFound = false;
			bool breakLoop = false;
			int widthOver = 0;
			int widthUnder = 0;
			Phases phase = Phases.p_gap;
			uint flags = 0;
			bool p_inner = false;

			Sector sector = null;
			if (idBitPosition >= 0)
				sector = findSector(track, side, sectToRead, idBitPosition);

			DSR dsr = new DSR();
			dsr.sectorMode();
			DataField data = null;

			// we normally loop from start to stop for the specified revolution.
			// However we continue if we are in the middle of ID, Inner GAP, or DATA field
			for (int currentBit = startBit; (currentBit < stopBit) || (phase != Phases.p_gap) || p_inner || dsr.inSyncSeq; currentBit++) {
				if (breakLoop) break;

				int currentByte = currentBit / 8;
				int bitInByte = currentBit % 8;

				if (currentByte == Global.Disk[track, side].packedFluxData.packedBits.Count()) {
				//// we cannot access beyond buffer. If this happen with only one revolution it
				//// indicates that we do not have enough data. If this happen in the last revolution
				//// the data for this revolution will be recovered incorrectly.
					if (Global.Disk[track, side].packedFluxDataRev.Count() == 1)
						// we have a shifted track with only 1 rev !
						Global.DataIncomplete = true;
					break;
				}

				byte bits = Global.Disk[track, side].packedFluxData.packedBits[currentByte];
				int bit = (bits >> bitInByte) & 0x1;

				int bitWidth = Global.Disk[track, side].packedFluxData.bitWidth[currentByte];
				//currentTime += bitWidth;
				if (bit != 0) {
					int flux = Global.fluxWidth(currentBit, track, side);
					currentTime += flux;
				}

				dsr.shiftBitsIn(bit);
				if (dsr.DataReady) {
					byteCount++;

					if ((phase != Phases.p_id) && (phase != Phases.p_data)) {
						// We are in a GAP						
						if (dsr.IAM) {
							switch (dsr.Data) {
								case 0xFC:	// IBM IAM
								case 0xFD:
								case 0xFE:	// IDAM - start of an ID block
								case 0xFF:
									if (idBitPosition < 0) {
										// if past index we have no data
										if (currentTime > Global.Disk[track, side].packedFluxDataRev[rev].revolutionTime) {
											breakLoop = true;
											break;
										}
										// search the corresponding sector
										sector = null;
										// start position can vary if sector with random noise happen before
										foreach (Sector s in Global.Disk[track, side].sectors)
											// if (((currentBit - startBit) - s.id.info.startBit) < 300) {
											// if (currentTime - s.id.info.startTime < 16000) {
											if ((byteCount - s.id.info.startByte) < 30) {
												sector = s;
												break;
											}
										Debug.Assert(sector != null, "Id not found in readSect",
											String.Format("Track={0} Side={1} #sect={2}", track, side, Global.Disk[track, side].sectors.Count));
										sector.flags |= Sector.SEC_PROCESSED;
										flags = 0;

										// ready to read the ID block
										byteLeftInBlock = 7;
										phase = Phases.p_id;
									}	// read multiple sectors
									else {
										// if we have already found the requested ID and we find another ID
										// this means that sector has no Data field. Therefore we should break
										if (idFound) {
											phase = Phases.p_id;
											breakLoop = true;	// don't need to continue
											break;
										}
										// if we have not yet reach the requested position we ignore
										if ((currentBit - startBit + 10) < idBitPosition) {
											dsr.sectorMode();
											phase = Phases.p_gap;
											break;
										}
										// otherwise we need to read the ID block
										byteLeftInBlock = 7;
										phase = Phases.p_id;
									}	// read one sector
									break;

								case 0xF8:	// DDAM
								case 0xF9:
								case 0xFA:
								case 0xFB:	// DAM - Start of a DATA block
									// If we have found the right ID field we check that this
									// Data field is less than 43 bytes apart from the Id
									// id field is already 7 bytes so 43+7 = 50
									if (idFound && ((byteCount - sector.id.info.startByte) <= 60)) {
										data = new DataField();
										data.buffer = new TrackBuffer();
										data.iam = dsr.Data;
										//dataFound = true;
										byteLeftInBlock = Global.sectSize(sector.id.size) + 3; // AM + 2 CRC + Size
										phase = Phases.p_data;
										p_inner = false;
										widthOver = 0;
										widthUnder = 0;

										data.info.startBit = currentBit - startBit;
										data.info.startByte = byteCount;
										data.info.startTime = currentTime;
									}
									else {
										dsr.sectorMode();
										phase = Phases.p_gap;
									}
									break;

								default:
									// other AM re-enable sync search
									dsr.sectorMode();
									break;
							}	// switch AM
						}	// IAM
					}	// GAP
					else {
						// We are in an ID or DATA segment
						byteLeftInBlock--;
						if (phase == Phases.p_id)
							// ID segment
							switch (byteLeftInBlock) {
								case 4:
									if (idBitPosition < 0) {
										idFound = true;	// used to read data
									}
									else {
										// we only check if we have the right sector number
										if (sectToRead != dsr.Data) {
											idFound = false;
											dsr.sectorMode();
										}
										else 
											idFound = true;
									}
									break;
								case 0:
									dsr.sectorMode();
									phase = Phases.p_gap;
									p_inner = true;
									break;
								default:
									break;
							}	// ID Segment
						else {
							switch (byteLeftInBlock) {
								case 2: data.crc = dsr.Data; break;
								case 1: data.crc += (ushort)(dsr.Data << 8);
									crc = dsr.CRC;
									break;
								case 0:	// end of DATA block
									data.info.crc = crc;
									data.info.byteCount = byteCount - data.info.startByte - 3;
									data.info.timeLength = currentTime - data.info.startTime;
									data.info.bitCount = currentBit - startBit - data.info.startBit;
									if ((widthOver > (Global.sectSize(sector.id.size) / 2)) || (widthUnder > (Global.sectSize(sector.id.size) / 2))) {
										if (widthOver > (Global.sectSize(sector.id.size) / 2))
											flags |= Sector.WIDTH_ABOVE;
										if (widthUnder > (Global.sectSize(sector.id.size) / 2))
											flags |= Sector.WIDTH_BELOW;
									}
									else if ((widthOver > 0) && (widthUnder > 0) && ((widthOver + widthUnder) > (Global.sectSize(sector.id.size) / 4)))
										flags |= Sector.WIDTH_INTRA;
									phase = Phases.p_gap;
									if (idBitPosition >= 0) breakLoop = true;
									dsr.sectorMode();
									idFound = false;
									
									sector.flags |= flags;	// We OR the flags from all revolutions for max detection
									if (!compare) {
										sector.data = data;
									}
									else if (sector.data != null) {
										int size = sector.data.info.byteCount;
										// We check that size is same otherwise we probably have a shifted track
										if (data.info.byteCount < size) {
											// to check fuzzy bits on shifted track we need at least 3 rev
											if (Global.Disk[track, side].packedFluxDataRev.Count() < 3)
												Global.DataIncomplete = true;
											size = data.info.byteCount;
										}
										//	//Debug.Assert(sref.buffer.Bytes.Count == sect.buffer.Bytes.Count, "Possible truncation",
										//	//	String.Format("buffers size s1={0} s2={1}\n", sref.buffer.Bytes.Count, sect.buffer.Bytes.Count));

										byte[] fuzzy;
										//if (sector.data.fuzzy != null)
										//	fuzzy = sector.data.fuzzy;
										//else {
											int dataSize = Global.sectSize(sector.id.size);
											fuzzy = new byte[dataSize];
										//}
										bool fuzzyFound = false;

										for (int i = 0; i < size - 3; i++) {
											byte xor = (byte)(data.buffer.Bytes[i].dataByte ^ sector.data.buffer.Bytes[i].dataByte);
											if (xor != 0) {
												fuzzyFound = true;
												fuzzy[i] |= xor;
											}
										}	// compare all bytes
										if (fuzzyFound && (sector.data.fuzzy == null))
											sector.data.fuzzy = fuzzy;

										// TODO if CRC original bad and CRC this one good
										// we save the good sector in DB for pasti write
									} // compare mode with data
									break;

								default:
									data.buffer.addByte(dsr.Data, dsr.MFM, (ushort)bitWidth, currentTime, phase);
									//int segment = 3 - ((byteLeftInBlock - 3) / (Global.sectSize(sector.id.size) / 4));
									if (bitWidth > widthUpperLimit) widthOver++;
									if (bitWidth < widthLowerLimit) widthUnder++;
									break;
							}	// switch
						}	// DATA Segment
					} // ID or DATA segment
				}	// Data ready
			}	// For all bits

			if (phase == Phases.p_data) {
				data.info.byteCount = byteCount - data.info.startByte;
				data.info.timeLength = currentTime - data.info.startTime;
			}

		}	// readSector()


		/// <summary>
		/// This function calls readAddr(), readSector(), readTrack(), and checkProtection()
		/// </summary>
		/// <param name="track">The track to read</param>
		/// <param name="side">The side to read</param>
		/// <param name="rev">The revolution to use</param>
		public static void fdcRealAll(int track, int side, int rev) {
			if ((Global.Disk[track, side].sectors != null) && (Global.Disk[track, side].sectors.Count > 0))
				return;

			// non existing track
			if (Global.Disk[track, side].packedFluxData == null) return;

			if (Global.Disk[track, side].sectors == null)
				Global.Disk[track, side].sectors = new List<Sector>();

			readAddr(track, side, rev);

			bool NEW_MODE = true;
			if (NEW_MODE) {
				if (Global.Disk[track, side].sectors.Count > 0) {
					readSector(track, side, rev, 0, -1, false);
					for (int j = 0; j < Global.Disk[track, side].packedFluxDataRev.Count(); j++) {
						if (j == rev) continue;
						readSector(track, side, j, 0, -1, true);
					}
					// check SWS
					foreach (Sector sector in Global.Disk[track, side].sectors) {
						if ((sector.flags & Sector.SEC_PROCESSED) == 0) {
							sector.flags |= Sector.SEC_IN_SEC;
							byte sectorNumber = sector.id.number;
							int idBitPosition = sector.id.info.startBit;
							readSector(track, side, rev, sectorNumber, idBitPosition, false);
							// now multiple readSector() for fuzzy bits
							for (int j = 0; j < Global.Disk[track, side].packedFluxDataRev.Count(); j++) {
								if (j == rev) continue;
								readSector(track, side, j, sectorNumber, idBitPosition, true);
							}
						}	// not precessed = SWS
					}	// check all sectors
				}	// non empty
			}	// new mode => faster

			else {
				for (int i = 0; i < Global.Disk[track, side].sectors.Count; i++) {
					byte sectorNumber = Global.Disk[track, side].sectors[i].id.number;
					int idBitPosition = Global.Disk[track, side].sectors[i].id.info.startBit;
					readSector(track, side, rev, sectorNumber, idBitPosition, false);

					// now multiple readSector() for fuzzy bits
					for (int j = 0; j < Global.Disk[track, side].packedFluxDataRev.Count(); j++) {
						if (j == rev) continue;
						readSector(track, side, j, sectorNumber, idBitPosition, true);
					}
				}
			}	// old mode => known to work :)

			readTrack(track, side, rev);

			Protections.checkProtection(track, side);
		}

		/// <summary>
		/// Release Track and Sector content
		/// </summary>
		/// <param name="track">The track number</param>
		/// <param name="side">The side number</param>
		public static void releaseTrack(int track, int side) {
			Global.Disk[track, side].sectors = null;
			Global.Disk[track, side].track = null;
		}

		/// <summary>
		/// Find a sector by its number in a list of sectors
		/// </summary>
		/// <param name="track">The track number</param>
		/// <param name="side">The side number</param>
		/// <param name="num">The sector number</param>
		/// <param name="idBitPosition">Minimum bit position of the sector</param>
		/// <returns></returns>
		public static Sector findSector(int track, int side, int num, int idBitPosition) {
			List<Sector> sectors = Global.Disk[track, side].sectors;
			foreach (Sector sector in sectors)
				if ((sector.id.number == num) && (sector.id.info.startBit == idBitPosition))
					return sector;
			return null;
		}
	}
}
