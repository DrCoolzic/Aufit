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

using KFReaderPackage;
using GlobalPackage;
using Aufit;
using System.Windows.Controls;
using System.Windows;

namespace WD1772Package {
	/// <summary>Different phases of a sector: ID, Inner Gap, Data, Post GAP</summary>
	public enum Phases : byte {
		p_gap_post = 0,	///<! Post Data GAP
		p_gap_inner,	///!< GAP between ID and DATA
		p_id,			///<! ID (Address) block
		p_data			///<! DATA block
	}


	/// <summary>Store information about a block of data</summary>
	public struct BlockInfo {
		/// <summary>First byte of the block</summary> 
		public int startByte;

		/// <summary>Number of bytes in the block</summary> 
		public int byteCount;

		/// <summary>First bit of the block</summary> 
		public int startBit;

		/// <summary>Number of bits (clock + data) in the block</summary>
		public int bitCount;

		/// <summary>Starting Time of the block in nanoseconds from index</summary>
		public int startTime;

		/// <summary>Length of the Block in nanoseconds </summary>
		public int timeLength;

		/// <summary>CRC value at the end of reading an ID or DATA block.</summary>
		/// <remarks> Should be zero on a "good" block</remarks>
		public ushort crc;
	}


	/// <summary>Store information about the ID Field of a Sector</summary>
	public class IdField {
		/// <summary>Returns the Index Address Mark</summary> 
		public byte IDAM {
			get { return buffer.DataByte[0]; }
		}

		/// <summary>Returns the track number</summary> 
		public byte Track {
			get { return buffer.DataByte[1]; }
		}

		/// <summary>Returns the Head/Side number</summary> 
		public byte Head {
			get { return buffer.DataByte[2]; }
		}

		/// <summary>Returns the sector number</summary> 
		public byte Sector {
			get { return buffer.DataByte[3]; }
		}

		/// <summary>Returns the encoded data segment length</summary> 
		public byte Length {
			get { return buffer.DataByte[4]; }
		}

		/// <summary>Returns the CRC</summary> 
		public ushort CRC {
			get { return (ushort)((int)(buffer.DataByte[5]) + (int)(buffer.DataByte[6] << 8)); }
		}		

		/// <summary>Store the ID Field content</summary>
		public TrackBuffer buffer = new TrackBuffer();

		/// <summary> Contains information about the Id field block</summary>
		public BlockInfo info = new BlockInfo();
	}


	/// <summary>Store information about the DATA Field of a Sector</summary>
	public class DataField {
		/// <summary>Store the Index Address Mark</summary> 
		public byte DAM;

		/// <summary>buffer for all the byte read in the DATA field of the sector</summary>
		/// <remarks>The buffer does not contain the DAM and CRC kept apart</remarks>
		public TrackBuffer buffer = new TrackBuffer();

		/// <summary>Store the CRC on two bytes</summary> 
		public ushort CRC;

		/// <summary>Array of bytes to store fuzzy mask</summary> 
		public byte[] fuzzy;

		/// <summary>information about the data field </summary>
		public BlockInfo info = new BlockInfo();

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

		/// <summary>Pointer to the Recovered Data Field of the sector (can be null)</summary>
		/// <remarks>If we find fuzzy bytes and good CRC we recover in this field</remarks>
		public DataField recovered_data;

		/// <summary>
		/// Pointer to the Inner GAP field
		/// </summary>
		public GapField innerGap;

		/// <summary>
		/// Pointer to the Post Gap field
		/// </summary>
		public GapField postGap;
	}


	/// <summary>Track information</summary>
	public class Track {
		/// <summary>Flag set when a short track is detected</summary>
		public const uint SHORT_TRACK = Bit.BIT_01;

		/// <summary>Flag set when a long track is detected</summary>
		public const uint LONG_TRACK = Bit.BIT_02;

		/// <summary>Flag set when a "data track" is detected</summary>
		public const uint DATA_TRACK = Bit.BIT_03;

		public const uint BAD_DATA_GAP = Bit.BIT_04;
		public const uint SECTOR_ST = Bit.BIT_05;
		public const uint NOT_STD_SIZ = Bit.BIT_06;
		public const uint NO_FLUX_AREA = Bit.BIT_07;
		public const uint DATA_OVER_IDX = Bit.BIT_08;
		public const uint DATA_AFTER_IDX = Bit.BIT_09;
		public const uint ID_OVER_IDX = Bit.BIT_10;
		public const uint ID_AFTER_IDX = Bit.BIT_11;
		public const uint HIDDEN_DATA = Bit.BIT_12;
		public const uint INV_SYNC_SEQ = Bit.BIT_13;
		// SYNC_IN_DATA ???
		// hidden data in track ?
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

		/// <summary>Protection(s) detected for this track</summary>
		public bool hasProtections = false;

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


	/// <summary>Emulate some WD1772 FDC Commands</summary>
	public class FDC {
		private readonly static int widthUpperLimit = 2070;
		private readonly static int widthLowerLimit = 1930;

		public static bool isGapPhase(Phases p) {
			return ((p == Phases.p_gap_inner) || (p == Phases.p_gap_post));
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

			Global.Disk[track, side].track = new Track();
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

			int invalidByteInGap = 0;
			int nfaCount = 0;
			int byteInSector = 0;
			bool equal = false;
			int revolutionTime = dt.packedFluxDataRev[rev].revolutionTime;
			PhaseSector ps = new PhaseSector(dt, revolutionTime);

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
				ps.computePhaseAndSector(currentTime); 

				if (dsr.DataReady) {
					byteCount++;

					if (dsr.SyncSeq > 4)
						Global.Disk[track, side].track.flags |= Track.INV_SYNC_SEQ;

					if (dsr.IAM && (dsr.Data == 0xFE) && (dt.track.firstSyncOffset == 0)) {
						Global.Disk[track, side].track.firstSyncOffset = byteCount - 3;
					}

					if ((dsr.Data >= 0xF5) && (dsr.Data <= 0xF7) && (FDC.isGapPhase(ps.Phase)))
						invalidByteInGap++;

					// check NFA
					if ((dsr.MFM == 0) || (dsr.MFM == 0xFFFF))
						nfaCount++;
					else {
						if (nfaCount > 8)
							Global.Disk[track, side].track.flags |= Track.NO_FLUX_AREA;
						nfaCount = 0;
					}

					if (ps.compareSector && (dt.sectors[ps.SectorNumber].data != null)) {
						if (dsr.Data != dt.sectors[ps.SectorNumber].data.buffer.DataByte[byteInSector]) {
							equal = false;
						}

						if (byteInSector == (dt.sectors[ps.SectorNumber].data.buffer.DataByte.Count - 1)) {
							if (equal)
								dt.sectors[ps.SectorNumber].flags |= Sector.SEC_EQ_TRACK;
							ps.compareSector = false;
						}
						byteInSector++;
					}

					// check if sector content is same as in track (Pasti optimization)
					if (!ps.compareSector && (ps.SectorNumber >= 0) && (ps.Phase == Phases.p_data) && ps.firstByte) {						
						if (dsr.Data == dt.sectors[ps.SectorNumber].data.DAM) {
							dt.sectors[ps.SectorNumber].trackOffset = byteCount + 1;
							byteInSector = 0;
							ps.compareSector = true;
							equal = true;
							ps.firstByte = false;
						}
					}

					Global.Disk[track, side].track.buffer.addByte(dsr.Data, dsr.MFM, (ushort)bitWidth, currentTime, ps.Phase);
				}	// data ready

			}	// for all bits
			if (nfaCount > 8)
				Global.Disk[track, side].track.flags |= Track.NO_FLUX_AREA;

			Global.Disk[track, side].track.info.byteCount = byteCount;
			Global.Disk[track, side].track.info.timeLength = currentTime;
			Global.Disk[track, side].track.info.bitCount = stopBit;

			if (invalidByteInGap > 5) {
				Global.Disk[track, side].track.flags |= Track.BAD_DATA_GAP;
			}

		}	// end of readTrack()


	
		/// <summary>Read all the ID fields of a track</summary>
		/// <param name="track">The track index</param>
		/// <param name="side">The side index</param>
		/// <param name="rev">The revolution index</param>
		/// <remarks>Interpret the flux transition as the WD1772 would do with
		/// a read address command. For each new ID a sector is added to the Disk
		/// data structure.</remarks>
		public static void readAddr(int track, int side, int rev) {
			int startBit = Global.Disk[track, side].packedFluxDataRev[rev].firstFluxBit;
			int stopBit = startBit + Global.Disk[track, side].packedFluxDataRev[rev].bitCount;
			int sectorCount = 0;
			int byteLeftInBlock = 0;
			int currentTime = 0;
			int byteCount = 0;
			ushort crc = 0;

			Phases phase = Phases.p_gap_post;
			DSR dsr = new DSR();
			dsr.sectorMode();

			// We read from the beginning to end of a track looking only for ID fields (in current revolution).
			// If the end of track happen in the middle of an ID field or a sync sequence we continue with 
			// flux from the next revolution. To catch marginal sync seq we search an extra 3 bytes.
			for (int currentBit = startBit; (currentBit < (stopBit + 48)) || (phase == Phases.p_id) || (dsr.SyncSeq > 0); currentBit++) {
				int currentByte = currentBit / 8;
				int bitInByte = currentBit % 8;

				// break immediately if we are beyond buffer
				if (currentByte == Global.Disk[track, side].packedFluxData.packedBits.Count()) {
					if ((phase == Phases.p_id) || (dsr.SyncSeq > 0))
						Global.Disk[track, side].hasIncompleteData = true;
					break;
				}

				byte bits = Global.Disk[track, side].packedFluxData.packedBits[currentByte];
				int bit = (bits >> bitInByte) & 0x1;
				int bitWidth = Global.Disk[track, side].packedFluxData.bitWidth[currentByte];

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
									sector.id.info.startBit = currentBit - startBit;
									sector.id.info.startByte = byteCount;
									sector.id.info.startTime = currentTime;
									byteLeftInBlock = 7;
									phase = Phases.p_id;
									sector.id.buffer.addByte(dsr.Data, dsr.MFM, (ushort)bitWidth, currentTime, phase);
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
						if (byteLeftInBlock > 0) {
							if (byteLeftInBlock == 1)
								crc = dsr.CRC;
							Global.Disk[track, side].sectors[sectorCount - 1].id.buffer.addByte(dsr.Data, dsr.MFM, (ushort)bitWidth, currentTime, phase);
						}
						else {
							Global.Disk[track, side].sectors[sectorCount - 1].id.info.byteCount = byteCount - Global.Disk[track, side].sectors[sectorCount - 1].id.info.startByte;
							Global.Disk[track, side].sectors[sectorCount - 1].id.info.crc = crc;
							Global.Disk[track, side].sectors[sectorCount - 1].id.info.timeLength = currentTime - Global.Disk[track, side].sectors[sectorCount - 1].id.info.startTime;
							Global.Disk[track, side].sectors[sectorCount - 1].id.info.bitCount = currentBit - Global.Disk[track, side].sectors[sectorCount - 1].id.info.startBit - startBit;
							phase = Phases.p_gap_inner;
							dsr.sectorMode();	// we re-enable sync
						}	// byteInBlock == 0
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
				(Global.Disk[track, side].sectors[0].id.Sector == Global.Disk[track, side].sectors[Global.Disk[track, side].sectors.Count - 1].id.Sector) &&
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
		/// <param name="idBitPosition">If positive the sector to find must be located just after this specified 
		/// position in nanosecond. If negative this means we try to read all sector at once</param>
		/// <param name="compare">If set to true we are in compare mode, else we are in read mode</param>
		/// <remarks>The position parameter is necessary to read the right data of a duplicated sector
		/// (i.e. we have several sectors with same number). Otherwise the data for the first encountered
		/// sector would always be returned (equivalent to position==0). \n
		/// To speedup reading sectors this parameter can be set to a negative value. In this case we try
		/// to read all sectors in one revolution. But as we may have sector within sector it is possible 
		/// that some of the sectors are not read in one pass. This can be checked with the SEC_PROCESSED flag. 
		/// For all sectors not read the function must be called again this time using a positive idBitPosition value.\n
		/// When compare is false we are reading the sector normally and we fill the SectorContent structure. 
		/// But if compare==true we are in "compare" mode. In that case we read the same sector using the rev parameter 
		/// and we compare the data buffer read with the "reference" data buffer to fill the fuzzy structure.\n
		/// New for being able to write IPF file: We are now storing the GAP bytes between ID and DATA (inner gap) and
		/// beyond DATA (post gap). We store the data just after the ID field and DATA field have been read. The value
		/// read should be the value used to write the track. The WD1772 never return this kind of data because during 
		/// a read track the data are often shifted due to presence of false 0x5224 sync mark. /// </remarks>
		public static void readSector(int track, int side, int rev, byte sectToRead, int idBitPosition, bool compare) {
			int startBit = Global.Disk[track, side].packedFluxDataRev[rev].firstFluxBit;
			int stopBit = startBit + Global.Disk[track, side].packedFluxDataRev[rev].bitCount;
			int byteLeftInBlock = 0;
			int currentTime = 0;
			int byteRead = 0;
			ushort crc = 0;
			bool idFound = false;
			int widthOver = 0;
			int widthUnder = 0;
			Phases phase = Phases.p_gap_post;
			uint flags = 0;
			//bool p_inner = false;	// TODO remove
			int idCount = 0;

			Sector sector = null;
			if (idBitPosition >= 0) {
				sector = findSector(track, side, sectToRead, idBitPosition);
				Debug.Assert(sector != null, "Sector not found in readSector",
					String.Format("Track {0} Side {1} sector {2}", track, side, sectToRead));
			}	// one sector read mode

			DSR dsr = new DSR();
			dsr.sectorMode();
			DataField data = null;

			//// we normally loop from start to stop for the specified revolution.
			//// However we continue if we are in the middle of ID, Inner GAP, or DATA field
			//// we also continue until we find same number of id as in readAddr()
			//for (int currentBit = startBit; 
			//	// (currentBit < stopBit) || (phase != Phases.p_gap_post) /*|| p_inner*/ || (idCount < Global.Disk[track, side].sectors.Count) || (dsr.SyncSeq > 0);  
			//	currentBit++) {

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
			for (int currentBit = startBit;
				!breakLoop;
				currentBit++) {
				//if (breakLoop) break;

				int currentByte = currentBit / 8;
				int bitInByte = currentBit % 8;

				if (currentByte == Global.Disk[track, side].packedFluxData.packedBits.Count()) {
					// We have reach the end of the buffer
					if (!compare) {
						if ((phase == Phases.p_gap_post) || (phase == Phases.p_gap_inner)) {
							Global.Disk[track, side].hasIncompleteGap = true;
							if (phase == Phases.p_gap_post) {
								sector.postGap.info.byteCount = byteRead - sector.postGap.info.startByte - 3;
								sector.postGap.info.timeLength = currentTime - sector.postGap.info.startTime;
								sector.postGap.info.bitCount = currentBit - startBit - sector.postGap.info.startBit;
							}
							else {
								sector.innerGap.info.byteCount = byteRead - sector.innerGap.info.startByte - 3;
								sector.innerGap.info.timeLength = currentTime - sector.innerGap.info.startTime;
								sector.innerGap.info.bitCount = currentBit - startBit - sector.innerGap.info.startBit;
							}
						}
						else
							Global.Disk[track, side].hasIncompleteData = true;
					}
					break;
				}	// end of buffer

				byte bits = Global.Disk[track, side].packedFluxData.packedBits[currentByte];
				int bit = (bits >> bitInByte) & 0x1;

				int bitWidth = Global.Disk[track, side].packedFluxData.bitWidth[currentByte];
				if (bit != 0) {
					int flux = Global.fluxWidth(currentBit, track, side);
					currentTime += flux;
				}

				dsr.shiftBitsIn(bit);
				if (dsr.DataReady) {
					byteRead++;

					if ((phase != Phases.p_id) && (phase != Phases.p_data)) {
						//////////////////
						// We are in a GAP
						//////////////////

						// if we have found at least an ID we accumulate the GAP data
						if ((phase == Phases.p_gap_inner) && (idCount > 0) && (sector != null) && !compare)
							sector.innerGap.buffer.addByte(dsr.Data, dsr.MFM, (ushort)bitWidth, currentTime, phase);
						else if ((idCount > 0) && (sector != null) && (sector.postGap != null) && !compare)
							sector.postGap.buffer.addByte(dsr.Data, dsr.MFM, (ushort)bitWidth, currentTime, phase);

						if (dsr.IAM) {
							switch (dsr.Data) {
								case 0xFC:	// IBM IAM
								case 0xFD:
								case 0xFE:	// IAM - start of an ID block
								case 0xFF:
									// Case ID field //

									// terminate gap block
									if ((sector != null) && !compare) {
										if ((sector.postGap != null) && (phase == Phases.p_gap_post)){
											// terminate post gap block
											sector.postGap.info.byteCount = byteRead - sector.postGap.info.startByte - 3;
											sector.postGap.info.timeLength = currentTime - sector.postGap.info.startTime;
											sector.postGap.info.bitCount = currentBit - startBit - sector.postGap.info.startBit;
										}
										else if ((sector.innerGap != null)) {
											// terminate inner gap block
											sector.innerGap.info.byteCount = byteRead - sector.innerGap.info.startByte - 3;
											sector.innerGap.info.timeLength = currentTime - sector.innerGap.info.startTime;
											sector.innerGap.info.bitCount = currentBit - startBit - sector.innerGap.info.startBit;
										}
									}

									if (idBitPosition < 0) {
										// if not found and past index we have no data (except if very close)
										int delta = currentTime - Global.Disk[track, side].packedFluxDataRev[rev].revolutionTime;
										if (delta > 32000) {
											breakLoop = true;
											break;
										}

										// search the corresponding sector
										sector = null;
										// start position can vary if sector with random noise happen before
										foreach (Sector s in Global.Disk[track, side].sectors)
											// if (((currentBit - startBit) - s.id.info.startBit) < 300) {
											// if (currentTime - s.id.info.startTime < 16000) {
											if ((byteRead - s.id.info.startByte) < 32) {
												sector = s;
												break;
											}

										if (sector == null) {
											throw new SectorNotFoundException(
												String.Format("*** Sector not found exception T{0:D2}.{1:D1} Sect count={2} *** switching to safe mode",
												track, side, Global.Disk[track, side].sectors.Count));
										}

										// correct start byte value here as it could be different 
										// from value found in readAddress
										sector.id.info.startByte = byteRead;
										sector.flags |= Sector.SEC_PROCESSED;
										flags = 0;

										// ready to read the ID block
										byteLeftInBlock = 7;
										phase = Phases.p_id;
									}	// read multiple sectors mode

									else {
										// if we have already found the requested ID we break
										if (idFound) {
											phase = Phases.p_id;
											breakLoop = true;	// don't need to continue
											break;
										}
										// if we have not yet reach the requested position we ignore
										if ((currentBit - startBit + 10) < idBitPosition) {
											dsr.sectorMode();
											phase = Phases.p_gap_post;
											break;
										}

										// otherwise we need to read the ID block
										byteLeftInBlock = 7;
										phase = Phases.p_id;

										// correct start byte value here as it could be different 
										// from value found in readAddress
										sector.id.info.startByte = byteRead;
										sector.flags |= Sector.SEC_PROCESSED;
										flags = 0;
									}	// read one sector mode
									break;

								case 0xF8:	// DDAM
								case 0xF9:
								case 0xFA:
								case 0xFB:	// DAM - Start of a DATA block
									// case DATA field

									// If we have found the right ID field we check that this
									// Data field is less than 43 bytes apart from the Id.
									// Id field is already 7 bytes 43 + 7 = 50
									if (idFound && ((byteRead - sector.id.info.startByte) <= 60)) {
										data = new DataField();
										data.DAM = dsr.Data;
										//dataFound = true;
										byteLeftInBlock = Global.sectSize(sector.id.Length) + 3; // AM + 2 CRC + Size
										phase = Phases.p_data;
										widthOver = 0;
										widthUnder = 0;

										if (!compare) {
											// terminate inner gap block
											sector.innerGap.info.byteCount = byteRead - sector.innerGap.info.startByte - 3;
											sector.innerGap.info.timeLength = currentTime - sector.innerGap.info.startTime;
											sector.innerGap.info.bitCount = currentBit - startBit - sector.innerGap.info.startBit;
										}

										// start data block
										data.info.startBit = currentBit - startBit;
										data.info.startByte = byteRead;
										data.info.startTime = currentTime;
									}
									else {
										dsr.sectorMode();
										phase = Phases.p_gap_post;
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
						//////////////////////////////////
						// We are in an ID or DATA segment
						//////////////////////////////////
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
								case 0:	// end of ID block
									dsr.sectorMode();
									idCount++;
									phase = Phases.p_gap_inner;

									if (!compare) {
										// start new inner GAP
										sector.innerGap = new GapField();
										sector.innerGap.info.startBit = currentBit - startBit;
										sector.innerGap.info.startByte = byteRead;
										sector.innerGap.info.startTime = currentTime;
										sector.innerGap.buffer.addByte(dsr.Data, dsr.MFM, (ushort)bitWidth, currentTime, phase);
									}

									break;
								default:
									break;
							}	// ID Segment
						else {
							switch (byteLeftInBlock) {
								case 2: data.CRC = dsr.Data; break;
								case 1: data.CRC += (ushort)(dsr.Data << 8);
									crc = dsr.CRC;
									break;
								case 0:	// end of DATA block
									data.info.crc = crc;
									data.info.byteCount = byteRead - data.info.startByte - 3;
									data.info.timeLength = currentTime - data.info.startTime;
									data.info.bitCount = currentBit - startBit - data.info.startBit;

									if ((widthOver > (Global.sectSize(sector.id.Length) / 2)) || (widthUnder > (Global.sectSize(sector.id.Length) / 2))) {
										if (widthOver > (Global.sectSize(sector.id.Length) / 2))
											flags |= Sector.WIDTH_ABOVE;
										if (widthUnder > (Global.sectSize(sector.id.Length) / 2))
											flags |= Sector.WIDTH_BELOW;
									}
									else if ((widthOver > 0) && (widthUnder > 0) && ((widthOver + widthUnder) > (Global.sectSize(sector.id.Length) / 4)))
										flags |= Sector.WIDTH_INTRA;

									phase = Phases.p_gap_post;
									if (!compare) {
										// start new post GAP
										sector.postGap = new GapField();
										sector.postGap.info.startBit = currentBit - startBit;
										sector.postGap.info.startByte = byteRead;
										sector.postGap.info.startTime = currentTime;
										sector.postGap.buffer.addByte(dsr.Data, dsr.MFM, (ushort)bitWidth, currentTime, phase);
									}

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
											// TODO ???
											// to check fuzzy bits on shifted track we need at least 3 rev
											//if (Global.Disk[track, side].packedFluxDataRev.Count() < 3)
											//	Global.DataIncomplete = true;
											size = data.info.byteCount;
										}
										//	//Debug.Assert(sref.buffer.Bytes.Count == sect.buffer.Bytes.Count, "Possible truncation",
										//	//	String.Format("buffers size s1={0} s2={1}\n", sref.buffer.Bytes.Count, sect.buffer.Bytes.Count));

										byte[] fuzzy;
										int dataSize = Global.sectSize(sector.id.Length);
										fuzzy = new byte[dataSize];
										bool fuzzyFound = false;

										for (int i = 0; i < size - 3; i++) {
											byte xor = (byte)(data.buffer.DataByte[i] ^ sector.data.buffer.DataByte[i]);
											if (xor != 0) {
												fuzzyFound = true;
												fuzzy[i] |= xor;
											}
										}	// compare all bytes
										if (fuzzyFound && (sector.data.fuzzy == null))
											sector.data.fuzzy = fuzzy;

										// if we have fuzzy sector and CRC is correct this indicates
										// that we need to keep the correctly read sector.
										if (fuzzyFound && (data.info.crc == 0))
											sector.recovered_data = data;

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
				data.info.byteCount = byteRead - data.info.startByte;
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

			//bool NEW_MODE = true;
			//if (NEW_MODE) {
			// we still have some cases not handled correctly with new mode
			try {
				if (Global.Disk[track, side].sectors.Count > 0) {
					readSector(track, side, rev, 0, -1, false);
					for (int j = 0; j < Global.Disk[track, side].packedFluxDataRev.Count(); j++) {
						if (j == rev) continue;
						readSector(track, side, j, 0, -1, true);
					}
					// check SWS
					// TODO if included sector is at start of track not detected
					foreach (Sector sector in Global.Disk[track, side].sectors) {
						if ((sector.flags & Sector.SEC_PROCESSED) == 0) {
							sector.flags |= Sector.SEC_IN_SEC;
							byte sectorNumber = sector.id.Sector;
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
			}
			// else {
			catch (SectorNotFoundException e) {
				SectorNotFoundException.ExceptionMessage = e.Message;
				

				for (int i = 0; i < Global.Disk[track, side].sectors.Count; i++) {
					// in recover we delete things we might have already found
					Global.Disk[track, side].sectors[i].data = null;
					Global.Disk[track, side].sectors[i].recovered_data = null;

					byte sectorNumber = Global.Disk[track, side].sectors[i].id.Sector;
					int idBitPosition = Global.Disk[track, side].sectors[i].id.info.startBit;
					readSector(track, side, rev, sectorNumber, idBitPosition, false);

					// now multiple readSector() for fuzzy bits
					for (int j = 0; j < Global.Disk[track, side].packedFluxDataRev.Count(); j++) {
						if (j == rev) continue;
						readSector(track, side, j, sectorNumber, idBitPosition, true);
					}
				}
			}

			readTrack(track, side, rev);
			Protections.checkProtection(track, side);
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
