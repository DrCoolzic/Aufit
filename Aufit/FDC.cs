/*!
@file FDC.cs
 * 
<summary>WD1772 FDC "main" commands: read track, read address, read sector</summary>

<div class="jlg">Copyright (C) 2011 Jean Louis-Guerin\n\n
This file is part of the Atari Universal FD Image Tool project.\n\n
The Atari Universal FD Image Tool project may be used and distributed without restriction provided
that this copyright statement is not removed from the file and that any
derivative work contains the original copyright notice and the associated
disclaimer.\n\n
The Atari Universal FD Image Tool project is free software; you can redistribute it
and/or modify  it under the terms of the GNU General Public License
as published by the Free Software Foundation; either version 2
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

using StreamNS;
using Aufit;
using System.Diagnostics;

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
	public struct IDBlock {
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
		public IDBlock addr = new IDBlock();		// store ID info
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
		public List<SectorInfo> sectors = new List<SectorInfo>(12);
		/// <summary>Time length of the track in nanoseconds</summary>
		public int timeLength = 0;
		/// <summary> Number of bytes in the track</summary>
		public int byteCount = 0;
		/// <summary>List of sync found</summary> 
		public List<SyncInfo> syncs = new List<SyncInfo>();
		/// <summary>TrackBuffer for the read track</summary>
		public TrackBuffer buffer;
	}

	public class ReadAddrInfo {
		public List<IDBlock> ids = new List<IDBlock>(12);
		public List<BlockInfo> blocs = new List<BlockInfo>(12);
	}

	public class ReadSectInfo {
		public BlockInfo dataBlock;
		public BlockInfo idBlock;
		public TrackBuffer buffer;
		public IDBlock id;
		public bool sectHasData;
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


		/// <summary>Read a track by decoding all the track mfm bits</summary>
		///
		/// This function interprets the bits as a WD1772 would do with a read track command. 
		/// It fills a ReadTrackInfo structure that gives detail information about the track 
		/// as well as a Buffer array that contains all the decoded bytes.
		public ReadTrackInfo readTrack() {
			ReadTrackInfo track = ProcessStream.Disk[ProcessStream.Track, ProcessStream.Side].readTrack;
			if (track != null) 
				return track;
			track = new ReadTrackInfo();
			ProcessStream.Disk[ProcessStream.Track, ProcessStream.Side].readTrack = track;
			track.buffer = new TrackBuffer();


			ProcessStream.TrackData currentTrack = ProcessStream.currentTrack();
			int start = ProcessStream.Start;
			int stop = ProcessStream.Stop;
			int indexFirstFluxCorrection = ProcessStream.IndexFirstFluxCorrection;

			track.sectors.Add(new SectorInfo());	// add dummy sector[0] for gap
			Phases phase = Phases.p_outer;
			DSR dsr = new DSR();
			byte syncInf;
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
						// TODO above normal
					}	// above normal
					if (clock < lowerClock) {
						// TODO below normal
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
							// TODO long sync seq
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
							// TODO invalid data in gap
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
									UpdateTrackInfo(prevBlockEndTime, phase, track, ref blockStartByte, ref blockStartTime);
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
									UpdateTrackInfo(prevBlockEndTime, phase, track, ref blockStartByte, ref blockStartTime);
									if (track.sectors.Count == 1) {
										// TODO why add a new sector ? we can just put it in 0 ?
										track.sectors.Add(new SectorInfo());

										byteLeftInBlock = 512;	// best guess
									}	// no ID before
									else
										byteLeftInBlock = DSR.actualSize(track.sectors[track.sectors.Count-1].addr.size) + 3; // AM + 2 CRC + Size
									phase = Phases.p_data;
									track.sectors[track.sectors.Count - 1].phases[(byte)phase].startBit = currentBit;
									break;

								default:
									// not an AM after 3+ sync : invalid
									// TODO invalid AM
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
									UpdateTrackInfo(prevBlockEndTime, phase, track, ref blockStartByte, ref blockStartTime);
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
								UpdateTrackInfo(prevBlockEndTime, phase, track, ref blockStartByte, ref blockStartTime);
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
										UpdateTrackInfo(prevBlockEndTime, phase, track, ref blockStartByte, ref blockStartTime);
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
							// TODO ERROR
						}

					} // ID or DATA


					track.buffer.addByte(dsr.Data, dsr.Clock, dsr.Mfm, phase, (ushort)clock, syncInf, prevBlockEndTime);
					crc = DSR.update_crc(dsr.Data, crc);
					prevBlockEndTime = currentTime;
				}	// while bytes in the DSR
			}	// For all transitions

			track.byteCount++;
			UpdateTrackInfo(currentTime, phase, track, ref blockStartByte, ref blockStartTime);
			track.sectors[track.sectors.Count - 1].endTime = currentTime;

			if (phase == Phases.p_id) {
				track.sectors[track.sectors.Count - 1].valid = true;
			}

			if (phase == Phases.p_data) {
				track.sectors[track.sectors.Count - 1].valid = false;
				// we put the byteLeftInBlock to sector 0 data block
				// We limit the size to keep 15 (12 0x00 + 3 sync)
				if (byteLeftInBlock > (track.sectors[0].phases[(byte)Phases.p_outer].byteCount - 15)) {
					byteLeftInBlock = track.sectors[0].phases[(byte)Phases.p_outer].byteCount - 15;
					if (byteLeftInBlock < 0) byteLeftInBlock = 0;
				}
				track.sectors[0].phases[(byte)Phases.p_data].startByte = 0;
				track.sectors[0].phases[(byte)Phases.p_data].byteCount = byteLeftInBlock;
				track.sectors[0].phases[(byte)Phases.p_data].startTime = 0;
				track.sectors[0].phases[(byte)Phases.p_data].timeLength = byteLeftInBlock * 32000;
				track.sectors[0].phases[(byte)Phases.p_data].startBit = 0;

				track.sectors[0].phases[(byte)Phases.p_outer].startByte = byteLeftInBlock;
				track.sectors[0].phases[(byte)Phases.p_outer].byteCount -= byteLeftInBlock;
				track.sectors[0].phases[(byte)Phases.p_outer].startTime = byteLeftInBlock * 32000;
				track.sectors[0].phases[(byte)Phases.p_outer].timeLength -= byteLeftInBlock * 32000;
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
		private static void UpdateTrackInfo(int endTime, Phases phase, ReadTrackInfo track, ref int blockStartByte, ref int blockStartTime) {
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
		public ReadAddrInfo readAddr() {
			ReadAddrInfo addr = ProcessStream.Disk[ProcessStream.Track, ProcessStream.Side].readAddr;
			if (addr != null)
				return addr;
			addr = new ReadAddrInfo();
			ProcessStream.Disk[ProcessStream.Track, ProcessStream.Side].readAddr = addr;

			ProcessStream.TrackData currentTrack = ProcessStream.currentTrack();
			int nbRev = currentTrack.indexCount - 1;
			int start = ProcessStream.Start;
			int stop = ProcessStream.Stop;
			int indexFirstFluxCorrection = ProcessStream.IndexFirstFluxCorrection;

			int currentTime = 0;
			int currentBit = 0;
			int byteCount = 0;
			int nbWin;
			DSR dsr = new DSR();
			Phases phase = Phases.p_outer;
			int syncCount = 0;
			byte previousSync = 0;
			ushort crc = 0;
			int byteLeftInBlock = 0;
			int prevBlockEndTime = 0;

			// We read for the current Revolution. However if we are in the middle of
			// ID field we continue to get the complete field. I am not aware of ID
			// field spanning over the index so we should be safe. I have added an
			// assert so we know what is going on if we pass the end of the revolution
			for (int i = start; (i < stop) || (phase == Phases.p_id); i++) {
				Debug.Assert(i < currentTrack.indexes[nbRev].fluxPosition,
					"ID Field extends beyond Index in the last revolution",
					"Internal error (please report): Trying to read an ID field bytes that extend beyond the last Index.");
				int flux = ProcessStream.sampleToTiming(currentTrack.flux[i]);
				if (i == start)
					flux -= indexFirstFluxCorrection;
				if (i == (stop - 1))
					flux += indexFirstFluxCorrection;
				currentTime += flux;

				if (flux > 32000) {
					nbWin = flux / 2000;
				}	// NFA
				else
					nbWin = currentTrack.nbWin[i];

				currentBit += nbWin;
				dsr.shiftBitsIn(nbWin);	// push data in shift register
				int clock = currentTrack.clock[i];

				while (dsr.byteReady()) {
					byteCount++;

					// we only search for sync if not in p_id
					if (phase != Phases.p_id) 
						currentBit -= dsr.searchSynch();
					else
						dsr.resetSync();

					dsr.extract();
					if (dsr.SyncFound) {
						syncCount++;
						// check bad sync sequence
						if ((syncCount > 1) && (dsr.Data != previousSync))
							syncCount = 1;
						// reset CRC on first A1
						if ((syncCount == 1) && (dsr.Data == 0xA1)) 
							crc = 0xFFFF;
						previousSync = dsr.Data;
					}	// sync byte

					// check short sync sequence
					if ((syncCount > 0) && (syncCount < 3) && !dsr.SyncFound)
						syncCount = 0;

					if (phase != Phases.p_id) {
						if ((syncCount > 2) && !dsr.SyncFound) {
							// 3+ sync & not sync => should be an AM
							syncCount = 0;	// reset sync count
							switch (dsr.Data) {
								case 0xFE:	// IDAM - start of an ID block
								case 0xFF:
									addr.ids.Add(new IDBlock());
									IDBlock id = addr.ids[addr.ids.Count - 1];
									id.iam = dsr.Data;
									addr.ids[addr.ids.Count - 1] = id;

									addr.blocs.Add(new BlockInfo());
									BlockInfo block = addr.blocs[addr.blocs.Count-1];
									block.startBit = currentBit;
									block.startByte = byteCount;
									block.startTime = prevBlockEndTime;
									addr.blocs[addr.blocs.Count - 1] = block;
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
						IDBlock id = addr.ids[addr.ids.Count - 1];
						switch (byteLeftInBlock) {
							case 6: id.track = dsr.Data; break;
							case 5: id.head = dsr.Data; break;
							case 4: id.sector = dsr.Data; break;
							case 3: id.size = dsr.Data; break;
							case 2: id.crc = dsr.Data; break;
							case 1:
								id.crc = (ushort)(id.crc << 8);
								id.crc += dsr.Data;
								break;
							case 0:	// end of ID block
								BlockInfo block = addr.blocs[addr.blocs.Count-1];
								block.byteCount = byteCount - block.startByte;
								block.crc = crc;
								block.timeLength = prevBlockEndTime - block.startTime;
								addr.blocs[addr.blocs.Count - 1] = block;
								phase = Phases.p_outer;
								break;
						}
						addr.ids[addr.ids.Count - 1] = id;
					} // we are in ID
					crc = DSR.update_crc(dsr.Data, crc);
					prevBlockEndTime = currentTime;
				}	// more byte to process
			}	// for all flux

			Debug.Assert(phase != Phases.p_id, "We are in an ID field at the end of reading all addresses");
			return addr;
		}	// end of readAddr()


		public ReadSectInfo readSector(byte sectToRead, int bytePosition = 0) {
			// if no list of sectors exist we need to create one
			if (ProcessStream.Disk[ProcessStream.Track, ProcessStream.Side].readSectors == null)
				ProcessStream.Disk[ProcessStream.Track, ProcessStream.Side].readSectors = new List<ReadSectInfo>();

			// if sector already there nothing to do
			if (sectExist(sectToRead, bytePosition)) 
				return ProcessStream.Disk[ProcessStream.Track, ProcessStream.Side].readSectors[sectIndex(sectToRead, bytePosition)];

			// not there create it
			ReadSectInfo sect = new ReadSectInfo();
			sect.buffer = new TrackBuffer();

			ProcessStream.TrackData currentTrack = ProcessStream.currentTrack();
			int nbRev = currentTrack.indexCount - 1;
			int start = ProcessStream.Start;
			int stop = ProcessStream.Stop;
			int indexFirstFluxCorrection = ProcessStream.IndexFirstFluxCorrection;

			Phases phase = Phases.p_outer;
			DSR dsr = new DSR();
			int syncCount = 0;
			byte previousSync = 0;
			int byteLeftInBlock = 0;
			int currentTime = 0;
			int prevBlockEndTime = 0;
			ushort crc = 0;
			int currentBit = 0;
			int clock;
			int nbWin;
			bool idFound = false;
			bool dataFound = false;
			int byteCount = 0;
			bool breakLoop = false;

			//			for (int i = start; (i < stop) || (phase == Phases.p_id); i++) {
			// TODO check limits
			for (int i = start; (i < stop)  || (phase == Phases.p_id) || (phase == Phases.p_data) ; i++) {
				if (breakLoop || (dataFound && (phase != Phases.p_data)))
					break;
				Debug.Assert(i < currentTrack.indexes[nbRev].fluxPosition,
					"ID/DATA Field extends beyond Index in the last revolution",
					String.Format("Internal error (please report): Trying to read an {0} field bytes that extend beyond the last Index.", (phase == Phases.p_id) ? "ID" : "DATA"));

				int flux = ProcessStream.sampleToTiming(currentTrack.flux[i]);
				if (i == start)
					flux -= indexFirstFluxCorrection;
				if (i == (stop - 1))
					flux += indexFirstFluxCorrection;
				currentTime += flux;

				if (flux > 32000)
					nbWin = flux / 2000;
				else
					nbWin = currentTrack.nbWin[i];

				currentBit += nbWin;
				dsr.shiftBitsIn(nbWin);	// push data in register
				clock = currentTrack.clock[i];

				while (dsr.byteReady()) {
					byteCount++;

					// TODO DOI => count < 0

					// We search sync in ID or DATA
					if ((phase != Phases.p_id) && (phase != Phases.p_data))
						currentBit -= dsr.searchSynch();
					else
						dsr.resetSync();
					dsr.extract();

					if (dsr.SyncFound) {
						syncCount++;
						// check bad sequence
						if ((syncCount > 1) && (dsr.Data != previousSync))
							syncCount = 1;
						// reset CRC on first A1
						if ((syncCount == 1) && (dsr.Data == 0xA1)) crc = 0xFFFF;
						previousSync = dsr.Data;
					}	// sync byte

					// check short sync sequence
					if ((syncCount > 0) && (syncCount < 3) && !dsr.SyncFound)
						syncCount = 0;

					if ((phase != Phases.p_id) && (phase != Phases.p_data)) { // We are in a GAP
							if ((syncCount > 2) && !dsr.SyncFound) {
							// 3+ sync & not sync => should be an AM
							syncCount = 0;	// reset sync count
							switch (dsr.Data) {
								case 0xFE:	// IDAM - start of an ID block
								case 0xFF:
									// if we have already found the requested ID this means that
 									// no DATA block were found before this ID and therefore we should break
									if (idFound) {
										sect.idBlock.crc = crc;
										sect.idBlock.byteCount = byteCount - sect.idBlock.startByte;
										sect.idBlock.timeLength = prevBlockEndTime - sect.idBlock.startTime;
										dataFound = false;
										phase = Phases.p_id;
										breakLoop = true;
									}

									// if we have not yet reach the requested position we ignore
									if ((byteCount + 1) < bytePosition)	// allow one extra char
										break;

									sect.id.iam = dsr.Data;
									sect.idBlock.startBit = currentBit;
									sect.idBlock.startByte = byteCount;
									sect.idBlock.startTime = prevBlockEndTime;
									byteLeftInBlock = 7;
									phase = Phases.p_id;
									break;

								case 0xFB:	// DAM - Start of a DATA block
								case 0xF8:	// DDAM
								case 0xFA:
								case 0xF9:
									// we check that the ID block is already found and that the DATA block is less than 43 bytes apart
									if (idFound && ((byteCount - sect.idBlock.startByte - 7) <= 43)) {
										dataFound = true;
										byteLeftInBlock = DSR.actualSize(sect.id.size) + 3; // AM + 2 CRC + Size
										sect.dataBlock.startBit = currentBit;
										sect.dataBlock.startByte = byteCount;
										sect.dataBlock.startTime = prevBlockEndTime;
										phase = Phases.p_data;
									}
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
								case 6: sect.id.track = dsr.Data; break;
								case 5: sect.id.head = dsr.Data; break;
								case 4: 
									sect.id.sector = dsr.Data;
									if (sectToRead != dsr.Data) {
										phase = Phases.p_outer;
										// TODO check if need to reset
									}
									else
										idFound = true;
									break;
								case 3: sect.id.size = dsr.Data; break;
								case 2: sect.id.crc = dsr.Data; break;
								case 1:
									sect.id.crc = (ushort)(sect.id.crc << 8);
									sect.id.crc += dsr.Data;
									break;
								case 0:	// end of ID block
									sect.idBlock.crc = crc;
									sect.idBlock.byteCount = byteCount - sect.idBlock.startByte;
									sect.idBlock.timeLength = prevBlockEndTime - sect.idBlock.startTime;
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
									sect.dataBlock.crc = crc;
									sect.dataBlock.byteCount = byteCount - sect.dataBlock.startByte;
									sect.dataBlock.timeLength = prevBlockEndTime - sect.dataBlock.startTime;
									phase = Phases.p_outer;
									break;

							}	// DATA Segment
					} // ID or DATA

					if (phase == Phases.p_data)
						sect.buffer.addByte(dsr.Data, dsr.Clock, dsr.Mfm, phase, (ushort)clock, 0, prevBlockEndTime);
					crc = DSR.update_crc(dsr.Data, crc);
					prevBlockEndTime = currentTime;
				}	// while bytes in the DSR
			}	// For all transitions

			if (phase == Phases.p_id) {
				//TODO
			}

			if (phase == Phases.p_data) {
				// TODO
			}

			sect.sectHasData = idFound && dataFound;
			if (idFound) {
				ProcessStream.Disk[ProcessStream.Track, ProcessStream.Side].readSectors.Add(sect);
				return sect;
			}

			// should not happen
			return null;

		}	// readSector


		public void readAll() {
			FDC fdc = new FDC();
			ReadTrackInfo track = fdc.readTrack();
			ReadAddrInfo addr = fdc.readAddr();
			for (int i = 0; i < addr.ids.Count; i++) {
				ReadSectInfo sect = fdc.readSector(addr.ids[i].sector, addr.blocs[i].startByte);
			}
		}

		public void clearAll() {
			ProcessStream.Disk[ProcessStream.Track, ProcessStream.Side].readTrack = null;
			ProcessStream.Disk[ProcessStream.Track, ProcessStream.Side].readAddr = null;
			ProcessStream.Disk[ProcessStream.Track, ProcessStream.Side].readSectors = null;
		}


		private bool sectExist(byte num, int position) {
			List<ReadSectInfo> sectors = ProcessStream.Disk[ProcessStream.Track, ProcessStream.Side].readSectors;
			foreach(ReadSectInfo sector in sectors)
				if ((sector.id.sector == num) && (sector.idBlock.startByte >= position))
					return true;
			return false;
		}

		private int sectIndex(byte num, int position) {
			int sectCount = ProcessStream.Disk[ProcessStream.Track, ProcessStream.Side].readSectors.Count;
			for (int i = 0; i < sectCount; i++)
				if ((ProcessStream.Disk[ProcessStream.Track, ProcessStream.Side].readSectors[i].id.sector == num) &&
					(ProcessStream.Disk[ProcessStream.Track, ProcessStream.Side].readSectors[i].idBlock.startByte >= position))
					return i;
			return -1;
		}


	}
}
