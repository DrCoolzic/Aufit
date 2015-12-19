/*!
@file FDC.cs
 * 
<summary>Emulate WD1772 FDC behavior</summary>

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
using System.Diagnostics;

using GlobalPackage;
using Aufit;


namespace FDCPackage {
	/// <summary>Different phases when decoding a sector: Id, Post Id Gap, Data, Post Data Gap</summary>
	public enum Phase : byte {
		/// <summary>Id phase</summary>
		Id,
		/// <summary>Post Id Gap phase</summary>
		PostIdGap,
		/// <summary>Data phase</summary>
		Data,
		/// <summary>Post Data Gap phase</summary>
		PostDataGap
	}


	/// <summary>Iam (Index Address Mark) Type</summary>
	public enum IamType {
		/// <summary>IDAM Id IAM Type</summary>
		IDAM,
		/// <summary>DAM Data IAM Type</summary>
		DAM,
		/// <summary>Generic IAM Type (neither Id nor Data IAM)</summary>
		IAM
	}


	/// <summary>Store information about a block of data</summary>
	public class BlockInfo {
		/// <summary>First byte of the block</summary> 
		public int startByte;
		/// <summary>First bit of the block</summary> 
		public int startBit;
		/// <summary>First IAM bit of the block</summary> 
		public int startBitIAM;
		/// <summary>Number of bits (clock + data) in the block</summary>
		public int bitCount;
		/// <summary>Starting Time of the block in nanoseconds from index</summary>
		public int startTime;
		/// <summary>Starting Time of the IAM in the block in nanoseconds from index</summary>
		public int startTimeIAM;
		/// <summary>Length of the Block in nanoseconds </summary>
		public int timeLength;
		/// <summary>first flux index of the block</summary>
		public int firstIndex;
		/// <summary>last flux index of the block</summary>
		public int lastIndex;
		/// <summary>Computed CRC value of ID or DATA block (should be 0)</summary>
		public ushort crc;
	}


	/// <summary>Store information about the ID Field of a Sector</summary>
	public class IdField {
		/// <summary>Property that returns the Index Address Mark</summary> 
		public byte IDAM {
			get { return buffer.DataByte.Count > 3 ? buffer.DataByte[3] : (byte)0; }
		}

		/// <summary>Returns the track number</summary> 
		public byte Track {
			get { return buffer.DataByte.Count > 4 ? buffer.DataByte[4] : (byte)0; }
		}

		/// <summary>Returns the Head/Side number</summary> 
		public byte Head {
			get { return buffer.DataByte.Count > 5 ? buffer.DataByte[5] : (byte)0; }
		}

		/// <summary>Returns the sector number</summary> 
		public byte Sector {
			get { return buffer.DataByte.Count > 6 ? buffer.DataByte[6] : (byte)0; ; }
		}

		/// <summary>Returns the encoded data segment length</summary> 
		public byte Length {
			get { return buffer.DataByte.Count > 7 ? buffer.DataByte[7] : (byte)0; }
		}

		/// <summary>Returns the CRC</summary> 
		public ushort CRC {
			get { return buffer.DataByte.Count > 9 ? (ushort)((int)(buffer.DataByte[8]) + (int)(buffer.DataByte[9] << 8)) : (ushort)0; }
		}

		/// <summary>Buffer for the ID Field content</summary>
		public TrackBuffer buffer = new TrackBuffer();
		/// <summary> Information about the Id field</summary>
		public BlockInfo info = new BlockInfo();
		/// <summary>The Post Id Gap field</summary>
		public GapField gap;
	}


	/// <summary>Store information about the DATA Field of a Sector</summary>
	public class DataField {
		/// <summary>Returns the Data Address Mark</summary> 
		public byte DAM {
			get { return (buffer.DataByte.Count > 3) ? buffer.DataByte[3] : (byte)0; }
		}

		/// <summary>Returns the CRC Value (last two bytes of Data buffer)</summary> 
		public ushort CRC {
			get {
				return (buffer.DataByte.Count > 3) ? (ushort)((int)(buffer.DataByte[buffer.DataByte.Count - 2]) +
					(int)(buffer.DataByte[buffer.DataByte.Count - 1] << 8)) : (ushort)0;
			}
		}

		/// <summary>Buffer for the DATA field content</summary>
		/// <remarks>The buffer contains the 2 Sync bytes + the DAM + the Data + the CRC. 
		/// Therefore when writing a sector some of this data might need to be removed</remarks>
		public TrackBuffer buffer = new TrackBuffer();

		/// <summary>Array of bytes to store fuzzy mask</summary>
		/// <remarks>Same size as the buufer that contains the data. Usually the 3 sync marks are ignored</remarks>
		public byte[] fuzzy;

		/// <summary>Information about the data field </summary>
		public BlockInfo info = new BlockInfo();

		/// <summary>The Post Data Gap field</summary>
		public GapField gap;

		/// <summary>Store the real number of bytes if sector is truncated</summary>
		public int truncByteCount;
	}


	/// <summary>The Type of Splice</summary>
	public enum SpliceType {
		/// <summary>Sector Splice</summary>
		Sector,
		/// <summary>Track Splice</summary>
		Track,
		/// <summary>Not realy a splice! Preamble sequence of $00</summary>
		Seq00
	}


	/// <summary>Store Information about the a splice</summary>
	public class Splice {
		/// <summary>The type of splice</summary>
		public SpliceType type;
		/// <summary>The time of the splice</summary>
		public int time;
		/// <summary>The index of the splice in the flux array</summary>
		public int bit;
	}


	/// <summary>Store information about GAP fields of a sector</summary>
	public class GapField {
		/// <summary>Buffer for the GAP field content</summary>
		public TrackBuffer buffer = new TrackBuffer();
		/// <summary>Information about the GAP field </summary>
		public BlockInfo info = new BlockInfo();
		/// <summary>A list of Splices found in this GAP</summary>
		public List<Splice> splices = new List<Splice>();
	}


	/// <summary>Store information about an IAM of a track</summary>
	public class IAM {
		/// <summary>The type of the IAM (IDAM/DAM/IAM)</summary>
		public IamType Type { get; set; }
		/// <summary>True if the IAM was repaired from another revolution</summary>
		public bool Repaired { get; set; }
		/// <summary>The actual value of the IAM</summary>
		public byte Value { get; set; }
		/// <summary>Information about the IAM</summary>
		public BlockInfo info = new BlockInfo();
	}


	[Flags]
	public enum SectorFlags : uint {
		None = 0,
		/// <summary>50% of bits cell width above normal (+3.5%)</summary>
		WIDTH_ABOVE = Bit.BIT_01,
		/// <summary>50% of bits cell width below normal (-3.5%)</summary>
		WIDTH_BELOW = Bit.BIT_02,
		/// <summary>50% of bits cell below and above normal</summary>
		WIDTH_VARIATION = Bit.BIT_03,
		/// <summary>IDAM not equal $FE</summary>
		NOT_STD_IDAM = Bit.BIT_04,
		/// <summary>Wrong track number in ID</summary>
		ID_BAD_TRK_NUM = Bit.BIT_05,
		/// <summary>Wrong head number in ID</summary>
		ID_BAD_HEAD_NUM = Bit.BIT_06,
		/// <summary>Invalid sector number in ID</summary>
		ID_BAD_SEC_NUM = Bit.BIT_07,
		/// <summary>Invalid sector length in ID</summary>
		ID_BAD_SEC_LEN = Bit.BIT_08,
		/// <summary>DAM not equal to $FB</summary>
		NOT_STD_DAM = Bit.BIT_09,
		/// <summary>Sector contains NFA region</summary>
		NFA_IN_SECT = Bit.BIT_10,
		/// <summary>Two or more sector with same number</summary>
		DUP_SEC_NUM = Bit.BIT_11,
		/// <summary>Sector read time is 3% above normal</summary>
		LONG_SECT = Bit.BIT_12,
		/// <summary>Sector read time is 3% below normal</summary>
		SHORT_SECT = Bit.BIT_13,
		/// <summary>Data field with no ID field</summary>
		DATA_NO_ID = Bit.BIT_14,
		/// <summary>Truncated sector</summary>
		TRUNC_SECT = Bit.BIT_15,
		/// <summary>Sector has DM type of fuzzy byte</summary>
		DM_FUZZY = Bit.BIT_16,
		/// <summary>The content of the sector can be read from track</summary>
		SEC_IN_TRACK = Bit.BIT_17,

		DM_HACK = Bit.BIT_31

		// BAD_ID_CRC => id.info.crc != 0
		// SECT_NO_DATA => data == null
		// BAD_DATA_CRC => data.info.crc != 0
		// FUZZY_SECT => data.fuzzy != null
	}


	/// <summary>Sector information. Contains Id, Data and Gap fields</summary>
	public class Sector {
		/// <summary>Pointer to the Id Field of a sector</summary>
		public IdField id;

		/// <summary>Pointer to the Data Field of the sector (can be null)</summary>
		public DataField data;
		/// <summary>Pointer to a Data Field no preceded by an Id(can be null)</summary>
		public DataField dataNoId;
		/// <summary>Pointer to the Recovered Data Field of the sector (can be null)</summary>
		/// <remarks>If we find fuzzy bytes and good CRC we recover in this field</remarks>
		public DataField dataRecovered;
		/// <summary>Sector's flags</summary>
		public SectorFlags flags;
		/// <summary>Same sector content than track at offset (used by Pasti) </summary>
		public int trackOffset;
	}


	[Flags]
	public enum TrackFlag : uint {
		None = 0,
		/// <summary>Track has less than 6062 bytes</summary>
		SHORT_TRACK = Bit.BIT_01,
		/// <summary>Track has more 6437 bytes</summary>
		LONG_TRACK = Bit.BIT_02,
		/// <summary>Data track detected</summary>
		DATA_TRACK = Bit.BIT_03,
		/// <summary>Track has more than 5 invalid bytes ($F5-F7) in GAP</summary>
		BAD_DATA_IN_GAP = Bit.BIT_04,
		/// <summary>Sector numbered non sequentially</summary>
		SECTORS_NOT_IN_SEQ = Bit.BIT_05,
		/// <summary>Hidden Data in track (DF & JMD)</summary>
		HIDDEN_DATA = Bit.BIT_06,
		/// <summary>Track has Sync sequence > 4</summary>
		INVALID_SYNC_SEQ = Bit.BIT_07,
		/// <summary>Track contains No Flux Area</summary>
		NFA_IN_TRACK = Bit.BIT_08,
		/// <summary>Track contains sector with non standard size</summary>
		SECTORS_NOT_STD_SIZE = Bit.BIT_09,
		/// <summary>Track contains duplicate sectors</summary>
		DUP_SEC_IN_TRACK = Bit.BIT_10
	}


	/// <summary>Track information</summary>
	public class Track {
		/// <summary>buffer for all the byte read in the DATA field of the sector</summary>
		public TrackBuffer buffer = new TrackBuffer();
		/// <summary> Contains information about the field </summary>
		public BlockInfo info = new BlockInfo();
		/// <summary>Track's flags</summary>
		public TrackFlag flags;
		/// <summary>Location in byte of the first A1 sync before an IAM sequence (used by Pasti)</summary>
		public int firstSyncOffset = 0;
		/// <summary>Index (in sectors) of the first sector of the track</summary>
		public int firstSectIndex = 0;
		/// <summary>Position of the track splice in respect to index pulse</summary>
		public int trackSpliceTime = -1;
		/// <summary>Protection(s) detected for this track</summary>
		public bool hasProtections = false;
		/// <summary>This track is shifted in respect to index pulse</summary>
		public bool shiftedTrack = false;
	}

	/// <summary>We raise an exception to indicate bad error</summary>
	public class ImageErrorException : Exception {
		/// <summary>SectorNotFoundException Constructor</summary>
		public ImageErrorException() : base() { }
		/// <summary>SectorNotFoundException Constructor</summary>
		public ImageErrorException(string message) : base(message) { }
		/// <summary>SectorNotFoundException Constructor</summary>
		public ImageErrorException(string message, Exception e) : base(message, e) { }

		private static string _ExceptionMessage;

		/// <summary>
		/// Get/SEt the content of the Exception message
		/// </summary>
		public static string ExceptionMessage {
			get { return _ExceptionMessage; }
			set { _ExceptionMessage += value + "\n"; }
		}

		/// <summary>
		/// Reset content of exception message
		/// </summary>
		public static void resetException() {
			_ExceptionMessage = null;
		}
	}


	/// <summary>FDC decode functions helper</summary>
	/// <remarks>We use ring buffers to handle the 4 last bytes information. 
	/// This allow to store the last three  Sync mark bytes in front of the ID and
	/// Data fields. The class has been design  to store information accurate at the
	/// bit level.</remarks>
	public class BitTrackDecode {
		private int _runningTime;
		private int _runningBit;
		private int _runningIndex;
		private int _byteCount;

		private int[] _currentIndex = new int[4];
		private int[] _currentTime = new int[4];
		private int[] _currentBit = new int[4];
		private byte[] _dataBuffer = new byte[4];
		private ushort[] _mfmBuffer = new ushort[4];
		private int _ringIndex;
		private int _bitWidthSum;
		private int _bitWidthCount;
		private ushort _bitWidthAvg;
		private int _fluxTimeInc;
		private int _fluxRem;
		private DSR _dsr;

		/// <summary>Get the current byte count</summary>
		public int ByteCount { get { return _byteCount; }}
		/// <summary>Get the current bit count</summary>
		public int BitCount { get { return _runningBit; }}
		/// <summary>Get the current bit width averaged</summary>
		public int BitWidthAvg { get { return _bitWidthAvg; }}

		/// <summary>Constructor</summary>
		/// <param name="dsr"></param>
		public BitTrackDecode(DSR dsr) {
			_dsr = dsr;
		}

		/// <summary>One more bit</summary>
		public void nextBit() {
			_runningBit++;
			_runningTime += _fluxTimeInc;
		}

		/// <summary>One more byte</summary>
		public void nextByte() {
			_byteCount++;
			_ringIndex = (_ringIndex + 1) % 4;
			_dataBuffer[_ringIndex] = _dsr.Data;
			_mfmBuffer[_ringIndex] = _dsr.MFM;
			_currentIndex[_ringIndex] = _runningIndex;
			_currentTime[_ringIndex] = _runningTime + _fluxRem;
			_currentBit[_ringIndex] = _runningBit;
			_bitWidthAvg = (_bitWidthCount == 0) ? (ushort)2000 : (ushort)(_bitWidthSum / _bitWidthCount);
			_bitWidthCount = _bitWidthSum = 0;
		}


		public int nextFlux(Global.DiskTrack dt, int currentIndex) {
			_runningIndex = currentIndex;
			int flux = dt.fluxData.fluxValue[currentIndex];
			int bitSpacing = dt.decodedData.bitSpacing[currentIndex];
			int bitWidth = dt.decodedData.bitWidth[currentIndex];
			_fluxTimeInc = flux / bitSpacing;
			_fluxRem = flux % bitSpacing;
			_bitWidthSum += bitWidth;
			_bitWidthCount++;
			return bitSpacing;
		}


		public void addByte(TrackBuffer b, Phase phase, bool addSync) {
			if (addSync) {
				for (int i = 1; i < 4; i++) {
					int x = (_ringIndex + i) % 4;
					b.addByte(_dataBuffer[x], _mfmBuffer[x], _bitWidthAvg, _currentTime[x], phase);
				}
			}   // flush sync seq
			b.addByte(_dsr.Data, _dsr.MFM, _bitWidthAvg, _currentTime[_ringIndex], phase);
		}


		public void storeStartInfo(BlockInfo bi, bool addSync) {
			int x;
			if (addSync) {
				x = (_ringIndex + 1) % 4;
				bi.startByte = _byteCount - 3;
				bi.startBitIAM = _currentBit[_ringIndex];
				bi.startTimeIAM = _currentTime[_ringIndex];
			}
			else {
				x = _ringIndex;
				bi.startByte = _byteCount;
			}
			bi.startBit = _currentBit[x];
			bi.startTime = _currentTime[x];
			bi.firstIndex = _currentIndex[x];
		}


		public void storeEndInfo(BlockInfo bi, bool removeSync) {
			int x = (removeSync) ? (_ringIndex + 1) % 4 : _ringIndex;
			bi.timeLength = _currentTime[x] - bi.startTime;
			bi.bitCount = _currentBit[x] - bi.startBit;
			bi.lastIndex = _currentIndex[x];
		}

		/// <summary>Used to set current values with saved info</summary>
		/// <param name="bi">Block info providing running values</param>
		/// <param name="currentIndex">current index</param>
		public void setInfo(BlockInfo bi, int currentIndex) {
			_runningIndex = currentIndex;
			_runningBit = bi.startBitIAM;
			_runningTime = bi.startTimeIAM;
			_byteCount = bi.startByte + 3;
		}

		/// <summary>Reset the current values</summary>
		public void resetInfo() {
			_runningBit = 0;
			_runningTime = 0;
			_byteCount = 0;
		}

	}   // class BitTrackInfo


	/// <summary>Emulate WD1772 FDC behavior</summary>
	public class FDC {
		// Bit cell width upper/lower values = 2000 +/- 3.5%
		private readonly static int WIDTH_UPPER_LIMIT = 2070;
		private readonly static int WIDTH_LOWER_LIMIT = 1930;

		private const int DELTA = 500;
		private const int MIN4000 = 4000 - DELTA;
		private const int MAX4000 = 4000 + DELTA;
		private const int MIN6000 = 6000 - DELTA;
		private const int MAX6000 = 6000 + DELTA;

		/// <summary>Check if phase is a post id or data gap</summary>
		/// <param name="p">phase to check</param>
		/// <returns></returns>
		public static bool isGapPhase(Phase p) {
			return ((p == Phase.PostIdGap) || (p == Phase.PostDataGap));
		}

		//class ShiftRegister<T> {
		//	private readonly T[] array;
		//	private int front;

		//	public ShiftRegister(T[] array) {
		//		this.array = array;
		//		front = 0;
		//	}

		//	public ShiftRegister(int size) {
		//		array = new T[size];
		//		front = 0;
		//	}


		//	public void ShiftIn(T val) {
		//		array[front] = val;
		//		front = (front + 1) % array.Length;
		//	}


		//	public T this[int index] {
		//		get {
		//			if (index > array.Length - 1) {
		//				throw new IndexOutOfRangeException();
		//			}
		//			return array[(front + index) % array.Length];
		//		}
		//	}

		//	public int Length { get { return array.Length; } }
		//}

		//class ShiftRegister {
		//	static int[] R4E = new int[] { 3, 3, 3, 2, 2, 3 };
		//	int[] register;
		//	public ShiftRegister(int[] array) {
		//		register = array;
		//	}

		//	public ShiftRegister(int size) {
		//		register = new int[size];
		//	}

		//	public void shiftIn(int val) {
		//		Array.Copy(register, 1, register, 0, register.Length - 1);
		//		register[5] = val;
		//	}

  //          public bool Is4e {
		//		get {
		//			bool check =
		//			register.SequenceEqual(new int[] { 3, 3, 3, 3, 2, 2 }) ||
		//			 register.SequenceEqual(new int[] { 2, 3, 3, 3, 3, 2 }) ||
		//			 register.SequenceEqual(new int[] { 2, 2, 3, 3, 3, 3 }) ||
		//			 register.SequenceEqual(new int[] { 3, 2, 2, 3, 3, 3 }) ||
		//			 register.SequenceEqual(new int[] { 3, 3, 2, 2, 3, 3 }) ||
		//			 register.SequenceEqual(new int[] { 3, 3, 3, 2, 2, 3 });
		//			return check;
		//		}
		//	}
		//	public bool Is00 {
		//		get { return (register.SequenceEqual(new int[] { 2, 2, 2, 2, 2, 2 })); }
		//	}
		//}


		class MfmBits {
			private int _index = 0;
			private int[] _spacing = new int[6] { 3, 3, 3, 3, 2, 2 };
			//private int[] _spacing = new int[6];
			private int[] _bit = new int[6];
			private int[] _time = new int[6];
			//ShiftRegister sr = new ShiftRegister(new int[6] { 3, 3, 3, 3, 2, 2 });
			//ShiftRegister<int> sr2 = new ShiftRegister<int>(6);

			public void add(int spacing, int bit, int time) {
				_spacing[_index] = spacing;
				//sr.shiftIn(spacing);
				_bit[_index] = bit;
				_time[_index] = time;
				_index = (_index + 1) % 6;
			}

			public Splice newSplice(SpliceType type, int revTime) {
				Splice splice = new Splice();
				splice.type = type;
				splice.bit = _bit[_index];
				splice.time = _time[_index];
				if (splice.time > revTime)
					splice.time -= revTime;
				return splice;
			}

			public bool Is4e {
				get {
					bool check = _spacing.SequenceEqual(new int[] { 3, 3, 3, 3, 2, 2 }) ||
					  _spacing.SequenceEqual(new int[] { 2, 3, 3, 3, 3, 2 }) ||
					  _spacing.SequenceEqual(new int[] { 2, 2, 3, 3, 3, 3 }) ||
					  _spacing.SequenceEqual(new int[] { 3, 2, 2, 3, 3, 3 }) ||
					  _spacing.SequenceEqual(new int[] { 3, 3, 2, 2, 3, 3 }) ||
					  _spacing.SequenceEqual(new int[] { 3, 3, 3, 2, 2, 3 });
					//bool c2 = sr.Is4e;
					return check;
				 }
            }

			public bool Is00 {
				get {
					bool check = _spacing.SequenceEqual(new int[] { 2, 2, 2, 2, 2, 2 });
					//bool c2 = sr.Is00;
					return check;
				}
            }
		}

		public enum GapPhase { S4E, S00, SXX, SCT }
		private static void scanGap(int track, int side, int rev, GapField gap, bool forceSect) {
			if (gap == null) return;
			if (gap.splices.Count > 0) return;

			int currentTime = gap.info.startTime;
			int currentBit = gap.info.startBit;
			int start = gap.info.firstIndex;
			int stop = gap.info.lastIndex;

			int[] fluxSegment = Global.Disk[track, side].fluxData.fluxValue.Skip(start).Take(stop - start).ToArray();
			// https://msdn.microsoft.com/en-us/library/1hsbd92d(v=vs.110).aspx
			// int[] fluxSegment = new ArraySegment<int>(Global.Disk[track, side].fluxData.fluxValue, start, stop - start).ToArray<int>();
			HistoData h = Global.computeSegmentHisto(fluxSegment);
			if (!h.Formatted) return;

			Splice splice = null;
			GapPhase phase = GapPhase.SXX;
			int fivty = (stop - start) / 2;
			int count = 0;
			int revTime = Global.Disk[track, side].fluxDataRev[rev].revolutionTime;

			MfmBits mfm = new MfmBits();
			for (int i = start; i < stop; i++) {
				int flux = Global.Disk[track, side].fluxData.fluxValue[i];
				currentTime += flux;
				int bitSpacing = Global.Disk[track, side].decodedData.bitSpacing[i];
				currentBit += bitSpacing;
				mfm.add(bitSpacing, currentBit, currentTime);

				switch(phase) {
					case GapPhase.SXX:
						if (mfm.Is4e) {
							phase = GapPhase.S4E;
						}
						else if (mfm.Is00) {
							phase = GapPhase.S00;
							if ((i- start) < 8)
								splice = mfm.newSplice(SpliceType.Sector, revTime);
							else
								splice = mfm.newSplice(SpliceType.Seq00, revTime);
							gap.splices.Add(splice);
						}
						break;

					case GapPhase.S4E:
						if (!mfm.Is4e) {
							phase = GapPhase.SCT;
							count = 0;
						}
						break;

					case GapPhase.SCT:
						if (++count == 5) {
							if (mfm.Is00) {
								phase = GapPhase.S00;
								splice = mfm.newSplice(SpliceType.Seq00, revTime);
							}
							else {
								phase = GapPhase.SXX;
								if ((i - start) < 8)
									splice = mfm.newSplice(SpliceType.Sector, revTime);
								else {
									splice = mfm.newSplice(forceSect ? SpliceType.Sector : SpliceType.Track, revTime);
									if ((Global.Disk[track, side].track.trackSpliceTime == -1) && !forceSect)
										Global.Disk[track, side].track.trackSpliceTime = splice.time;
								}
                            }
							gap.splices.Add(splice);
						}
						break;

					case GapPhase.S00:
						if (!mfm.Is00) {
							phase = GapPhase.SXX;
						}
						break;
				}	// phases
			}   // for all index
		}	// scanGap


		/// <summary>Read a track</summary>
		/// <param name="track">The track index</param>
		/// <param name="side">The side index</param>
		/// <param name="rev">The revolution index</param>
		/// <remarks>The data placed in the buffer are equivalent to what would 
		/// be read by a WD1772 decode the flux transitions. We store the
		/// phase of a byte based on information detected by readAddr and readSector.
		/// Therefore this function must be called last.</remarks>
		public static void readTrack(int track, int side, int rev) {
			if (Global.Disk[track, side].track != null) return;
			int start = Global.firstIndex(track, side, rev);
			int stop = Global.lastIndex(track, side, rev);

			Global.Disk[track, side].track = new Track();
			Global.DiskTrack dt = Global.Disk[track, side];
			DSR dsr = new DSR();
			dsr.trackMode();
			BitTrackDecode bti = new BitTrackDecode(dsr);

			int invalidByteInGap = 0;
			int nfaCount = 0;
			Phase phase = Phase.PostDataGap;
			int currentSector = -1;
			int byteLeftInBlock = 0;
			int syncCount = 0;
			bool dataNoId = false;

			bti.storeStartInfo(Global.Disk[track, side].track.info, false);
			Global.Disk[track, side].track.info.firstIndex = start;

			// We read from the beginning to end of a track with AM decoder on all time
			for (int currentIndex = start; currentIndex < stop; currentIndex++) {
				int bitSpacing = bti.nextFlux(dt, currentIndex);

				while (bitSpacing-- > 0) {
					dsr.shiftIn(bitSpacing > 0 ? 0 : 1);
					bti.nextBit();

					if ((dsr.MFM == 0x4489) || (dsr.MFM == 0x5224))
						syncCount++;

					if (dsr.DataReady) {
						bti.nextByte();

						if (dsr.SyncSeq > 4)
							Global.Disk[track, side].track.flags |= TrackFlag.INVALID_SYNC_SEQ;

						// check NFA
						if ((dsr.MFM == 0) || (dsr.MFM == 0xFFFF))
							nfaCount++;
						else {
							if (nfaCount > 8)
								Global.Disk[track, side].track.flags |= TrackFlag.NFA_IN_TRACK;
							nfaCount = 0;
						}

						if (dsr.IAM)
							switch (dsr.Data) {
								// Case ID field //
								case 0xFC:
								case 0xFD:
								case 0xFE:
								case 0xFF:
									if (dt.track.firstSyncOffset == 0)
										Global.Disk[track, side].track.firstSyncOffset = bti.ByteCount - 3;
									currentSector++;
									byteLeftInBlock = 7;
									phase = Phase.Id;
									dataNoId = false;
									break;

								// Case DATA field
								case 0xF8:
								case 0xF9:
								case 0xFA:
								case 0xFB:
									phase = Phase.Data;
									byteLeftInBlock = 1;
									if ((dt.sectors != null) && (dt.sectors.Count > 0)) {
										int s = (currentSector != -1) ? currentSector : dt.sectors.Count - 1;
										// ===================== TODO CHECK => optimize for pasti ====================================================
										dt.sectors[s].trackOffset = bti.ByteCount + 1;
										byteLeftInBlock = Global.sectSize(dt.sectors[s].id.Length) + 3; // AM + 2 CRC + Size
										syncCount = 0;
										if (dt.sectors[s].data != null) {
											if (!dataNoId) dataNoId = true;
											else dataNoId = false;
										}   // sector has data
									}   // data but no sector => inconsistent
									break;

								// other = data track ?
								default:
									// TODO
									break;
							}   // switch on IAM

						else  // not IAM
							switch (phase) {
								case Phase.Id:
									byteLeftInBlock--;
									if (byteLeftInBlock == 0)
										phase = Phase.PostIdGap;
									break;

								case Phase.Data:
									byteLeftInBlock--;
									if (byteLeftInBlock == 0) {
										int s = (currentSector != -1) ? currentSector : dt.sectors.Count - 1;
										phase = Phase.PostDataGap;
										if (dataNoId && (syncCount == 0))
											dt.sectors[s].flags |= SectorFlags.SEC_IN_TRACK;
									}   // last byte
									break;

								default:    // gap
									if ((dsr.Data >= 0xF5) && (dsr.Data <= 0xF7))
										invalidByteInGap++;
									break;
							}   // not IAM

						bti.addByte(Global.Disk[track, side].track.buffer, phase, false);
					}   // data ready
				}   // shift bit in
			}   // for all bits

			bti.storeEndInfo(Global.Disk[track, side].track.info, false);

			if ((invalidByteInGap > 5) && (Global.Disk[track, side].sectors.Count > 0))
				Global.Disk[track, side].track.flags |= TrackFlag.BAD_DATA_IN_GAP;
		}   // end of readTrack()


		/// <summary>Read and Store IAM info for all revolutions of a track</summary>
		/// <param name="track">The track index</param>
		/// <param name="side">The side index</param>
		public static void readIAM(int track, int side) {
			Global.DiskTrack dt = Global.Disk[track, side];
			if (dt.iams != null) return;

			int start = Global.firstIndex(track, side, 0);
			//int stop = dt.fluxData.totalFluxCount;
			int stop = Global.lastIndex(track, side, dt.fluxDataRev.Length - 1);

			List<IAM>[] iams = new List<IAM>[dt.fluxDataRev.Length];
			for (int i = 0; i < dt.fluxDataRev.Length; i++) iams[i] = new List<IAM>();

			IAM iam = null;
			DSR dsr = new DSR();
			dsr.sectorMode();
			BitTrackDecode bti = new BitTrackDecode(dsr);
			int rev = 0;

			// We read from the first to last index looking only for all IAM
			for (int currentIndex = start; currentIndex < stop; currentIndex++) {
				int last = Global.lastIndex(track, side, rev);
				if (currentIndex > last) {
					rev++;
					bti.resetInfo();
				}
				int bitSpacing = bti.nextFlux(dt, currentIndex);
				while (bitSpacing-- > 0) {
					dsr.shiftIn(bitSpacing > 0 ? 0 : 1);
					bti.nextBit();

					if (dsr.DataReady) {
						bti.nextByte();

						if (dsr.IAM) {  // IAM position
							if (iam != null)
								bti.storeEndInfo(iam.info, true);

							iam = new IAM();
							iams[rev].Add(iam);
							bti.storeStartInfo(iam.info, true);
							dsr.sectorMode();
							iam.Repaired = false;
							iam.Value = dsr.Data;

							if (Enumerable.Range(0xFC, 4).Contains(dsr.Data))   // ID AM
								iam.Type = IamType.IDAM;
							else if (Enumerable.Range(0xF8, 4).Contains(dsr.Data))  // Data AM
								iam.Type = IamType.DAM;
							else
								iam.Type = IamType.IAM;
						}   // IAM position
					}   // one byte ready
				}   // shift all bits
			}   // for all index
			if (iam != null)
				bti.storeEndInfo(iam.info, true);
			Global.Disk[track, side].iams = iams;
		}   // end of readIAM()


		/// <summary>Read and Store IAM info and store ID content for complete track</summary>
		/// <param name="track">The track index</param>
		/// <param name="side">The side index</param>
		/// <param name="rev">The revolution index</param>
		//public static List<Sector> readId(int track, int side, int rev) {
		public static void readId(Global.DiskTrack dt, IdField id) {
			int firstIndex = (id.info.firstIndex - 16) < 0 ? 0 : id.info.firstIndex - 16;
			int lastIndex = id.info.lastIndex;
			int byteLeftInBlock = 0;
			ushort crc = 0;
			Phase phase = Phase.PostDataGap;
			DSR dsr = new DSR();
			dsr.sectorMode();
			BitTrackDecode bti = new BitTrackDecode(dsr);

			for (int currentIndex = firstIndex; (currentIndex <= lastIndex) || (phase != Phase.PostIdGap); currentIndex++) {
				if (currentIndex > dt.fluxData.fluxValue.Length - 1)
					break;

				int bitSpacing = bti.nextFlux(dt, currentIndex);
				while (bitSpacing-- > 0) {
					dsr.shiftIn(bitSpacing > 0 ? 0 : 1);
					bti.nextBit();
					if (dsr.DataReady) {
						bti.nextByte();

						if (phase != Phase.Id) { // we are in GAP
							if (phase == Phase.PostIdGap)
								bti.addByte(id.gap.buffer, phase, false);

							if (dsr.IAM) {  // IAM position
								if (Enumerable.Range(0xFC, 4).Contains(dsr.Data)) { // ID AM
									byteLeftInBlock = 7;
									phase = Phase.Id;
									bti.setInfo(id.info, currentIndex);
									bti.addByte(id.buffer, phase, true);
								}   // IDAM
								else {
									Debug.Assert(true, "Damned");
									phase = Phase.PostDataGap;
									dsr.sectorMode();
								}   // non standard IAM
							}   // IAM position
						}   // we are in GAP

						else {  // we are in ID
							byteLeftInBlock--;
							if (byteLeftInBlock > 0) {
								bti.addByte(id.buffer, phase, false);
								if (byteLeftInBlock == 1) crc = dsr.CRC;
							}   // not last byte
							else {
								dsr.sectorMode();   // we re-enable sync
								id.info.crc = crc;
								bti.storeEndInfo(id.info, false);
								phase = Phase.PostIdGap;

								id.gap = new GapField();
								bti.storeStartInfo(id.gap.info, false);
								bti.addByte(id.gap.buffer, phase, false);
							}   // byteInBlock == 0
						} // we are in ID
					}   // one byte ready
				}   // shift all bits
			}   // for all index

			if (phase == Phase.PostIdGap) {
				bti.storeEndInfo(id.gap.info, false);
				id.gap.buffer.removeLastBytes(1);
				if (id.gap.buffer.DataByte.Count == 0)
					id.gap = null;
			}
			if (phase == Phase.Id)
				bti.storeEndInfo(id.info, false);
		}   // end of readId()


		public static SectorFlags readData(Global.DiskTrack dt, DataField data, int size, bool full) {
			int firstIndex = (data.info.firstIndex - 16) < 0 ? 0 : data.info.firstIndex - 16;
			int lastIndex = data.info.lastIndex;
			Phase phase = Phase.PostIdGap;
			DSR dsr = new DSR();
			dsr.sectorMode();
			BitTrackDecode bti = new BitTrackDecode(dsr);

			int byteLeftInBlock = 0;
			int widthOver = 0;
			int widthUnder = 0;
			ushort crc = 0;
			SectorFlags flags = 0;
			int nfaCount = 0;

			for (int currentIndex = firstIndex; (currentIndex <= lastIndex) || (phase != Phase.PostDataGap); currentIndex++) {
				if (currentIndex > dt.fluxData.fluxValue.Length - 1)
					break;
				if ((!full) && (phase == Phase.PostDataGap))
					break;

				int bitSpacing = bti.nextFlux(dt, currentIndex);
				while (bitSpacing-- > 0) {
					dsr.shiftIn(bitSpacing > 0 ? 0 : 1);
					bti.nextBit();
					if (dsr.DataReady) {
						bti.nextByte();
						if (full) {
							if ((currentIndex > lastIndex) && !flags.HasFlag(SectorFlags.TRUNC_SECT)) {
								flags |= SectorFlags.TRUNC_SECT;
								data.truncByteCount = bti.ByteCount - data.info.startByte;
							}
							// check NFA
							if ((dsr.MFM == 0) || (dsr.MFM == 0xFFFF))
								nfaCount++;
							else {
								if (nfaCount > 8)
									flags |= SectorFlags.NFA_IN_SECT;
								nfaCount = 0;
							}
						}

						if (phase != Phase.Data) { // we are in GAP
							if (phase == Phase.PostDataGap)
								bti.addByte(data.gap.buffer, phase, false);

							if (dsr.IAM) {  // IAM position
								if (Enumerable.Range(0xF8, 4).Contains(dsr.Data)) { // DATA AM
									bti.setInfo(data.info, currentIndex);           // we are synchronized set running values
									bti.addByte(data.buffer, Phase.Data, true);    // we add the sync seq
									byteLeftInBlock = size;
									phase = Phase.Data;
									widthOver = widthUnder = 0;
								}   // Data AM

								else {  // all other AM => stop
									Debug.Assert(true, "Damned");
									phase = Phase.PostDataGap;
									dsr.sectorMode();
								}   // not Data AM
							}   // IAM Position
						}   // we are in GAP

						else {  // we are in DATA
							byteLeftInBlock--;
							switch (byteLeftInBlock) {
								case 1:
									bti.addByte(data.buffer, phase, false);
									crc = dsr.CRC;
									break;

								case 0: // end of DATA block
									dsr.sectorMode();
									data.info.crc = crc;
									bti.storeEndInfo(data.info, false);
									phase = Phase.PostDataGap;

									if ((widthOver > (size / 2)) || (widthUnder > (size / 2))) {
										if (widthOver > (size / 2)) flags |= SectorFlags.WIDTH_ABOVE;
										if (widthUnder > (size / 2)) flags |= SectorFlags.WIDTH_BELOW;
									}
									else if ((widthOver > 0) && (widthUnder > 0) && ((widthOver + widthUnder) > (size / 4)))
										flags |= SectorFlags.WIDTH_VARIATION;

									data.gap = new GapField();
									bti.storeStartInfo(data.gap.info, false);
									bti.addByte(data.gap.buffer, phase, false);
									break;

								default:    // other data bytes
									bti.addByte(data.buffer, phase, false);
									if (bti.BitWidthAvg > WIDTH_UPPER_LIMIT) widthOver++;
									if (bti.BitWidthAvg < WIDTH_LOWER_LIMIT) widthUnder++;
									break;
							}   // switch byte
						} // we are in DATA
					}   // one byte ready
				}   // shift in all bits;
			}   // for all index

			if ((phase == Phase.PostDataGap) && (data.gap != null)) {
				bti.storeEndInfo(data.gap.info, false);
				data.gap.buffer.removeLastBytes(1);
				if (data.gap.buffer.DataByte.Count == 0)
					data.gap = null;
			}
			if (phase == Phase.Data)
				bti.storeEndInfo(data.info, false);
			return flags;
		}


		/// <summary>
		/// Reads and stores the content of a sector + compares content from other revolutions
		/// </summary>
		/// <param name="track"></param>
		/// <param name="side"></param>
		/// <param name="rev"></param>
		/// <param name="sector"></param>
		/// <param name="dataNoId"></param>
		/// <returns></returns>
		public static SectorFlags readDataCompare(int track, int side, int rev, Sector sector, int iamIndex, bool dataNoId) {
			Global.DiskTrack dt = Global.Disk[track, side];

			int size = Global.sectSize(sector.id.Length) + 3; // AM + 2 CRC + Size
			DataField data = dataNoId ? sector.dataNoId : sector.data;

			SectorFlags flags = readData(dt, data, size, true);


			for (int j = 0; j < Global.Disk[track, side].fluxDataRev.Count(); j++) {
				if (j == rev) continue;
				DataField dataRev = new DataField();
				transferInfo(dt.iams[j][iamIndex], dataRev.info);
				readData(dt, dataRev, size, false);
				// check fuzzy bytes if same size
				if (dataRev.buffer.DataByte.Count == data.buffer.DataByte.Count) {
					byte[] fuzzy = new byte[dataRev.buffer.DataByte.Count];
					bool fuzzyFound = false;

					for (int i = 3; i < dataRev.buffer.DataByte.Count; i++) {
						byte xor = (byte)(dataRev.buffer.DataByte[i] ^ data.buffer.DataByte[i]);
						if (xor != 0) {
							fuzzyFound = true;
							fuzzy[i] |= xor;
						}
					}   // compare all bytes

					if (fuzzyFound) {
						if (data.fuzzy == null) data.fuzzy = fuzzy;
						else
							for (int i = 3; i < dataRev.buffer.DataByte.Count; i++)
								data.fuzzy[i] |= fuzzy[i];
					}

					// if we have fuzzy sector and CRC is correct this indicates
					// that we need to keep the correctly read sector.
					if (fuzzyFound && (dataRev.info.crc == 0))
						sector.dataRecovered = dataRev;
				}   // same size
			}   // compare other rev
			return flags;
		}   // readDataCompare()


		public static void processTrack(int track, int side, int rev) {
			Global.DiskTrack dt = Global.Disk[track, side];
			if ((dt.sectors != null) && (dt.sectors.Count > 0)) return;
			if ((dt.fluxData == null) || (dt.decodedData == null)) return;
			Global.Disk[track, side].sectors = new List<Sector>();
			bool consistentIAM = true;

			readIAM(track, side);
			int maxSize = 0;
			for (int r = 0; r < Global.Disk[track, side].fluxDataRev.Count(); r++) {
				if (Global.Disk[track, side].iams[r].Count > maxSize)
					maxSize = Global.Disk[track, side].iams[r].Count;
			}
			for (int r = 0; r < Global.Disk[track, side].fluxDataRev.Count(); r++) {
				int refRev = 0;
				int diff = maxSize - Global.Disk[track, side].iams[r].Count;
				if (diff > 0) {
					if (diff > 1) {
						try {
							throw new ImageErrorException(
							  String.Format("*** T{0:D2}.{1:D1} Too many inconsistencies turning off sector detection ***", track, side));
						}
						catch (ImageErrorException e) { ImageErrorException.ExceptionMessage = e.Message; }
						consistentIAM = false;
						break;
					}
					for (refRev = 0; refRev < Global.Disk[track, side].fluxDataRev.Count(); refRev++) {
						if (Global.Disk[track, side].iams[refRev].Count == maxSize) break;
					}   // first good
					for (int iamIndex = 0; iamIndex < Global.Disk[track, side].iams[r].Count; iamIndex++) {
						IAM iam = Global.Disk[track, side].iams[r][iamIndex];
						IAM refIAM = Global.Disk[track, side].iams[refRev][iamIndex];
						if (iam.info.startByte > (refIAM.info.startByte + 5)) {
							IAM repair = new IAM();
							repair.Type = refIAM.Type;
							repair.Repaired = true;
							transferInfo(refIAM, repair.info);
							Global.Disk[track, side].iams[r].Insert(iamIndex, repair);
							try {
								throw new ImageErrorException(
								  String.Format("*** T{0:D2}.{1:D1} {2} not found in Revolution {3} recovering from {4} ***",
								  track, side, refIAM.Type, r + 1, refRev + 1));
							}
							catch (ImageErrorException e) { ImageErrorException.ExceptionMessage = e.Message; }
							break;
						}   // found the guilty
						else
							Debug.Assert(true, "Damned IAM problem");
					}   // search first error
				}   // found problem
			}   // check all rev

			Sector sector = null;
			bool dataNoId = false;
			int maxPostSize = 0;
			int lastBlock = -1;
			int lastTruncNFABlock = -1;

			if (consistentIAM) {
				// read all index field
				foreach (IAM iam in Global.Disk[track, side].iams[rev]) {
					if (iam.Type == IamType.IDAM) {
						sector = new Sector();
						Global.Disk[track, side].sectors.Add(sector);
						sector.id = new IdField();
						transferInfo(iam, sector.id.info);
						readId(Global.Disk[track, side], sector.id);
					}   // id
				}

				// read all data fields
				int idCount = -1;
				for (int iamIndex = 0; iamIndex < Global.Disk[track, side].iams[rev].Count; iamIndex++) {
					IAM iam = Global.Disk[track, side].iams[rev][iamIndex];
					if (iam.Type == IamType.IDAM) idCount++;
					if ((iam.Type == IamType.DAM) && (Global.Disk[track, side].sectors.Count > 0)) {
						int secIndex = (idCount < 0) ? Global.Disk[track, side].sectors.Count - 1 : idCount;
						sector = Global.Disk[track, side].sectors[secIndex];
						if (sector.data != null) {
							sector.dataNoId = new DataField();
							transferInfo(iam, sector.dataNoId.info);
							dataNoId = true;
						}   // second data
						else {
							sector.data = new DataField();
							transferInfo(iam, sector.data.info);
							dataNoId = false;
						}   // normal data
						sector.flags |= readDataCompare(track, side, rev, sector, iamIndex, dataNoId);
					}   // data IAM
				}   // all IAM
			}   // consistent

			// read track
			readTrack(track, side, rev);

			// search lastBlock
			for (int secIndex = 0; secIndex < Global.Disk[track, side].sectors.Count; secIndex++) {
				sector = Global.Disk[track, side].sectors[secIndex];

				if ((sector.data != null) && (sector.data.gap != null)) {
					int sum = 0;
					if (sector.dataNoId != null) {
						sum += sector.dataNoId.buffer.DataByte.Count;
						if (sector.dataNoId.gap != null)
							sum += sector.dataNoId.gap.buffer.DataByte.Count;
					}   // extra sector
					sum += sector.data.gap.buffer.DataByte.Count;
                    if (sum > maxPostSize) {
						maxPostSize = sum;
						lastBlock = secIndex;
					}
				}   // sector with data
				else {
					if ((sector.id.gap != null) && (sector.id.gap.buffer.DataByte.Count > maxPostSize)) {
						maxPostSize = sector.id.gap.buffer.DataByte.Count;
						lastBlock = secIndex;
					}
				}   // sector no data

				// Truncated and NFA
				if (sector.flags.HasFlag(SectorFlags.NFA_IN_SECT) && sector.flags.HasFlag(SectorFlags.TRUNC_SECT))
					lastTruncNFABlock = Global.Disk[track, side].sectors.Count - 1;

				// search splices
				if (sector.id.gap != null)
					scanGap(track, side, rev, sector.id.gap, (sector.data != null) ? true : false);
				if ((sector.data != null) && (sector.data.gap != null))
					scanGap(track, side, rev, sector.data.gap, false);
				if ((sector.dataNoId != null) && (sector.dataNoId.gap != null))
					scanGap(track, side, rev, sector.dataNoId.gap, false);

			}   // all sectors

			if (lastTruncNFABlock != -1) {  // NFA win over largest gap
				lastBlock = lastTruncNFABlock;
				Global.Disk[track, side].track.trackSpliceTime = 0;
			}

			int firstBlock = (lastBlock == (Global.Disk[track, side].sectors.Count - 1)) ? 0 : lastBlock + 1;
			Global.Disk[track, side].track.firstSectIndex = firstBlock;

			// check protections
			Protections.checkProtection(track, side);
		}   // processTrack()


		private static void transferInfo(IAM iam, BlockInfo info) {
			info.firstIndex = iam.info.firstIndex;
			info.lastIndex = iam.info.lastIndex;
			info.startBit = iam.info.startBit;
			info.startBitIAM = iam.info.startBitIAM;
			info.startByte = iam.info.startByte;
			info.startTime = iam.info.startTime;
			info.startTimeIAM = iam.info.startTimeIAM;
		}


		/// <summary>
		/// Release Track and Sector content
		/// </summary>
		/// <param name="track">The track number</param>
		/// <param name="side">The side number</param>
		public static void resetTrack(int track, int side) {
			Global.Disk[track, side].sectors = null;
			Global.Disk[track, side].track = null;
			Global.Disk[track, side].iams = null;
			Global.Disk[track, side].hasIncompleteData = false;
			Global.Disk[track, side].hasIncompleteGap = false;
		}


		/// <summary>
		/// Find a sector by its number in a list of sectors
		/// </summary>
		/// <param name="track">The track number</param>
		/// <param name="side">The side number</param>
		/// <param name="sec">The sector number</param>
		/// <param name="idBitPosition">Minimum bit position of the sector</param>
		/// <returns></returns>
		public static Sector findSector(int track, int side, int sec) {
			List<Sector> sectors = Global.Disk[track, side].sectors;
			foreach (Sector sector in sectors) {
				if (sector.id.Sector == sec)
					return sector;
			}
			return null;
		}
	}   // class FDC
}   // name space
