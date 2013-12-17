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
using Aufit;

namespace StreamNS {
public class ProcessStream {
	/// <summary>
	/// This function fill the histogram _histo array compute the entropy and display debug information
	/// </summary>
	/// <param name="text">The TextBox where the debug information are written</param>
	/// <returns>the computed entropy</returns>
	public static double computeHisto(int track, int side, int rev, TextBox text) {
		if (Global.Disk[track, side].flux == null) return 0;

		int start, stop;
		int threshold;
		int[] tmpHisto;

		if (rev > 0) {
			start = Global.Disk[track, side].indexes[rev-1].fluxPosition;
			stop = Global.Disk[track, side].indexes[rev].fluxPosition;
			threshold = 2;
		}
		else {
			start = Global.Disk[track, side].indexes[0].fluxPosition;
			stop = Global.Disk[track, side].indexes[Global.Disk[track, side].indexCount - 1].fluxPosition;
			threshold = 10;
		}

		int sizeBeforeTrim = Global.Disk[track, side].flux.Max() + 1;
		tmpHisto = new int[sizeBeforeTrim];
		for (int i = start; i < stop; i++)
			tmpHisto[Global.Disk[track, side].flux[i]]++;

		// we trim the array size to keep only significant entries. This is useful as
		// we may end up with a huge array if track has Non Flux areas. Computations for
		// Bezier takes long time on large array. 
		int histoSize = sizeBeforeTrim;
		for (int i = sizeBeforeTrim - 1; i >= 0; i--) {
			if (tmpHisto[i] >= threshold) break;
			else histoSize--;
		}
		Global.Disk[track, side].histogram = new int[histoSize];
		for (int i = 0; i < histoSize; i++)
			Global.Disk[track, side].histogram[i] = tmpHisto[i];

		int maxFound = 0;
		bool searchMax = true;
		int last = 0;
		Global.Disk[track, side].bands = new List<int>();
		int notNullEntries = 0;

		// find bands
		for (int i = 0; i < histoSize; i++) {
			int current = Global.Disk[track, side].histogram[i] = tmpHisto[i];
			if (current > 0) notNullEntries++;
			if (searchMax) {
				// we have reached the maximum when we go down but at least 10
				if (current < last - 20) {
					// we ignore too small values
					if (last > 40) {
						maxFound++;
						Global.Disk[track, side].bands.Add(current);
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
			if (Global.Disk[track, side].histogram[i] > 0) {
				double p = (double)Global.Disk[track, side].histogram[i] / (double)notNullEntries;
				entropy += p * Math.Log(p, 2);
			}
		Global.Disk[track, side].entropy = entropy;
		if (entropy < 0) Global.Disk[track, side].bands.Clear();

		text.AppendText(String.Format("Track {0:D2}.{1} Rev {2}", track, side, rev));
		text.AppendText(String.Format(" * {0} * (Ent {1:F0})",
			(entropy < 2000) ? (entropy < 0) ? "Unformatted track" : "Unformatted area" : "Normal track", entropy));
		text.AppendText(String.Format(" - Fluxes {0}", stop - start));
		text.AppendText(String.Format(" - Histo size {0} ({1} ns)\n", 
			histoSize, Global.sampleToTiming(histoSize-1)));
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
			Global.Disk[track, side].flux = stream.FluxValues;
			Global.Disk[track, side].fluxCount = stream.FluxCount;
			Global.Disk[track, side].indexes = stream.Indexes;
			Global.Disk[track, side].indexCount = stream.IndexCount;
			Global.SampleClock = stream.SampleClock;
			Global.Disk[track, side].fluxMax = stream.StreamStat.fluxMax;
			Global.Disk[track, side].fluxMin = stream.StreamStat.fluxMin;
			return true;
		}
		Global.Disk[track, side].flux = null;
		Global.Disk[track, side].fluxCount = 0;
		Global.Disk[track, side].indexes = null;
		Global.Disk[track, side].indexCount = 0;
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

		Global.TrackData track = Global.currentTrack();
		Global.Disk[Global.Track, Global.Side].clock = new int[track.fluxCount];
		Global.Disk[Global.Track, Global.Side].nbWin = new int[track.fluxCount];
		
		DPLL dpll = new DPLL();
		int sizeBeforeTrim = Global.currentTrack().fluxMax + 1;		
		int[] tmpHisto = new int[sizeBeforeTrim];

		// compute min max and store nbWin & clock	
		for (int i = 0; i < track.indexes[track.indexCount - 1].fluxPosition; i++) {
			int flux = Global.sampleToTiming(track.flux[i]);
			tmpHisto[track.flux[i]]++;
			curentTime += flux;
			int nbWin = dpll.dataInWindow(curentTime);
			currentBit += nbWin;
			Global.currentTrack().nbWin[i] = nbWin;

			int clk = dpll.Clock;
			if (clk > maxClock) maxClock = clk;
			if (clk < minClock) minClock = clk;
			Global.currentTrack().clock[i] = clk;
			clockAvg += clk;

		}

		Global.Disk[Global.Track, Global.Side].clockMin = minClock;
		Global.Disk[Global.Track, Global.Side].clockMax = maxClock;
		Global.Disk[Global.Track, Global.Side].clockAvg = clockAvg / track.indexes[track.indexCount - 1].fluxPosition;
	}


}	// streamProcess Class
}	// namespace


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

