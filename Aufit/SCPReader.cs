using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aufit {
	public class FileHeader {
		public string identifier = "";
		public int version;
		public int revision;
		public int diskType;
		public int revCount;
		public int startTrack;
		public int endTrack;
		public int imageFlags;
		public int bitcellWidth;
		public int checksum;
		public int[] TrackHeader;
		public string timeStamp;
	}

	public class RevInfo {
		public int indexTime;
		public int fluxCount;
		public int fluxOffset;
	}

	public class TrackInfo {
		public string identifier = "";
		public int track;
		public int side;
		public RevInfo[] revInfo;
		public int[] fluxValues;
		public long checksum;
	}

	public enum SCPStatus : int {
		/// <summary>Status OK - The file was read and decoded successfully</summary>
		sdsOk = 0,
		/// <summary>Missing Data at end of Stream file</summary>
		sdsMissingData,
		/// <summary>Invalid  header code</summary>
		sdsInvalidHeader,
		/// <summary>Stream position must match the encoder position</summary>
		sdsWrongPosition,
		/// <summary>Hardware buffering problem</summary>
		sdsDevBuffer,
		/// <summary>Hardware did not detect FD Index</summary>
		sdsDevIndex,
		/// <summary>Unknown hardware error</summary>
		sdsTransfer,
		/// <summary>Unknown OOB header</summary>
		sdsInvalidOOB,
		/// <summary>Stream missing end of file</summary>
		sdsMissingEnd,
		/// <summary>No index was found or stream is shorter than the reference position of last index</summary>
		sdsIndexReference,
		/// <summary>An Index is missing</summary>
		sdsMissingIndex,
		/// <summary>Error opening/reading the stream file</summary>
		sdsReadError
	}	
	
	public class SCPReader {

		//private IndexInfo[] _indexArray;
		/// <summary>Number of IndexInfo structure in the _indexArray</summary>
		//private int _indexCount;
		///// <summary>Array of FluxValues (flux transition values) in sample clocks</summary>
		//private int[] _fluxValues;

		///// <summary>Number of flux transitions recorded in the FluxValue array</summary>
		//private int _fluxCount;


		///// <summary>minimum flux value found during parsing</summary>
		//private int _fluxMin;
		///// <summary>maximum flux value found during parsing</summary>
		//private int _fluxMax;

		/// <summary> Value of the Sample Clock</summary>
		private double _sckValue;

		private int _fluxMin = 10000000;
		private int _fluxMax = 0;

		/// <summary>Reads and decodes the KF Stream file specified</summary>	
		/// <param name="fileName">Name of the stream file to read and decode</param>
		/// <returns>A status code of type <paramref name="StreamStatus"/> to indicate if the 
		/// file was read and parsed correctly.</returns>
		/// <see cref="StreamStatus"/>
		/// <remarks>
		/// This is the main function of the library. It parses the stream file passed as an
		/// argument and fills in 4 structures:
		/// - An array of flux value that can be retrieved with @ref FluxValues and @ref FluxCount properties
		/// - An IndexInfo array that can be retrieved with @ref Indexes and @ref IndexCount properties
		/// - An Info string that can be retrieved with @ref InfoString property
		/// - Statistic structure that can be retrieved with @ref StreamStat property
		/// .
		///
		/// Internally this function calls the private functions parseStream(), 
		/// indexAnalysis(), and fillStreamStat(). The function update the sck 
		/// and ick default clock values if these values were passed in an Info block from KF 
		/// device (FW 2.0+). In all cases the sck and ick can be retrieved with 
		/// @ref SampleClock and @ref IndexClock properties.
		/// </remarks>
		public SCPStatus readSCP(string fileName) {
			FileStream fs;
			try {
				fs = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read);
			}
			catch (Exception exc) {
				Console.WriteLine("Error: {0}", exc.Message);
				return SCPStatus.sdsReadError;
			}

			// read file into buffer
			byte[] sbuf = new byte[fs.Length];
			int bytes = fs.Read(sbuf, 0, (int)fs.Length);
			Debug.Assert(bytes == fs.Length);	// should be same
			fs.Close();

			// we parse the file
			SCPStatus status = decodeScp(sbuf);


			return status;
		}

		private SCPStatus decodeScp(byte[] buffer) {
			FileHeader file = new FileHeader();
			int bufferIndex = 0;

			// Read File Header
			file.identifier += ((char)buffer[bufferIndex++]).ToString();	// 0x00
			file.identifier += ((char)buffer[bufferIndex++]).ToString();	// 0x01
			file.identifier += ((char)buffer[bufferIndex++]).ToString();	// 0x02
			if (file.identifier != "SCP")
				return SCPStatus.sdsInvalidHeader;
			file.version = buffer[bufferIndex] >> 4;						// 0x03
			file.revision = buffer[bufferIndex++] & 0xF;
			file.diskType = buffer[bufferIndex++];							// 0x04
			file.revCount = buffer[bufferIndex++];							// 0x05
			file.startTrack = buffer[bufferIndex++];						// 0x06
			file.endTrack = buffer[bufferIndex++];							// 0x07
			file.imageFlags = buffer[bufferIndex++];						// 0x08
			file.bitcellWidth = buffer[bufferIndex++];						// 0x09
			bufferIndex++;													// 0x0A reserved
			bufferIndex++;													// 0x0B reserved
			file.checksum = int32Extract(buffer, bufferIndex);				// 0x0C-0x0F
			bufferIndex += 4;
			
			file.TrackHeader = new int[164];
			for (int tnum = 0; tnum < 164; tnum++) {
				file.TrackHeader[tnum] = int32Extract(buffer, bufferIndex);
				bufferIndex += 4;
				if (file.TrackHeader[tnum] != 0)
					// TODO if
					readTrack(buffer, tnum, file.TrackHeader[tnum]);
			}

			// TODO check checksum
			// TODO read timestamp

			
			return SCPStatus.sdsOk;
		}

		private SCPStatus readTrack(byte[] buffer, int trackComp, int trackHeaderPosition) {
			int track = trackComp / 2;
			int side = trackComp % 2;
			_fluxMin = 100000;
			_fluxMax = 0;

			TrackInfo ti = new TrackInfo();
			int bufferIndex = trackHeaderPosition;

			ti.identifier += ((char)buffer[bufferIndex++]).ToString();	// 0x00
			ti.identifier += ((char)buffer[bufferIndex++]).ToString();	// 0x01
			ti.identifier += ((char)buffer[bufferIndex++]).ToString();	// 0x02
			if (ti.identifier != "TRK")
				return SCPStatus.sdsInvalidHeader;
			ti.track = buffer[bufferIndex] / 2;							// 0x03
			ti.side = buffer[bufferIndex++] % 2;
			ti.revInfo = new RevInfo[5];

			int fluxSize = 0;
			for (int i = 0; i < 5; i++) {
				ti.revInfo[i] = new RevInfo();
				ti.revInfo[i].indexTime = int32Extract(buffer, bufferIndex);
				bufferIndex += 4;
				ti.revInfo[i].fluxCount = int32Extract(buffer, bufferIndex);
				bufferIndex += 4;
				fluxSize += ti.revInfo[i].fluxCount;
				ti.revInfo[i].fluxOffset = int32Extract(buffer, bufferIndex);
				bufferIndex += 4;
			}

			ti.fluxValues = new int[fluxSize];

			int nbRev = 0;
			for (int i = 0; i < 5; i++) {
				if (ti.revInfo[i].indexTime != 0) {
					bufferIndex = ti.revInfo[i].fluxOffset + trackHeaderPosition;
					if (i > 0) ti.revInfo[i].fluxOffset = ti.revInfo[i - 1].fluxOffset + ti.revInfo[i - 1].fluxCount;
					else ti.revInfo[i].fluxOffset = 0;
					nbRev++;
					for (int j = 0; j < ti.revInfo[i].fluxCount; j++) {
						ti.fluxValues[j] = buffer[bufferIndex++] << 8;
						ti.fluxValues[j] += buffer[bufferIndex++];
						if (ti.fluxValues[j] > _fluxMax) _fluxMax = ti.fluxValues[j];
						if (ti.fluxValues[j] < _fluxMin) _fluxMin = ti.fluxValues[j];
					}
				}
			}
			ti.checksum = int32Extract(buffer, bufferIndex);

			Global.Rev = 1;
			Global.Disk[track, side].fluxData = new FluxData();
			Global.Disk[track, side].fluxData.fluxValue = ti.fluxValues;
			Global.Disk[track, side].fluxData.fluxCount = ti.fluxValues.Count();

			Global.SampleClock = 40000000.0;	// 1 / 25 ns
			Global.Disk[track, side].fluxData.fluxMax = _fluxMax;
			Global.Disk[track, side].fluxData.fluxMin = _fluxMin;

			Global.Disk[track, side].revData = new RevData[nbRev];
			for (int i = 0; i < nbRev; i++) {
				Global.Disk[track, side].revData[i].revolutionTime = ti.revInfo[i].indexTime;
				Global.Disk[track, side].revData[i].fluxCount = ti.revInfo[i].fluxCount;
				Global.Disk[track, side].revData[i].indexTimePosition = 0;
				Global.Disk[track, side].revData[i].fluxOffset = ti.revInfo[i].fluxOffset;
			}



			return SCPStatus.sdsOk;

		}



		/// <summary>
		/// Extract the next 4 bytes from the Stream File as a 32bits integer
		/// </summary>
		/// <param name="sbuf">buffer that contains the 4 bytes to extract (little-Indian ordering)</param>
		/// <param name="pos">Position of the first byte</param>
		/// <returns>The 32 bits integer</returns>
		/// <remarks>This is due to the poor casting capabilities of C#</remarks>
		private int int32Extract(byte[] sbuf, int pos) {
			return sbuf[pos] + (sbuf[pos + 1] << 8) + (sbuf[pos + 2] << 16) + (sbuf[pos + 3] << 24);
		}


	}
}
