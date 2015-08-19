/*!
@file ProcessStream.cs
<summary> Process information from the KryoFlux Stream File</summary>

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
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

using KFStreamPackage;
using WD1772Package;
using GlobalPackage;
using System.IO;
using System.Diagnostics;
using System.Windows.Threading;
using System.Reflection;

namespace KFStreamPackage {
	/// <summary>
	/// Class to Process all KryoFlux Stream files
	/// </summary>
	public class ProcessStream {
		private static bool _interpolate = false;
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
				
				Global.Disk[track, side].fluxData = new FluxData();
				Global.Disk[track, side].fluxData.fluxValue = new int[stream.FluxCount];
				Global.Disk[track, side].fluxData.totalFluxCount = stream.FluxCount;
				Global.Disk[track, side].fluxDataRev = new FluxDataRev[stream.IndexCount - 1];

				int fluxMin = Int32.MaxValue;
				int fluxMax = 0;
				double tick = 1000000000.0 / stream.SampleClock;
				double sample = tick;

				for (int i = 0; i <= stream.Indexes[0].fluxPosition; i++ )
					Global.Disk[track, side].fluxData.fluxValue[i] = (int)((double)stream.FluxValues[i] * sample);

				for (int rev = 0; rev < stream.IndexCount - 1; rev++) {
					double interpolate = 200000000.0 / (double)stream.Indexes[rev + 1].indexTime;
					if (_interpolate)
						sample = interpolate;
					Global.Disk[track, side].fluxDataRev[rev].revolutionTime = (int)((double)stream.Indexes[rev + 1].indexTime * sample);
					Global.Disk[track, side].fluxDataRev[rev].fluxCount = stream.Indexes[rev + 1].fluxPosition - stream.Indexes[rev].fluxPosition;
					int offset = stream.Indexes[rev].fluxPosition;
					Global.Disk[track, side].fluxDataRev[rev].firstFluxIndex = offset;

					for (int f = 0; f < Global.Disk[track, side].fluxDataRev[rev].fluxCount; f++) {
						Global.Disk[track, side].fluxData.fluxValue[offset + f] = (int)((double)stream.FluxValues[offset + f] * sample);
						int value = Global.Disk[track, side].fluxData.fluxValue[offset + f];
						if (value > fluxMax) fluxMax = value;
						if (value < fluxMin) fluxMin = value;
						if ((double)stream.Indexes[rev].preIndexTime * sample > 10000.0) {
							// If we have an NFA at border of revolution we split at position of the index.
							// The pre-index part is added to the last transition and the post-index part
							// is added to the first transition.
							if (f == 0)
								Global.Disk[track, side].fluxData.fluxValue[offset + f] -= (int)((double)stream.Indexes[rev].preIndexTime * sample);
							else if (f == (Global.Disk[track, side].fluxDataRev[rev].fluxCount - 1))
								Global.Disk[track, side].fluxData.fluxValue[offset + f] += (int)((double)(stream.Indexes[rev].preIndexTime * sample));
						}	// NFA
					}	// all flux in rev
				}	// all rev

				Global.Disk[track, side].fluxData.maxFlux = fluxMax;
				Global.Disk[track, side].fluxData.minFlux = fluxMin;

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


		public static bool writeStreamTracks(string sourceImage, TextBox tb) {
			// we first create the directory
			if (Directory.Exists(sourceImage)) {
				if (MessageBox.Show(String.Format("Directory already exists\n '{0}'\nDo you want to overwrite content", sourceImage),
						"Overwrite directory content?", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.No)
						return false;
			}
			Directory.CreateDirectory(sourceImage);

			tb.Clear();
			// write all raw files
			for (int side = 0; side < 2; side++) {
				for (int track = 0; track < 84; track++) {
					if (Global.Disk[track, side].fluxData != null) {
						KFWriter w = new KFWriter(tb);
						string trackName = String.Format("track{0:D2}.{1}.raw", track, side);
						string fileName = Path.Combine(sourceImage, Path.GetFileName(trackName));
						bool s = w.writeStream(fileName, Global.Disk[track, side].fluxData, Global.Disk[track, side].fluxDataRev);
						tb.AppendText(String.Format("File {0} written\n", fileName));
					}
				}
			}

			// if dtc installed use it to generate the ctr file
			if (File.Exists("dtc.exe")) {
				try {
					ProcessStartInfo si = new ProcessStartInfo();
					//string a = Directory.GetParent(Assembly.GetExecutingAssembly().Location).FullName;
					//string b = System.AppDomain.CurrentDomain.BaseDirectory;
					//string execFile = Path.Combine(b, Path.GetFileName("dtc.exe"));
					si.FileName = "dtc.exe";

					string inputFiles = Path.Combine(sourceImage, Path.GetFileName("*"));
					string outputFile = Path.Combine(Path.GetDirectoryName(sourceImage), Path.GetFileName(Path.GetFileName(sourceImage) + ".ctr"));
					si.Arguments = @"-m1 -f""" + inputFiles + @""" -i0 -f""" + outputFile + @""" -i2";
					//si.UseShellExecute = false;
					//si.RedirectStandardOutput = true;
					//si.CreateNoWindow = true;

					Process process = Process.Start(si);
					process.WaitForExit();
					//string s = process.StandardOutput.ReadToEnd();
				}
				catch (Exception exc) {
					MessageBox.Show(String.Format("Error: {0}", exc.Message));
				}

			}
			return true;
		}		

	}	// streamProcess Class
}	// name space
