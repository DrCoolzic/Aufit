/*!
@file ProcessStream.cs
<summary> Process information from the KryoFlux Stream File</summary>

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
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

using KFReaderPackage;
using WD1772Package;
using GlobalPackage;

namespace KFReaderPackage {
	/// <summary>
	/// Class to Process all KryoFlux Stream files
	/// </summary>
	public class ProcessStream {
		/// <summary>
		/// Reads, Parses and fills the DiskTracks[track, side] structure
		/// </summary>
		/// <param name="name">Complete name of the stream file to read</param>
		/// <param name="track">The track number where to store the information (0-83)</param>
		/// <param name="side">The side number (0-1)</param>
		/// <returns>True if file processed without problem; false if error while processing</returns>
		public static bool readStreamTrack(string name, int track, int side) {
			KFReader stream = new KFReader();
			StreamStatus status = stream.readStream(name);
			if (status == StreamStatus.sdsOk) {
				
				double tick = 1000000000.0 / stream.SampleClock;
				double revSum = 0;
				for (int i = 0; i < stream.IndexCount - 1; i++)
					revSum += (double)(stream.Indexes[i + 1].indexTime) * tick;
				tick = tick * (1000000000 / revSum);

				Global.Disk[track, side].fluxData = new FluxData();
				Global.Disk[track, side].fluxData.fluxValue = new int[stream.FluxCount];
				for (int i = 0; i < stream.FluxCount; i++)
					Global.Disk[track, side].fluxData.fluxValue[i] = (int)((double)stream.FluxValues[i] * tick);

				Global.Disk[track, side].fluxData.fluxCount = stream.FluxCount;
				Global.Disk[track, side].fluxData.maxFlux = (int)((double)stream.StreamStat.fluxMax * tick);
				Global.Disk[track, side].fluxData.minFlux = (int)((double)stream.StreamStat.fluxMin * tick);

				Global.Disk[track, side].fluxDataRev = new FluxDataRev[stream.IndexCount - 1];
				for (int i = 0; i < stream.IndexCount - 1; i++) {
					Global.Disk[track, side].fluxDataRev[i].revolutionTime = (int)((double)stream.Indexes[i + 1].indexTime * tick);
					Global.Disk[track, side].fluxDataRev[i].fluxCount = stream.Indexes[i + 1].fluxPosition - stream.Indexes[i].fluxPosition;
					Global.Disk[track, side].fluxDataRev[i].timeToIndex = (int)((double)stream.Indexes[i].preIndexTime * tick);
					Global.Disk[track, side].fluxDataRev[i].firstFluxIndex = stream.Indexes[i].fluxPosition;
				}
				return true;
			}	// read correctly

			Global.Disk[track, side].fluxData = null;
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

	}	// streamProcess Class
}	// namespace