/*!
@file Global.cs

<summary>Contains access to all global variables and function</summary>

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

This is not really object oriented ! but iterator allow an easy access to the complete
disk and structures.

@author Jean Louis-Guerin
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using KFReaderNS;
using WD1772NS;
using System.Windows.Controls;

namespace GlobalNS {

/// <summary>
/// The RevData structure contains the information for all the revolutions of one track including the pointers to the Flux Data.
/// </summary>
/// <remarks>The RevData are kept in an array with an entry for each revolution</remarks>
public struct RevData {
	/// <summary>
	/// The time it takes to do a complete revolution. This time is given in number of 
	/// sample clock. In other word this is the exact time between two index pulses.
	/// </summary>
	public int revolutionTime;
	/// <summary>
	/// The time it takes to get to the index pulse inside the first transition for this revolution. The position 
	/// is given as a number of sample clocks before the index signal. Note that in the case of no
	/// flux area at the beginning of the track this value can be huge
	/// </summary>
	public int indexTimePosition;
	/// <summary>
	/// Position (index) in the flux array of the first flux that contains the index signal
	/// </summary>
	public int fluxOffset;
	/// <summary>
	/// The number of flux transitions for this specific revolution
	/// </summary>
	public int fluxCount;
}

/// <summary>
/// The FluxData structure contains all the flux transition related information
/// </summary>
public class FluxData {
	/// <summary>Array of flux transitions recorded for this track (all revolutions). The value is 
	/// given in number of sample clock</summary>
	public int[] fluxValue;
	/// <summary>Total number of  flux transitions recorded for this track (all revolutions)</summary>
	public int fluxCount;
	/// <summary>Value of the shortest flux transition recorded for this track in sample clock</summary>
	/// <remarks>This value is filled by readStreamTrack() method</remarks>
	public int fluxMin;
	/// <summary>Value of the largest flux transition recorded for this track in sample clock</summary>
	/// <remarks>This value is filled by readStreamTrack() method</remarks>
	public int fluxMax;
}

/// <summary>
/// Contains the decoded data. These data are decoded by the DPLL from the flux transitions
/// </summary>
public class decodeData {
	/// <summary>The clock period computed by the DPLL for the corresponding flux transition</summary>
	public int[] decodedClock;
	/// <summary>The number of inspection windows to get to the flux.</summary>
	/// <remarks>This array is filled by mfmDecode() method. The array has the same size as the flux array.
	/// nbWin is the number of inspection windows to the next flux transition (i.e. bit to shift
	/// in the DSR</remarks></remarks>
	public int[] fluxInWin;
	/// <summary>Value minimum value of the clock as computed by the DPLL</summary>
	public int clockMin;
	/// <summary>Value maximum value of the clock as computed by the DPLL</summary>
	public int clockMax;
	//public int clockAvg;
}

/// <summary>
/// Contains the histogram/entropy information
/// </summary>
public struct HistoData {
	/// <summary>Histogram Array: Flux transition repartition</summary>
	/// <remarks>This value is set by computeHisto()</remarks>
	public int[] histogram;
	/// <summary>Computed entropy for this track</summary>
	/// <remarks>This value is set by computeHisto()</remarks>
	public double entropy;
	/// <summary>List of maximums in the histogram</summary>
	/// <remarks>This value is set by computeHisto()</remarks>
	public List<int> bands;
}

/// <summary>
/// Contains all global parameters and functions for AUFIT
/// </summary>
public class Global {
	private static TrackData[,] _diskInfo = new TrackData[85, 2];
	private static double _sampleClock = 0;

	/// <summary>Contains information about one track</summary>
	public struct TrackData {
		/// <summary> Array of RevData structure for this track. There is one entry for each revolution</summary>
		public RevData[] revData;
		/// <summary>Pointer to the flux data structure for this track</summary>
		public FluxData fluxData;
		/// <summary>Pointer to the data decoded by the DPLL</summary>
		public decodeData decodeData;
		/// <summary>Histogram / entropy data</summary>
		public HistoData histoData;

		//public byte[] packedBits;
		//public int[] packedClock;
		//public int[] bitPosition;

		/// <summary>Information from a read track</summary>
		/// <remarks>This value is set by readTrack()</remarks>
		public TrackContent trackContent;
		/// <summary>Information from a read address</summary>
		/// <remarks>This value is set by readAddr()</remarks>
		public AddrContent addrContent;
		/// <summary>Information from a read sector</summary>
		/// <remarks>This value is set by readSect()</remarks>
		public List<SectorContent> sectorsContent;
	}

	/// <summary>
	/// This function is used to re-initialized all disk/track structure. Should be called each time a new FD is read.
	/// </summary>
	public static void resetDisk() {
		_diskInfo = new TrackData[85, 2];
		_sampleClock = 0;
		Track = 0;
		Side = 0;
		Rev = 1;
	}


	/// <summary>
	/// This function is used to get the position of the first flux of a track in the FluxValue buffer for a specific revolution
	/// </summary>
	/// <param name="track">The track we want to use</param>
	/// <param name="side">The side we want to use.</param>
	/// <param name="rev">The revolution we want to use</param>
	/// <returns>The offset of the first flux in the buffer</returns>
	/// <remarks>he revision parameter should be in the range 1 to the number of revolutions recorded.
	/// rev==0 has a special meaning: it indicates that we want to use the data from *all* revolutions</remarks>
	public static int startOffset(int track, int side, int rev) {
		if (rev == 0) rev++;
		return Disk[track, side].revData[rev - 1].fluxOffset;
	}

	/// <summary>
	/// This function is used to get the position of the last flux of a track in the FluxValue buffer for a specific revolution
	/// </summary>
	/// <param name="track">The track we want to use</param>
	/// <param name="side">The side we want to use.</param>
	/// <param name="rev">The revolution we want to use</param>
	/// <returns>The offset of the last flux in the buffer</returns>
	/// <remarks>The revision parameter should be in the range 1 to the number of revolutions recorded.
	/// rev==0 has a special meaning: it indicates that we want to use the data from *all* revolutions</remarks>
	public static int stopOffset(int track, int side, int rev) {
		if (rev == 0) rev = Disk[track, side].revData.Count();
		return (Disk[track, side].revData[rev - 1].fluxOffset + Disk[track, side].revData[rev - 1].fluxCount);
	}

	/// <summary>
	/// This function is used to get the position of the index in the first flux
	/// </summary>
	/// <param name="track">The track we want to use</param>
	/// <param name="side">The side we want to use.</param>
	/// <param name="rev">The revolution we want to use</param>
	/// <returns>The offset of the first flux in the buffer</returns>
	/// <remarks>he revision parameter should be in the range 1 to the number of revolutions recorded.
	public static int indexTimePosition(int track, int side, int rev) {
		if (rev == 0) rev++;
		return (Disk[track, side].revData[rev - 1].indexTimePosition);
	}


	/// <summary>
	/// Access the sample clock frequency in Hz (i.e. 1/sampling_period 
	/// </summary>
	public static double SampleClock {
		get { return _sampleClock; }
		set { _sampleClock = value; }
	}

	/// <summary>
	/// A two dimensions array that store TrackData structures for a complete disk.
	/// There are 85 Tracks * 2 Sides (170) TrackData structures.
	/// </summary>
	public static TrackData[,] Disk {
		get { return _diskInfo; }
		set { _diskInfo = value; }
	}

	/// <summary>
	/// Shortcut to get access to the current track info. This is equivalent to DiskTracks[Track, Side]
	/// </summary>
	/// <returns>DiskTracks[Track, Side]</returns>
	public static TrackData currentTrack() {
		return _diskInfo[Track, Side];
	}


	// we use auto-implemented properties (C# 3.0++) that uses anonymous backing field
	/// <summary>
	/// Return the current revolution value
	/// </summary>
	public static int Rev { get; set;  }

	/// <summary>
	/// Return the current Track value
	/// </summary>
	public static int Track { get; set; }

	/// <summary>
	/// Return the current Side value
	/// </summary>
	public static int Side { get; set; }

	/// <summary>
	/// This functions takes a sample value and returns the corresponding timing in nanoseconds based on the sample clock.
	/// </summary>
	/// <param name="sValue">The sample value</param>
	/// <returns>The timing value in nanoseconds</returns>
	public static int sampleToTiming(int sValue) {
		double compute = (double)sValue * 1000000000.0 / _sampleClock;
		return (int)compute;
	}

	/// <summary>
	/// This function takes a timing value and convert it back to the corresponding integer sample value
	/// </summary>
	/// <param name="tValue">The timing value in nanoseconds</param>
	/// <returns>The sample value as an integer</returns>
	public static int timingToSample(double tValue) {
		double compute = _sampleClock * (double)tValue / 1000000000.0;
		return (int)compute;
	}

	/// <summary>
	/// Converts Flux transition into bits and clocks using the DPLL. Also sets min max values
	/// </summary>
	public static void mfmDecode(int track, int side) {
		if (Global.Disk[track, side].decodeData != null) return;

		int minClock = Int32.MaxValue;
		int maxClock = 0;
		int clockAvg = 0;

		int curentTime = 0;
		int currentBit = 0;

		Global.TrackData td = Global.Disk[track, side];
		Global.Disk[track, side].decodeData = new decodeData();
		Global.Disk[track, side].decodeData.decodedClock = new int[td.fluxData.fluxCount + 1];
		Global.Disk[track, side].decodeData.fluxInWin = new int[td.fluxData.fluxCount + 1];
		
		DPLL dpll = new DPLL();
		int sizeBeforeTrim = Global.Disk[track, side].fluxData.fluxMax + 1;		
		int[] tmpHisto = new int[sizeBeforeTrim];

		// TODO check below
		int lastRev = Global.Disk[track, side].revData.Count() - 1;
		for (int i = Global.Disk[track, side].revData[0].fluxOffset;
			i < (Global.Disk[track, side].revData[lastRev].fluxOffset + Global.Disk[track, side].revData[lastRev].fluxCount); i++) {
			int flux = Global.sampleToTiming(td.fluxData.fluxValue[i]);
			tmpHisto[td.fluxData.fluxValue[i]]++;
			curentTime += flux;
			int nbWin = dpll.dataInWindow(curentTime);
			currentBit += nbWin;
			Global.Disk[track, side].decodeData.fluxInWin[i] = nbWin;

			int clk = dpll.Clock;
			if (clk > maxClock) maxClock = clk;
			if (clk < minClock) minClock = clk;
			Global.Disk[track, side].decodeData.decodedClock[i] = clk;
			clockAvg += clk;

		}

		Global.Disk[track, side].decodeData.clockMin = minClock;
		Global.Disk[track, side].decodeData.clockMax = maxClock;
		//Global.Disk[track, side].clockAvg = clockAvg / track.indexes[track.indexCount - 1].fluxPosition;
	}


//Global.currentTrack().packedBits = new byte[(currentBit / 8) + 1];
//Global.currentTrack().packedClock = new int[(currentBit / 8) + 1];

//int lastClockPos = 0;
//int sigmaClocks = 0;
//int nbClocks = 0;
//for (int i = 0; i < track.indexes[track.indexCount - 1].fluxPosition; i++) {
//	int bytePos = Global.currentTrack().bitPosition[i] / 8;
//	int setBit = 0x1 << (Global.currentTrack().bitPosition[i] % 8);
//	Global.currentTrack().packedBits[bytePos] |= (byte)setBit;

//	if (Global.currentTrack().nbWin[i] < 100) {
//		sigmaClocks += Global.currentTrack().clock[i];
//		nbClocks++;
//	}
//	else {
//		sigmaClocks = Global.currentTrack().clock[i];
//		nbClocks = 1;
//	}	// NFA
//	if (bytePos > lastClockPos) {
//		while (bytePos > lastClockPos)
//			Global.currentTrack().packedClock[lastClockPos++] = sigmaClocks / nbClocks;
//		nbClocks = 0;
//		sigmaClocks = 0;			
//	}

//}


	/// <summary>
	/// This function fill the histogram _histo array compute the entropy and display debug information
	/// </summary>
	/// <param name="track">We compute the histogram for this track</param>
	/// <param name="side">We compute the histogram for this side</param>
	/// /// <param name="rev">We compute the histogram for this revolution</param>
	/// <param name="text">The TextBox where the debug information are written</param>
	/// <returns>the computed entropy</returns>
	public static double computeHisto(int track, int side, int rev, TextBox text) {
		//if (Global.Disk[track, side].flux == null) return 0;
		if (Global.Disk[track, side].fluxData == null) return 0;

		int threshold;
		int[] tmpHisto;

		if (rev > 0)	threshold = 2;
		else 			threshold = 10;

		int start = Global.startOffset(track, side, rev);
		int stop = Global.stopOffset(track, side, rev);

		int sizeBeforeTrim = Global.Disk[track, side].fluxData.fluxValue.Max() + 1;
		tmpHisto = new int[sizeBeforeTrim];
		for (int i = start; i < stop; i++)
			tmpHisto[Global.Disk[track, side].fluxData.fluxValue[i]]++;

		// we trim the array size to keep only significant entries. This is useful as
		// we may end up with a huge array if track has Non Flux areas. Computations for
		// Bezier takes long time on large array. 
		int histoSize = sizeBeforeTrim;
		for (int i = sizeBeforeTrim - 1; i >= 0; i--) {
			if (tmpHisto[i] >= threshold) break;
			else histoSize--;
		}
		Global.Disk[track, side].histoData.histogram = new int[histoSize];
		for (int i = 0; i < histoSize; i++)
			Global.Disk[track, side].histoData.histogram[i] = tmpHisto[i];

		int maxFound = 0;
		bool searchMax = true;
		int last = 0;
		Global.Disk[track, side].histoData.bands = new List<int>();
		int notNullEntries = 0;

		// find bands
		for (int i = 0; i < histoSize; i++) {
			int current = Global.Disk[track, side].histoData.histogram[i] = tmpHisto[i];
			if (current > 0) notNullEntries++;
			if (searchMax) {
				// we have reached the maximum when we go down but at least 10
				if (current < last - 20) {
					// we ignore too small values
					if (last > 40) {
						maxFound++;
						Global.Disk[track, side].histoData.bands.Add(current);
					}
					searchMax = false;
				}	// max reached
			}	// looking for max
			else {
				if (current > last)
					searchMax = true;
			}
			last = current;
		}

		double entropy = 0;
		for (int i = 0; i < histoSize; i++)
			if (Global.Disk[track, side].histoData.histogram[i] > 0) {
				double p = (double)Global.Disk[track, side].histoData.histogram[i] / (double)notNullEntries;
				entropy += p * Math.Log(p, 2);
			}
		Global.Disk[track, side].histoData.entropy = entropy;
		if (entropy < 0) Global.Disk[track, side].histoData.bands.Clear();

		text.AppendText(String.Format("Track {0:D2}.{1} Rev {2}", track, side, rev));
		text.AppendText(String.Format(" * {0} * (Ent {1:F0})",
			(entropy < 2000) ? (entropy < 0) ? "Unformatted track" : "Unformatted area" : "Normal track", entropy));
		text.AppendText(String.Format(" - Fluxes {0}", stop - start));
		text.AppendText(String.Format(" - Histo size {0} ({1} ns)\n",
			histoSize, Global.sampleToTiming(histoSize - 1)));
		text.ScrollToEnd();
		return entropy;
	}

	/// <summary>
	/// Update the CRC with the character provided
	/// </summary>
	/// <param name="c">Character used to update CRC</param>
	/// <param name="crc">Current CRC value</param>
	/// <returns>Updated CRC</returns>
	public static ushort updateCRC(byte c, ushort crc) {
		for (int i = 0; i < 8; i++)
			crc = (ushort)((crc << 1) ^ (((((crc >> 8) ^ (c << i)) & 0x0080) != 0) ? 0x1021 : 0));
		return crc;
	} // end of update_crc


	/// <summary>
	/// Check if the flux is at a border of an inspection window
	/// </summary>
	/// <param name="clock">current PLL clock</param>
	/// <param name="nb_win">number of inspection windows</param>
	/// <param name="flux">position of flux</param>
	/// <returns>true if the flux is at the border of the inspection window</returns>
	public static bool isBorderBit(int clock, int nb_win, int flux) {
		int lcenter = nb_win * (clock / 2) + (clock / 4);	// (nb_win * 2000) + 1000;
		int hcenter = nb_win * (clock / 2) - (clock / 4);	// (nb_win * 2000) - 1000;
		int tenpc = clock / 10;	// 10%
		bool lower_win = ((flux > (lcenter - tenpc)) && (flux < (lcenter + tenpc)));	// ((flux > (lcenter - 400)) && (flux < (lcenter + 400)));
		bool upper_win = ((flux > (hcenter - tenpc)) && (flux < (hcenter + tenpc)));	// ((flux > (hcenter - 400)) && (flux < (hcenter + 400)));
		return (lower_win || upper_win);
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
		if (size < 4)
			s = 128 << (size & 0x03);
		else
			s = 512;
		return s;
	}

}	// class Global
}	// Aufit name space
