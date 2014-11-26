/*!
@file TrackBuffer.cs
 * 
<summary>Buffer class that holds the track information</summary>

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
using System.Threading.Tasks;
using System.Windows.Media;

using WD1772Package;

namespace Aufit {
	/// <summary>
	/// Helper for storing and displaying Track / Sector information
	/// </summary>
	public class TrackBuffer {
		/// <summary>
		/// Store all important information about one decoded byte
		/// </summary>
		public struct TrackByte {
			/// <summary>The Data Byte</summary>
			public byte dataByte;
			/// <summary>The Clock Byte</summary>
			//public byte clockByte;
			/// <summary>The complete MFM word (two bytes)</summary>
			public ushort mfmWord;
			/// <summary>Current clock value</summary>
			public int density;
			/// <summary>Start Time of the Byte</summary>
			public int startTime;
			/// <summary>Current Phase (id / inner gap / data / outer gap</summary>
			public Phases phase;
		}

		private List<TrackByte> _bytes;
		// _fuzzy

		/// <summary>
		/// Get access to the buffer of TrackByte
		/// </summary>
		public List<TrackByte> Bytes {
			get { return _bytes; }
		}

		/// <summary>
		/// Create a new TrackBuffer object. Set initial size of the array to 7000
		/// </summary>
		public TrackBuffer() {
			_bytes = new List<TrackByte>(7000);
		}

		/// <summary>
		/// Add one more byte to the buffer
		/// </summary>
		/// <param name="data">Data byte</param>
		/// <param name="clock">Clock byte</param>
		/// <param name="mfm">MFM word</param>
		/// <param name="phase">Current Phase</param>
		/// <param name="density">Current Clock value</param>
		/// <param name="startTime">Byte start time</param>
		public void addByte(byte data, ushort mfm, Phases phase, ushort density, int startTime) {
			TrackByte b = new TrackByte();
			b.dataByte = data;
			//b.clockByte = clock;
			b.mfmWord = mfm;
			b.density = density;
			b.startTime = startTime;
			b.phase = phase;
			_bytes.Add(b);
		}


	}
}
