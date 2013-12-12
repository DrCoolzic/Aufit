/*!
@file FDC.cs
 * 
<summary>WD1772 FDC "main" commands: read track, read address, read sector</summary>

<div class="jlg">Copyright (C) 2011 Jean Louis-Guerin\n\n
This file is part of the Atari Universal FD Image Tools project.\n\n
The Atari Universal FD Image Tools project may be used and distributed without restriction provided
that this copyright statement is not removed from the file and that any
derivative work contains the original copyright notice and the associated
disclaimer.\n\n
The Atari Universal FD Image Tools project is free software; you can redistribute it
and/or modify  it under the terms of the GNU General Public License
as published by the Free Software Foundation; either version 2
of the License, or (at your option) any later version.\n\n
The Atari Universal FD Image Tools project is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.\n
See the GNU General Public License for more details.\n\n
You should have received a copy of the GNU General Public License
along with the Atari Universal FD Image Tools project; if not, write to the Free Software
Foundation, Inc., 51 Franklin St, Fifth Floor, Boston, MA  02110-1301  USA\n</div>

@author Jean Louis-Guerin
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using StreamNS;
using Aufit;

namespace WD1772NS {
	/// <summary>
	/// Different phases inside a sector: ID, Inner Gap, Data, Outer Gap
	/// </summary>
	public enum Phases : byte {
		p_id,		// ID (Address) block
		p_inner,	// GAP inside sectors (between ID and DATA)
		p_data,		// DATA block
		p_outer,	// GAP outside (between) sectors
	}

	/// <summary>
	/// Store the content read from a sector ID (Address) Segment
	/// </summary>
	public struct IdSegment {
		/// <summary>Store the Index Address Mark</summary> 
		public byte iam;
		/// <summary>Store the track number</summary> 
		public byte track;
		/// <summary>Store the Head/Side number</summary> 
		public byte head;
		/// <summary>Store the sector number</summary> 
		public byte sector;
		/// <summary>Store the encoded data segment size</summary> 
		public byte size;
		/// <summary>Store the CRC on two bytes</summary> 
		public ushort crc;
	}

	/// <summary>
	/// Various Information about a GAP / ID / DATA block
	/// </summary>
	public struct BlockInfo {
		/// <summary>Number of bytes in the block</summary> 
		public int byteCount;
		/// <summary>First byte of the block</summary> 
		public int startByte;
		/// <summary>First bit of the block</summary> 
		public int startBit;
		/// <summary>
		/// Starting Time of the block in nanoseconds from index
		/// </summary>
		public int startTime;
		/// <summary>
		///  Length of the Block in nanoseconds 
		/// </summary>
		public int timeLength;
		/// <summary>
		/// CRC result at the end of reading an ID or DATA block
		/// </summary>
		public ushort crc;
	}

	/// <summary>
	/// Contains all information for a sector.
	/// </summary>
	/// <remarks>Normally a sector contains 4 BlockInfo (one per phase): ID / INNER / DATA / OUTER
	/// We also have a pointer to an IdContent to store the ID information</remarks>
	public class SectorInfo {
		/// <summary>
		/// For each phase (p_id -> p_inner -> p_data -> p_outer) we store block information
		/// </summary>
		public BlockInfo[] phases = new BlockInfo[4];	// one block per phase
		/// <summary>Pointer to the IdSegment</summary>
		public IdSegment addr = new IdSegment();		// store ID info
		/// <summary>
		/// If SWS stores the number of the sector that contains this one, or -1 otherwise
		/// </summary>
		public int sws = -1;
		/// <summary>Set to true if the Sector has been validated</summary>
		public bool valid = false;
		/// <summary>
		/// Store the last two CRC bytes of a data segment as we have no other place
		/// </summary>
		public ushort dataSegmentCrc = 0xFFFF;
		/// <summary>Sector start time</summary> 
		public int startTime;
		/// <summary>Sector end time</summary> 
		public int endTime;
	}

	/// <summary>
	/// Store information for each sync found on the track
	/// </summary>
	public class SyncInfo {
		/// <summary>Time position of the sync</summary>
		public int time;
		/// <summary>How many sync in a row</summary>
		public int row;
		/// <summary>Sync value</summary>
		public byte value;
		/// <summary>Bad Sync sequence</summary>
		public bool badSync = false;
	}

	/// <summary>
	/// Store information about all the sectors of a track
	/// </summary>
	/// <remarks>Contains all pertinent information for a complete track. This information
	/// is filled when reading a complete track @sa read_track()
	/// Note that sectors[0] is a pseudo sector to store GAP1 information</remarks>
	public class ReadTrackInfo {
		/// <summary>List of all sectors in the track</summary>
		public List<SectorInfo> sectors = new List<SectorInfo>(11);
		/// <summary>Time length of the track in nanoseconds</summary>
		public int timeLength = 0;
		/// <summary> Number of bytes in the track</summary>
		public int byteCount = 0;
		/// <summary>List of sync found</summary> 
		public List<SyncInfo> syncs = new List<SyncInfo>();
		/// <summary>TrackBuffer for the read track</summary>
		public TrackBuffer buffer;
	}

	/// <summary>
	/// WD1772 FDC Commands
	/// </summary>
	public class FDC {

		private readonly int upperClock = 2000 + (4 * 20);
		private readonly int lowerClock = 2000 - (4 * 20);
		private readonly int BORDER_BIT = 0x0001;
		private readonly int TIME_VIOLATION = 0x0002;
		private readonly int NFA = 0x0004;

		private ReadTrackInfo _trackInfo;
		// private ReadAddressInfo
		// private List<ReadSectorInfo>


		/// <summary>
		/// Access to the current track information structure
		/// </summary>
		public ReadTrackInfo TrackInfo {
			get { return _trackInfo; }
		}


		/// <summary>Read a track by decoding all the track mfm bits</summary>
		///
		/// This function interprets the bits as a WD1772 would do with a read track command. 
		/// It fills a ReadTrackInfo structure that gives detail information about the track 
		/// as well as a Buffer array that contains all the decoded bytes.
		public ReadTrackInfo readTrack() {
			ProcessStream.TrackData currentTrack = ProcessStream.currentTrack();
			int nbRev = currentTrack.indexCount - 1;
			int start, stop;
			int indexFirstFluxCorrection;
			byte syncInf;

			if (ProcessStream.Rev > 0) {
				start = currentTrack.indexes[ProcessStream.Rev - 1].fluxPosition;
				stop = currentTrack.indexes[ProcessStream.Rev].fluxPosition-1;
				indexFirstFluxCorrection = ProcessStream.sampleToTiming(currentTrack.indexes[ProcessStream.Rev - 1].preIndexTime);
			}	// one rev
			else {
				start = currentTrack.indexes[0].fluxPosition;
				stop = currentTrack.indexes[nbRev].fluxPosition;
				indexFirstFluxCorrection = ProcessStream.sampleToTiming(currentTrack.indexes[0].preIndexTime);
			}	// all rev

			ReadTrackInfo track = new ReadTrackInfo();
			_trackInfo = track;
			track.buffer = new TrackBuffer();

			track.sectors.Add(new SectorInfo());	// add dummy sector[0] for gap
			Phases phase = Phases.p_outer;
			DSR dsr = new DSR();

			int syncCount = 0;
			byte previousSync = 0;
			int byteLeftInBlock = 0;
			int blockStartByte = 0;
			int blockStartTime = 0;
			int currentTime = 0;
			int prevBlockEndTime = 0;
			bool sws = false;
			ushort crc = 0;
			int currentBit = 0;
			int clock;
			int checking = 0;
			int nbWin;

//			for (int i = start; (i < stop) || (phase == Phases.p_id); i++) {
			for (int i = start; i < stop; i++) {

				int flux = ProcessStream.sampleToTiming(currentTrack.flux[i]);
				if (i == start) 
					flux -= indexFirstFluxCorrection;
				if (i == (stop - 1))
					flux += indexFirstFluxCorrection;
				currentTime += flux;

				if (flux > 32000) {
					checking |= NFA;
					int nfa_start = currentTime;
					int nfa_stop = currentTime + flux;
					nbWin = flux / 2000;
				}	// NFA
				else
					nbWin = currentTrack.nbWin[i];

				currentBit += nbWin;
				dsr.shiftBitsIn(nbWin);	// push data in register
				clock = currentTrack.clock[i];

				if (DSR.check_border(clock, nbWin, flux))
					checking |= BORDER_BIT;
				if ((nbWin < 2) || nbWin > 4) 
					checking |= TIME_VIOLATION;

				while (dsr.byteReady()) {
					track.byteCount++;

					// check above or below normal
					if (clock > upperClock) {
						// TODO
					}	// above normal
					if (clock < lowerClock) {
						// TODO
					}	// below normal

					int bitShift = dsr.searchSynch();
					syncInf = (byte)bitShift;
					dsr.extract();
					currentBit -= bitShift;
				
					if (dsr.SyncFound) {
						syncInf |= 0x80;
						syncCount++;
						track.syncs.Add(new SyncInfo());
						track.syncs[track.syncs.Count - 1].value = dsr.Data;
						track.syncs[track.syncs.Count - 1].time = currentTime;
						track.syncs[track.syncs.Count - 1].row = syncCount;
						track.syncs[track.syncs.Count - 1].badSync = false;

						// check bad sequence
						if ((syncCount > 1) && (dsr.Data != previousSync)) {
							syncCount = 1;
							// We skip 0xC2 followed by 0xA1 as this happen during re-sync
							if (previousSync != 0xC2) {
								track.syncs[track.syncs.Count - 1].badSync = true;
							}
						}	// bad sync sequence

						// reset CRC on first A1
						if ((syncCount == 1) && (dsr.Data == 0xA1)) crc = 0xFFFF;

						// check long sequence
						if (syncCount > 3) {
							// TODO
						}

						previousSync = dsr.Data;
					}	// sync byte

					// check short sync sequence
					if ((syncCount > 0) && (syncCount < 3) && !dsr.SyncFound) {
						syncCount = 0;
						track.syncs[track.syncs.Count - 1].badSync = true;
					}	// short sync sequence
					
					if ((phase != Phases.p_id) && (phase != Phases.p_data)) {
						// We are in a GAP
						// check for invalid data into gap
						if ((dsr.Data > 244) && (dsr.Data > 248)) {
							// TODO
						} // IDG invalid data in GAP

						if ((syncCount > 2) && !dsr.SyncFound) {
							// 3+ sync & not sync => should be an AM
							syncCount = 0;	// reset sync count
							switch (dsr.Data) {
								case 0xFC:	// IAM - IBM 34 Format - ignore
								case 0xFD:
									break;

								case 0xFE:	// IDAM - start of an ID block
								case 0xFF:
									UpdateBlockInfo(prevBlockEndTime, phase, track, ref blockStartByte, ref blockStartTime);
									track.sectors[track.sectors.Count - 1].endTime = prevBlockEndTime;
									track.sectors.Add(new SectorInfo());

									track.sectors[track.sectors.Count-1].addr.iam = dsr.Data;
									track.sectors[track.sectors.Count - 1].startTime = prevBlockEndTime;
									byteLeftInBlock = 7;
									phase = Phases.p_id;
									track.sectors[track.sectors.Count - 1].phases[(byte)phase].startBit = currentBit;;
									break;

								case 0xFB:	// DAM - Start of a DATA block
								case 0xF8:	// DDAM
								case 0xFA:
								case 0xF9:
									UpdateBlockInfo(prevBlockEndTime, phase, track, ref blockStartByte, ref blockStartTime);
									if (track.sectors.Count == 1) {
										track.sectors.Add(new SectorInfo());

										byteLeftInBlock = 512;	// best guess
									}	// no ID before
									else
										byteLeftInBlock = DSR.actualSize(track.sectors[track.sectors.Count()-1].addr.size) + 3; // AM + 2 CRC + Size
									phase = Phases.p_data;
									track.sectors[track.sectors.Count - 1].phases[(byte)phase].startBit = currentBit;
									break;

								default:
									// not an AM after 3+ sync : invalid
									// TODO
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
								case 6: track.sectors[track.sectors.Count-1].addr.track = dsr.Data; break;
								case 5: track.sectors[track.sectors.Count - 1].addr.head = dsr.Data; break;
								case 4: track.sectors[track.sectors.Count - 1].addr.sector = dsr.Data; break;
								case 3: track.sectors[track.sectors.Count - 1].addr.size = dsr.Data; break;
								case 2: track.sectors[track.sectors.Count - 1].addr.crc = dsr.Data; break;
								case 1:
									track.sectors[track.sectors.Count - 1].addr.crc = (ushort)(track.sectors[track.sectors.Count - 1].addr.crc << 8);
									track.sectors[track.sectors.Count - 1].addr.crc += dsr.Data;
									break;
								case 0:	// end of ID block
									UpdateBlockInfo(prevBlockEndTime, phase, track, ref blockStartByte, ref blockStartTime);
									if (sws) {
										sws = false;
										track.sectors[track.sectors.Count - 1].sws = track.sectors[track.sectors.Count - 2].addr.sector;
									}
									// ID Block we store the CRC for checking
									track.sectors[track.sectors.Count - 1].phases[(byte)phase].crc = crc;
									phase = Phases.p_inner;
									track.sectors[track.sectors.Count - 1].phases[(byte)phase].startBit = currentBit;
									break;
							}
						else {	
							// DATA Segment - first check SWS
							if ((syncCount > 2) && (previousSync == 0xA1) && !dsr.SyncFound && (dsr.Data == 0xFE) ) {
								// 3+ sync followed by IDAM => SWS
								// note: we also check that sync is A1 as we may detect C2 in data block
								sws = true;
								syncCount = 0;
								UpdateBlockInfo(prevBlockEndTime, phase, track, ref blockStartByte, ref blockStartTime);
								track.sectors[track.sectors.Count - 1].phases[(byte)phase].crc = 0xFFFF; // set to bad value
								track.sectors[track.sectors.Count - 1].endTime = prevBlockEndTime;
								track.sectors.Add(new SectorInfo());

								track.sectors[track.sectors.Count - 1].startTime = prevBlockEndTime;
								track.sectors[track.sectors.Count - 1].addr.iam = dsr.Data;
								byteLeftInBlock = 7;
								phase = Phases.p_id;
								track.sectors[track.sectors.Count - 1].phases[(byte)phase].startBit = currentBit;
							}	// SWS
							else {
								switch (byteLeftInBlock) {
									case 2: track.sectors[track.sectors.Count - 1].dataSegmentCrc = dsr.Data; break;
									case 1:
										track.sectors[track.sectors.Count - 1].dataSegmentCrc = (ushort)(track.sectors[track.sectors.Count - 1].addr.crc << 8);
										track.sectors[track.sectors.Count - 1].dataSegmentCrc += dsr.Data;
										break;
									case 0:
										UpdateBlockInfo(prevBlockEndTime, phase, track, ref blockStartByte, ref blockStartTime);
										// Data block we store CRC for checking
										track.sectors[track.sectors.Count - 1].phases[(byte)phase].crc = crc;
										track.sectors[track.sectors.Count - 1].valid = true;	// valid sector
										phase = Phases.p_outer;
										track.sectors[track.sectors.Count - 1].phases[(byte)phase].startBit = currentBit;
										break;
								}
							}	// not a SWS
						}	// DATA Segment
						
						// in an ID or DATA segment we should not find a sync inside!
						bitShift = dsr.searchSynch();
						if (dsr.SyncFound) {
							syncInf = (byte)bitShift;
							syncInf |= 0x80;
							// ERROR
						}

					} // ID or DATA


					track.buffer.addByte(dsr.Data, dsr.Clock, dsr.Mfm, phase, (ushort)clock, syncInf, prevBlockEndTime);
					crc = DSR.update_crc(dsr.Data, crc);
					prevBlockEndTime = currentTime;
				}	// while bytes in the DSR
			}	// For all transitions

			track.byteCount++;
			UpdateBlockInfo(currentTime, phase, track, ref blockStartByte, ref blockStartTime);
			track.sectors[track.sectors.Count - 1].endTime = currentTime;

			if (phase == Phases.p_id) {
				track.sectors[track.sectors.Count - 1].valid = true;
			}

			if (phase == Phases.p_data) {
				track.sectors[track.sectors.Count - 1].valid = false;
			}

			if (track.sectors.Count == 1) {
				track.sectors[track.sectors.Count - 1].valid = true;
			}	// empty track

			return track;
		}	// readTrack


		/// <summary>
		/// Update BlockInfo  fields
		/// </summary>
		/// <param name="endTime"></param>
		/// <param name="phase"></param>
		/// <param name="track"></param>
		/// <param name="blockStartByte"></param>
		/// <param name="blockStartTime"></param>
		private static void UpdateBlockInfo(int endTime, Phases phase, ReadTrackInfo track, ref int blockStartByte, ref int blockStartTime) {
			track.sectors[track.sectors.Count - 1].phases[(byte)phase].startByte = blockStartByte;
			track.sectors[track.sectors.Count - 1].phases[(byte)phase].byteCount = track.byteCount - blockStartByte;
			if ((phase == Phases.p_inner) || (phase == Phases.p_outer))
				track.sectors[track.sectors.Count - 1].phases[(byte)phase].byteCount -= 3;	// remove sync
			track.sectors[track.sectors.Count - 1].phases[(byte)phase].startTime = blockStartTime;
			track.sectors[track.sectors.Count - 1].phases[(byte)phase].timeLength = endTime - blockStartTime;
			blockStartByte = track.byteCount;
			blockStartTime = endTime;
		}

	}
}
