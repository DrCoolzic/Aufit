/*!
@file ProcessStream.cs
<summary> Process information from the Stream File</summary>

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
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

using KFStreamReaderNS;
using WD1772NS;

namespace StreamNS {

/// <summary>
/// Contains functions to Process the information retrieved from the Stream file
/// </summary>
public class ProcessStream {
	/// <summary>
	/// Contains information about one track
	/// </summary>
	public struct TrackData {
		/// <summary> Array of Index structure for this track</summary>
		/// <remarks>This array is filled by readStreamTrack() method</remarks>
		public IndexInfo[] indexes;
		/// <summary>Effective number of Index structure recorded for this track</summary>
		/// <remarks>This array is filled by readStreamTrack() method</remarks>
		public int indexCount;
		/// <summary>Array of flux transitions for this track</summary>
		/// <remarks>This array is filled by readStreamTrack() method</remarks>
		public int[] flux;
		/// <summary>Effective number of  flux transitions recorded for this track</summary>
		/// <remarks>This array is filled by readStreamTrack() method</remarks>
		public int fluxCount;
		/// <summary>Value of the shortest flux transition recorded for this track in sample clock</summary>
		/// <remarks>This value is filled by readStreamTrack() method</remarks>
		public int fluxMin;
		/// <summary>Value of the largest flux transition recorded for this track in sample clock</summary>
		/// <remarks>This value is filled by readStreamTrack() method</remarks>
		public int fluxMax;

		/// <summary>Array of clock values as decoded by the DPLL.</summary>
		/// This array is filled by mfmDecode() method. The array has the same size as the flux array</remarks>
		public int[] clock;
		/// <summary>Array of nbWin values as decoded by the DPLL.</summary>
		/// This array is filled by mfmDecode() method. The array has the same size as the flux array.
		/// nbWin is the number of inspection windows to the next flux transition (i.e. bit to shift
		/// in the DSR</remarks>
		public int[] nbWin;
		/// <summary>Smallest value of the clock computed by the DPLL for this track</summary>
		/// This value is filled in the mfmDecode() method.</remarks>
		public int clockMin;
		/// <summary>Smallest value of the clock computed by the DPLL for this track</summary>
		/// This value is filled in the mfmDecode() method.</remarks>
		public int clockMax;

		//public byte[] packedBits;
		//public int[] packedClock;
		//public int[] bitPosition;

		/// <summary>Computed entropy for this track</summary>
		/// <remarks>This value is set by computeHisto()</remarks>
		public double entropy;
		/// <summary>Histogram Array: Flux transition repartition</summary>
		/// <remarks>This value is set by computeHisto()</remarks>
		public int[] histogram;
		/// <summary>List of maximums in the histogram</summary>
		/// <remarks>This value is set by computeHisto()</remarks>
		public List<int> bands;

		/// <summary>Information from a read track</summary>
		/// <remarks>This value is set by readTrack()()</remarks>
		public ReadTrackInfo readTrack;

		public ReadAddrInfo readAddr;
	}

	private static TrackData[,] _diskInfo = new TrackData[85, 2];
	private static double _sampleClock = 0;


	public static void reset() {
		_diskInfo = null;
		_diskInfo = new TrackData[85, 2];
		_sampleClock = 0;
	}



	/// <summary>
	/// A two dimensions array that store TrackData structures for a complete disk.
	/// There are 85 Tracks * 2 Sides TRackInfo structures.
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
	public static int Rev { get; set; }
	/// <summary>
	/// Return the current Track value
	/// </summary>
	public static int Track { get; set; }
	/// <summary>
	/// Return the current Side value
	/// </summary>
	public static int Side { get; set; }


	/// <summary>
	/// This functions takes a KF sample value and returns the corresponding timing in nanoseconds based on the sample clock.
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
	/// This function fill the histogram _histo array compute the entropy and display debug information
	/// </summary>
	/// <param name="text">The TextBox where the debug information are written</param>
	/// <returns>the computed entropy</returns>
	public static double computeHisto(TextBox text) {
		if (currentTrack().flux == null) return 0;

		int start, stop;
		int threshold;
		int[] tmpHisto;

		if (Rev > 0) {
			start = _diskInfo[Track, Side].indexes[Rev - 1].fluxPosition;
			stop = _diskInfo[Track, Side].indexes[Rev].fluxPosition;
			threshold = 2;
		}
		else {
			start = _diskInfo[Track, Side].indexes[0].fluxPosition;
			stop = _diskInfo[Track, Side].indexes[_diskInfo[Track, Side].indexCount - 1].fluxPosition;
			threshold = 10;
		}

		int sizeBeforeTrim = _diskInfo[Track, Side].flux.Max() + 1;
		tmpHisto = new int[sizeBeforeTrim];
		for (int i = start; i < stop; i++) 
			tmpHisto[_diskInfo[Track, Side].flux[i]]++;

		// we trim the array size to keep only significant entries. This is useful as
		// we may end up with a huge array if track has Non Flux areas. Computations for
		// Bezier takes long time on large array. 
		int histoSize = sizeBeforeTrim;
		for (int i = sizeBeforeTrim - 1; i >= 0; i--) {
			if (tmpHisto[i] >= threshold) break;
			else histoSize--;
		}
		_diskInfo[Track, Side].histogram = new int[histoSize];
		for (int i = 0; i < histoSize; i++) 
			_diskInfo[Track, Side].histogram[i] = tmpHisto[i];

		int maxFound = 0;
		bool searchMax = true;
		int last = 0;
		_diskInfo[Track, Side].bands = new List<int>();
		int notNullEntries = 0;

		// find bands
		for (int i = 0; i < histoSize; i++) {
			int current = _diskInfo[Track, Side].histogram[i] = tmpHisto[i];
			if (current > 0) notNullEntries++;
			if (searchMax) {
				// we have reached the maximum when we go down but at least 10
				if (current < last - 20) {
					// we ignore too small values
					if (last > 40) {
						maxFound++;
						_diskInfo[Track, Side].bands.Add(current);
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
			if (_diskInfo[Track, Side].histogram[i] > 0) {
				double p = (double)_diskInfo[Track, Side].histogram[i] / (double)notNullEntries;
				entropy += p * Math.Log(p, 2);
			}
		_diskInfo[Track, Side].entropy = entropy;
		if (entropy < 0) _diskInfo[Track, Side].bands.Clear();

		text.AppendText(String.Format("Track {0:D2}.{1} Rev {2}", Track, Side, Rev));
		text.AppendText(String.Format(" * {0} * (Ent {1:F0})",
			(entropy < 2000) ? (entropy < 0) ? "Unformatted track" : "Unformatted area" : "Normal track", entropy));
		text.AppendText(String.Format(" - Fluxes {0}", stop - start));
		text.AppendText(String.Format(" - Histo size {0} ({1} ns)\n", 
			histoSize, sampleToTiming(histoSize-1)));
		text.ScrollToEnd();

		return entropy;
	}


	/// <summary>
	/// Reads, Parses and fills the DiskTracks[track, side] structure
	/// </summary>
	/// <param name="name">Complete name of the stream file to read</param>
	/// <param name="track">The track number where to store the information (0-83)</param>
	/// <param name="side">The side number (0-1)</param>
	/// <returns>True if file processed without problem; false if error while processing</returns>
	public static bool readStreamTrack(string name, int track, int side) {
		KFStreamReader stream = new KFStreamReader();
		StreamStatus status = stream.readStream(name);
		if (status == StreamStatus.sdsOk) {
			_diskInfo[track, side].flux = stream.FluxValues;
			_diskInfo[track, side].fluxCount = stream.FluxCount;
			_diskInfo[track, side].indexes = stream.Indexes;
			_diskInfo[track, side].indexCount = stream.IndexCount;
			_sampleClock = stream.SampleClock;
			_diskInfo[track, side].fluxMax = stream.StreamStat.fluxMax;
			_diskInfo[track, side].fluxMin = stream.StreamStat.fluxMin;
			return true;
		}
		_diskInfo[track, side].flux = null;
		_diskInfo[track, side].indexes = null;
		return false;
	}

	/// <summary>
	/// This is an asynchronous wrapper of the readTrack() function
	/// </summary>
	/// <param name="name">Name of the stream file to read</param>
	/// <param name="track">Store information for this track</param>
	/// <param name="side">Store the information for this side</param>
	/// <returns>True if file processed without problem; false if error while processing</returns>
	/// See also <see cref="readTrack()"/> function.
	public static Task<bool> readStreamTrackAsync(string name, int track, int side) {
		return Task<bool>.Run(() => readStreamTrack(name, track, side));
	}


	/// <summary>
	/// Converts Flux transition into bits and clocks using the DPLL. Also sets min max values
	/// </summary>
	public static void mfmDecode() {
		int minClock = Int32.MaxValue;
		int maxClock = 0;
		int clockAvg = 0;

		int curentTime = 0;
		int currentBit = 0;

		TrackData track = _diskInfo[Track, Side];
		_diskInfo[Track, Side].clock = new int[track.fluxCount];
		_diskInfo[Track, Side].nbWin = new int[track.fluxCount];
		
		DPLL dpll = new DPLL();
		int sizeBeforeTrim = _diskInfo[Track, Side].fluxMax + 1;		
		int[] tmpHisto = new int[sizeBeforeTrim];

		// compute min max and store nbWin & clock	
		for (int i = 0; i < track.indexes[track.indexCount - 1].fluxPosition; i++) {
			int flux = sampleToTiming(track.flux[i]);
			tmpHisto[track.flux[i]]++;
			curentTime += flux;
			int nbWin = dpll.dataInWindow(curentTime);
			currentBit += nbWin;
			_diskInfo[Track, Side].nbWin[i] = nbWin;

			int clk = dpll.Clock;
			if (clk > maxClock) maxClock = clk;
			if (clk < minClock) minClock = clk;
			_diskInfo[Track, Side].clock[i] = clk;
			clockAvg += clk;

		}


		_diskInfo[Track, Side].clockMin = minClock;
		_diskInfo[Track, Side].clockMax = maxClock;
		clockAvg = clockAvg / track.indexes[track.indexCount - 1].fluxPosition;



	}


}	// streamProcess Class
}	// namespace


//_diskInfo[Track, Side].packedBits = new byte[(currentBit / 8) + 1];
//_diskInfo[Track, Side].packedClock = new int[(currentBit / 8) + 1];

//int lastClockPos = 0;
//int sigmaClocks = 0;
//int nbClocks = 0;
//for (int i = 0; i < track.indexes[track.indexCount - 1].fluxPosition; i++) {
//	int bytePos = _diskInfo[Track, Side].bitPosition[i] / 8;
//	int setBit = 0x1 << (_diskInfo[Track, Side].bitPosition[i] % 8);
//	_diskInfo[Track, Side].packedBits[bytePos] |= (byte)setBit;

//	if (_diskInfo[Track, Side].nbWin[i] < 100) {
//		sigmaClocks += _diskInfo[Track, Side].clock[i];
//		nbClocks++;
//	}
//	else {
//		sigmaClocks = _diskInfo[Track, Side].clock[i];
//		nbClocks = 1;
//	}	// NFA
//	if (bytePos > lastClockPos) {
//		while (bytePos > lastClockPos)
//			_diskInfo[Track, Side].packedClock[lastClockPos++] = sigmaClocks / nbClocks;
//		nbClocks = 0;
//		sigmaClocks = 0;			
//	}

//}

