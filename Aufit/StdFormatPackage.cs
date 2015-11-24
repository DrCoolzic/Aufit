using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Diagnostics;

using GlobalPackage;
using WD1772Package;
using Aufit;

namespace StdFormatPackage {
	public enum FileType { ST, MSA, DIM }

	class StdWriter {
		TextBox _saveImageBox;
		bool _debug;

		/// <summary>Constructor</summary>
		/// <param name="tb">TextBox used to display write information (debug information)</param>
		/// <remarks>TextBox parameter: where to display info</remarks>
		public StdWriter(TextBox tb) {
			_saveImageBox = tb;
		}


		/// <summary>Write the Pasti file</summary>
		/// <param name="fileName">Name of the Pasti file to write</param>
		/// <param name="forceDs">When set to true this force writing Pasti 
		/// information for a Double sided floppy disk</param>
		/// <returns>True if succeeds; false if fails</returns>
		public bool writeStd(string fileName, uint flags, FileType type) {
			FileStream fs;
			_debug = ((flags & Global.DEBUG) != 0) ? true : false;

			// encode file into buffer
			List<byte> sbuf = new List<byte>();
			switch (type) {
				case FileType.ST:
					encodeST(sbuf, flags);
					break;
				case FileType.MSA:
					encodeMSA(sbuf, flags);
					break;
			}

			try {
				fs = new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.Read);
			}
			catch (Exception exc) {
				Console.WriteLine("Error: {0}", exc.Message);
				return false;
			}
			fs.Write(sbuf.ToArray(), 0, sbuf.Count);
			fs.Close();
			return true;		
		}


		/// <summary>
		/// Encode the Floppy content into a buffer using Pasti file structure
		/// </summary>
		/// <param name="buffer">Contains the data to be written to Pasti file</param>
		/// <returns>Always returns true!</returns>
		private void encodeST(List<byte> buffer, uint  flags) {
			int start = 0;
			byte trackCount = 0;
			int sideCount = 1;	// single sided

			bool forceDs = ((flags & Global.FORCE_DS) != 0) ? true : false;
			bool optimize = ((flags & Global.OPTIMIZE) != 0) ? true : false;

			// check for double sided - we check that we have some sectors on side 1
			if (forceDs) sideCount = 2;
			else {
				for (int track = 0; track < 84; track++)
					if ((Global.Disk[track, 1].track != null) && (Global.Disk[track, 1].sectors != null) && (Global.Disk[track, 1].sectors.Count != 0)) {
						sideCount = 2;	// found at least one sector
						break;
					}
			}

			// number of tracks
			for (int side = 0; side < sideCount; side++)
				for (int track = 0; track < 84; track++)
					if ((Global.Disk[track, side].track != null) && (Global.Disk[track, side].sectors.Count > 0))
						trackCount++;
			_saveImageBox.AppendText(String.Format("{0} Sided Diskette with a total of {1} tracks\n", (sideCount == 1) ? "Single" : "Double", trackCount));
										
			// Write all track records
			for (int track = 0; track < 84; track++) {
				for (int side = 0; side < sideCount; side++) {
					Global.DiskTrack dt = Global.Disk[track, side];
					if ((dt.track == null) || (Global.Disk[track, side].sectors.Count == 0))
						continue;
					if (_debug) 
						_saveImageBox.AppendText(String.Format("Processing T{0:D2}.{1}\n", track, side));
							
					// write all sectors data
					int sectorCount = dt.sectors.Count;
					for (int sect = 0; sect < sectorCount; sect++) {
						Sector s = FDC.findSector(track, side, sect + 1, -1);
						Debug.Assert(s != null, "Did not found the sector?");
						start = buffer.Count;
						for (int i = 4; i < s.data.buffer.DataByte.Count - 2; i++) {
							if (s.recovered_data != null)
								buffer.Add(s.recovered_data.buffer.DataByte[i]);
							else
								buffer.Add(s.data.buffer.DataByte[i]);
						}
						if (_debug) 
							_saveImageBox.AppendText(String.Format("      Writing Sector {0} {1} bytes ({2}-{3}) {4}\n",
							s.id.Sector, s.data.buffer.DataByte.Count - 6, start, buffer.Count - 1,
							(s.recovered_data != null) ? "recovered" : ""));
						else if (s.recovered_data != null)
							_saveImageBox.AppendText(String.Format("T{0:D2}.{1:D1} recovered Sector {2}\n", 
								track, side, s.id.Sector));			
					}	// sectors
				} // for all tracks
			}	// for all side
		}	// encode ST


		private ushort msaCompress(List<byte> inputTrack, List<byte> outputTrack) {
			byte lastByte;
			int sameCount;
			int inputPos;

			lastByte = inputTrack[0];
			sameCount = 1;
			inputPos = 1;
			do {
				if( lastByte != inputTrack[inputPos] ) {
					if(sameCount >= 4) { // RLE
						outputTrack.Add(0xE5);
						outputTrack.Add(lastByte);
						outputTrack.Add((byte)(sameCount >> 8));
						outputTrack.Add((byte)sameCount);
					}
					else {
						if(lastByte == 0xE5) {
							outputTrack.Add(0xE5);
							outputTrack.Add(lastByte);	// = E5
							outputTrack.Add((byte)(sameCount >> 8));
							outputTrack.Add((byte)sameCount);
						}
						else { // direct
							for(int k=0; k < sameCount; k++) {
								outputTrack.Add(lastByte);
							}
						}
					}	// less than 4
					sameCount = 1;
				}	// bytes differ
				else {
					sameCount++;
				}
				lastByte = inputTrack[inputPos];
				inputPos++;
			} while(inputPos < inputTrack.Count);

			// purge
			if(sameCount != 0) {
				if(sameCount >= 4) { // RLE
					outputTrack.Add(0xE5);
					outputTrack.Add(lastByte);
					outputTrack.Add((byte)(sameCount >> 8));
					outputTrack.Add((byte)sameCount);
				}
				else {
					if(lastByte == 0xE5) {
						outputTrack.Add(0xE5);
						outputTrack.Add(lastByte);	// = E5
						outputTrack.Add((byte)(sameCount >> 8));
						outputTrack.Add((byte)sameCount);
					}
					else { // direct
						for(int k=0; k < sameCount; k++) {
							outputTrack.Add(lastByte);
						}
					}
				}	// less than 4
			}
			return (ushort)outputTrack.Count;
		}


		private ushort fillTrackBuffer(List<Byte> outputTrack, int track, int side, bool optimize) {
			Global.DiskTrack dt = Global.Disk[track, side];
			int sectorCount = dt.sectors.Count;			
			ushort trackLength = (ushort)(512 * sectorCount);
			List<byte> trackBuffer;

			if (optimize)
				trackBuffer = new List<byte>();
			else
				trackBuffer = outputTrack;

			// process all sectors data
			for (int sect = 0; sect < sectorCount; sect++) {
				Sector s = FDC.findSector(track, side, sect + 1, -1);
				Debug.Assert(s != null, "Did not found the sector?");

				TrackBuffer data;
				if (s.recovered_data != null)
					data = s.recovered_data.buffer;
				else
					data = s.data.buffer;

				for (int i = 0; i < data.DataByte.Count; i++)
					trackBuffer.Add(data.DataByte[i]);

				if (_debug)
					_saveImageBox.AppendText(String.Format("   Writing Sector {0} {1} bytes {2}\n",
					s.id.Sector, data.DataByte.Count, (s.recovered_data != null) ? "recovered" : ""));
				else if (s.recovered_data != null)
					_saveImageBox.AppendText(String.Format("T{0:D2}.{1:D1} recovered Sector {2}\n",
						track, side, s.id.Sector));
			}	// sectors

			if (optimize) {
				trackLength = msaCompress(trackBuffer, outputTrack);
					if (_debug)
					_saveImageBox.AppendText(String.Format("   Track data optimized from {0} to {1} bytes\n",
						trackBuffer.Count, outputTrack.Count));
				if (trackLength >= trackBuffer.Count) { // RLE bigger restore original 
					outputTrack.Clear();
					foreach (byte b in trackBuffer)
						outputTrack.Add(b);
					trackLength = (ushort)outputTrack.Count;
				}
			}
			return trackLength;
		}


		private void encodeMSA(List<byte> buffer, uint  flags) {
			ushort trackCount = 0;
			ushort sideCount = 1;		// single sided
			ushort sectorPerTrack = 0;

			bool forceDs = ((flags & Global.FORCE_DS) != 0) ? true : false;
			bool optimize = ((flags & Global.OPTIMIZE) != 0) ? true : false;

			// check for double sided - we check that we have some sectors on side 1
			if (forceDs) sideCount = 2;
			else {
				for (int track = 0; track < 84; track++)
					if ((Global.Disk[track, 1].track != null) && (Global.Disk[track, 1].sectors != null) && (Global.Disk[track, 1].sectors.Count != 0)) {
						sideCount = 2;	// found at least one sector
						break;
					}
			}

			// number of tracks
			for (int track = 0; track < 84; track++)
				if ((Global.Disk[track, 0].track != null) && (Global.Disk[track, 0].sectors.Count > 0))
					trackCount++;
			sectorPerTrack = (ushort)Global.Disk[0, 0].sectors.Count;	// assume already checked MSA OK
			_saveImageBox.AppendText(String.Format("{0} Sided Diskette - {1} tracks each with {2} sectors\n", (sideCount == 0) ? "Single" : "Double", trackCount, sectorPerTrack));

			// write MSA Header
			writeInt16BigE(buffer, 0x0E0F);						// MSA marker
			writeInt16BigE(buffer, sectorPerTrack);				// number of sectors / track
			writeInt16BigE(buffer, (ushort)(sideCount - 1));	// number of side
			writeInt16BigE(buffer, 0);							// start track;
			writeInt16BigE(buffer, (ushort)(trackCount - 1));	// number of track;
										
			// Write all track records
			for (int track = 0; track < 84; track++) {
				for (int side = 0; side < sideCount; side++) {
					Global.DiskTrack dt = Global.Disk[track, side];
					if ((dt.track == null) || (dt.sectors.Count == 0))
						continue;
					if (_debug)
						_saveImageBox.AppendText(String.Format("Processing T{0:D2}.{1}\n", track, side));
					List<byte> trackBuffer = new List<byte>();
					ushort size = fillTrackBuffer(trackBuffer, track, side, optimize);
					writeInt16BigE(buffer, size);
					foreach (byte b in trackBuffer)
						writeByte(buffer, b);
					if (_debug)
						_saveImageBox.AppendText(String.Format("   Writing T{0:D2}.{1} data {2} bytes\n", track, side, size));
				} // for all tracks
			}	// for all side
		}	// encode ST


		//private static void writeInt32(List<byte> buffer, uint val) {
		//	buffer.Add((byte)(val & 0xFF));
		//	buffer.Add((byte)((val >> 8) & 0xFF));
		//	buffer.Add((byte)((val >> 16) & 0xFF));
		//	buffer.Add((byte)((val >> 24) & 0xFF));
		//}


		private static void writeInt16BigE(List<byte> buffer, ushort val) {
			buffer.Add((byte)(val >> 8));
			buffer.Add((byte)(val & 0xFF));
		}

		private static void writeByte(List<byte> buffer, byte val) {
			buffer.Add(val);
		}


	}
}
