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
	/// <summary>
	/// Different phases of a sector: ID, Gap, Data
	/// </summary>
	public enum Phases : byte {
		p_gap = 0,	// GAP
		p_id,		// ID (Address) block
		p_data,		// DATA block
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

		/// <summary>Starting Time of the block in nanoseconds from index</summary>
		public int startTime;

		/// <summary>Length of the Block in nanoseconds </summary>
		public int timeLength;

		/// <summary>Number of bits (clock + data) in the block</summary>
		public int bitCount;

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

		/// <summary> Contains information about the field </summary>
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

		/// <summary>Array to store fuzzy mask</summary> 
		public byte[] fuzzy;

		/// <summary>information about the data field </summary>
		public BlockInfo info = new BlockInfo();
	}


	/// <summary>
	/// Sector information. Contains ID & Data fields and flags
	/// </summary>
	public class Sector {
		public const int WIDTH_ABOVE = Bit.BIT_01;
		public const int WIDTH_BELOW = Bit.BIT_02;
		public const int WIDTH_INTRA = Bit.BIT_03;
		public const int NO_STD_IDAM = Bit.BIT_04;
		public const int BAD_TRK_NUM = Bit.BIT_05;
		public const int BAD_HEAD_NUM = Bit.BIT_06;
		public const int BAD_SEC_NUM = Bit.BIT_07;
		public const int BAD_SEC_LEN = Bit.BIT_08;
		public const int SEC_IN_SEC = Bit.BIT_09;
		public const int NO_STD_DAM = Bit.BIT_10;
		public const int DUP_SEC_NUM = Bit.BIT_11;
		public const int LONG_SECT = Bit.BIT_12;
		public const int SHORT_SECT = Bit.BIT_13;
		// BAD_ID_CRC => id.info.crc != 0
		// SECT_NO_DATA => data == null
		// BAD_DATA_CRC => data.info.crc != 0
		// FUZZY_SECT => data.fuzzy != null

		/// <summary>Pointer to the Id Field of a sector</summary>
		public IdField id;

		/// <summary>Pointer to the Data Field of the sector (can be null)</summary>
		public DataField data;

		/// <summary>Protection Flags related to the sector</summary>
		public int flags;
	}


	/// <summary>Track information</summary>
	public class Track {
		public const int SHORT_TRACK = Bit.BIT_01;
		public const int LONG_TRACK = Bit.BIT_02;
		public const int DATA_TRACK = Bit.BIT_03;

		public const int BAD_DATA_GAP = Bit.BIT_05;

		public const int NO_FLUX_AREA = Bit.BIT_07;
		public const int DATA_OVER_IDX = Bit.BIT_08;
		public const int DATA_AFTER_IDX = Bit.BIT_09;
		public const int ID_OVER_IDX = Bit.BIT_10;
		public const int ID_AFTER_IDX = Bit.BIT_11;
		// SYNC_IN_DATA ???
		// hidden data in track ?
		// invalid sync seq
		// partially formatted track
		// Fuzzy track ???

		/// <summary>buffer for all the byte read in the DATA field of the sector</summary>
		public TrackBuffer buffer;

		/// <summary> Contains information about the field </summary>
		public BlockInfo info = new BlockInfo();

		/// <summary>Protection Flags related to the track</summary>
		public int flags;

		public bool hasSyncSeq = false;

		public bool hasProtections = false;

	}



	/// <summary>Emulate some WD1772 FDC Commands</summary>
	public class FDC {
		private readonly static int widthUpperLimit = 2000 + (4 * 20);
		private readonly static int widthLowerLimit = 2000 - (4 * 20);

		/// <summary>Read a track by decoding all the track MFM bits</summary>
		/// <remarks>This function interprets the bits stream as a WD1772 would do with a 
		/// read track command. The data placed in the buffer are equivalent to what would 
		/// be read by a WD1772 decode the flux transitions. However we do an interpretation 
		/// of the different segments of a sector (called phases) to be able to display 
		/// useful information.</remarks>
		/// TODO do something special for SDS ?
		public static void readTrack(int track, int side, int rev) {
			if (Global.Disk[track, side].track != null) return;

			Global.Disk[track, side].track = new Track();
			Global.Disk[track, side].track.buffer = new TrackBuffer();
			Global.Disk[track, side].track.info.startTime = 0;
			Global.Disk[track, side].track.info.startByte = 0;
			Global.Disk[track, side].track.info.startBit = 0;

			int startBit = Global.Disk[track, side].packedFluxDataRev[rev].firstFluxBit;
			int stopBit = startBit + Global.Disk[track, side].packedFluxDataRev[rev].bitCount;
			int currentTime = 0;
			int byteCount = 0;

			DSR dsr = new DSR();
			dsr.trackMode();

			// we use the sector information
			int sectorNumber = 0;
			int sectorCount = Global.Disk[track, side].sectors.Count;
			Phases phase = Phases.p_gap;
			int invalidByteInGap = 0;
			int nfaCount = 0;
			int nfaStart;

			// We read from the beginning to end of a track looking with AM decoder on all time
			for (int currentBit = startBit; (currentBit < stopBit); currentBit++) {
				int currentByte = currentBit / 8;
				int bitInByte = currentBit % 8;
				byte bits = Global.Disk[track, side].packedFluxData.packedBits[currentByte];
				int bit = (bits >> bitInByte) & 0x1;
				int bitWidth = Global.Disk[track, side].packedFluxData.bitWidth[currentByte];
				currentTime += bitWidth;
				dsr.shiftBitsIn(bit);

				// here we set phase if sectors found in readAddr
				if ((Global.Disk[track, side].sectors != null) && (Global.Disk[track, side].sectors.Count > 0)) {
					Sector sector = Global.Disk[track, side].sectors[sectorNumber];
					int nextIdStart = sector.id.info.startBit + startBit;
					int nextIdStop = nextIdStart + sector.id.info.bitCount;
					int nextDataStart = 0;
					int nextDataStop = 0;
					if (sector.data != null) {
						nextDataStart = sector.data.info.startBit + startBit;
						nextDataStop = nextDataStart + sector.data.info.bitCount;
					}
					if (currentBit == nextIdStart)
						phase = Phases.p_id;
					else if (currentBit == nextIdStop) {
						if (sector.data == null)
							sectorNumber = (sectorNumber + 1) % sectorCount;
						phase = Phases.p_gap;
					}
					else if (currentBit == nextDataStart)
						phase = Phases.p_data;
					else if (currentBit == nextDataStop) {
						phase = Phases.p_gap;
						sectorNumber = (sectorNumber + 1) % sectorCount;
					}
				}

				if (dsr.DataReady) {
					byteCount++;

					if (dsr.IAM & dsr.LastSyncA1) {
						Global.Disk[track, side].track.hasSyncSeq = true;
					}
					if ((dsr.Data >= 0xF5) && (dsr.Data <= 0xF7) && (phase == Phases.p_gap))
						invalidByteInGap++;

					// check NFA
					if ((dsr.MFM == 0) || (dsr.MFM == 0xFFFF)) {
						if (nfaCount == 0) {
							nfaStart = currentTime;
						}
						nfaCount++;
					}
					else {
						if (nfaCount > 8)
							Global.Disk[track, side].track.flags |= Track.NO_FLUX_AREA;
						nfaCount = 0;
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
		/// <returns>The structure that contains all the address info for a track</returns>
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
			// TODO we may want to continue on sync seq
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

				// TODO Better ?????
				//int testBit = 0x1 << (currentBit % 8);
				//if ((bits & (byte)testBit) != 0) {
				//	int flux = Global.fluxWidth(currentBit, track, side);
				//	currentTime += flux;
				//}
				currentTime += Global.Disk[track, side].packedFluxData.bitWidth[currentByte];

				dsr.shiftBitsIn(bit);

				if (dsr.DataReady) {
					byteCount++;

					if (phase != Phases.p_id) {	// not in ID						
						if (dsr.IAM) {
							if (dsr.LastSyncA1) {
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

										// Check SWS
										if (Global.Disk[track, side].sectors.Count > 1) {
											Sector prevSect = Global.Disk[track, side].sectors[Global.Disk[track, side].sectors.Count - 2];
											// we take a min value for GAP3 (22+12+3) and min for GAP4 (24+8+3) 
											int prevEnd = prevSect.id.info.startByte + prevSect.id.info.byteCount + 37 + Global.sectSize(prevSect.id.size) + 10;
											if (byteCount < prevEnd)
												Global.Disk[track, side].sectors[Global.Disk[track, side].sectors.Count - 1].flags |= Sector.SEC_IN_SEC;
										}
										break;
									default:
										// other AM re-enable sync search
										dsr.sectorMode();
										break;
								}	// switch AM
							}	// IAM after 3 A1 sync seq
							else dsr.sectorMode();
						}	// IAM
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

			//// since we now continue when finding sync we can have duplicate sector in first last position
			//if ((ac.ids.Count > 0) && (ac.ids[0].number == ac.ids[ac.ids.Count - 1].number) && (ac.blocs[ac.ids.Count - 1].startTime > trackLength))
			//		ac.ids.RemoveAt(ac.ids.Count - 1);
		}	// end of readAddr()


		/// <summary>
		/// Reads the DATA field of a specified sector
		/// </summary>
		/// <param name="track">Track</param>
		/// <param name="side">Side</param>
		/// <param name="rev">Revolution value</param>
		/// <param name="sectToRead">Sector number to read</param>
		/// <param name="position">The sector to find must be located after this specified position in nanosecond</param>
		/// <param name="compare">If set to true we are in compare mode, else we are in read mode</param>
		/// <returns>a ReadSectInfo block</returns>
		/// <remarks>The position parameter is necessary to read the data of a sector multiply  defined 
		/// (index.e. we have several sectors with same number). Otherwise the data for the first encountered
		/// sector would always be returned (equivalent to position==0).
		/// When compare is false we are reading the sector normally and we fill the SectorContent structure. 
		/// But if compare==true we are in "compare" mode. In that case we read the sector using the rev parameter 
		/// and we compare the data buffer read with the "reference" data buffer to fill the fuzzyMask structure.</remarks>
		public static void readSector(int track, int side, int rev, byte sectToRead, int idBitPosition, bool compare) {
			Sector sector = findSector(track, side, sectToRead, idBitPosition);
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
			DSR dsr = new DSR();
			dsr.sectorMode();
			DataField data = null;
			int flags = 0;
			bool p_inner = false;

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
				currentTime += bitWidth;

				dsr.shiftBitsIn(bit);
				if (dsr.DataReady) {
					byteCount++;

					if ((phase != Phases.p_id) && (phase != Phases.p_data)) {
						// We are in a GAP						
						if (dsr.IAM) {
							if (dsr.LastSyncA1) {
								switch (dsr.Data) {
									case 0xFC:	// IBM IAM
									case 0xFD:
									case 0xFE:	// IDAM - start of an ID block
									case 0xFF:
										// if we have already found the requested ID and we find another ID
										// this means that sector has no Data field. Therefore we should break
										if (idFound) {
											//dataFound = false;
											phase = Phases.p_id;
											breakLoop = true;
											break;
										}
										// if we have not yet reach the requested position we ignore
										if ((currentBit - startBit) < idBitPosition) {
											dsr.sectorMode();
											phase = Phases.p_gap;
											break;
										}
										// otherwise we need to read the ID block
										byteLeftInBlock = 7;
										phase = Phases.p_id;
										break;

									case 0xF8:	// DDAM
									case 0xF9:
									case 0xFA:
									case 0xFB:	// DAM - Start of a DATA block
										// If we have found the right ID field we check that this
										// Data field is less than 43 bytes apart from the Id
										// id field is already 7 bytes so 43+7 = 50
										if (idFound && ((byteCount - sector.id.info.startByte) <= 50)) {
											data = new DataField();
											data.buffer = new TrackBuffer();
											data.iam = dsr.Data;
											//dataFound = true;
											byteLeftInBlock = Global.sectSize(sector.id.size) + 3; // AM + 2 CRC + Size
											phase = Phases.p_data;
											p_inner = false;
											widthOver = widthUnder = 0;

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
							}	// $C2 Sync + IAM ==> ignore
							else dsr.sectorMode();
						}	// IAM
					}	// GAP
					else {
						// We are in an ID or DATA segment
						byteLeftInBlock--;
						if (phase == Phases.p_id)
							// ID segment
							switch (byteLeftInBlock) {
								// we only check if we have the right sector number
								case 4: 
									if (sectToRead != dsr.Data) {
										//phase = Phases.p_gap;
										idFound = false;
										dsr.sectorMode();
									}
									else
										idFound = true;
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
									if (widthOver > (Global.sectSize(sector.id.size) / 2))
										Bit.setBits(ref flags, Sector.WIDTH_ABOVE);
									if (widthUnder > (Global.sectSize(sector.id.size) / 2))
										Bit.setBits(ref flags, Sector.WIDTH_BELOW);
									if ((widthOver > (Global.sectSize(sector.id.size) / 10)) && (widthUnder > (Global.sectSize(sector.id.size) / 10)))
										Bit.setBits(ref flags, Sector.WIDTH_INTRA);
									phase = Phases.p_gap;
									breakLoop = true;
									break;
								default:
									data.buffer.addByte(dsr.Data, dsr.MFM, (ushort)bitWidth, currentTime, phase);
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

			// if no data field found nothing to do
			if (data == null) return;

			// We OR the flags from all revolutions for max detection
			sector.flags |= flags;


			if (!compare) {
				sector.data = data;
			}
			else {
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
				if (sector.data.fuzzy != null)
					fuzzy = sector.data.fuzzy;
				else {
					int dataSize = Global.sectSize(sector.id.size);
					fuzzy = new byte[dataSize];
				}
				bool fuzzyFound = false;

				for (int i = 0; i < size; i++) {
					byte xor = (byte)(data.buffer.Bytes[i].dataByte ^ sector.data.buffer.Bytes[i].dataByte);
					if (xor != 0) {
						fuzzyFound = true;
						fuzzy[i] |= xor;
					}
				}	// compare all bytes

				if (fuzzyFound)
					sector.data.fuzzy = fuzzy;
			} // compare mode
		}	// readSector()


		/// <summary>
		/// This function calls readTrack(), readAddr() and readSector().
		/// </summary>
		/// <remarks>ReadTRack() is called first, then readAddr(). For all sector founds by 
		/// readAddr() the function readeSctor() is called a first time to read the content
		/// for the current revolution, then it calls again the readSector() function in "compare"
		/// mode for all others revolutions. This allows to find fuzzy bits.</remarks>
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
			for (int i = 0; i < Global.Disk[track, side].sectors.Count; i++) {
				byte sectorNumber = Global.Disk[track, side].sectors[i].id.number;
				int idBitPosition = Global.Disk[track, side].sectors[i].id.info.startBit;
				readSector(track, side, rev, sectorNumber, idBitPosition, false);

				//// now multiple readSector() for fuzzy bits
				for (int j = 0; j < Global.Disk[track, side].packedFluxDataRev.Count(); j++) {
					if (j == rev) continue;
					readSector(track, side, j, sectorNumber, idBitPosition, true);
				}

			//	if (sc.fuzzyMask != null)
			//		tc.hasProtection = true;				
			}
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
