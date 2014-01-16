/*!
@file FDC.cs
 * 
<summary>Emulate WD1772 FDC commands: read track, read address, read sector</summary>

<div class="jlg">Copyright (C) 2011 Jean Louis-Guerin\n\n
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

using KFReaderSpace;
using GlobalSpace;
using Aufit;

namespace WD1772Space {
	/// <summary>
	/// Different phases inside a sector: ID, Inner Gap, Data, Outer Gap
	/// </summary>
	public enum Phases : byte {
		p_id = 0,		// ID (Address) block
		p_inner,	// GAP inside sectors (between ID and DATA)
		p_data,		// DATA block
		p_outer,	// GAP outside (between) sectors
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
	/// Various Information about a block of data read from track. Used for: GAP / ID / DATA
	/// </summary>
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

	/// <summary>Contains all information for a sector.</summary>
	/// <remarks>Normally a sector contains 4 BlockInfo (one per phase): ID / INNER / DATA / OUTER
	/// but may contain less. We use a pseudo sector 0 that contains the GAP placed before the
	/// first real sector and may contain DATA for a track with Data beyond index. Therefore in
	/// read track we have an extra pseudo sector. This structure contains a pointer to an IDField
	/// structure to copy all the data read from the ID field</remarks>
	public class SectorInfo {
		/// <summary>
		/// For each phase p_id, p_inner, p_data, p_outer we have an associated block information
		/// </summary>
		public BlockInfo[] phases = new BlockInfo[4];	// one block per phase
		/// <summary>Pointer to the IdSegment</summary>
		public IDField addr = new IDField();		// store ID info
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
		/// <summary>value of the Sync in sequence</summary>
		public int syncInSeq;
		/// <summary>Sync value</summary>
		public byte value;
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
		public TrackBuffer buffer;

		/// <summary>Set to true when protections have been found for this track</summary>
		/// <remarks>This include track as well as sector protections</remarks>
		public bool hasProtection = false;

		/// <summary>Set to true when the track has a DATA segment that span over index pulse</summary>
		public bool dataOverIdx;
		/// <summary>Set to true when the track has an ID segment that span over index pulse</summary>
		public bool idOverIdx;
		/// <summary>Set to true when the track has Data segment beyond the index pulse</summary>
		public bool dataBeyondIdx;

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
		/// <summary>Position of NFA on track</summary>
		public int NFA = -1;
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
		public List<BlockInfo> blocs = new List<BlockInfo>();
	}

	/// <summary>
	/// Store the information collected after doing a read sector command
	/// </summary>
	/// <remarks>Contains information for a specific sector. This information
	/// is filled by the readSector() function.</remarks>
	public class SectorContent {
		/// <summary>Block information for DATA field of the sector</summary>
		public BlockInfo dataBlock;
		/// <summary>buffer for all the byte read in the DATA field of the sector</summary>
		public TrackBuffer buffer;
		/// <summary>Block information for ID field of the sector</summary>
		public BlockInfo idBlock;
		/// <summary>contains the information of the ID field of the sector</summary>
		public IDField id;
		/// <summary>This flag is set to true when a sector has a DATA field. If the 
		///sector contains an ID field but no DATA field the flag is set to false.</summary>
		public bool sectHasData;
		/// <summary>This byte array contains the result of XOR operation on same byte on two
		/// different revolution. This is an indicator of fuzzy byte. When no difference is found
		/// the mask pointer is kept null (no memory allocation)</summary>
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
	/// WD1772 FDC Commands
	/// </summary>
	public class FDC {
		private readonly static int upperClock = 2000 + (4 * 20);
		private readonly static int lowerClock = 2000 - (4 * 20);

		/// <summary>Read a track by decoding all the track MFM bits</summary>
		/// <remarks>This function interprets the bits stream as a WD1772 would do with a read track command. 
		/// It fills a ReadTrackInfo structure that gives detailed information about the track 
		/// as well as a MFMBuffer that contains all the decoded bytes.</remarks>
		/// TODO do something special for SDS ?
		public static TrackContent readTrack(int track, int side, int rev, bool forceRead = false) {
			// check if already there
			TrackContent tc = Global.Disk[track, side].trackContent;
			if ((tc != null) && !forceRead)
				return tc;

			tc = new TrackContent();
			tc.buffer = new TrackBuffer();
			Global.Disk[track, side].trackContent = tc;

			Global.DiskTrack dt = Global.Disk[track, side];
			int start = Global.firstIndex(track, side, rev);
			int stop = Global.lastIndex(track, side, rev);
			int indexFirstFluxCorrection = Global.indexTimePosition(track, side, rev);

			// add dummy sector[0] to store GAP and potentially DATA segment information
			tc.sectors.Add(new SectorInfo());

			Phases phase = Phases.p_outer;
			DSR dsr = new DSR();
			int syncInSeq = 0;			// number of sync found
			byte lastSync = 0;			// last sync value found
			int byteLeftInBlock = 0;	// byte to go in p_id and p_data
			int blockStartByte = 0;		// first byte of the block
			int blockStartTime = 0;		// position of start of the block in nanosecond
			int currentTime = 0;		// running time
			int prevBlockEndTime = 0;	// end time of the previous block
			bool sws = false;			// sector within sector found
			ushort crc = 0;				// running CRC
			int currentBit = 0;			// running bit position
			int clock;					// value of decoded clock
			int nbWin;					// number of inspection window
			int clockCount = 0;			// number of clock accumulated for avg
			int clockAvg = 0;			// average clock value of assembled bits
			int gapPosition = 0;		// position of byte inside gap
			int lastTrackByte = 0;
			int lastTrackTime = 0;

			// normally we stop at the stop value (last transition of the track)
			// however we continue if we are in middle of an ID or an intra-gap field (after idOverIdx)
			// so we detect and display correctly ID over index ...
			// added syncCount because we may have just the start of the sync of ID are very end !!!
			// Good example is ComputerHitsVolume2 Disk 2 track 78
			tc.idOverIdx = false;
			for (int i = start; 
				(i < stop) || (phase == Phases.p_id) || ((phase == Phases.p_inner) && tc.idOverIdx) || (syncInSeq > 0); 
				i++) {
				// we cannot go beyond last flux so we always abort. This imply that last revolution might no be detected correctly
				if (i >= dt.fluxData.fluxCount) break;

				if ((i >= stop) && (phase == Phases.p_id))
					tc.idOverIdx = true;
				// we store the number of byte and timing
				if ((i >= stop) && (lastTrackByte == 0)) {
					lastTrackByte = tc.byteCount;
					lastTrackTime = currentTime;
				}
				
				int flux = Global.sampleToTiming(dt.fluxData.fluxValue[i]);
				if (i == start)				flux -= indexFirstFluxCorrection;
				else if (i == (stop - 1))	flux += indexFirstFluxCorrection;
				if (flux > 150000) {
					tc.NFA = currentTime;
					nbWin = flux / 2000;	// NFA
				}
				else 
					nbWin = dt.decodeData.fluxInWindow[i];
				currentTime += flux;
				currentBit += nbWin;

				dsr.shiftBitsIn(nbWin);	// push data in register
				clock = dt.decodeData.clockPeriod[i];
				clockAvg += clock;
				clockCount++;

				// check timing violation and write splice
				if ((nbWin < 2) || nbWin > 4) {
					tc.sectors[tc.sectors.Count - 1].phases[(byte)phase].timingViolation++;
					// here we check if the timing violation result from a write splice. Write splice
					// can result from "track write splice" (nothing we can guess here) but the can also
					// result from "sector write splice". In that case they are located in position 18-22
					// in intra-gap (beginning of sect) or 0-2 in inter-gap (end of sect).
					if (((phase == Phases.p_inner) && (gapPosition > 19) && (gapPosition < 23)) ||
						 ((phase == Phases.p_outer) && (gapPosition < 2)))
						tc.sectors[tc.sectors.Count - 1].phases[(byte)phase].splice++;
				}	// timing violation

				// check if we have accumulated enough bytes
				while (dsr.byteReady()) {
					tc.byteCount++;
					gapPosition++;

					// take the average clock value for all bits
					if (clockCount > 0) {
						clock = clockAvg / clockCount;
						clockAvg = 0;
						clockCount = 0;
					}	// only one time

					// check above or below normal
					if (clock > upperClock) {
						tc.sectors[tc.sectors.Count - 1].phases[(byte)phase].overClock++;
					}	// above normal
					if (clock < lowerClock) {
						tc.sectors[tc.sectors.Count - 1].phases[(byte)phase].underClock++;
					}	// below normal

					int bitShift = dsr.searchSynch();
					dsr.extract();
					currentBit -= bitShift;		// correction current bit
				
					if (dsr.SyncFound) {
						syncInSeq++;
						tc.syncs.Add(new SyncInfo());
						tc.syncs[tc.syncs.Count - 1].value = dsr.Data;
						tc.syncs[tc.syncs.Count - 1].startTime = currentTime;
						tc.syncs[tc.syncs.Count - 1].startByte = tc.byteCount;
						tc.syncs[tc.syncs.Count - 1].syncInSeq = syncInSeq;
						tc.syncs[tc.syncs.Count - 1].badSync = false;

						// check bad sequence
						if ((syncInSeq > 1) && (dsr.Data != lastSync)) {
							syncInSeq = 1;
							// We do not report 0xC2 followed by 0xA1 as an error because this happen often during re-sync
							if (lastSync != 0xC2) {
								tc.syncs[tc.syncs.Count - 1].badSync = true;
								tc.sectors[tc.sectors.Count - 1].phases[(byte)phase].badSync++;
							}
						}	// bad sync sequence

						// we reset the running CRC on first A1
						if ((syncInSeq == 1) && (dsr.Data == 0xA1)) crc = 0xFFFF;

						// check long sync sequence - we only count one
						if (syncInSeq == 4) {
							tc.sectors[tc.sectors.Count - 1].phases[(byte)phase].badSync++;
							tc.syncs[tc.syncs.Count - 1].badSync = true;
						}
						lastSync = dsr.Data;
					}	// sync byte

					// check short sync sequence
					if ((syncInSeq > 0) && (syncInSeq < 3) && !dsr.SyncFound) {
						syncInSeq = 0;
						tc.syncs[tc.syncs.Count - 1].badSync = true;
						tc.sectors[tc.sectors.Count - 1].phases[(byte)phase].badSync++;
					}	// short sync sequence
					
					if ((phase != Phases.p_id) && (phase != Phases.p_data)) {	// We are in a GAP						
						// check for invalid data into gap
						if ((dsr.Data >= 0xF5) && (dsr.Data <= 0xF7)) {
							tc.sectors[tc.sectors.Count - 1].phases[(byte)phase].invDataGap++;
						} // IDG invalid data in GAP

						if ((syncInSeq > 2) && !dsr.SyncFound) {
							// 3+ sync & data not sync => should be an AM
							syncInSeq = 0;	// reset sync count
							switch (dsr.Data) {
								case 0xFC:	// IAM - IBM 34 Format - ignore
								case 0xFD:
									break;

								case 0xFE:	// IDAM - start of an ID block
								case 0xFF:
									// if (phase == Phases.p_inner) TODO ID no DATA ?
									UpdateTrackInfo(tc, phase, ref blockStartByte, ref blockStartTime, prevBlockEndTime);
									tc.sectors[tc.sectors.Count - 1].endTime = prevBlockEndTime;

									tc.sectors.Add(new SectorInfo());
									tc.sectors[tc.sectors.Count-1].addr.iam = dsr.Data;
									tc.sectors[tc.sectors.Count - 1].startTime = prevBlockEndTime;
									byteLeftInBlock = 7;
									phase = Phases.p_id;
									tc.sectors[tc.sectors.Count - 1].phases[(byte)phase].startBit = currentBit;
									break;

								case 0xFB:	// DAM - Start of a DATA block
								case 0xF8:	// DDAM
								case 0xFA:
								case 0xF9:
									UpdateTrackInfo(tc, phase, ref blockStartByte, ref blockStartTime, prevBlockEndTime);

									// check DATA before found a sector ID
									if (tc.sectors.Count == 1) {
										byteLeftInBlock = 512;	// best guess as we do not have ID information
									}	// no ID before
									else
										byteLeftInBlock = Global.sectSize(tc.sectors[tc.sectors.Count-1].addr.size) + 3; // AM + 2 CRC + Size
									
									phase = Phases.p_data;
									tc.sectors[tc.sectors.Count - 1].phases[(byte)phase].startBit = currentBit;
									break;

								default:	// not an AM after 3+ sync : invalid but ignore								
									break;
							}	// switch AM
						}	// 3+ sync
					}	// GAP
					else {	// We are in an ID or DATA segment						
						byteLeftInBlock--;
						if (phase == Phases.p_id) switch (byteLeftInBlock) {	// ID segment
								case 6: tc.sectors[tc.sectors.Count - 1].addr.track = dsr.Data; break;
								case 5: tc.sectors[tc.sectors.Count - 1].addr.head = dsr.Data; break;
								case 4: tc.sectors[tc.sectors.Count - 1].addr.number = dsr.Data; break;
								case 3: tc.sectors[tc.sectors.Count - 1].addr.size = dsr.Data; break;
								case 2: tc.sectors[tc.sectors.Count - 1].addr.crc = dsr.Data; break;
								case 1:
									tc.sectors[tc.sectors.Count - 1].addr.crc = (ushort)(tc.sectors[tc.sectors.Count - 1].addr.crc << 8);
									tc.sectors[tc.sectors.Count - 1].addr.crc += dsr.Data;
									break;
								case 0:	// end of ID block
									UpdateTrackInfo(tc, phase, ref blockStartByte, ref blockStartTime, prevBlockEndTime);

									if (sws) {
										sws = false;
										tc.sectors[tc.sectors.Count - 1].sws = tc.sectors[tc.sectors.Count - 2].addr.number;
									}
									// ID Block we store the CRC for checking
									tc.sectors[tc.sectors.Count - 1].phases[(byte)phase].crc = crc;
									phase = Phases.p_inner;
									tc.sectors[tc.sectors.Count - 1].phases[(byte)phase].startBit = currentBit;
									gapPosition = 0;
									break;
						}
						else {	// DATA Segment 
							// 3+ sync followed by IDAM => SWS
							// note: we also check that sync is A1 as we may detect C2 in data block
							if ((syncInSeq > 2) && (lastSync == 0xA1) && !dsr.SyncFound && (dsr.Data == 0xFE)) {
								sws = true;
								syncInSeq = 0;
								UpdateTrackInfo(tc, phase, ref blockStartByte, ref blockStartTime, prevBlockEndTime);

								tc.sectors[tc.sectors.Count - 1].phases[(byte)phase].crc = 0xFFFF; // set to bad value
								tc.sectors[tc.sectors.Count - 1].endTime = prevBlockEndTime;

								tc.sectors.Add(new SectorInfo());
								tc.sectors[tc.sectors.Count - 1].startTime = prevBlockEndTime;
								tc.sectors[tc.sectors.Count - 1].addr.iam = dsr.Data;
								byteLeftInBlock = 7;
								phase = Phases.p_id;
								tc.sectors[tc.sectors.Count - 1].phases[(byte)phase].startBit = currentBit;
								gapPosition = 0;
							}	// SWS
							else switch (byteLeftInBlock) {
									//case 2: tc.sectors[tc.sectors.Count - 1].dataSegmentCrc = dsr.Data; break;
									//case 1:
									//	tc.sectors[tc.sectors.Count - 1].dataSegmentCrc = (ushort)(tc.sectors[tc.sectors.Count - 1].addr.crc << 8);
									//	tc.sectors[tc.sectors.Count - 1].dataSegmentCrc += dsr.Data;
									//	break;
									case 0:
										UpdateTrackInfo(tc, phase, ref blockStartByte, ref blockStartTime, prevBlockEndTime);

										// Data block we store CRC for checking
										tc.sectors[tc.sectors.Count - 1].phases[(byte)phase].crc = crc;
										tc.sectors[tc.sectors.Count - 1].valid = true;	// validate sector
										phase = Phases.p_outer;
										tc.sectors[tc.sectors.Count - 1].phases[(byte)phase].startBit = currentBit;
										break;
							}	// not a SWS
						}	// DATA Segment
						
						// we look for sync inside ID or DATA segment.
						bitShift = dsr.searchSynch();
						if (dsr.SyncFound)
							// was not very useful but now we use it to optimize Pasti Write
							tc.sectors[tc.sectors.Count - 1].phases[(byte)phase].badSync++;
					} // ID or DATA

					tc.buffer.addByte(dsr.Data, dsr.Clock, dsr.MFM, phase, (ushort)clock, prevBlockEndTime);
					crc = Global.updateCRC(dsr.Data, crc);
					prevBlockEndTime += 32000;	// we need this for NFA
				}	// while bytes in the DSR
				prevBlockEndTime = currentTime;
			}	// For all flux transitions

			UpdateTrackInfo(tc, phase, ref blockStartByte, ref blockStartTime, currentTime);
			tc.sectors[tc.sectors.Count - 1].endTime = currentTime;

			// data beyond index (not set for single sector track)
			if ((phase == Phases.p_inner) && (tc.sectors.Count > 2))
				tc.dataBeyondIdx = true;

			// phase DATA but not just after an id over index
			if ((phase == Phases.p_data) && !tc.idOverIdx) {
				tc.dataOverIdx = true;
				// we put the byteLeftInBlock to sector 0 data block
				// We limit the size to keep 15 (12 0x00 + 3 sync) bytes in front of ID
				if (byteLeftInBlock > (tc.sectors[0].phases[(byte)Phases.p_outer].byteCount - 15)) {
					byteLeftInBlock = tc.sectors[0].phases[(byte)Phases.p_outer].byteCount - 15;
					if (byteLeftInBlock < 0) byteLeftInBlock = 0;
				}

				tc.sectors[0].phases[(byte)Phases.p_data].startByte = 0;
				tc.sectors[0].phases[(byte)Phases.p_data].byteCount = byteLeftInBlock;
				tc.sectors[0].phases[(byte)Phases.p_data].startTime = 0;
				tc.sectors[0].phases[(byte)Phases.p_data].timeLength = byteLeftInBlock * 32000;
				tc.sectors[0].phases[(byte)Phases.p_data].startBit = 0;

				tc.sectors[0].phases[(byte)Phases.p_outer].startByte = byteLeftInBlock;
				tc.sectors[0].phases[(byte)Phases.p_outer].byteCount -= byteLeftInBlock;
				tc.sectors[0].phases[(byte)Phases.p_outer].startTime = byteLeftInBlock * 32000;
				tc.sectors[0].phases[(byte)Phases.p_outer].timeLength -= byteLeftInBlock * 32000;
			}

			tc.timeLength = currentTime;
			// we went beyond the last byte of track
			if (lastTrackByte > 0) {
				tc.byteCount = lastTrackByte;
				tc.timeLength = lastTrackTime;
			}

			if (tc.sectors.Count == 1) {
				tc.sectors[tc.sectors.Count - 1].valid = true;
			}	// empty track

			tc.hasProtection = 
				tc.dataOverIdx ||				// we have DATA over the index
				tc.idOverIdx ||					// we have ID over the index
				tc.dataBeyondIdx ||				// ID found but no DATA found
				false;
			return tc;
		}	// end of readTrack()


		/// <summary>
		/// Update BlockInfo fields
		/// </summary>
		private static void UpdateTrackInfo(TrackContent track, Phases phase, ref int blockStartByte, ref int blockStartTime, int endTime) {
			track.sectors[track.sectors.Count - 1].phases[(byte)phase].startByte = blockStartByte;
			track.sectors[track.sectors.Count - 1].phases[(byte)phase].byteCount = track.byteCount - blockStartByte;
			if ((phase == Phases.p_inner) || (phase == Phases.p_outer))
				track.sectors[track.sectors.Count - 1].phases[(byte)phase].byteCount -= 3;	// remove sync
			track.sectors[track.sectors.Count - 1].phases[(byte)phase].startTime = blockStartTime;
			track.sectors[track.sectors.Count - 1].phases[(byte)phase].timeLength = endTime - blockStartTime;
			blockStartByte = track.byteCount;
			blockStartTime = endTime;
		}


		/// <summary>
		/// Read all the ID fields of a track
		/// </summary>
		/// <returns>The structure that contains all the address info for a track</returns>
		public static AddrContent readAddr(int track, int side, int rev, bool forceRead = false) {
			AddrContent ac = Global.Disk[track, side].addrContent;
			if ((ac != null) && !forceRead)
				return ac;

			ac = new AddrContent();
			Global.Disk[track, side].addrContent = ac;

			Global.DiskTrack dt = Global.Disk[track, side];
			int start =  Global.firstIndex(track, side, rev);
			int stop = Global.lastIndex(track, side, rev);
			int indexFirstFluxCorrection = Global.indexTimePosition(track, side, rev);

			Phases phase = Phases.p_outer;
			DSR dsr = new DSR();
			int syncInSeq = 0;
			byte lastSync = 0;
			int byteLeftInBlock = 0;
			int currentTime = 0;
			int prevBlockEndTime = 0;
			ushort crc = 0;
			int currentBit = 0;
			int nbWin;
			int clockCount = 0;			// number of clock accumulated for avg
			int clockAvg = 0;			// average clock value of assembled bits
			int byteCount = 0;

			// We read from start to stop for current revolution. However if we are in the middle of an
			// ID field we continue to get the complete ID. We also check if in middle of sync seq
			for (int i = start; (i < stop) || (phase == Phases.p_id) || (syncInSeq > 0); i++) {
				if (i >= dt.fluxData.fluxCount) break;

				int flux = Global.sampleToTiming(dt.fluxData.fluxValue[i]);
				if (i == start)				flux -= indexFirstFluxCorrection;
				else if (i == (stop - 1))	flux += indexFirstFluxCorrection;
				if (flux > 150000) 
					nbWin = flux / 2000;	// NFA
				else				
					nbWin = dt.decodeData.fluxInWindow[i];
				currentTime += flux;
				currentBit += nbWin;

				dsr.shiftBitsIn(nbWin);	// push data in shift register
				int clock = dt.decodeData.clockPeriod[i];
				clockAvg += clock;
				clockCount++;

				while (dsr.byteReady()) {
					byteCount++;

					// take the average clock value for all bits
					if (clockCount > 0) {
						clock = clockAvg / clockCount;
						clockAvg = 0;
						clockCount = 0;
					}	// only one time

					// we only search for sync if not in p_id
					if (phase != Phases.p_id) 
						currentBit -= dsr.searchSynch();
					else
						dsr.resetSync();

					dsr.extract();
					if (dsr.SyncFound) {
						syncInSeq++;

						// check bad sync sequence
						if ((syncInSeq > 1) && (dsr.Data != lastSync)) 
							syncInSeq = 1;

						// reset CRC on first A1
						if ((syncInSeq == 1) && (dsr.Data == 0xA1)) crc = 0xFFFF;

						lastSync = dsr.Data;
					}	// sync byte

					// check short sync sequence
					if ((syncInSeq > 0) && (syncInSeq < 3) && !dsr.SyncFound)
						syncInSeq = 0;

					if (phase != Phases.p_id) {	// not in ID
						if ((syncInSeq > 2) && !dsr.SyncFound) {
							// 3+ sync & not sync => should be an AM
							syncInSeq = 0;	// reset sync count
							switch (dsr.Data) {
								case 0xFE:	// IDAM - start of an ID block
								case 0xFF:
									ac.ids.Add(new IDField());

									IDField id = ac.ids[ac.ids.Count - 1];
									id.iam = dsr.Data;
									ac.ids[ac.ids.Count - 1] = id;

									ac.blocs.Add(new BlockInfo());
									BlockInfo block = ac.blocs[ac.blocs.Count-1];
									block.startBit = currentBit;
									block.startByte = byteCount;
									block.startTime = prevBlockEndTime;
									ac.blocs[ac.blocs.Count - 1] = block;

									byteLeftInBlock = 7;
									phase = Phases.p_id;
									break;
								default:	// Ignore all other AM									
									break;
							}	// switch AM
						}	// 3+ sync
					}	// not in ID
					else {	// We are in an ID
						byteLeftInBlock--;
						IDField id = ac.ids[ac.ids.Count - 1];
						switch (byteLeftInBlock) {
							case 6: id.track = dsr.Data; break;
							case 5: id.head = dsr.Data; break;
							case 4: id.number = dsr.Data; break;
							case 3: id.size = dsr.Data; break;
							case 2: id.crc = dsr.Data; break;
							case 1:
								id.crc = (ushort)(id.crc << 8);
								id.crc += dsr.Data;
								break;
							case 0:	// end of ID block
								BlockInfo block = ac.blocs[ac.blocs.Count-1];
								block.byteCount = byteCount - block.startByte;
								block.crc = crc;
								block.timeLength = prevBlockEndTime - block.startTime;
								ac.blocs[ac.blocs.Count - 1] = block;
								phase = Phases.p_outer;
								break;
						}
						ac.ids[ac.ids.Count - 1] = id;
					} // we are in ID
					crc = Global.updateCRC(dsr.Data, crc);
					prevBlockEndTime += 32000;	// for NFA
				}	// more byte to process
				prevBlockEndTime = currentTime;
			}	// for all flux
			return ac;
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
		/// (i.e. we have several sectors with same number). Otherwise the data for the first encountered
		/// sector would always be returned (equivalent to position==0).
		/// When compare is false we are reading the sector normally and we fill the SectorContent structure. 
		/// But if compare==true we are in "compare" mode. In that case we read the sector using the rev parameter 
		/// and we compare the data buffer read with the "reference" data buffer to fill the fuzzyMask structure.</remarks>
		public static SectorContent readSector(int track, int side, int rev, byte sectToRead, int position, bool compare, bool forceRead = false) {
			// if no list of sectors exist we need to create one 
			// ? TODO does not seems to be the best place!
			if (Global.Disk[track, side].sectorsContent == null)
				Global.Disk[track, side].sectorsContent = new List<SectorContent>();

			// if not in compare mode and sector already there nothing to do
			SectorContent sref = findSectorContent(track, side, sectToRead, position);
			if ((!compare) && (sref != null) && !forceRead) return sref;

			// if not there or we are in compare mode create a new sectorContent
			SectorContent sc = new SectorContent();
			sc.buffer = new TrackBuffer();

			Global.DiskTrack dt = Global.Disk[track, side];
			int start = Global.firstIndex(track, side, rev);
			int stop = Global.lastIndex(track, side, rev);
			int indexFirstFluxCorrection = Global.indexTimePosition(track, side, rev);

			Phases phase = Phases.p_outer;
			DSR dsr = new DSR();
			int syncInSeq = 0;
			byte lastSync = 0;
			int byteLeftInBlock = 0;
			int currentTime = 0;
			int prevBlockEndTime = 0;
			ushort crc = 0;
			int currentBit = 0;
			int nbWin;
			int byteCount = 0;
			int clock;
			int clockAvg = 0;
			int clockCount = 0;
			bool idFound = false;
			bool dataFound = false;
			bool breakLoop = false;

			// we loop normally from start to stop of the specified revolution.
			// However we continue if we are in the middle of an ID or DATA field (or ever starting sync seq! see readAddr() for more info)
			for (int i = start; (i < stop) || (phase == Phases.p_id) || (phase == Phases.p_inner) || (phase == Phases.p_data) || (syncInSeq > 0); i++) {
				if ( breakLoop || (dataFound && (phase != Phases.p_data)) || (i >= dt.fluxData.fluxCount) )
					break;
				//Debug.Assert(i < currentTrack.indexes[nbRev].fluxPosition, "Index loop overflow in readSect()", "Internal error please report");

				int flux = Global.sampleToTiming(dt.fluxData.fluxValue[i]);			
				if (i == start) flux -= indexFirstFluxCorrection;
				//if (i == (stop - 1)) flux += indexFirstFluxCorrection;
				currentTime += flux;

				if (flux > 150000) 
					nbWin = flux / 2000;
				else				
					nbWin = dt.decodeData.fluxInWindow[i];

				currentBit += nbWin;
				dsr.shiftBitsIn(nbWin);	// push data in register
				clock = dt.decodeData.clockPeriod[i];
				clockAvg += clock;
				clockCount++;

				while (dsr.byteReady()) {
					byteCount++;

					// take the average clock for all bits
					if (clockCount > 0) {
						clock = clockAvg / clockCount;
						clockAvg = 0;
						clockCount = 0;
					}	// only one time

					if ((phase != Phases.p_id) && (phase != Phases.p_data))
						currentBit -= dsr.searchSynch();
					else
						dsr.resetSync();
					dsr.extract();

					if (dsr.SyncFound) {
						syncInSeq++;
						// check bad sequence
						if ((syncInSeq > 1) && (dsr.Data != lastSync)) syncInSeq = 1;
						// reset CRC on first A1
						if ((syncInSeq == 1) && (dsr.Data == 0xA1)) crc = 0xFFFF;
						lastSync = dsr.Data;
					}	// sync byte

					// check short sync sequence
					if ((syncInSeq > 0) && (syncInSeq < 3) && !dsr.SyncFound) syncInSeq = 0;

					if ((phase != Phases.p_id) && (phase != Phases.p_data)) { // We are in a GAP
							if ((syncInSeq > 2) && !dsr.SyncFound) {
							// 3+ sync & not sync => should be an AM
							syncInSeq = 0;	// reset sync count
							switch (dsr.Data) {
								case 0xFE:	// IDAM - start of an ID block
								case 0xFF:
									// if we have already found the requested ID and we find another ID
									// this means that no DATA block were found before this ID
									// therefore we should break
									if (idFound) {
										//sect.idBlock.crc = crc;
										//sect.idBlock.byteCount = byteCount - sect.idBlock.startByte;
										//sect.idBlock.timeLength = prevBlockEndTime - sect.idBlock.startTime;
										dataFound = false;
										phase = Phases.p_id;
										breakLoop = true;
									}

									// if we have not yet reach the requested position we ignore
									if ((prevBlockEndTime + 32000) < position)	// allow one extra char
										break;

									sc.id.iam = dsr.Data;
									sc.idBlock.startBit = currentBit;
									sc.idBlock.startByte = byteCount;
									sc.idBlock.startTime = prevBlockEndTime;
									byteLeftInBlock = 7;
									phase = Phases.p_id;
									break;

								case 0xFB:	// DAM - Start of a DATA block
								case 0xF8:	// DDAM
								case 0xFA:
								case 0xF9:
									// we check that this DATA block is for the ID we are looking for
									// and that the DATA block is less than 43 bytes apart
									if (idFound && ((byteCount - sc.idBlock.startByte - 7) <= 43)) {
										dataFound = true;
										byteLeftInBlock = Global.sectSize(sc.id.size) + 3; // AM + 2 CRC + Size
										sc.dataBlock.startBit = currentBit;
										sc.dataBlock.startByte = byteCount;
										sc.dataBlock.startTime = prevBlockEndTime;
										phase = Phases.p_data;
									}
									else
										phase = Phases.p_outer;
									break;
								default:
									break;
							}	// switch AM
						}	// 3+ sync
					}	// GAP
					else {
						// We are in an ID or DATA segment
						byteLeftInBlock--;
						if (phase == Phases.p_id)
							// ID segment
							switch (byteLeftInBlock) {
								case 6: sc.id.track = dsr.Data; break;
								case 5: sc.id.head = dsr.Data; break;
								case 4: 
									sc.id.number = dsr.Data;
									if (sectToRead != dsr.Data) {
										phase = Phases.p_outer;
										idFound = false;
									}
									else
										idFound = true;
									break;
								case 3: sc.id.size = dsr.Data; break;
								case 2: sc.id.crc = dsr.Data; break;
								case 1:
									sc.id.crc = (ushort)(sc.id.crc << 8);
									sc.id.crc += dsr.Data;
									break;
								case 0:	// end of ID block
									sc.idBlock.crc = crc;
									sc.idBlock.byteCount = byteCount - sc.idBlock.startByte;
									sc.idBlock.timeLength = prevBlockEndTime - sc.idBlock.startTime;
									phase = Phases.p_inner;
									break;
								default:
									break;
							}	// ID Segment
						else
							switch (byteLeftInBlock) {
								case 2: 
									//sect.dataCRC = dsr.Data; 
									break;
								case 1:
									//sect.dataCrc = (ushort)(sect.dataCrc << 8);
									//sect.dataCrc += dsr.Data;
									break;
								case 0:	// end of DATA block
									sc.dataBlock.crc = crc;
									sc.dataBlock.byteCount = byteCount - sc.dataBlock.startByte;
									sc.dataBlock.timeLength = prevBlockEndTime - sc.dataBlock.startTime;
									phase = Phases.p_outer;
									break;

							}	// DATA Segment
					} // ID or DATA

					if (phase == Phases.p_data) {
						sc.buffer.addByte(dsr.Data, dsr.Clock, dsr.MFM, phase, (ushort)clock, prevBlockEndTime);
						// check clock above or below normal
						if (clock > upperClock)
							sc.dataBlock.overClock++;
						if (clock < lowerClock)
							sc.dataBlock.underClock++;
					}
					crc = Global.updateCRC(dsr.Data, crc);
					prevBlockEndTime += 32000;
				}	// while bytes in the DSR
				prevBlockEndTime = currentTime;
			}	// For all transitions

			sc.sectHasData = idFound && dataFound;

			if (!compare) {
				if (!idFound)
					return null; // request for non existing sector
				Global.Disk[track, side].sectorsContent.Add(sc);
				return sc;
			}	// normal mode
			else {	// compare mode
				if (!idFound)
					return null; // request for non existing sector

				int size = sref.dataBlock.byteCount;
				bool fuzzyFound = false;
				TrackBuffer fuzzyMask;
				if ((sref.buffer.Bytes.Count == 0) || (sc.buffer.Bytes.Count == 0)) return null;
				//Debug.Assert(sref.buffer.Bytes.Count == sect.buffer.Bytes.Count, "Possible truncation",
				//	String.Format("buffers size s1={0} s2={1}\n", sref.buffer.Bytes.Count, sect.buffer.Bytes.Count));

				// we may have data beyond index and therefore on the last rev the sector can get truncated
				if (size > sc.dataBlock.byteCount)
					size = sc.dataBlock.byteCount;

				if (sref.fuzzyMask != null) {	// previous mask exist append
					fuzzyMask = sref.fuzzyMask;
					for (int i = 0; i < size; i++) {
						byte xor = (byte)(sref.buffer.Bytes[i].dataByte ^ sc.buffer.Bytes[i].dataByte);
						// we only update when mismatch ==> so we keep mismatches from previous check.
						if (xor != 0) {
							fuzzyFound = true;
							TrackBuffer.TrackByte tb = fuzzyMask.Bytes[i];
							tb.dataByte = (byte)xor;
							tb.clockByte = sref.buffer.Bytes[i].clockByte;
							tb.mfmWord = sref.buffer.Bytes[i].mfmWord;
							tb.density = sref.buffer.Bytes[i].density;
							tb.startTime = sref.buffer.Bytes[i].startTime;
							tb.phase = Phases.p_inner;
							fuzzyMask.Bytes[i] = tb;
						}
					}
				}	// existing mask
				else {	// previous mask does not exist we create
					fuzzyMask = new TrackBuffer();
					for (int i = 0; i < size; i++) {
						byte xor = (byte)(sref.buffer.Bytes[i].dataByte ^ sc.buffer.Bytes[i].dataByte);
						if (xor != 0) 
							fuzzyFound = true;
						TrackBuffer.TrackByte tb = new TrackBuffer.TrackByte();
						tb.dataByte = xor;
						tb.clockByte = sref.buffer.Bytes[i].clockByte;
						tb.mfmWord = sref.buffer.Bytes[i].mfmWord;
						tb.density = sref.buffer.Bytes[i].density; ;
						tb.startTime = sref.buffer.Bytes[i].startTime;
						tb.phase = Phases.p_inner;
						fuzzyMask.Bytes.Add(tb);
					}
				}	// new mask
				if (fuzzyFound)
					sref.fuzzyMask = fuzzyMask;
				return sref;
			}	// differential mode
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
		public static void fdcAllRead(int track, int side, int rev, bool forceRead = false) {
			TrackContent tc = readTrack(track, side, rev, forceRead);
			AddrContent ac = readAddr(track, side, rev, forceRead);
			for (int i = 0; i < ac.ids.Count; i++) {
				SectorContent sc = readSector(track, side, rev, ac.ids[i].number, ac.blocs[i].startTime, false, forceRead);
				if (sc == null) 
					continue;
				// now multiple readSector() for fuzzy bits
				for (int j = 1; j < Global.Disk[track, side].revData.Count() + 1; j++) {
					if (j == rev) continue;
					readSector(track, side, j, ac.ids[i].number, ac.blocs[i].startByte, true);
				}
				if (sc.fuzzyMask != null)
					tc.hasProtection = true;				
			}
			Protections.checkProtection(track, side);
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
				if ((sector.id.number == num) && (sector.idBlock.startTime >= position))
					return sector;
			return null;
		}


	}
}
