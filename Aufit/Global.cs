/*!
@file Aufit.cs

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

using KFStreamReaderNS;
using WD1772NS;

namespace Aufit {

/// <summary>
/// Contains all global parameters and functions ...
/// </summary>
public class Global {
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
		public int clockAvg;

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
		/// <remarks>This value is set by readTrack()</remarks>
		public TrackContent trackContent;

		public AddrContent addrContent;
		public List<SectorContent> sectorsContent;
	}

	private static TrackData[,] _diskInfo = new TrackData[85, 2];
	private static double _sampleClock = 0;

	public static void resetDisk() {
		_diskInfo = null;
		_diskInfo = new TrackData[85, 2];
		_sampleClock = 0;
		Track = 0;
		Side = 0;
		Rev = 0;
	}

	public static double SampleClock {
		get { return _sampleClock; }
		set { _sampleClock = value; }
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


}	// class Global
}	// Aufit namespace
