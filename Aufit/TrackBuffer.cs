/*!
@file TrackBuffer.cs
 * 
<summary>Buffer class that holds the track information</summary>

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
using System.Threading.Tasks;
using System.Windows.Media;

using WD1772Package;

namespace Aufit {
	/// <summary>
	/// Buffer to store Track / Sector information
	/// </summary>
	public class TrackBuffer {
		private List<byte> _dataByte;
		private List<ushort> _mfmWord;
		private List<int> _bitWidth;
		private List<int> _startTime;
		private List<Phases> _phase;

		public List<byte> DataByte {
			get { return _dataByte; }
		}

		public List<ushort> MFMWord {
			get { return _mfmWord; }
		}

		public List<int> BitWidth {
			get { return _bitWidth; }
		}

		public List<int> StartTime {
			get { return _startTime; }
		}

		public List<Phases> Phase {
			get { return _phase; }
		}


		/// <summary>
		/// Create a new TrackBuffer object. Set initial size of the array to 600
		/// </summary>
		public TrackBuffer() {
			_dataByte = new List<byte>(600);
			_mfmWord = new List<ushort>(600);
			_bitWidth = new List<int>(600);
			_startTime = new List<int>(600);
			_phase = new List<Phases>(600);

		}


		/// <summary>
		/// Add one more byte to the buffer
		/// </summary>
		/// <param name="data">Data byte</param>
		/// <param name="mfm">MFM word</param>
		/// <param name="bitWidth">Current bitWidth value</param>
		/// <param name="startTime">Byte start time</param>
		/// <param name="phase">Current Phase</param>
		public void addByte(byte data, ushort mfm, ushort bitWidth, int startTime, Phases phase) {
			_dataByte.Add(data);
			_mfmWord.Add(mfm);
			_bitWidth.Add(bitWidth);
			_startTime.Add(startTime);
			_phase.Add(phase);
		}
	
	}


}
