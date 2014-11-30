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
	enum Sync { No, A1, C2 }

	/// <summary>
	/// Different phases inside a sector: ID, Inner Gap, Data, Outer Gap
	/// </summary>
	public enum Phases : byte {
		p_id = 0,	// ID (Address) block
		p_inner,	// GAP inside sectors (between ID and DATA)
		p_data,		// DATA block
		p_outer,	// GAP outside (between) sectors
	}


	/// <summary>Contains all information for a sector.</summary>
	/// <remarks>Normally a sector contains 4 FieldInfo (one per phase): ID / INNER / DATA / OUTER
	/// but may contain less. We use a pseudo sector 0 that contains the GAP placed before the
	/// first real sector and may contain DATA for a track with Data beyond index. Therefore in
	/// read track we have an extra pseudo sector. \n 
	/// The SectorInfo structure contains a pointer to an IDField
	/// structure to copy all the data read from the ID field</remarks>
	public class SectorInfo {
		/// <summary>
		/// For each phase p_id, p_inner, p_data, p_outer we have an associated information block
		/// </summary>
		public FieldInfo[] phases = new FieldInfo[4];	// one block per phase
		/// <summary>Pointer to the IdField</summary>
		public IDField addr = new IDField();			// store ID info
		/// <summary>
		/// If we find a sector within another sector we store here the number of the sector 
		/// that contains this one, or -1 otherwise
		/// </summary>
		public int sws = -1;
		/// <summary>Set to true if the Sector has been validated ???</summary>
		public bool valid = false;
		/// <summary>Sector start time = first ID byte</summary> 
		public int startTime;
		/// <summary>Sector end time = last inter GAP byte</summary> 
		public int endTime;
	}

	/// <summary>Store information for each sync found on the track</summary>
	public class SyncInfo {
		/// <summary>Time position of the sync</summary>
		public int startTime;
		/// <summary>Byte position of the sync</summary>
		public int startByte;
		/// <summary>count the number of Sync in sequence</summary>
		public int syncInSeq;
		/// <summary>Sync value</summary>
		public ushort value;
		/// <summary>Bad Sync sequence = part of a bad sequence</summary>
		public bool badSync = false;
	}

	/// <summary>
	/// Store information collected after doing a read track command.
	/// </summary>
	/// <remarks>Contains information for a complete track. This information is filled
	/// by the readTrack() function. The two main fields are the List of sectors and the
	/// buffer. Note that sectors[0] is a pseudo sector to store GAP and eventually DATA 
	/// information before detecting the first ID on the track and should not be
	/// considered as a real sector</remarks>
	public class TrackContent {
		/// <summary>List of all sectors in the track</summary>
		public List<SectorInfo> sectors = new List<SectorInfo>();
		/// <summary>Length of the track in nanoseconds</summary>
		public int timeLength = 0;
		/// <summary> Number of bytes in the track</summary>
		public int byteCount = 0;
		/// <summary>List of sync found on the track</summary> 
		public List<SyncInfo> syncs = new List<SyncInfo>();
		/// <summary>buffer for all the byte read in the track</summary>
		public TrackBuffer buffer = null;

		/// <summary>Set to true when protections have been found for this track</summary>
		/// <remarks>This include track as well as sector protections. This is primarily 
		/// used to set track grid buttons background color.</remarks>
		public bool hasProtection = false;

		/// <summary>Set to true when the track has a DATA segment that span over index pulse</summary>
		public bool dataOverIdx;
		/// <summary>Set to true when the track has an ID segment that span over index pulse</summary>
		public bool idOverIdx;
		/// <summary>Set to true when the track has Data segment beyond the index pulse</summary>
		public bool dataBeyondIdx;
		/// <summary>Position of NFA on track</summary>
		public int NFA = -1;

		/// <summary>All the sectors of the track are numbered sequentially from 1 to n</summary>
		public bool sectorInSeq = true;
		/// <summary>All the sectors use valid sector numbers from 0x00-0xF4</summary>
		public bool sectorValidNumber = true;
		/// <summary>All the sectors of the track have unique sector number</summary>
		public bool sectorUnique = true;
		/// <summary>All sectors in the track have standard timing values</summary>
		public bool sectorStandard = true;
		/// <summary>All sector has standard size</summary>
		public bool sectorSandardSize = true;
		/// <summary>Short track</summary>
		public bool shortTrack = false;
		/// <summary>Long Track</summary>
		public bool longTrack = false;
		public bool incompleteTrack = false;
	}

	/// <summary>
	/// Store the content read from a sector ID (Address) Field
	/// </summary>
	public struct IDField {
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
	}




	/// <summary>
	/// Store the information collected after doing a read address command
	/// </summary>
	/// <remarks>Contains information for all ID found on a track. This information
	/// is filled by the readAddr() function</remarks>
	public class AddrContent {
		/// <summary>List of all ID found in the track</summary>
		public List<IDField> ids = new List<IDField>();
		/// <summary>Block information associated with all ID found in the track</summary>
		public List<FieldInfo> blocs = new List<FieldInfo>();
	}


	/// <summary>
	/// Store the information collected after doing a read sector command
	/// </summary>
	/// <remarks>Contains information for a specific sector. This information
	/// is filled by the readSector() function.</remarks>
	public class SectorContent {
		/// <summary>Block information for DATA field of the sector</summary>
		public FieldInfo dataBlock;
		/// <summary>buffer for all the byte read in the DATA field of the sector</summary>
		public TrackBuffer data;
		/// <summary>Block information for ID field of the sector</summary>
		public FieldInfo idBlock;
		/// <summary>contains the information of the ID field of the sector</summary>
		public IDField id;
		/// <summary>This flag is set to true when a sector has a DATA field. If the 
		///sector contains an ID field but no DATA field the flag is set to false.</summary>
		public bool sectHasData;
		/// <summary>This byte array contains the result of XOR operation on same byte on two
		/// or more different revolutions. This is an indicator of fuzzy byte. When no 
		/// difference is found the mask pointer is kept null (no memory allocation)</summary>
		public TrackBuffer fuzzyMask;

		/// <summary>Sector has more than 50% of the bytes with clock above 5%</summary>
		public bool sectorOverClocked = false;
		/// <summary>Sector has more than 50% of the bytes with clock below 5%</summary>
		public bool sectorUnderClocked = false;
		/// <summary>Sector has more than 25% of the bytes with clock above 5% AND more than 25% of the bytes with clock below 5%</summary>
		public bool sectorIntraVariation = false;
		/// <summary>Sector has data byte in range 0xF5 - 0xF7 in the GAP field</summary>
		public bool sectorBadDataInGap = false;
		/// <summary>The Track field in the ID is not equal to current track</summary>
		public bool sectorValidTrack = true;
		/// <summary>Sector contains invalid sync sequences</summary>
		public bool sectorBadSync = false;
	}






	/// <summary>
	/// Store information about a block of data read from track.
	/// /// </summary>
	public struct FieldInfo {
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

		/// <summary>CRC value at the end of reading an ID or DATA block. Should be zero on a "good" block</summary>
		public ushort crc;

		/// <summary>Count the number of over-clocked bytes</summary>
		public int overClock;
		/// <summary>Count the number of under-clocked bytes</summary>
		public int underClock;
		/// <summary>Count the number of bad sync</summary>
		public int badSync;
		/// <summary>Count the number of timing violations (under 2 or over 4 inspection windows)</summary>
		public int timingViolation;
		/// <summary>Count the number of invalid data into gap</summary>
		public int invDataGap;
		/// <summary>?? TODO</summary>
		public int splice;
	}


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
		public FieldInfo info = new FieldInfo();
	}

	public class DataField {
		/// <summary>Store the Index Address Mark</summary> 
		public byte iam;

		/// <summary>buffer for all the byte read in the DATA field of the sector</summary>
		public TrackBuffer buffer;

		/// <summary>Store the CRC on two bytes</summary> 
		public ushort crc;

		/// <summary> Contains information about the field </summary>
		public FieldInfo info = new FieldInfo();

		public byte[] fuzzy;
	}

	public class Sector {
		public IdField id;
		public DataField data;
	}


	public class Track {
		/// <summary>buffer for all the byte read in the DATA field of the sector</summary>
		public TrackBuffer buffer;

		/// <summary> Contains information about the field </summary>
		public FieldInfo info = new FieldInfo();

	}




	/// <summary>Emulate WD1772 FDC Commands</summary>
	public class FDC {
		//private readonly static int upperClock = 2000 + (4 * 20);
		//private readonly static int lowerClock = 2000 - (4 * 20);

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

			// We read from the beginning to end of a track looking with AM decoder on all time
			for (int currentBit = startBit; (currentBit < stopBit); currentBit++) {
				int currentByte = currentBit / 8;
				int bitInByte = currentBit % 8;
				byte bits = Global.Disk[track, side].packedFluxData.packedBits[currentByte];
				int bit = (bits >> bitInByte) & 0x1;
				int bitWidth = Global.Disk[track, side].packedFluxData.bitWidth[currentByte];
				currentTime += bitWidth;
				dsr.shiftBitsIn(bit);

				if (dsr.DataReady) {
					byteCount++;
					Global.Disk[track, side].track.buffer.addByte(dsr.Data, dsr.MFM, (ushort)bitWidth, currentTime);
				}	// data ready

			}	// for all bits

			Global.Disk[track, side].track.info.byteCount = byteCount;
			Global.Disk[track, side].track.info.timeLength = currentTime;

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

			Phases phase = Phases.p_outer;
			DSR dsr = new DSR();
			dsr.sectorMode();

			// We read from the beginning to end of a track looking only for ID field (in current revolution).
			// If the end of track happen in the middle of an ID field we continue with data from the next revolution.
			// TODO we may want to continue on sync seq
			for (int currentBit = startBit; (currentBit < stopBit) || (phase == Phases.p_id); currentBit++) {
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
								phase = Phases.p_outer;
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
			//// if no list of sectors exist we need to create one 
			//// ? TODO is this the best place to create the list!
			//if (Global.Disk[track, side].sectorsContent == null)
			//	Global.Disk[track, side].sectorsContent = new List<SectorContent>();

			Sector sector = findSector(track, side, sectToRead, idBitPosition);

			//// if not in compare mode and sector already there nothing to do
			//SectorContent sref = findSectorContent(track, side, sectToRead, position);
			//if (!compare && (sref != null))
			//	return sref;

			//// sanity check in compare mode the reference should already be there
			//if (compare && (sref == null)) {
			//	sref = readSector(track, side, rev, sectToRead, position, false);
			//	Debug.Assert(!(compare && (sref == null)), "Read Sector: Reference sector not found in compare mode",
			//		String.Format("Track {0:D2}.{1} Rev {2} Sector {3} at {4}", track, side, rev, sectToRead, position));
			//};

			int startBit = Global.Disk[track, side].packedFluxDataRev[rev].firstFluxBit;
			int stopBit = startBit + Global.Disk[track, side].packedFluxDataRev[rev].bitCount;
			int lastBye = Global.Disk[track, side].packedFluxData.packedBits.Count();
			int byteLeftInBlock = 0;
			int currentTime = 0;
			int byteCount = 0;
			ushort crc = 0;
			bool idFound = false;
			bool breakLoop = false;

			Phases phase = Phases.p_outer;
			DSR dsr = new DSR();
			dsr.sectorMode();
			DataField data = null;

			//// if not there or we are in compare mode create a new sectorContent
			//SectorContent sc = new SectorContent();
			//sc.data = new TrackBuffer();

			// we normally loop from start to stop for the specified revolution.
			// However we continue if we are in the middle of ID, Inner GAP, or DATA field
			for (int currentBit = startBit; (currentBit < stopBit) || (phase != Phases.p_outer); currentBit++) {
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
							// TODO check only after 2 A1 and not 3C1
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
											phase = Phases.p_outer;
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
										if (idFound && ((byteCount - sector.id.info.startByte) <= 50)) {
											data = new DataField();
											data.buffer = new TrackBuffer();
											data.iam = dsr.Data;
											//dataFound = true;
											byteLeftInBlock = Global.sectSize(sector.id.size) + 3; // AM + 2 CRC + Size
											phase = Phases.p_data;

											data.info.startBit = currentBit - startBit;
											data.info.startByte = byteCount;
											data.info.startTime = currentTime;
										}
										else {
											dsr.sectorMode();
											phase = Phases.p_outer;
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
										phase = Phases.p_outer;
										idFound = false;
										dsr.sectorMode();
									}
									else
										idFound = true;
									break;
								case 0:
									dsr.sectorMode();
									phase = Phases.p_inner;
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
									data.info.byteCount = byteCount - data.info.startByte;
									data.info.timeLength = currentTime - data.info.startTime;
									phase = Phases.p_outer;
									breakLoop = true;
									break;
								default:
									data.buffer.addByte(dsr.Data, dsr.MFM, (ushort)bitWidth, currentTime);
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
				size -= 3;
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

			if (Global.Disk[track, side].sectors == null)
				Global.Disk[track, side].sectors = new List<Sector>();

			readTrack(track, side, rev);
			readAddr(track, side, rev);
			for (int i = 0; i < Global.Disk[track, side].sectors.Count; i++) {
				byte sectorNumber = Global.Disk[track, side].sectors[i].id.number;
				int idBitPosition = Global.Disk[track, side].sectors[i].id.info.startBit;
				readSector(track, side, rev, sectorNumber, idBitPosition, false);

				// now multiple readSector() for fuzzy bits
				for (int j = 0; j < Global.Disk[track, side].packedFluxDataRev.Count(); j++) {
					if (j == rev) continue;
					readSector(track, side, j, sectorNumber, idBitPosition, true);
				}

			//	if (sc.fuzzyMask != null)
			//		tc.hasProtection = true;				
			}

			//Protections.checkProtection(track, side);
		}

		/// <summary>
		/// Free the track, address, and sectors content
		/// </summary>
		/// <param name="track"></param>
		/// <param name="side"></param>
		public static void clearTrackContent(int track, int side) {
			Global.Disk[track, side].trackContent = null;
			Global.Disk[track, side].addrContent = null;
			Global.Disk[track, side].sectorsContent = null;
		}


		/// <summary>
		/// Find a specific sector
		/// </summary>
		/// <param name="track">The track to use</param>
		/// <param name="side">The size to use</param>
		/// <param name="num">The sector to find</param>
		/// <param name="position">The starting position of the sector</param>
		/// <returns>The sector found or null if not found</returns>
		/// <remarks>It is necessary  to use the position parameter for the case where several 
		/// sectors with the same number are present oin a track</remarks>
		public static SectorContent findSectorContent(int track, int side, byte num, int position) {
			List<SectorContent> sectors = Global.Disk[track, side].sectorsContent;
			foreach(SectorContent sector in sectors)
				// we need to allow a certain slack for the position ?
				if ((sector.id.number == num) && (sector.idBlock.startTime >= (position - 32000)))
					return sector;
			return null;
		}

		public static Sector findSector(int track, int side, int num, int idBitPosition) {
			List<Sector> sectors = Global.Disk[track, side].sectors;
			foreach (Sector sector in sectors)
				if ((sector.id.number == num) && (sector.id.info.startBit == idBitPosition))
					return sector;
			return null;
		}

	}
}
