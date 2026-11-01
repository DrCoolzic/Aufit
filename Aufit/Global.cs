/*!
@file Global.cs

<summary>Contains access to all global variables and function</summary>

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

This is not really object oriented ! but iterator allow an easy access to the complete
disk and structures.

@author Jean Louis-Guerin
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Diagnostics;
using FDCPackage;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows;
using System.IO;
using Microsoft.Win32;
using System.ComponentModel;
using Aufit;

namespace GlobalPackage {

	/// <summary>
	/// The FluxDataRev structure contains information for one revolution of a track.
	/// This information allow to access the flux information located in the FluxData class.
	/// </summary>
	public struct FluxDataRev {
		/// <summary>
		/// The time it takes to complete a revolution in nanoseconds (time between two indexes). 
		/// </summary>
		public int revolutionTime;

		/// <summary>
		///  Index of the first flux for this revolution. This index is used
		///  to find the first flux in the FluxValue[] array of the FluxData class.
		/// </summary>
		public int firstFluxIndex;

        /// <summary>Gives the position of the index pulse inside the flux that contains it. The position 
        /// before the index signal is given in nanoseconds . Note that in the case of no
        /// flux area at the beginning of the track this value can be huge</summary>
        public int preIndexTime;

        /// <summary>
        /// The number of flux transitions for this revolution.
        /// </summary>
        public int fluxCount;
	}


	/// <summary>Contains the information related to flux transitions</summary>
	public class FluxData {
		/// <summary>
		/// Array of flux transitions recorded for this track (including all revolutions).
		/// The values are given directly in nanoseconds. 
		/// </summary>
		public int[] fluxValue;

		/// <summary>Total number of flux transitions for ALL revolutions of this track</summary>
		public int totalFluxCount;

		/// <summary>Value of the shortest flux transition recorded for this track in nanoseconds</summary>
		public int minFlux;

		/// <summary>Value of the largest flux transition recorded for this track in nanoseconds</summary>
		public int maxFlux;
	}


	/// <summary>Contains the decoded data. These data are decoded by the DPLL from the flux transitions</summary>
	public class DecodedData {
		/// <summary>The width of the bit cell computed by the DPLL</summary>
		public ushort[] bitWidth;

		/// <summary>The number of inspection windows to get to the flux.</summary>
		/// <remarks>This array is filled by mfmDecode() method. The array has the same size as the flux array.
		/// bitSpacing is the number of inspection windows to the next flux transition (i.e. it is equal to the
		/// number bit to shift in the DSR</remarks>
		public ushort[] bitSpacing;

		/// <summary>Value minimum value of the bit cell width as computed by the DPLL</summary>
		public int minBitWidth;

		/// <summary>Value maximum value of the bit cell width as computed by the DPLL</summary>
		public int maxBitWidth;
	}


	/// <summary>Stores histogram/entropy information</summary>
	public struct HistoData {
		/// <summary>Histogram Array: Flux transition repartition</summary>
		public int[] histogram;

		/// <summary>Computed entropy for the track</summary>
		public double entropy;

		/// <summary>The track seems to have data</summary>
		public int MFMBitSum;

		public bool HasMFM {
			get { return (MFMBitSum > 5000); }
		}

		public List<Tuple<int, int>> peaks;

		public bool Unformatted {
			get { return (entropy > 6.5); }
		}

		public bool Formatted {
			get { return (entropy < 6.5); }
		}
	}


	/// <summary>Defines the position of bits in a unsigned int</summary>
	public class Bit {
		/// <summary>bit position</summary>
		public const uint BIT_01 = 0x00000001;
		public const uint BIT_02 = 0x00000002;
		public const uint BIT_03 = 0x00000004;
		public const uint BIT_04 = 0x00000008;
		public const uint BIT_05 = 0x00000010;
		public const uint BIT_06 = 0x00000020;
		public const uint BIT_07 = 0x00000040;
		public const uint BIT_08 = 0x00000080;
		public const uint BIT_09 = 0x00000100;
		public const uint BIT_10 = 0x00000200;
		public const uint BIT_11 = 0x00000400;
		public const uint BIT_12 = 0x00000800;
		public const uint BIT_13 = 0x00001000;
		public const uint BIT_14 = 0x00002000;
		public const uint BIT_15 = 0x00004000;
		public const uint BIT_16 = 0x00008000;
		public const uint BIT_17 = 0x00010000;
		public const uint BIT_18 = 0x00020000;
		public const uint BIT_19 = 0x00040000;
		public const uint BIT_20 = 0x00080000;
		public const uint BIT_21 = 0x00100000;
		public const uint BIT_22 = 0x00200000;
		public const uint BIT_23 = 0x00400000;
		public const uint BIT_24 = 0x00800000;
		public const uint BIT_25 = 0x01000000;
		public const uint BIT_26 = 0x02000000;
		public const uint BIT_27 = 0x04000000;
		public const uint BIT_28 = 0x08000000;
		public const uint BIT_29 = 0x10000000;
		public const uint BIT_30 = 0x20000000;
		public const uint BIT_31 = 0x40000000;
		public const uint BIT_32 = 0x80000000;

		//#region flag_helper_functions
		///// <summary>Set the specified bit(s) in the flag</summary>
		///// <param name="flag">The flag to update</param>
		///// <param name="bits">An array of bits to set in the flag</param>
		//public static void setBits(ref uint flag, params uint[] bits) {
		//	for (int i = 0; i < bits.Length; i++)
		//		flag |= bits[i];
		//}

		///// <summary>Reset the specified bit(s) in the flag</summary>
		///// <param name="flag">The flag to update</param>
		///// <param name="bits">An array of bits to reset in the flag</param>		
		//public static void resetBits(ref uint flag, params uint[] bits) {
		//	for (int i = 0; i < bits.Length; i++)
		//		flag &= ~bits[i];
		//}

		///// <summary>Test the specified bit(s) in the flag</summary>
		///// <param name="flag">The flag to test</param>
		///// <param name="bits">An array of bits to test in the flag</param>
		///// <returns>True if any of the tested bits is set</returns>
		//public static bool testBits(uint flag, params uint[] bits) {
		//	bool result = false;
		//	for (int i = 0; i < bits.Length; i++)
		//		result |= ((flag & bits[i]) != 0);
		//	return result;
		//}
		//#endregion flag_helper_functions
	}

	[Flags]
	public enum ExportFlags : uint {
		None = 0,
		FORCE_DS = Bit.BIT_01,
		OPTIMIZE = Bit.BIT_02,
		DEBUG = Bit.BIT_03,
		SYNC_HEAD = Bit.BIT_04,
		FORCE_TRACK = Bit.BIT_05
	}

	/// <summary>
	/// Contains all global parameters and functions for AUFIT
	/// </summary>
	public class Global {
		// Button and Track Color
		public static Color ProtectedTrackColor = Colors.Orange;                    // Aufit.Properties.Settings.Default.ProtectedTrackColor;
		public static Color UnformattedTrackColor = Colors.SlateGray;               // Aufit.Properties.Settings.Default.UnformattedTrackColor;
        public static Color ShiftedTrackColor = Color.FromRgb(0x42, 0xED, 0x42);    // Aufit.Properties.Settings.Default.ShiftedTrackColor;
		public static Color NormalTrackColor = Colors.LimeGreen;                    // Aufit.Properties.Settings.Default.NormalTrackColor;
		public static Color DataOneIdTrackColor = Colors.Blue;                      // Aufit.Properties.Settings.Default.DataOneIdTrack;
		public static Color DataTrackColor = Colors.LightBlue;                      // Aufit.Properties.Settings.Default.DataTrack;

		// Sector Color
		public static Color IGapColor = Colors.LightGreen;
		public static Color ShortSectorColor = Colors.Aqua;
		public static Color BitRateSectorColor = Colors.Purple;
		public static Color FuzzySectorColor = Colors.Orange;
		public static Color LongSectorColor = Colors.Blue;
		public static Color NormalSectorColor = Colors.DeepSkyBlue;
		public static Color IdColor = Colors.Yellow;
		public static Color NoIdDataColor = Colors.Yellow;

		private static DiskTrack[,] _disk = new DiskTrack[85, 2];


		/// <summary>
		/// Scale used to reduce the size of the histogram arrays
		/// </summary>
		public const int HISTO_SCALE = 50;

		/// <summary>Contains information about a track of the floppy disk</summary>
		public struct DiskTrack {
			/// <summary>Pointer to the FluxData class for this track</summary>
			public FluxData fluxData;

			/// <summary>Array of FluxDataRev structure for this track. </summary>
			/// <remarks>There is one entry for each revolution</remarks>
			public FluxDataRev[] fluxDataRev;

			/// <summary>Pointer to the data decoded by the DPLL</summary>
			public DecodedData decodedData;

			/// <summary>Histogram & entropy data</summary>
			public HistoData histoData;

			/// <summary>Track Information</summary>
			public Track track;

			/// <summary>Sectors Information</summary>
			public List<Sector> sectors;

			/// <summary>Image has shifted track and some data are missing</summary>
			/// <remarks>This happen on the last revolution</remarks>
			public bool hasIncompleteData;

			/// <summary>Image some gap information is missing</summary>
			public bool hasIncompleteGap;

			public List<IAM>[] iams;

		}


		/// <summary>This function is used to re-initialized all disk/track structure. </summary>
		/// <remarks>Should be called each time a new FD is read. Hopefully should also free 
		/// all memory from previous read.</remarks>
		public static void resetDisk() {
			_disk = new DiskTrack[85, 2];
		}


		/// <summary>
		/// This function is used to get the index of the first flux of a track in the FluxValue buffer for a specific revolution
		/// </summary>
		/// <param name="track">The track we want to use</param>
		/// <param name="side">The side we want to use.</param>
		/// <param name="rev">The revolution we want to use</param>
		/// <returns>The index of the first flux in the buffer</returns>
		public static int firstIndex(int track, int side, int rev) {
			return Disk[track, side].fluxDataRev[rev].firstFluxIndex;
		}


		/// <summary>
		/// This function is used to get the position of the last flux of a track in the FluxValue buffer for a specific revolution
		/// </summary>
		/// <param name="track">The track we want to use</param>
		/// <param name="side">The side we want to use.</param>
		/// <param name="rev">The revolution we want to use</param>
		/// <returns>The index of the last flux in the buffer</returns>
		public static int lastIndex(int track, int side, int rev) {
			return (Disk[track, side].fluxDataRev[rev].firstFluxIndex + Disk[track, side].fluxDataRev[rev].fluxCount);
		}


		/// <summary>
		/// A two dimensions array that store TrackData structures for a complete disk.
		/// There are 85 Tracks * 2 Sides (170) TrackData structures.
		/// </summary>
		public static DiskTrack[,] Disk {
			get { return _disk; }
			set { _disk = value; }
		}


		/// <summary>Decodes the MFM Flux transition into clock/data bits</summary>
		/// <remarks> Fill packed structure and store useful information</remarks>
		public static void mfmDecode(int track, int side) {
			// if already there nothing to do
			// nothing to process
			if (Global.Disk[track, side].fluxData == null) return;
			if (Global.Disk[track, side].decodedData != null) return;

			int minBitWidth = Int32.MaxValue;
			int maxBitWidth = 0;
			int currentTime = 0;

			Global.Disk[track, side].decodedData = new DecodedData();
			int decodedCount = Global.Disk[track, side].fluxData.totalFluxCount + 1;
			Global.Disk[track, side].decodedData.bitWidth = new ushort[decodedCount];
			Global.Disk[track, side].decodedData.bitSpacing = new ushort[decodedCount];

			DPLL dpll = new DPLL();
			for (int index = 0; index < Global.Disk[track, side].fluxData.totalFluxCount; index++) {
				int flux = Global.Disk[track, side].fluxData.fluxValue[index];
				currentTime += flux;
				int bitSpacing = dpll.bitSpacing(currentTime);
				Global.Disk[track, side].decodedData.bitSpacing[index] = (ushort)bitSpacing;

				int bitWidth = dpll.BitWidth;
				Global.Disk[track, side].decodedData.bitWidth[index] = (ushort)bitWidth;
				if (bitWidth > maxBitWidth) maxBitWidth = bitWidth;
				if (bitWidth < minBitWidth) minBitWidth = bitWidth;
			}   // decode MFM data from flux

			Global.Disk[track, side].decodedData.minBitWidth = minBitWidth;
			Global.Disk[track, side].decodedData.maxBitWidth = maxBitWidth;
		}


		/// <summary>This function fills the histogram array, computes the entropy and
		/// checks if MFM bits are found for a complete track. It displays information
		/// on the provided TextBox.
		/// <param name="track">We compute the histogram for this track</param>
		/// <param name="side">We compute the histogram for this side</param>
		/// <param name="rev">We compute the histogram for this revolution</param>
		/// <param name="text">The TextBox where the information are written</param>
		/// <returns>the computed entropy</returns>
		public static bool computeTrackHisto(int track, int side, int rev, TextBox text) {
			if (Global.Disk[track, side].fluxDataRev == null) return true;

			int first = Disk[track, side].fluxDataRev[rev].firstFluxIndex;
			int last = (Disk[track, side].fluxDataRev[rev].firstFluxIndex + Disk[track, side].fluxDataRev[rev].fluxCount);
			int[] fluxSegment = Disk[track, side].fluxData.fluxValue.Skip(first).Take(last - first).ToArray();
			HistoData h = computeSegmentHisto(fluxSegment);
			Global.Disk[track, side].histoData = h;

			// log information
			text.AppendText(String.Format("Track {0:D2}.{1} Rev {2}", track, side, rev + 1));
			text.AppendText(String.Format(" * {0} * (Entropy {1:F2})",
				(h.Unformatted) ? "Unformatted track" : "Normal track", h.entropy));
			text.AppendText(String.Format(" - Fluxes {0}", last - first));
			text.AppendText(String.Format(" - Histogram size {0} ({1} ns) {2} ({3})\n",
				h.histogram.Length, (h.histogram.Length - 1) * HISTO_SCALE, h.HasMFM ? "HasBits" : "NoBits", h.MFMBitSum));
			text.ScrollToEnd();
			return h.Formatted;
		}


		/// <summary>This function fills the histogram array, computes the entropy and
		/// checks if MFM bits are found for a segment of flux transitions.
		/// <param name="fluxArray">The array of flux</param>
		/// <returns>A HistoData object with computed values</returns>
		public static HistoData computeSegmentHisto(int[] fluxSegment) {
			const int THRESHOLD = 2;
			int[] tmpHisto = null;
			int sizeBeforeTrim = 0;
			int fluxCount = 0;
			HistoData h = new HistoData();
			h.entropy = 0;

			sizeBeforeTrim = (fluxSegment.Max() / HISTO_SCALE) + 2;
			tmpHisto = new int[sizeBeforeTrim];
			for (int i = 0; i < fluxSegment.Length; i++) {
				int flux = fluxSegment[i] / HISTO_SCALE;
				tmpHisto[flux]++;
				fluxCount++;
			}

			// we trim the array size to keep only significant entries. This is useful as
			// we may end up with a huge array if track has Non Flux areas. 
			// Computations for Bezier curves takes long time on large array. 
			int histoSize = sizeBeforeTrim;
			for (int i = sizeBeforeTrim - 1; i >= 0; i--) {
				if (tmpHisto[i] >= THRESHOLD) break;
				else histoSize--;
			}
			h.histogram = new int[histoSize];
			for (int i = 0; i < histoSize; i++)
				h.histogram[i] = tmpHisto[i];

			for (int i = 0; i < histoSize; i++)
				if (h.histogram[i] > 0) {
					//if (h.histogram[i] > 1000) {
					//	if ((i > (3400 / HISTO_SCALE)) && (i < (8600 / HISTO_SCALE)))
					//		h.hasMFM = true;
					//}   // large number of flux transitions
					double p = h.histogram[i] / (double)(fluxCount);
					h.entropy += p * Math.Log(p, 2);
				}
			h.entropy = -h.entropy;
			if (h.Formatted) {
				h.peaks = getPeaks(h.histogram, HISTO_SCALE);
				int bitSum = 0;
				foreach (Tuple<int, int> peak in h.peaks) {
					if (fluxInRange(peak.Item1, 2000, 500))
						bitSum += peak.Item2;
				}
				h.MFMBitSum = bitSum;
			}

			return h;
		}


		// http://stackoverflow.com/questions/23823860/finding-peak-intervals-from-column-data
		public static List<Tuple<int, int>> getPeaks(int[] values, int scale) {
			List<Tuple<int, int>> results = new List<Tuple<int, int>>();

			List<int> curInterval = new List<int>();

			bool decreasing = false;
			for (int i = 0; i < values.Length; i++) {
				if (curInterval.Count > 0) {
					if (values[i] < curInterval.Last() && !decreasing) {
						if (curInterval.Last() > 2)
							results.Add(new Tuple<int, int>((i - 1) * scale, curInterval.Last()));
						curInterval.Clear();
						decreasing = true;
					}
					else if (values[i] >= curInterval.Last() && decreasing) {
						decreasing = false;
					}
				}
				curInterval.Add(values[i]);
			}
			return results;
		}

		/// <summary>Return the actual size of the data field of a sector</summary>
		/// <param name="size">encoded size of the sector</param>
		/// <returns>"decoded size" of the data segment</returns>
		/// <remarks>In the ID field the size of the sector is coded 0=128, 1=256, 2=512, 3=1024
		/// The function returns the decoded value. Only the last 2 bytes are used by
		/// The WD1772 therefore all other bits are ignored.</remarks>
		public static int sectSize(byte size) {
			int s;
			s = 128 << (size & 0x03);
			return s;
		}


		/// <summary>Ask where to save a .png image</summary>
		/// <param name="target">Visual to save as png image</param>
		/// <param name="scale">!the drawing will be scaled using this value
		/// (default to 1.0 if not provided)</param>
		public static void saveImage(Visual target, double scale, string name, bool ask) {
			SaveFileDialog sfd = new SaveFileDialog();
			sfd.InitialDirectory = Aufit.MainWindow.DirectoryName;
			//sfd.FileName = Aufit.MainWindow.ImageName + name;
			sfd.FileName = name;
			if (ask) {
				sfd.RestoreDirectory = true;
				sfd.Filter = "Png file|*.png|All Files|*.*";
				if (sfd.ShowDialog() == true)
					savePngFromVisual(target, sfd.FileName, scale);
			}
			else
				savePngFromVisual(target, sfd.FileName, scale);
		}


		// http://mcleodsean.wordpress.com/2008/10/07/bitmap-snapshots-of-wpf-visuals/
		/// <summary>Take a visual and save it as a png image</summary>
		/// <param name="target">The visual</param>
		/// <param name="filename">Where to save</param>
		private static void savePngFromVisual(Visual target, string filename, double scale) {
			if (target == null) return;

			Rect bounds = VisualTreeHelper.GetDescendantBounds(target);
			DrawingVisual dv = new DrawingVisual();
			using (DrawingContext dc = dv.RenderOpen()) {
				dv.Transform = new ScaleTransform(scale, scale);
				VisualBrush vb = new VisualBrush(target);
				dc.DrawRectangle(vb, null, new Rect(new Point(), bounds.Size));
			}

			RenderTargetBitmap rtb = new RenderTargetBitmap((int)(scale * bounds.Width), (int)(scale * bounds.Height), 96, 96, PixelFormats.Pbgra32);
			rtb.Render(dv);

			PngBitmapEncoder png = new PngBitmapEncoder();
			png.Frames.Add(BitmapFrame.Create(rtb));

			using (Stream stm = File.Create(filename)) {
				png.Save(stm);
			}
		}


		/// <summary>This is an asynchronous version of decodeAndProcessTrack()</summary>
		/// <param name="track">The track to read and decode</param>
		/// <param name="side">The side to read and decode</param>
		/// <param name="rev">The revolution to read and decode</param>
		/// <returns>A pointer to the TrackContent structure</returns>
		public static Task<List<Sector>> decodeAndProcessTrackAsync(int track, int side, int rev) {
			return Task<List<Sector>>.Run(() => decodeAndProcessTrack(track, side, rev));
		}


		/// <summary>Decode the flux transitions and do all FDC read commands</summary>
		/// <param name="track">The track to read and decode</param>
		/// <param name="side">The side to read and decode</param>
		/// <param name="rev">The revolution to read and decode</param>
		/// <returns>A pointer to the list of sectors</returns>
		public static List<Sector> decodeAndProcessTrack(int track, int side, int rev) {
			if (Global.Disk[track, side].fluxData == null) return null;
			List<Sector> sectors = Global.Disk[track, side].sectors;
			if (sectors != null) return sectors;
			Global.mfmDecode(track, side);
			FDC.processTrack(track, side, rev);
			sectors = Global.Disk[track, side].sectors;
			return sectors;
		}


		public static bool fluxInRange(int flux, int bitWidth, int range) {
			int upper_high = 4 * bitWidth + range;
			int upper_low = 4 * bitWidth - range;
			int middle_high = 3 * bitWidth + range;
			int middle_low = 3 * bitWidth - range;
			int lower_high = 2 * bitWidth + range;
			int lower_low = 2 * bitWidth - range;
			bool inRange = (((flux < upper_high) && (flux > upper_low)) ||
							((flux < middle_high) && (flux > middle_low)) ||
							((flux < lower_high) && (flux > lower_low)));
			return inRange;
		}
	}   // class Global


	/// <summary>Information about a post GAP (inter sector gap)</summary>
	public class GapInfoOld {
			/// <summary></summary>
			public int spliceTime;

			/// <summary>Number of 4E gap leading char</summary>
			public int count4E;

			/// <summary>Number of 00 gap trailing char</summary>
			public int count00;

			/// <summary>True if char other than 4E/00 found</summary>
			public bool badDataFound;

			public void reset() {
				badDataFound = false;
				count00 = 0;
				count4E = 0;
				spliceTime = 0;
			}


		/// <summary>Fills GapInfo structure by scanning a  GAP field</summary>
		/// <param name="gap">The gap field</param>
		/// <param name="gi">The GapInfo structure</param>
		/// <returns>True if bad gap values found</returns>
		public static bool scanGap(GapField gap, GapInfoOld gi) {
			gi.reset();
			if (gap == null) return false;
			bool in4E = false;

			for (int i = gap.buffer.DataByte.Count - 1; i >= 0; i--) {
				byte b = gap.buffer.DataByte[i];
				if (!in4E && (isShifted00(b) || (i == (gap.buffer.DataByte.Count - 1)))) {
					gi.count00++;
					if (b != 0x00) {
						gi.spliceTime = gap.buffer.StartTime[i];
						gi.badDataFound = true;
					}
				}   // 00
				else {
					in4E = true;
					gi.count4E++;
					if ((b != 0x4E) && (i != 0)) {
						gi.spliceTime = gap.buffer.StartTime[i];
						gi.badDataFound = true;
					}
				}   // not 00
			}   // all bytes
			return gi.badDataFound;
		}


		private bool checkGAP_OLD(GapField gap, GapInfoOld gi, bool skipFirst) {
			bool in4E = true;
			int setAside = 0;
			gi.reset();
			if (gap == null) return false;

			for (int i = skipFirst ? 1 : 0; i < gap.buffer.DataByte.Count; i++) {
				byte b = gap.buffer.DataByte[i];
				if (in4E) {
					if (isShifted00(b)) {
						if (b != 0x00) {
							if (!gi.badDataFound) {
								gi.spliceTime = gap.buffer.StartTime[i];
							}
							gi.badDataFound = true;
						}
						setAside++;
						if (setAside > 2) {
							gi.count00 = setAside;
							in4E = false;
						}
					}   // 00
					else {
						gi.count4E += setAside;
						gi.count4E++;
						setAside = 0;
						if (b != 0x4E) {
							if (!gi.badDataFound) {
								gi.spliceTime = gap.buffer.StartTime[i];
							}
							gi.badDataFound = true;
						}
					}   // not 00
				}   // in4E
				else {
					gi.count00++;
					if (b != 0x00) {
						if (!gi.badDataFound) {
							gi.spliceTime = gap.buffer.StartTime[i];
						}
						gi.badDataFound = true;
					}
				}   // !in4e
			}
			return gi.badDataFound;
		}


		/// <summary>
		/// Check if byte b is a possible MFM shifted value of 4E
		/// </summary>
		/// <param name="b">Byte value</param>
		/// <returns>True if byte b is a possible shifted value of 4E</returns>
		public static bool isShifted4E(byte b) {
			return ((b == 0x4E) || (b == 0x12) || (b == 0xC9) || (b == 0x24) || (b == 0x93) || (b == 0x48) || (b == 0x27) || (b == 0x90) ||
					(b == 0xE4) || (b == 0x21) || (b == 0x9C) || (b == 0x42) || (b == 0x39) || (b == 0x84) || (b == 0x72) || (b == 0x09));
		}


		/// <summary>
		/// Check if byte b is a possible MFM shifted value of 00
		/// </summary>
		/// <param name="b">Byte to test</param>
		/// <returns>True if byte b is a possible shifted value of 4E</returns>
		public static bool isShifted00(byte b) {
			return ((b == 0x00) || (b == 0xFF));
		}
	}	// class gapInfo

}   // Aufit name space
