/*!
@file Global.cs

<summary>Contains access to all global variables and function</summary>

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

This is not really object oriented ! but iterator allow an easy access to the complete
disk and structures.

@author Jean Louis-Guerin
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Diagnostics;

using KFReaderPackage;
using WD1772Package;

namespace GlobalPackage {

/// <summary>
/// The FluxDataRev structure contains the information for one revolution of one track 
/// This information is to be used with the FluxData class
/// </summary>
/// <remarks>
/// The FluxDataRev are kept in an array with an entry for each revolution and contains 
/// pointers to information in the FluxData class. This class is used when flux samples
/// file is provided e.g. SCP or KF-RAW file.
/// </remarks>
public struct FluxDataRev {
	/// <summary>
	/// The time it takes to complete a revolution in nanoseconds (time between two indexes). 
	/// </summary>
	public int revolutionTime;
	/// <summary>
	/// The time it takes to get to the index-pulse in the first flux transition. 
	/// </summary>
	/// <remarks>
	/// The time is given in nanoseconds. This is the time from the beginning of the
	/// first flux transition of a revolution and the index-pulse signal. Note that in 
	/// case of a no flux area at the beginning of the track this value can be huge.
	/// </remarks>
	public int timeToIndex;
	/// <summary>
	///  Position of the first flux for this revolution. The position is given as
	///  an index in the FluxValue[] array of the FluxData class.
	/// </summary>
	public int firstFluxIndex;
	/// <summary>
	/// The number of flux transitions for this revolution.
	/// </summary>
	public int fluxCount;
}


/// <summary>
/// The PackedFluxDataRev structure contains the information for one revolution of one track 
/// This information is to be used with PackedFluxData class
/// </summary>
/// <remarks>
/// The PackedFluxDataRev are kept in an array with an entry for each revolution and contains 
/// pointers to information in the PackedFluxData class. This class is used when data from
/// device independent file is provided for example IPF or CTR files.
/// </remarks>
public struct PackedFluxDataRev {
	/// <summary>
	/// The time it takes to complete a revolution in nanoseconds (time between two indexes). 
	/// </summary>
	public int revolutionTime;
	/// <summary>
	///  Position of the first flux for this revolution. This position is given as the
	///  absolute position inside the packBits[] array of PackedFluxData class
	/// </summary>
	public int firstFluxBit;
	/// <summary>
	/// The number of clock/data bits for this revolution.
	/// </summary>
	public int bitCount;
	/// <summary>
	/// The number of flux transitions for this revolution.
	/// </summary>
	public int fluxCount;
}


/// <summary>
/// The FluxData structure contains the flux transition related information
/// </summary>
/// <remarks>
/// This class is used when flux samples file is provided e.g. SCP or KF-RAW file.
/// </remarks>
public class FluxData {
	/// <summary>Array of flux transitions recorded for this track (including all revolutions). 
	/// The value is given in number of sample clock</summary>
	public int[] fluxValue;
	/// <summary>Total number of  flux transitions recorded for this track  for ALL revolutions</summary>
	public int fluxCount;
	/// <summary>Value of the shortest flux transition recorded for this track in sample clock</summary>
	/// <remarks>This value is filled by readStreamTrack() method</remarks>
	public int minFlux;
	/// <summary>Value of the largest flux transition recorded for this track in sample clock</summary>
	/// <remarks>This value is filled by readStreamTrack() method</remarks>
	public int maxFlux;
}


/// <summary>Contains the clock/data information for the revolution(s) of a track</summary>
/// <remarks>
/// The clock/data bits are stored packed in a byte array (8 clock+data).
/// This class is used when data from /// device independent file is provided for example 
/// IPF or CTR files.
/// </remarks>
public class PackedFluxData {
	/// <summary>
	/// Decoded clock + data bits packed in a byte
	/// </summary>
	public byte[] packBits;
	/// <summary>
	/// The average width of a bit (half cell) time  in nanoseconds for a packBits byte.
	/// </summary>
	/// <remarks>If null standard width assumed</remarks>
	public int[] bitWidth;
	/// <summary>
	/// The timing of the first flux transition in nanoseconds.
	/// </summary>
	/// <remarks>
	/// The timing of the first flux (first "1" bit in a packBits) in nanoseconds.
	/// This reference is from the start of all samples which may or may not be the 
	/// sampling of the first track of the first revolution.
	/// </remarks>
	public int[] firstFluxTiming;
	/// <summary>
	/// Index of the first transition in the flux transition array.
	/// </summary>
	/// <remarks>
	/// When packed data are extracted from a flux transition array, this value is the index in the
	/// flux transition array of the first flux (first "1" bit in a packBits). 
	/// If packed data are not extracted from a flux transitions array its value is null.
	/// </remarks>
	public int[] firstFluxIndex;
	/// <summary>
	/// The maximum width in nanoseconds of a bit (half cell) for all samples
	/// </summary>
	/// <remarks>If null standard width assumed</remarks>
	public int maxBitWidth;
	/// <summary>
	/// The minimum width in nanoseconds of a bit (half cell) for all samples
	/// </summary>
	/// <remarks>If null standard width assumed</remarks>
	public int minBitWidth;
	/// <summary>
	/// The minimum flux transition in nanoseconds for all samples
	/// </summary>
	/// <remarks>If null standard width assumed</remarks>
	public int minFlux;
	/// <summary>
	/// The maximum flux transition in nanoseconds for all samples
	/// </summary>
	/// <remarks>If null standard width assumed</remarks>
	public int maxFlux;
}


/// <summary>
/// Contains the histogram/entropy information
/// </summary>
public struct HistoData {
	/// <summary>Histogram Array: Flux transition repartition</summary>
	public int[] histogram;
	/// <summary>Computed entropy for the track</summary>
	public double entropy;
	/// <summary>
	/// The track seems to have data
	/// </summary>
	public bool hasData;
}


/// <summary>
/// Contains all global parameters and functions for AUFIT
/// </summary>
public class Global {
	public const double UNFORMATED_ENTROPY = 6.5;
	public const int HISTO_SCALE = 50;

	private static DiskTrack[,] _disk = new DiskTrack[85, 2];
	private static bool _incompleteData = false;

	/// <summary>Contains information about a track of the floppy disk</summary>
	public struct DiskTrack {
		/// <summary>
		/// Pointer to the FluxData class for this track
		/// </summary>
		public FluxData fluxData;

		/// <summary> 
		/// Array of FluxDataRev structure for this track. 
		/// There is one entry for each revolution
		/// </summary>
		public FluxDataRev[] fluxDataRev;

		/// <summary>
		/// Pointer to the PackedFluxData class for this track
		/// </summary>
		public PackedFluxData packedFluxData;

		/// <summary> 
		/// Array of PackedFluxDataRev structure for this track. 
		/// There is one entry for each revolution
		/// </summary>
		public PackedFluxDataRev[] packedFluxDataRev;

		/// <summary>
		/// Histogram / entropy data
		/// </summary>
		public HistoData histoData;


		/// <summary>Information from a FDC read track command</summary>
		/// <remarks>This value is set by readTrack()</remarks>
		public TrackContent trackContent;

		/// <summary>Information from a FDC read address command</summary>
		/// <remarks>This value is set by readAddr()</remarks>
		public AddrContent addrContent;

		/// <summary>Information from a FDC read sector command</summary>
		/// <remarks>This value is set by readSect()</remarks>
		public List<SectorContent> sectorsContent;
	}


	/// <summary>
	/// This function is used to re-initialized all disk/track structure. Should be called 
	/// each time a new FD is read. Hopefully should also free all memory from previous read.
	/// </summary>
	public static void resetDisk() {
		_disk = new DiskTrack[85, 2];
		_incompleteData = false;
	}


	/// <summary>Set to true if we do not have enough data</summary>
	/// <remarks>This usually happen if we need more than one revolution to get all
	/// data. For example if we have data over index.</remarks>
	public static bool DataIncomplete {
		get { return _incompleteData; }
		set { _incompleteData = value; }
	}


	/// <summary>
	/// Return the flux value corresponding to a specific bit.
	/// </summary>
	/// <param name="bitPosition">position of the bit in the packed bit array</param>
	/// <param name="track">The current track</param>
	/// <param name="side">The current side</param>
	/// <returns>The flux value in nanoseconds</returns>
	public static int fluxValue(int bitPosition, int track, int side) {
		DiskTrack dt = Global.Disk[track, side];
		int packedIndex = bitPosition / 8;
		int bit = bitPosition % 8;
		int testBit = (0x1 << (bitPosition % 8)) >> 1;	
		int value = 0;
		int count = 0;
	
		// if flux exist use it other we compute
		if (dt.fluxData != null) {
			byte bits = dt.packedFluxData.packBits[packedIndex];		
			for (int i = bit - 1; i >= 0; i--) {
				if ((bits & testBit) != 0) count++;
				testBit >>= 1;
			}
			value = dt.fluxData.fluxValue[dt.packedFluxData.firstFluxIndex[packedIndex] + count];
		}
		else {	
			if (bit == 0) {
				bit = 8;
				packedIndex -= 1;
			}
			for (int pbi = packedIndex; pbi >= 0; pbi--) {
				byte bits = dt.packedFluxData.packBits[pbi];
				for (int i = bit - 1; i >= 0; i--) {
					testBit = 0x1 << (i % 8);
					if ((bits & testBit) != 0) goto done;
					else count++;
				}
				bit = 8;
			}
		done:
			value = (count + 1) * dt.packedFluxData.bitWidth[packedIndex];
		}
		return value;
	}

	/// <summary>
	/// This function is used to get the indexStart of the first flux of a track in the FluxValue buffer for a specific revolution
	/// </summary>
	/// <param name="track">The track we want to use</param>
	/// <param name="side">The side we want to use.</param>
	/// <param name="rev">The revolution we want to use</param>
	/// <returns>The indexStart of the first flux in the buffer</returns>
	/// <remarks>The revision parameter should be in the range 1 to the number of revolutions recorded.
	/// rev==0 has a special meaning: it indicates that we want to use the data from *all* revolutions</remarks>
	public static int firstIndex(int track, int side, int rev) {
		if (rev == 0) rev++;
		return Disk[track, side].fluxDataRev[rev - 1].firstFluxIndex;
	}


	// TODO remove
	/// <summary>
	/// This function is used to get the position of the last flux of a track in the FluxValue buffer for a specific revolution
	/// </summary>
	/// <param name="track">The track we want to use</param>
	/// <param name="side">The side we want to use.</param>
	/// <param name="rev">The revolution we want to use</param>
	/// <returns>The indexStart of the last flux in the buffer</returns>
	/// <remarks>The revision parameter should be in the range 1 to the number of revolutions recorded.
	/// rev==0 has a special meaning: it indicates that we want to use the data from *all* revolutions</remarks>
	public static int lastIndex(int track, int side, int rev) {
		if (rev == 0) rev = Disk[track, side].fluxDataRev.Count();
		return (Disk[track, side].fluxDataRev[rev - 1].firstFluxIndex + Disk[track, side].fluxDataRev[rev - 1].fluxCount);
	}

	// TODO remove
	/// <summary>This function is used to get the position of the indexStart in the first flux</summary>
	/// <param name="track">The track we want to use</param>
	/// <param name="side">The side we want to use.</param>
	/// <param name="rev">The revolution we want to use</param>
	/// <returns>The offset of the first flux in the buffer</returns>
	/// <remarks>The revision parameter should be in the range 1 to the number of revolutions recorded.
	public static int indexTimePosition(int track, int side, int rev) {
		if (rev == 0) rev++;
		return (Disk[track, side].fluxDataRev[rev - 1].timeToIndex);
	}


	/// <summary>
	/// A two dimensions array that store TrackData structures for a complete disk.
	/// There are 85 Tracks * 2 Sides (170) TrackData structures.
	/// </summary>
	public static DiskTrack[,] Disk {
		get { return _disk; }
		set { _disk = value; }
	}


	/// <summary>
	/// Decodes the MFM Flux transition into clock/data bits
	/// </summary>
	/// <remarks> Fill packed structure and store useful information
	/// </remarks>
	public static void mfmDecode(int track, int side) {
		// if already there nothing to do
		if (Global.Disk[track, side].packedFluxData != null) return;

		int minBitWidth = Int32.MaxValue;
		int maxBitWidth = 0;
		int currentPosition = 0;
		int widthSum = 0;
		int currentBit = 0;
		DiskTrack td = Global.Disk[track, side];

		int decodeCount = Global.Disk[track, side].fluxData.fluxCount + 1;
		ushort[] decodeBitWidth = new ushort[decodeCount];
		int[] decodeBitPosition = new int[decodeCount];
		int[] decodeFluxPosition = new int[decodeCount];
		
		DPLL dpll = new DPLL();
		for (int index = 0; index < Global.Disk[track, side].fluxData.fluxCount; index++) {
			int flux = Global.Disk[track, side].fluxData.fluxValue[index];
			currentPosition += flux;
			int bitSpacing = dpll.bitSpacing(currentPosition);
			currentBit += bitSpacing;
			decodeBitPosition[index] = currentBit;
			decodeFluxPosition[index] = currentPosition;
			int bitWidth = dpll.bitWidth;
			if (bitWidth > maxBitWidth) maxBitWidth = bitWidth;
			if (bitWidth < minBitWidth) minBitWidth = bitWidth;
			widthSum += bitWidth;
			decodeBitWidth[index] = (ushort)bitWidth;
		}	// decode MFM data from flux

		Global.Disk[track, side].packedFluxData = new PackedFluxData();
		int packedCount = (currentBit / 8) + 1;
		Global.Disk[track, side].packedFluxData.packBits = new byte[packedCount];
		Global.Disk[track, side].packedFluxData.bitWidth = new int[packedCount];
		Global.Disk[track, side].packedFluxData.firstFluxTiming = new int[packedCount];
		Global.Disk[track, side].packedFluxData.firstFluxIndex = new int[packedCount];

		int lastPackedBytePos = 0;
		int lastIndex = 0;
		widthSum = 0;
		int widthCount = 0;
		Global.Disk[track, side].packedFluxData.packBits[0] = 1;

		for (int fluxIndex = 0; fluxIndex < Global.Disk[track, side].fluxData.fluxCount; fluxIndex++) {
			int bitPosition = decodeBitPosition[fluxIndex];
			int bytePos = bitPosition / 8;
			int setBit = 0x1 << (bitPosition % 8);
			Global.Disk[track, side].packedFluxData.packBits[bytePos] |= (byte)setBit;
			widthSum += decodeBitWidth[fluxIndex];
			widthCount++;
			if (bytePos > lastPackedBytePos) {
				int avgClock = widthSum / widthCount;
				while (bytePos > lastPackedBytePos) {
					Global.Disk[track, side].packedFluxData.firstFluxTiming[lastPackedBytePos] = decodeFluxPosition[lastIndex];
					Global.Disk[track, side].packedFluxData.bitWidth[lastPackedBytePos] = avgClock;
					Global.Disk[track, side].packedFluxData.firstFluxIndex[lastPackedBytePos++] = lastIndex;
				}
				widthCount = 0;
				widthSum = 0;
				lastIndex = fluxIndex;
			}	// new entry (could be multiple for NFA)
		}	// fill packed data

		Global.Disk[track, side].packedFluxData.minBitWidth = minBitWidth;
		Global.Disk[track, side].packedFluxData.maxBitWidth = maxBitWidth;
		Global.Disk[track, side].packedFluxData.minFlux = Global.Disk[track, side].fluxData.minFlux;
		Global.Disk[track, side].packedFluxData.maxFlux = Global.Disk[track, side].fluxData.maxFlux;

		int revCount = Global.Disk[track, side].fluxDataRev.Count();
		Global.Disk[track, side].packedFluxDataRev = new PackedFluxDataRev[revCount];
		for (int i = 0; i < revCount; i++) {
			Global.Disk[track, side].packedFluxDataRev[i].revolutionTime = Global.Disk[track, side].fluxDataRev[i].revolutionTime;
			int indexStart = Global.Disk[track, side].fluxDataRev[i].firstFluxIndex;
			int bitWidth = decodeBitWidth[indexStart];
			int indexPosition = Global.Disk[track, side].fluxDataRev[i].timeToIndex;
			int correction = indexPosition / bitWidth;
			int bitPosition = decodeBitPosition[indexStart];
			bitPosition -= correction;
			Global.Disk[track, side].packedFluxDataRev[i].firstFluxBit = bitPosition;
			int indexEnd = indexStart + Global.Disk[track, side].fluxDataRev[i].fluxCount - 1;
			int bitCount = decodeBitPosition[indexEnd] - decodeBitPosition[indexStart];
			Global.Disk[track, side].packedFluxDataRev[i].bitCount = bitCount - correction;
			Global.Disk[track, side].packedFluxDataRev[i].fluxCount = Global.Disk[track, side].fluxDataRev[i].fluxCount;
		}
	}


	/// <summary>
	/// This function fill the histogram _histo array compute the entropy and display information
	/// </summary>
	/// <param name="track">We compute the histogram for this track</param>
	/// <param name="side">We compute the histogram for this side</param>
	/// <param name="rev">We compute the histogram for this revolution</param>
	/// <param name="text">The TextBox where the information are written</param>
	/// <returns>the computed entropy</returns>
	public static double computeHisto(int track, int side, int rev, TextBox text) {
		if ((Global.Disk[track, side].fluxDataRev == null) && (Global.Disk[track, side].packedFluxDataRev == null)) return 0;

		const int THRESHOLD = 2;
		int[] tmpHisto = null;
		int sizeBeforeTrim = 0;
		int fluxCount = 0;

		if (Global.Disk[track, side].fluxDataRev != null) {
			int first = Global.firstIndex(track, side, rev);
			int last = Global.lastIndex(track, side, rev);
			sizeBeforeTrim = (Global.Disk[track, side].fluxData.fluxValue.Max() / HISTO_SCALE) + 2;
			tmpHisto = new int[sizeBeforeTrim];

			for (int i = first; i < last; i++) {
				int flux = Global.Disk[track, side].fluxData.fluxValue[i] / HISTO_SCALE;
				tmpHisto[flux]++;
				fluxCount++;
			}
		}

		else if (Global.Disk[track, side].packedFluxDataRev != null) {
			int firstBit = Global.Disk[track, side].packedFluxDataRev[rev - 1].firstFluxBit;
			int lastBit = firstBit + Global.Disk[track, side].packedFluxDataRev[rev - 1].bitCount;
			sizeBeforeTrim = (Global.Disk[track, side].packedFluxData.maxFlux / HISTO_SCALE) + 2;
			tmpHisto = new int[sizeBeforeTrim];

			for (int bitPosition = firstBit; bitPosition < lastBit; bitPosition++) {
				int bytePos = bitPosition / 8;
				int testBit = 0x1 << (bitPosition % 8);
				if ((Global.Disk[track, side].packedFluxData.packBits[bytePos] & (byte)testBit) != 0) {
					int flux = Global.fluxValue(bitPosition, track, side) / HISTO_SCALE;
					tmpHisto[flux]++;
					fluxCount++;
				}
			}
		}

		else {
			Debug.Assert(false, "impossible call to computeHisto!");
		}

		// we trim the array size to keep only significant entries. This is useful as
		// we may end up with a huge array if track has Non Flux areas. Computations for
		// Bezier takes long time on large array. 
		int histoSize = sizeBeforeTrim;
		for (int i = sizeBeforeTrim - 1; i >= 0; i--) {
			if (tmpHisto[i] >= THRESHOLD) break;
			else histoSize--;
		}
		Global.Disk[track, side].histoData.histogram = new int[histoSize];
		for (int i = 0; i < histoSize; i++)
			Global.Disk[track, side].histoData.histogram[i] = tmpHisto[i];

		bool hasData = false;
		double entropy = 0;
		for (int i = 0; i < histoSize; i++)
			if (Global.Disk[track, side].histoData.histogram[i] > 0) {
				if (Global.Disk[track, side].histoData.histogram[i] > 300) {
					if ((i > (3400/HISTO_SCALE)) && (i < (8600/HISTO_SCALE))) hasData = true;
				}	// large number of flux transitions
				double p = (double)Global.Disk[track, side].histoData.histogram[i] / (double)(fluxCount); 
				entropy += p * Math.Log(p, 2);
			}
		entropy = -entropy;

		Global.Disk[track, side].histoData.entropy = entropy;
		Global.Disk[track, side].histoData.hasData = hasData;

		text.AppendText(String.Format("Track {0:D2}.{1} Rev {2}", track, side, rev));
		text.AppendText(String.Format(" * {0} * (Entropy {1:F2})",
			(entropy > UNFORMATED_ENTROPY) ? "Unformatted track" : "Normal track", entropy));
		text.AppendText(String.Format(" - Fluxes {0}", fluxCount));
		text.AppendText(String.Format(" - Histogram size {0} ({1} ns)\n",
			histoSize, (histoSize - 1) * HISTO_SCALE));
		text.ScrollToEnd();
		return entropy;
	}

	/// <summary>
	/// Return the actual size of the data field of a sector
	/// </summary>
	/// <param name="size">encoded size of the sector</param>
	/// <returns>"decoded size" of the data segment</returns>
	/// 
	/// In the ID field the size of the sector is coded 0=128, 1=256, 2=512, 3=1024
	/// The function returns the decoded value. The value 0 and 1 are not used in
	/// Atari FD and value above 3 is not valid, therefore in all these cases something
	/// is wrong with the size field in those cases the functions return a "default
	/// value" of 512.
	public static int sectSize(byte size) {
		int s;
		//if (size < 4)
		s = 128 << (size & 0x03);
		//else
		//	s = 512;
		return s;
	}

}	// class Global
}	// Aufit name space
